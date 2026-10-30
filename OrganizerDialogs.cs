using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ChoppaAI;

internal enum OrganizerEntryKind
{
    Alarm,
    Appointment,
    Device,
}

internal sealed class OrganizerEntryDialog : Window
{
    private readonly OrganizerEntryKind _kind;
    private readonly TextBox _titleInput = new();
    private readonly DatePicker _dateInput = new();
    private readonly TextBox _timeInput = new();
    private readonly TextBox _deviceInput = new();
    private readonly TextBox _issueInput = new();
    private readonly TextBox _statusInput = new();
    private readonly TextBox _locationInput = new();
    private readonly TextBox _notesInput = new();
    private readonly TextBlock _errorMessage = new();

    public OrganizerEntryDialog(OrganizerEntryKind kind)
    {
        _kind = kind;
        Title = kind switch
        {
            OrganizerEntryKind.Alarm => "Set an alarm",
            OrganizerEntryKind.Appointment => "Save an appointment",
            OrganizerEntryKind.Device => "Log a device",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        Width = 430;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height - 40;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Background = Brushes.White;

        var layout = new DockPanel { Margin = new Thickness(18) };
        Content = layout;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(buttons);

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 82,
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true,
        };
        buttons.Children.Add(cancelButton);
        var saveButton = new Button
        {
            Content = kind == OrganizerEntryKind.Alarm ? "Set alarm" : "Save",
            MinWidth = 82,
            IsDefault = true,
        };
        saveButton.Click += Save_Click;
        buttons.Children.Add(saveButton);

        var fields = new StackPanel();
        layout.Children.Add(fields);
        fields.Children.Add(new TextBlock
        {
            Text = kind switch
            {
                OrganizerEntryKind.Alarm => "What should I remind you about?",
                OrganizerEntryKind.Appointment => "What’s the appointment?",
                OrganizerEntryKind.Device => "What device are you logging?",
                _ => string.Empty,
            },
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12),
        });

        switch (kind)
        {
            case OrganizerEntryKind.Alarm:
                AddField(fields, "Alarm", _titleInput, "Alarm label");
                AddScheduleFields(fields);
                break;
            case OrganizerEntryKind.Appointment:
                AddField(fields, "Appointment", _titleInput, "Title or person");
                AddScheduleFields(fields);
                AddField(fields, "Location (optional)", _locationInput, "Location");
                AddField(fields, "Notes (optional)", _notesInput, "Notes", multiline: true);
                break;
            case OrganizerEntryKind.Device:
                AddField(fields, "Device or model", _deviceInput, "For example, Pixel 8");
                AddField(fields, "Issue or reason", _issueInput, "What should I remember?");
                AddField(fields, "Status (optional)", _statusInput, "For example, waiting for parts");
                AddField(fields, "Notes (optional)", _notesInput, "Notes", multiline: true);
                fields.Children.Add(new TextBlock
                {
                    Text = "Don’t include passwords, IMEI, or serial numbers.",
                    Foreground = Brushes.DimGray,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 5, 0, 0),
                });
                break;
        }

        _errorMessage.Foreground = Brushes.Firebrick;
        _errorMessage.TextWrapping = TextWrapping.Wrap;
        _errorMessage.Margin = new Thickness(0, 8, 0, 0);
        fields.Children.Add(_errorMessage);
    }

    public string TitleText => _titleInput.Text.Trim();
    public string DeviceText => _deviceInput.Text.Trim();
    public string IssueText => _issueInput.Text.Trim();
    public string StatusText => _statusInput.Text.Trim();
    public string LocationText => _locationInput.Text.Trim();
    public string NotesText => _notesInput.Text.Trim();
    public DateTimeOffset DueAt { get; private set; }

    private void AddScheduleFields(Panel parent)
    {
        _dateInput.SelectedDate = DateTime.Today;
        _dateInput.Margin = new Thickness(0, 0, 10, 0);
        _timeInput.Text = DateTime.Now.AddMinutes(5).ToString("HH:mm", CultureInfo.InvariantCulture);

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_dateInput);
        _timeInput.Width = 82;
        _timeInput.VerticalContentAlignment = VerticalAlignment.Center;
        _timeInput.Padding = new Thickness(6, 4, 6, 4);
        row.Children.Add(_timeInput);
        AddField(parent, "Date and time (24-hour clock)", row);
    }

    private static void AddField(
        Panel parent,
        string label,
        FrameworkElement input,
        string? placeholder = null,
        bool multiline = false)
    {
        var group = new StackPanel { Margin = new Thickness(0, 0, 0, 9) };
        group.Children.Add(new TextBlock
        {
            Text = label,
            Margin = new Thickness(0, 0, 0, 3),
            FontWeight = FontWeights.SemiBold,
        });

        if (input is TextBox textBox)
        {
            textBox.Padding = new Thickness(7, 5, 7, 5);
            textBox.VerticalContentAlignment = VerticalAlignment.Center;
            if (multiline)
            {
                textBox.AcceptsReturn = true;
                textBox.TextWrapping = TextWrapping.Wrap;
                textBox.MinHeight = 58;
                textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            else
            {
                textBox.MinHeight = 30;
            }

            if (placeholder is not null)
            {
                textBox.ToolTip = placeholder;
            }
        }

        group.Children.Add(input);
        parent.Children.Add(group);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_kind == OrganizerEntryKind.Device)
        {
            if (string.IsNullOrWhiteSpace(DeviceText) || string.IsNullOrWhiteSpace(IssueText))
            {
                _errorMessage.Text = "Enter both a device/model and an issue or reason.";
                return;
            }

            DialogResult = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(TitleText))
        {
            _errorMessage.Text = _kind == OrganizerEntryKind.Alarm
                ? "Enter what the alarm is for."
                : "Enter an appointment title.";
            return;
        }

        if (_dateInput.SelectedDate is not DateTime selectedDate
            || !TimeOnly.TryParseExact(
                _timeInput.Text.Trim(),
                ["H:mm", "HH:mm"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var time))
        {
            _errorMessage.Text = "Choose a date and enter a time in 24-hour format, such as 14:30.";
            return;
        }

        var localDateTime = DateTime.SpecifyKind(
            selectedDate.Date.Add(time.ToTimeSpan()),
            DateTimeKind.Unspecified);
        if (TimeZoneInfo.Local.IsInvalidTime(localDateTime))
        {
            _errorMessage.Text = "That local time does not exist because the clock changes. Choose another time.";
            return;
        }

        DueAt = new DateTimeOffset(localDateTime, TimeZoneInfo.Local.GetUtcOffset(localDateTime));
        if (DueAt <= DateTimeOffset.Now)
        {
            _errorMessage.Text = "Choose a future date and time.";
            return;
        }

        DialogResult = true;
    }
}

