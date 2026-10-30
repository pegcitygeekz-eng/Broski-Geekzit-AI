using System.Diagnostics;

namespace Broski.Launcher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var applicationPath = Path.Combine(AppContext.BaseDirectory, "Broski.exe");
        if (!File.Exists(applicationPath))
        {
            MessageBox.Show(
                $"Broski could not be found beside the launcher:\r\n{applicationPath}",
                "Broski Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        try
        {
            var startInfo = new ProcessStartInfo(applicationPath)
            {
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory,
            };
            foreach (var argument in args)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Windows did not start Broski.");
            return 0;
        }
        catch (InvalidOperationException error)
        {
            MessageBox.Show(
                $"Broski could not be started:\r\n{error.Message}",
                "Broski Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            MessageBox.Show(
                $"Windows could not start Broski:\r\n{error.Message}",
                "Broski Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }
}
