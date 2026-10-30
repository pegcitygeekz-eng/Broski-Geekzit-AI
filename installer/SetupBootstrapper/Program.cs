using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace ChoppaAI.Setup;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        string? temporaryDirectory = null;
        try
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                $"Broski-Setup-{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporaryDirectory);

            var scriptPath = ExtractResource(
                "Install-ChoppaAI.ps1",
                Path.Combine(temporaryDirectory, "Install-ChoppaAI.ps1"));
            ExtractResource(
                "ChoppaAI-Payload.zip",
                Path.Combine(temporaryDirectory, "ChoppaAI-Payload.zip"));

            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Normal,
                WorkingDirectory = temporaryDirectory
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-STA");
            startInfo.ArgumentList.Add("-WindowStyle");
            startInfo.ArgumentList.Add("Normal");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-FromBootstrapper");

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Windows could not start PowerShell.");
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Broski AI Setup could not start or complete:\r\n\r\n{exception.Message}",
                "Broski AI Setup v0.0.3",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
        finally
        {
            if (temporaryDirectory is not null && Directory.Exists(temporaryDirectory))
            {
                try
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
                catch (IOException exception)
                {
                    MessageBox.Show(
                        $"Setup finished, but temporary files could not be removed from:\r\n{temporaryDirectory}\r\n\r\n{exception.Message}",
                        "Broski AI Setup 0.0.3",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                catch (UnauthorizedAccessException exception)
                {
                    MessageBox.Show(
                        $"Setup finished, but temporary files could not be removed from:\r\n{temporaryDirectory}\r\n\r\n{exception.Message}",
                        "Broski AI Setup 0.0.3",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }
    }

    private static string ExtractResource(string resourceName, string destinationPath)
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded setup file is missing: {resourceName}");
        using var destination = File.Create(destinationPath);
        resource.CopyTo(destination);
        return destinationPath;
    }
}
