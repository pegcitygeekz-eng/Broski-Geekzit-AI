using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Xml.Linq;

namespace ChoppaAI;

internal static class WindowsAlarmScheduler
{
    private static readonly XNamespace TaskNamespace =
        "http://schemas.microsoft.com/windows/2004/02/mit/task";

    public static void Schedule(AlarmRecord alarm)
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "Broski.exe");
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException(
                "The installed Broski.exe was not found, so Windows cannot schedule this alarm.",
                executable);
        }

        var currentUser = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("Windows could not identify the current user for this alarm.");
        var taskName = GetTaskName(alarm.Id);
        var taskDefinition = new XDocument(
            new XElement(TaskNamespace + "Task",
                new XAttribute("version", "1.4"),
                new XElement(TaskNamespace + "RegistrationInfo",
                    new XElement(TaskNamespace + "Author", "Broski AI"),
                    new XElement(TaskNamespace + "Description", $"Broski alarm: {alarm.Title}")),
                new XElement(TaskNamespace + "Triggers",
                    new XElement(TaskNamespace + "TimeTrigger",
                        new XElement(TaskNamespace + "StartBoundary",
                            alarm.DueAt.LocalDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)),
                        new XElement(TaskNamespace + "Enabled", "true"))),
                new XElement(TaskNamespace + "Principals",
                    new XElement(TaskNamespace + "Principal",
                        new XAttribute("id", "Author"),
                        new XElement(TaskNamespace + "UserId", currentUser),
                        new XElement(TaskNamespace + "LogonType", "InteractiveToken"),
                        new XElement(TaskNamespace + "RunLevel", "LeastPrivilege"))),
                new XElement(TaskNamespace + "Settings",
                    new XElement(TaskNamespace + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(TaskNamespace + "DisallowStartIfOnBatteries", "false"),
                    new XElement(TaskNamespace + "StopIfGoingOnBatteries", "false"),
                    new XElement(TaskNamespace + "StartWhenAvailable", "true"),
                    new XElement(TaskNamespace + "WakeToRun", "true"),
                    new XElement(TaskNamespace + "Enabled", "true"),
                    new XElement(TaskNamespace + "ExecutionTimeLimit", "PT1H"),
                    new XElement(TaskNamespace + "Priority", "7")),
                new XElement(TaskNamespace + "Actions",
                    new XAttribute("Context", "Author"),
                    new XElement(TaskNamespace + "Exec",
                        new XElement(TaskNamespace + "Command", executable),
                        new XElement(TaskNamespace + "Arguments", $"--alarm {alarm.Id:D}"),
                        new XElement(TaskNamespace + "WorkingDirectory", AppContext.BaseDirectory)))));

        var xmlPath = Path.Combine(Path.GetTempPath(), $"BroskiAlarm-{alarm.Id:N}.xml");
        try
        {
            taskDefinition.Save(xmlPath);
            RunSchedulerCommand("/Create", "/TN", taskName, "/XML", xmlPath, "/F");
        }
        finally
        {
            if (File.Exists(xmlPath))
            {
                File.Delete(xmlPath);
            }
        }
    }

    public static void Delete(Guid alarmId)
    {
        RunSchedulerCommand("/Delete", "/TN", GetTaskName(alarmId), "/F");
    }

    private static string GetTaskName(Guid alarmId) => $"BroskiAI-Alarm-{alarmId:N}";

    private static void RunSchedulerCommand(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("schtasks.exe")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows could not start the Task Scheduler command.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var details = string.Join(
                Environment.NewLine,
                new[] { standardOutput.Trim(), standardError.Trim() }
                    .Where(output => output.Length > 0));
            throw new InvalidOperationException(
                $"Windows Task Scheduler returned exit code {process.ExitCode}. {details}".Trim());
        }
    }
}
