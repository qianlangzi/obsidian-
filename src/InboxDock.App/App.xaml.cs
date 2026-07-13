namespace InboxDock.App;

public partial class App : System.Windows.Application
{
    private MainWindow? mainWindow;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        var logDirectory = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InboxDock");
        var logPath = System.IO.Path.Combine(logDirectory, "startup.log");
        try
        {
            System.IO.Directory.CreateDirectory(logDirectory);
            System.IO.File.WriteAllText(logPath, $"{DateTimeOffset.Now:O} Starting InboxDock{Environment.NewLine}");
            mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
            System.IO.File.AppendAllText(logPath, $"{DateTimeOffset.Now:O} Main window shown{Environment.NewLine}");
        }
        catch (Exception exception)
        {
            System.IO.File.AppendAllText(logPath, exception + Environment.NewLine);
            System.Windows.MessageBox.Show(exception.Message, "InboxDock 无法启动", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
}
