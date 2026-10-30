using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows.Forms;

namespace ChoppaAI.Setup;

internal static class Program
{
    private const string ProductId = "ChoppaAI.DesktopPet";
    private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ChoppaAI";
    private const string ManifestFileName = "InstallManifest.json";
    private const string UninstallerFileName = "BroskiUninstaller.exe";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SetupForm());
    }

    private sealed class SetupForm : Form
    {
        private readonly TextBox _pathInput = new();
        private readonly Button _installButton = new();
        private readonly Label _status = new();
        private readonly ProgressBar _progress = new();

        public SetupForm()
        {
            Text = "Broski AI Setup 0.0.3";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new System.Drawing.Size(620, 370);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new System.Drawing.Font("Segoe UI", 10);
            BackColor = System.Drawing.Color.FromArgb(24, 22, 31);
            ForeColor = System.Drawing.Color.FromArgb(245, 242, 250);

            var header = new Panel
            {
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(620, 145),
                BackColor = System.Drawing.Color.FromArgb(105, 61, 190)
            };
            Controls.Add(header);
            header.Controls.Add(new Label
            {
                Text = "Install Broski AI",
                Location = new System.Drawing.Point(28, 24),
                Size = new System.Drawing.Size(540, 38),
                Font = new System.Drawing.Font("Segoe UI Semibold", 20),
                ForeColor = System.Drawing.Color.White
            });
            header.Controls.Add(new Label
            {
                Text = "Setup installs version 0.0.3 and creates Start menu, desktop, and sign-in startup shortcuts. Ollama is separate and is not included.",
                Location = new System.Drawing.Point(30, 73),
                Size = new System.Drawing.Size(555, 62),
                ForeColor = System.Drawing.Color.FromArgb(244, 237, 255)
            });

            Controls.Add(new Label
            {
                Text = "Install location",
                Location = new System.Drawing.Point(30, 158),
                Size = new System.Drawing.Size(150, 25)
            });
            _pathInput.Text = GetDefaultInstallDirectory();
            _pathInput.Location = new System.Drawing.Point(30, 186);
            _pathInput.Size = new System.Drawing.Size(430, 30);
            _pathInput.BackColor = System.Drawing.Color.FromArgb(39, 36, 49);
            _pathInput.ForeColor = ForeColor;
            _pathInput.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(_pathInput);

            var browse = CreateButton("Browse...", 472, 184, 105, 32);
            browse.Click += (_, _) => BrowseForDirectory();
            Controls.Add(browse);

            _progress.Location = new System.Drawing.Point(30, 232);
            _progress.Size = new System.Drawing.Size(547, 12);
            _progress.Style = ProgressBarStyle.Marquee;
            _progress.Visible = false;
            Controls.Add(_progress);

            _status.Text = "Choose an install location, then select Install.";
            _status.Location = new System.Drawing.Point(30, 253);
            _status.Size = new System.Drawing.Size(547, 42);
            _status.ForeColor = System.Drawing.Color.FromArgb(210, 204, 221);
            Controls.Add(_status);

            var cancel = CreateButton("Cancel", 382, 315, 90, 34);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            _installButton.Text = "Install";
            _installButton.Location = new System.Drawing.Point(482, 315);
            _installButton.Size = new System.Drawing.Size(95, 34);
            _installButton.UseVisualStyleBackColor = false;
            _installButton.BackColor = System.Drawing.Color.FromArgb(130, 82, 220);
            _installButton.ForeColor = System.Drawing.Color.White;
            _installButton.Click += InstallClicked;
            Controls.Add(_installButton);
            AcceptButton = _installButton;
            CancelButton = cancel;
        }

        private static Button CreateButton(string text, int x, int y, int width, int height) => new()
        {
            Text = text,
            Location = new System.Drawing.Point(x, y),
            Size = new System.Drawing.Size(width, height),
            UseVisualStyleBackColor = false,
            BackColor = System.Drawing.Color.FromArgb(49, 45, 61),
            ForeColor = System.Drawing.Color.FromArgb(245, 242, 250)
        };

        private static string GetDefaultInstallDirectory()
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath);
            var registeredDirectory = key?.GetValue("InstallLocation") as string;
            if (!string.IsNullOrWhiteSpace(registeredDirectory))
            {
                return registeredDirectory;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Broski");
        }

        private void BrowseForDirectory()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choose a parent folder for the Broski installation folder.",
                ShowNewFolderButton = true,
                SelectedPath = Path.GetDirectoryName(_pathInput.Text) ?? string.Empty
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _pathInput.Text = Path.Combine(dialog.SelectedPath, "Broski");
            }
        }

        private async void InstallClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_pathInput.Text))
            {
                MessageBox.Show(this, "Enter an install location to continue.", "Install Broski AI",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            _status.Text = "Installing Broski AI...";
            try
            {
                var result = await Task.Run(() => Install(_pathInput.Text.Trim()));
                var start = MessageBox.Show(this,
                    $"Broski AI was installed successfully.\r\n\r\nLocation: {result}\r\n\r\nStart Broski now?",
                    "Broski installation complete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                if (start == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(Path.Combine(result, "BroskiLauncher.exe"))
                    {
                        WorkingDirectory = result,
                        UseShellExecute = true
                    });
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception exception)
            {
                _status.Text = "Setup could not complete.";
                MessageBox.Show(this,
                    $"Broski was not installed.\r\n\r\n{exception.Message}",
                    "Broski setup error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _pathInput.Enabled = !busy;
            _installButton.Enabled = !busy;
            _progress.Visible = busy;
            UseWaitCursor = busy;
        }
    }

    private static string Install(string requestedDirectory)
    {
        var installDirectory = Path.GetFullPath(requestedDirectory);
        ValidateInstallDirectory(installDirectory);
        EnsureAppsAreClosed(installDirectory);

        using var uninstallKey = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath);
        var registeredDirectory = uninstallKey?.GetValue("InstallLocation") as string;
        InstallationManifest? previousManifest = null;
        string? previousDirectory = null;
        if (!string.IsNullOrWhiteSpace(registeredDirectory))
        {
            previousDirectory = Path.GetFullPath(registeredDirectory);
            previousManifest = ReadManifest(previousDirectory);
            if (previousDirectory.Equals(installDirectory, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(installDirectory))
            {
                var oldFiles = previousManifest.Files.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var file in Directory.EnumerateFiles(installDirectory, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(installDirectory, file);
                    if (relativePath.Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!oldFiles.Contains(relativePath))
                    {
                        throw new IOException($"Setup will not overwrite an unrelated file: {file}");
                    }
                }
            }
        }

        if (Directory.Exists(installDirectory) &&
            File.Exists(Path.Combine(installDirectory, ManifestFileName)))
        {
            var targetManifest = ReadManifest(installDirectory);
            if (previousManifest is not null &&
                !installDirectory.Equals(previousDirectory, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("A second Broski installation already exists at the selected destination.");
            }

            previousManifest ??= targetManifest;
            previousDirectory ??= installDirectory;
        }
        else if (Directory.Exists(installDirectory) && Directory.EnumerateFileSystemEntries(installDirectory).Any())
        {
            throw new IOException($"The destination folder is not empty. Choose another folder: {installDirectory}");
        }
        else if (File.Exists(installDirectory))
        {
            throw new IOException($"The destination path is not a folder: {installDirectory}");
        }

        var shortcuts = GetShortcutPaths();
        var allowedShortcuts = shortcuts.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownShortcuts = previousManifest?.Shortcuts
            .Where(shortcut => allowedShortcuts.Contains(shortcut))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        foreach (var shortcut in shortcuts.Values)
        {
            if (File.Exists(shortcut) && !knownShortcuts.Contains(shortcut))
            {
                throw new IOException($"Setup will not overwrite an unrelated shortcut: {shortcut}");
            }
        }

        var stagingDirectory = Path.Combine(Path.GetTempPath(), $"Broski-Install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            ExtractPayload(stagingDirectory);
            var files = Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(stagingDirectory, file))
                .ToArray();
            var directories = Directory.EnumerateDirectories(stagingDirectory, "*", SearchOption.AllDirectories)
                .Select(directory => Path.GetRelativePath(stagingDirectory, directory))
                .ToArray();

            Directory.CreateDirectory(installDirectory);
            foreach (var file in files)
            {
                var source = GetSafePath(stagingDirectory, file);
                var destination = GetSafePath(installDirectory, file);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, overwrite: true);
            }

            CreateShortcut(shortcuts["start"], Path.Combine(installDirectory, "BroskiLauncher.exe"),
                installDirectory, "Broski AI desktop companion", Path.Combine(installDirectory, "Broski.exe"));
            CreateShortcut(shortcuts["uninstall"], Path.Combine(installDirectory, UninstallerFileName),
                installDirectory, "Uninstall Broski AI", Path.Combine(installDirectory, "Broski.exe"));
            CreateShortcut(shortcuts["startup"], Path.Combine(installDirectory, "BroskiLauncher.exe"),
                installDirectory, "Start Broski when I sign in", Path.Combine(installDirectory, "Broski.exe"));
            CreateShortcut(shortcuts["desktop"], Path.Combine(installDirectory, "BroskiLauncher.exe"),
                installDirectory, "Broski AI desktop companion", Path.Combine(installDirectory, "Broski.exe"));

            var manifest = new InstallationManifest
            {
                ProductId = ProductId,
                Version = "0.0.3",
                InstallDirectory = installDirectory,
                Files = files,
                Directories = directories,
                Shortcuts = shortcuts.Values.ToArray()
            };
            WriteManifest(installDirectory, manifest);
            WriteUninstallRegistration(installDirectory);

            if (previousManifest is not null && previousDirectory is not null &&
                !previousDirectory.Equals(installDirectory, StringComparison.OrdinalIgnoreCase))
            {
                RemoveOwnedFiles(previousDirectory, previousManifest, removeShortcuts: false);
            }
            else if (previousManifest is not null)
            {
                RemoveObsoleteOwnedFiles(installDirectory, previousManifest, manifest);
            }

            return installDirectory;
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private static void ValidateInstallDirectory(string directory)
    {
        var root = Path.GetPathRoot(directory);
        if (string.IsNullOrEmpty(root) ||
            directory.TrimEnd(Path.DirectorySeparatorChar).Equals(root.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Choose a dedicated installation folder, not a drive root.");
        }

        var protectedFolders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };
        foreach (var protectedFolder in protectedFolders.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var protectedRoot = Path.GetFullPath(protectedFolder)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (directory.StartsWith(protectedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"Choose a user-writable installation folder instead of {protectedFolder}.");
            }
        }
    }

    private static void EnsureAppsAreClosed(string installDirectory)
    {
        var allowedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(installDirectory, "Broski.exe"),
            Path.Combine(installDirectory, "BroskiLauncher.exe"),
            Path.Combine(installDirectory, "ChoppaAI.exe")
        };
        using var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath);
        if (key?.GetValue("InstallLocation") is string registered &&
            !string.IsNullOrWhiteSpace(registered))
        {
            foreach (var name in new[] { "Broski", "BroskiLauncher", "ChoppaAI" })
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    using (process)
                    {
                        try
                        {
                            var processPath = process.MainModule?.FileName;
                            if (!string.IsNullOrEmpty(processPath) &&
                                (Path.GetFullPath(processPath)
                                     .StartsWith(Path.GetFullPath(registered).TrimEnd(Path.DirectorySeparatorChar) +
                                                 Path.DirectorySeparatorChar,
                                         StringComparison.OrdinalIgnoreCase) ||
                                 allowedPaths.Contains(Path.GetFullPath(processPath))))
                            {
                                throw new IOException("Close Broski before running setup so the old installation can be replaced.");
                            }
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                            throw new IOException("Close Broski before running setup so the old installation can be replaced.");
                        }
                    }
                }
            }
        }
    }

    private static void ExtractPayload(string destination)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Broski-Payload.zip")
            ?? throw new InvalidOperationException("The Broski application payload is missing from setup.");
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            var path = GetSafePath(destination, entry.FullName);
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(path);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var source = entry.Open();
            using var output = File.Create(path);
            source.CopyTo(output);
        }

        foreach (var required in new[] { "Broski.exe", "BroskiLauncher.exe", UninstallerFileName })
        {
            if (!File.Exists(Path.Combine(destination, required)))
            {
                throw new InvalidDataException($"The setup payload is missing {required}.");
            }
        }
        if (!Directory.Exists(Path.Combine(destination, "animations")) ||
            !Directory.Exists(Path.Combine(destination, "blueprints")))
        {
            throw new InvalidDataException("The setup payload is missing required animation or blueprint assets.");
        }
    }

    private static string GetSafePath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException($"A setup path is invalid: {relativePath}");
        }

        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"A setup path escapes its destination: {relativePath}");
        }

        return fullPath;
    }

    private static InstallationManifest ReadManifest(string directory)
    {
        var path = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(path))
        {
            throw new IOException($"The existing installation manifest is missing: {path}");
        }

        var manifest = JsonSerializer.Deserialize<InstallationManifest>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The existing installation manifest is invalid.");
        if (manifest.ProductId != ProductId ||
            !Path.GetFullPath(manifest.InstallDirectory).TrimEnd(Path.DirectorySeparatorChar)
                .Equals(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The destination contains an unrecognized installation manifest.");
        }

        foreach (var relativePath in manifest.Files.Concat(manifest.Directories))
        {
            _ = GetSafePath(directory, relativePath);
        }
        return manifest;
    }

    private static void WriteManifest(string directory, InstallationManifest manifest)
    {
        var path = Path.Combine(directory, ManifestFileName);
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(manifest, JsonOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static Dictionary<string, string> GetShortcutPaths()
    {
        var programs = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "Broski AI");
        var startup = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "Startup");
        return new Dictionary<string, string>
        {
            ["start"] = Path.Combine(programs, "Broski.lnk"),
            ["uninstall"] = Path.Combine(programs, "Uninstall Broski.lnk"),
            ["startup"] = Path.Combine(startup, "Broski.lnk"),
            ["desktop"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Broski.lnk")
        };
    }

    private static void CreateShortcut(string path, string target, string workingDirectory, string description, string icon)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows Script Host is unavailable; setup cannot create shortcuts.");
        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Windows could not create the shortcut service.");
            dynamic shellObject = shell;
            shortcut = shellObject.CreateShortcut(path);
            dynamic shortcutObject = shortcut;
            shortcutObject.TargetPath = target;
            shortcutObject.WorkingDirectory = workingDirectory;
            shortcutObject.Description = description;
            shortcutObject.IconLocation = icon;
            shortcutObject.Save();
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                Marshal.FinalReleaseComObject(shortcut);
            }
            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    private static void WriteUninstallRegistration(string directory)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath, writable: true)
            ?? throw new IOException("Windows could not register Broski AI for uninstall.");
        var uninstaller = Path.Combine(directory, UninstallerFileName);
        key.SetValue("DisplayName", "Broski AI");
        key.SetValue("DisplayVersion", "0.0.3");
        key.SetValue("Publisher", "Broski AI");
        key.SetValue("InstallLocation", directory);
        key.SetValue("DisplayIcon", Path.Combine(directory, "Broski.exe"));
        key.SetValue("UninstallString", $"\"{uninstaller}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void RemoveObsoleteOwnedFiles(
        string directory,
        InstallationManifest oldManifest,
        InstallationManifest newManifest)
    {
        var currentFiles = newManifest.Files.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var relativePath in oldManifest.Files.Where(file => !currentFiles.Contains(file)))
        {
            var path = GetSafePath(directory, relativePath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        var currentDirectories = newManifest.Directories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var relativePath in oldManifest.Directories
                     .Where(dir => !currentDirectories.Contains(dir))
                     .OrderByDescending(dir => dir.Length))
        {
            var path = GetSafePath(directory, relativePath);
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
    }

    private static void RemoveOwnedFiles(
        string directory,
        InstallationManifest manifest,
        bool removeShortcuts)
    {
        foreach (var relativePath in manifest.Files)
        {
            var path = GetSafePath(directory, relativePath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        var manifestPath = Path.Combine(directory, ManifestFileName);
        if (File.Exists(manifestPath))
        {
            File.Delete(manifestPath);
        }
        if (removeShortcuts)
        {
            var allowedShortcuts = GetShortcutPaths().Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var shortcut in manifest.Shortcuts.Where(allowedShortcuts.Contains))
            {
                if (File.Exists(shortcut))
                {
                    File.Delete(shortcut);
                }
            }
        }
        foreach (var relativePath in manifest.Directories.OrderByDescending(dir => dir.Length))
        {
            var path = GetSafePath(directory, relativePath);
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
    }
}

internal sealed class InstallationManifest
{
    public string ProductId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string InstallDirectory { get; set; } = string.Empty;
    public string[] Files { get; set; } = [];
    public string[] Directories { get; set; } = [];
    public string[] Shortcuts { get; set; } = [];
}