internal sealed class AlarmDialog : Window
{
    public AlarmDialog(AlarmRecord alarm)
    {
        Title = "Broski alarm";
        Width = 360;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;
        Topmost = true;

        var layout = new StackPanel { Margin = new Thickness(20) };
        Content = layout;
        layout.Children.Add(new TextBlock
        {
            Text = $"Alarm: {alarm.Title}",
            FontSize = 19,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        });
        layout.Children.Add(new TextBlock
        {
            Text = $"Scheduled for {alarm.DueAt.LocalDateTime.ToString("f", CultureInfo.CurrentCulture)}",
            Margin = new Thickness(0, 0, 0, 16),
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        layout.Children.Add(buttons);

        var snooze = new Button
        {
            Content = "Snooze 5 min",
            MinWidth = 105,
            Margin = new Thickness(0, 0, 8, 0),
        };
        snooze.Click += (_, _) =>
        {
            Snoozed = true;
            DialogResult = true;
        };
        buttons.Children.Add(snooze);

        var dismiss = new Button
        {
            Content = "Dismiss",
            MinWidth = 85,
            IsDefault = true,
            IsCancel = true,
        };
        dismiss.Click += (_, _) =>
        {
            Snoozed = false;
            DialogResult = true;
        };
        buttons.Children.Add(dismiss);
    }

    public bool Snoozed { get; private set; }
}

internal sealed class OrganizerOverviewDialog : Window
{
    public OrganizerOverviewDialog(OrganizerDataStore organizer)
    {
        Title = "Saved organizer";
        Width = 520;
        Height = 410;
        MinWidth = 420;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Brushes.White;

        var layout = new DockPanel { Margin = new Thickness(14) };
        Content = layout;

        var done = new Button
        {
            Content = "Done",
            MinWidth = 80,
            IsDefault = true,
            IsCancel = true,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
        };
        DockPanel.SetDock(done, Dock.Bottom);
        layout.Children.Add(done);

        var tabs = new TabControl();
        layout.Children.Add(tabs);
        tabs.Items.Add(CreateTab("Alarms", organizer.Alarms
            .OrderBy(alarm => alarm.DueAt)
            .Select(alarm => $"{alarm.DueAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)} — {alarm.Title}")));
        tabs.Items.Add(CreateTab("Appointments", organizer.Appointments
            .OrderBy(appointment => appointment.StartsAt)
            .Select(appointment =>
                $"{appointment.StartsAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)} — {appointment.Title}" +
                (string.IsNullOrWhiteSpace(appointment.Location) ? string.Empty : $" ({appointment.Location})"))));
        tabs.Items.Add(CreateTab("Devices", organizer.Devices
            .OrderByDescending(device => device.LoggedAt)
            .Select(device =>
                $"{device.Device} — {device.Issue}" +
                (string.IsNullOrWhiteSpace(device.Status) ? string.Empty : $" [{device.Status}]"))));
    }

    private static TabItem CreateTab(string header, IEnumerable<string> records)
    {
        var items = records.ToArray();
        UIElement content = items.Length == 0
            ? new TextBlock
            {
                Text = "Nothing saved here yet.",
                Margin = new Thickness(12),
            }
            : new ListBox
            {
                ItemsSource = items,
                Padding = new Thickness(6),
            };
        return new TabItem { Header = header, Content = content };
    }
}
