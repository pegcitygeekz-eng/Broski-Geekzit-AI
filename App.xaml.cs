using System.Windows;

namespace ChoppaAI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Guid? alarmId = null;
        if (e.Args.Length == 2
            && e.Args[0].Equals("--alarm", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(e.Args[1], out var parsedAlarmId))
            {
                MessageBox.Show(
                    "The scheduled alarm ID is invalid.",
                    "Broski alarm",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            alarmId = parsedAlarmId;
        }

        MainWindow = new MainWindow(alarmId);
        MainWindow.Show();
    }
}
