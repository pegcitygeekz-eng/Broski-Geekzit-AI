using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows.Forms;

namespace ChoppaAI.Uninstaller;

internal static class Program
{
    private const string ProductId = "ChoppaAI.DesktopPet";
    private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ChoppaAI";
    private const string ManifestFileName = "InstallManifest.json";
    private const uint MoveFileDelayUntilReboot = 0x4;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            Uninstall();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Broski could not be fully uninstalled.\r\n\r\n{exception.Message}",
                "Broski uninstall error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void Uninstall()
    {
        var installDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        var manifestPath = Path.Combine(installDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException(
                $"The uninstall manifest is missing. To avoid deleting unrelated files, nothing was removed: {manifestPath}");
        }

        var manifest = JsonSerializer.Deserialize<InstallationManifest>(File.ReadAllText(manifestPath))
            ?? throw new InvalidDataException("The uninstall manifest is invalid.");
        if (manifest.ProductId != ProductId ||
            !Path.GetFullPath(manifest.InstallDirectory).TrimEnd(Path.DirectorySeparatorChar)
                .Equals(installDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("This folder is not a recognized Broski AI installation. No files were removed.");
        }

        ValidateManifest(manifest, installDirectory);
        var choice = MessageBox.Show(
            $"Uninstall Broski AI from:\r\n{installDirectory}\r\n\r\nUnrelated files will be left in place. User data outside the installation folder will be kept.",
            "Uninstall Broski AI",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (choice != DialogResult.Yes)
        {
            return;
        }

        EnsureAppsAreClosed(installDirectory);
        var knownShortcuts = GetShortcutPaths().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var shortcut in manifest.Shortcuts.Where(knownShortcuts.Contains))
        {
            if (File.Exists(shortcut))
            {
                File.Delete(shortcut);
            }
        }

        using (var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath, writable: true))
        {
            var registeredDirectory = key?.GetValue("InstallLocation") as string;
            if (!string.IsNullOrWhiteSpace(registeredDirectory) &&
                Path.GetFullPath(registeredDirectory).TrimEnd(Path.DirectorySeparatorChar)
                    .Equals(installDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryPath, throwOnMissingSubKey: false);
            }
        }

        foreach (var relativePath in manifest.Files.Append(ManifestFileName).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = GetSafePath(installDirectory, relativePath);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException) when (Path.GetFullPath(path).Equals(
                       Path.GetFullPath(Environment.ProcessPath ?? string.Empty),
                       StringComparison.OrdinalIgnoreCase))
            {
                if (!MoveFileEx(path, null, MoveFileDelayUntilReboot))
                {
                    throw new IOException("Windows could not schedule the uninstaller for removal at restart.");
                }
            }
        }

        foreach (var relativePath in manifest.Directories.OrderByDescending(path => path.Length))
        {
            var path = GetSafePath(installDirectory, relativePath);
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }

        if (Directory.Exists(installDirectory) && !Directory.EnumerateFileSystemEntries(installDirectory).Any())
        {
            Directory.Delete(installDirectory);
        }

        MessageBox.Show(
            "Broski AI has been removed. User data and unrelated files outside the installation folder were left in place.",
            "Broski uninstall complete",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void ValidateManifest(InstallationManifest manifest, string installDirectory)
    {
        foreach (var relativePath in manifest.Files.Concat(manifest.Directories))
        {
            _ = GetSafePath(installDirectory, relativePath);
        }

    }

    private static string GetSafePath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException($"The uninstall manifest contains an invalid path: {relativePath}");
        }

        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"The uninstall manifest points outside the installation folder: {relativePath}");
        }

        return path;
    }

    private static void EnsureAppsAreClosed(string installDirectory)
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
                        if (!string.IsNullOrWhiteSpace(processPath) &&
                            Path.GetFullPath(processPath).StartsWith(
                                Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar) +
                                Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            throw new IOException("Close Broski and its launcher before uninstalling.");
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        throw new IOException("Close Broski and its launcher before uninstalling.");
                    }
                }
            }
        }
    }

    private static string[] GetShortcutPaths()
    {
        var programs = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "Broski AI");
        var startup = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "Startup");
        return
        [
            Path.Combine(programs, "Broski.lnk"),
            Path.Combine(programs, "Uninstall Broski.lnk"),
            Path.Combine(startup, "Broski.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Broski.lnk")
        ];
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileEx(string existingFileName, string? newFileName, uint flags);
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
