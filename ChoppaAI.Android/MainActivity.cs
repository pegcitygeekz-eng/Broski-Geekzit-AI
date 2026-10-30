using System.Net.Http.Json;
using System.Text.Json;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using ChoppaAI.Shared;

namespace ChoppaAI.Mobile;

[Activity(
    Label = "Choppa AI",
    MainLauncher = true,
    Exported = true,
    Theme = "@android:style/Theme.Material.Light.NoActionBar",
    ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation
        | global::Android.Content.PM.ConfigChanges.ScreenSize)]
public sealed class MainActivity : Activity
{
    private const string DefaultServerUrl = "http://10.0.2.2:11434";
    private const string DefaultModel = "mistral:latest";
    private const string SystemPrompt =
        "You are Choppa, a friendly mobile AI mascot. Keep replies friendly, conversational, and concise. " +
        "Understand and respond naturally to common greetings, thanks, farewells, and everyday polite phrases. " +
        "Be honest about your capabilities. You can answer questions, brainstorm, and help with writing. " +
        "Respond only as JSON in this exact shape: {\"canPerform\":true,\"response\":\"Your reply\"}.";
    private static readonly HttpClient OllamaClient = new()
    {
        Timeout = TimeSpan.FromMinutes(2)
    };

    private readonly List<string> _models = [];
    private readonly List<(string Role, string Content)> _chatHistory = [];
    private string _serverUrl = DefaultServerUrl;
    private string _model = DefaultModel;
    private LinearLayout _messages = null!;
    private ScrollView _messageScroll = null!;
    private EditText _messageInput = null!;
    private Spinner _modelSpinner = null!;
    private TextView _connectionStatus = null!;
    private Button _connectButton = null!;
    private Button _sendButton = null!;
    private bool _isSending;
    private bool _updatingModels;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        LoadSettings();
        BuildInterface();
        AddMessage("Hey! Connect to Ollama on your PC, choose a model, and send me a message.", fromUser: false);
        _ = RefreshModelsAsync();
    }

    private void BuildInterface()
    {
        var page = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        page.SetPadding(Dp(18), Dp(18), Dp(18), Dp(12));
        page.SetBackgroundColor(Color.ParseColor("#F3F6FB"));

        var header = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        header.SetGravity(GravityFlags.CenterVertical);
        var brand = new TextView(this)
        {
            Text = "CHOPPA AI",
            TextSize = 23,
            Typeface = Typeface.Create("sans-serif-medium", TypefaceStyle.Bold)
        };
        brand.SetTextColor(Color.ParseColor("#1C2336"));
        header.AddView(brand, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));

        var settingsButton = MakeButton("PC connection", "#E0E8F7", "#264A76");
        settingsButton.Click += (_, _) => ShowConnectionSettings();
        header.AddView(settingsButton, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            Dp(44)));
        page.AddView(header, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));

        var subtitle = new TextView(this)
        {
            Text = "Your AI companion, powered by Ollama on your PC",
            TextSize = 13
        };
        subtitle.SetTextColor(Color.ParseColor("#68758B"));
        subtitle.SetPadding(0, Dp(4), 0, Dp(12));
        page.AddView(subtitle);

        var modelRow = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        modelRow.SetGravity(GravityFlags.CenterVertical);
        _modelSpinner = new Spinner(this);
        _modelSpinner.ItemSelected += ModelSpinner_ItemSelected;
        modelRow.AddView(_modelSpinner, new LinearLayout.LayoutParams(0, Dp(48), 1));

        _connectButton = MakeButton("Connect", "#264A76", "#FFFFFF");
        _connectButton.Click += async (_, _) => await RefreshModelsAsync();
        modelRow.AddView(_connectButton, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            Dp(44)));
        page.AddView(modelRow, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));

        _connectionStatus = new TextView(this)
        {
            Text = "Connect to your PC to load models.",
            TextSize = 12
        };
        _connectionStatus.SetTextColor(Color.ParseColor("#68758B"));
        _connectionStatus.SetPadding(Dp(2), 0, 0, Dp(8));
        page.AddView(_connectionStatus);

        _messageScroll = new ScrollView(this)
        {
            FillViewport = true
        };
        _messages = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        _messages.SetPadding(0, Dp(6), 0, Dp(12));
        _messageScroll.AddView(_messages);
        page.AddView(_messageScroll, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            0,
            1));

        var composer = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        composer.SetGravity(GravityFlags.CenterVertical);
        _messageInput = new EditText(this)
        {
            Hint = "Message Choppa...",
            ImeOptions = ImeAction.Send,
            InputType = global::Android.Text.InputTypes.ClassText
                | global::Android.Text.InputTypes.TextFlagCapSentences
        };
        _messageInput.SetSingleLine(true);
        _messageInput.SetTextColor(Color.ParseColor("#172B45"));
        _messageInput.SetHintTextColor(Color.ParseColor("#8792A5"));
        _messageInput.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
        _messageInput.EditorAction += async (_, args) =>
        {
            if (args is not null && args.ActionId == ImeAction.Send)
            {
                args.Handled = true;
                await SendMessageAsync();
            }
        };
        composer.AddView(_messageInput, new LinearLayout.LayoutParams(0, Dp(52), 1));

        _sendButton = MakeButton("Send", "#264A76", "#FFFFFF");
        _sendButton.Click += async (_, _) => await SendMessageAsync();
        composer.AddView(_sendButton, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            Dp(48)));
        page.AddView(composer, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));

        SetContentView(page);
    }

    private async Task RefreshModelsAsync()
    {
        _connectButton.Enabled = false;
        _connectionStatus.Text = $"Connecting to {_serverUrl}...";
        _connectionStatus.SetTextColor(Color.ParseColor("#68758B"));
        try
        {
            using var response = await OllamaClient.GetAsync(BuildApiUri("api/tags"));
            await EnsureSuccessAsync(response);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("models", out var modelList)
                || modelList.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Ollama's model list was missing or invalid.");
            }

            var availableModels = modelList
                .EnumerateArray()
                .Select(model => model.TryGetProperty("name", out var name) ? name.GetString() : null)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (availableModels.Length == 0)
            {
                throw new InvalidOperationException(
                    "Ollama is running, but no models are installed. Run `ollama pull mistral` on your PC.");
            }

            _models.Clear();
            _models.AddRange(availableModels);
            _updatingModels = true;
            var adapter = new ArrayAdapter<string>(
                this,
                global::Android.Resource.Layout.SimpleSpinnerItem,
                _models);
            adapter.SetDropDownViewResource(global::Android.Resource.Layout.SimpleSpinnerDropDownItem);
            _modelSpinner.Adapter = adapter;
            var selectedIndex = _models.FindIndex(
                model => string.Equals(model, _model, StringComparison.OrdinalIgnoreCase));
            _modelSpinner.SetSelection(selectedIndex >= 0 ? selectedIndex : 0);
            _model = _models[selectedIndex >= 0 ? selectedIndex : 0];
            _updatingModels = false;
            SaveSettings();
            _connectionStatus.Text = $"Connected. {_models.Count} model(s) available.";
            _connectionStatus.SetTextColor(Color.ParseColor("#247447"));
        }
        catch (HttpRequestException error)
        {
            ShowConnectionError($"Couldn't reach Ollama. Check the PC URL, network, and Ollama settings. {error.Message}");
        }
        catch (TaskCanceledException error)
        {
            ShowConnectionError($"Ollama took too long to respond. {error.Message}");
        }
        catch (JsonException error)
        {
            ShowConnectionError($"Ollama returned an unreadable model list. {error.Message}");
        }
        catch (InvalidDataException error)
        {
            ShowConnectionError(error.Message);
        }
        catch (InvalidOperationException error)
        {
            ShowConnectionError(error.Message);
        }
        catch (UriFormatException error)
        {
            ShowConnectionError($"The PC URL is invalid. {error.Message}");
        }
        finally
        {
            _updatingModels = false;
            _connectButton.Enabled = true;
        }
    }

    private async Task SendMessageAsync()
    {
        if (_isSending)
        {
            return;
        }

        var prompt = _messageInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }
        if (BasicPhraseResponder.TryRespond(prompt, out var phraseResponse))
        {
            _messageInput.Text = string.Empty;
            _messageInput.ClearFocus();
            ((InputMethodManager?)GetSystemService(InputMethodService))?
                .HideSoftInputFromWindow(_messageInput.WindowToken, HideSoftInputFlags.None);
            AddMessage(prompt, fromUser: true);
            AddMessage(phraseResponse, fromUser: false);
            _chatHistory.Add(("user", prompt));
            _chatHistory.Add(("assistant", phraseResponse));
            if (_chatHistory.Count > 20)
            {
                _chatHistory.RemoveRange(0, _chatHistory.Count - 20);
            }
            return;
        }
        if (_models.Count == 0)
        {
            AddMessage("Connect to Ollama and load a model before chatting.", fromUser: false);
            return;
        }

        _messageInput.Text = string.Empty;
        _messageInput.ClearFocus();
        ((InputMethodManager?)GetSystemService(InputMethodService))?
            .HideSoftInputFromWindow(_messageInput.WindowToken, HideSoftInputFlags.None);
        AddMessage(prompt, fromUser: true);
        _chatHistory.Add(("user", prompt));
        if (_chatHistory.Count > 20)
        {
            _chatHistory.RemoveRange(0, _chatHistory.Count - 20);
        }

        _isSending = true;
        _sendButton.Enabled = false;
        _messageInput.Enabled = false;
        try
        {
            var messagesList = new List<object>
            {
                new { role = "system", content = SystemPrompt }
            };
            messagesList.AddRange(_chatHistory.Select(m => new { role = m.Role, content = m.Content }));

            var request = new
            {
                model = _model,
                stream = false,
                format = "json",
                messages = messagesList,
                options = new
                {
                    temperature = 0.7,
                    num_predict = 180
                }
            };
            using var response = await OllamaClient.PostAsJsonAsync(
                BuildApiUri("api/chat"),
                request);
            await EnsureSuccessAsync(response);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            if (!root.TryGetProperty("message", out var message)
                || !message.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(content.GetString()))
            {
                throw new InvalidDataException("Ollama's chat response did not contain a message.");
            }

            var reply = ParseAssistantReply(content.GetString()!);
            _chatHistory.Add(("assistant", reply));
            if (_chatHistory.Count > 20)
            {
                _chatHistory.RemoveRange(0, _chatHistory.Count - 20);
            }
            AddMessage(reply, fromUser: false);
        }
        catch (HttpRequestException error)
        {
            AddMessage($"Couldn't reach Ollama. Check the PC URL and network. {error.Message}", fromUser: false);
        }
        catch (TaskCanceledException error)
        {
            AddMessage($"Ollama took too long to respond. {error.Message}", fromUser: false);
        }
        catch (JsonException error)
        {
            AddMessage($"Ollama returned an unreadable reply. {error.Message}", fromUser: false);
        }
        catch (InvalidDataException error)
        {
            AddMessage(error.Message, fromUser: false);
        }
        catch (InvalidOperationException error)
        {
            AddMessage(error.Message, fromUser: false);
        }
        catch (UriFormatException error)
        {
            AddMessage($"The PC URL is invalid. {error.Message}", fromUser: false);
        }
        finally
        {
            _isSending = false;
            _sendButton.Enabled = true;
            _messageInput.Enabled = true;
        }
    }

    private static string ParseAssistantReply(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        if (!root.TryGetProperty("canPerform", out var canPerform)
            || canPerform.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !root.TryGetProperty("response", out var response)
            || response.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Ollama's reply was missing its response or capability result.");
        }

        var text = response.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidDataException("Ollama returned an empty reply.");
        }
        return text.Length > 1500 ? $"{text[..1497].TrimEnd()}..." : text;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var details = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            $"Ollama returned {(int)response.StatusCode} ({response.ReasonPhrase}): {details}");
    }

    private Uri BuildApiUri(string path)
    {
        var server = _serverUrl.Trim().TrimEnd('/') + "/";
        if (!Uri.TryCreate(server, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new UriFormatException("Enter a full HTTP or HTTPS address for Ollama.");
        }
        return new Uri(baseUri, path);
    }

    private void ShowConnectionSettings()
    {
        var content = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        content.SetPadding(Dp(22), Dp(4), Dp(22), 0);
        var address = new EditText(this)
        {
            Text = _serverUrl,
            Hint = "http://192.168.1.20:11434",
            InputType = global::Android.Text.InputTypes.ClassText
                | global::Android.Text.InputTypes.TextVariationUri
        };
        address.SetSingleLine(true);
        content.AddView(address);
        var help = new TextView(this)
        {
            Text = "Use your PC's LAN IP and port 11434 on a phone. The emulator default is 10.0.2.2. Ollama must allow connections from your local network.",
            TextSize = 13
        };
        help.SetTextColor(Color.ParseColor("#68758B"));
        help.SetPadding(0, Dp(8), 0, 0);
        content.AddView(help);

        var dialogBuilder = new AlertDialog.Builder(this);
        dialogBuilder.SetTitle("Connect to Ollama");
        dialogBuilder.SetView(content);
        dialogBuilder.SetNegativeButton("Cancel", (_, _) => { });
        dialogBuilder.SetPositiveButton("Save and connect", (_, _) =>
        {
            var server = address.Text?.Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(server)
                || !Uri.TryCreate(server, UriKind.Absolute, out var uri)
                || uri is null)
            {
                Toast.MakeText(this, "Enter a full HTTP or HTTPS URL.", ToastLength.Long)?.Show();
                return;
            }
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                Toast.MakeText(this, "Enter a full HTTP or HTTPS URL.", ToastLength.Long)?.Show();
                return;
            }

            _serverUrl = uri.ToString().TrimEnd('/');
            SaveSettings();
            _ = RefreshModelsAsync();
        });
        var dialog = dialogBuilder.Create()
            ?? throw new InvalidOperationException("The connection settings dialog could not be created.");
        dialog.Show();
    }

    private void ModelSpinner_ItemSelected(object? sender, AdapterView.ItemSelectedEventArgs e)
    {
        if (_updatingModels || e.Position < 0 || e.Position >= _models.Count)
        {
            return;
        }
        _model = _models[e.Position];
        SaveSettings();
    }

    private void AddMessage(string text, bool fromUser)
    {
        var row = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal
        };
        row.SetGravity(fromUser ? GravityFlags.Right : GravityFlags.Left);
        var bubble = new TextView(this)
        {
            Text = text,
            TextSize = 15
        };
        bubble.SetMaxWidth(Dp(310));
        bubble.SetTextColor(fromUser ? Color.White : Color.ParseColor("#172B45"));
        bubble.SetPadding(Dp(14), Dp(10), Dp(14), Dp(10));
        var background = new GradientDrawable();
        background.SetColor(fromUser ? Color.ParseColor("#264A76") : Color.White);
        background.SetCornerRadius(Dp(18));
        bubble.Background = background;

        var bubbleParams = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent);
        bubbleParams.SetMargins(
            fromUser ? Dp(46) : 0,
            Dp(5),
            fromUser ? 0 : Dp(46),
            Dp(5));
        row.AddView(bubble, bubbleParams);
        _messages.AddView(row, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.WrapContent));
        _messageScroll.Post(() => _messageScroll.FullScroll(FocusSearchDirection.Down));
    }

    private Button MakeButton(string text, string background, string foreground)
    {
        var button = new Button(this)
        {
            Text = text,
            TextSize = 13
        };
        button.SetAllCaps(false);
        button.SetTextColor(Color.ParseColor(foreground));
        button.BackgroundTintList = global::Android.Content.Res.ColorStateList.ValueOf(
            Color.ParseColor(background));
        return button;
    }

    private void ShowConnectionError(string message)
    {
        _connectionStatus.Text = message;
        _connectionStatus.SetTextColor(Color.ParseColor("#B3261E"));
    }

    private void LoadSettings()
    {
        var preferences = GetSharedPreferences("choppa-settings", FileCreationMode.Private)!;
        _serverUrl = preferences.GetString("server_url", DefaultServerUrl) ?? DefaultServerUrl;
        _model = preferences.GetString("model", DefaultModel) ?? DefaultModel;
    }

    private void SaveSettings()
    {
        var preferences = GetSharedPreferences("choppa-settings", FileCreationMode.Private)!;
        var editor = preferences.Edit();
        if (editor is null)
        {
            throw new InvalidOperationException("Android could not open app preferences.");
        }

        using (editor)
        {
            editor.PutString("server_url", _serverUrl);
            editor.PutString("model", _model);
            editor.Apply();
        }
    }

    private int Dp(float value) => (int)(value * Resources!.DisplayMetrics!.Density + 0.5f);
}
