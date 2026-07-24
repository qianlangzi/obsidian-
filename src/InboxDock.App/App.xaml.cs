using System.IO;
using System.Reflection;
using System.Windows;
using InboxDock.App.Diagnostics;
using InboxDock.App.SystemIntegration;
using InboxDock.App.UpdateChecking;
using InboxDock.App.ViewModels;
using InboxDock.App.Views;
using InboxDock.Core.Configuration;
using InboxDock.Core.Vault;

namespace InboxDock.App;

public partial class App : System.Windows.Application
{
    private MainWindow? mainWindow;
    private OnboardingWindow? onboardingWindow;
    private SingleInstanceService? singleInstance;
    private AppLog? log;
    private GitHubUpdateService? updateChecker;

    internal static AppLog? LogInstance { get; private set; }

    internal static string AppVersion { get; } = GetAppVersion();

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例检查：第二实例通知第一实例后退出。
        singleInstance = new SingleInstanceService();
        if (!singleInstance.TryAcquire())
        {
            SingleInstanceService.SignalFirstInstance();
            Shutdown(0);
            return;
        }

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InboxDock",
            "Logs");
        log = new AppLog(logDirectory);
        LogInstance = log;

        try
        {
            log.Info("Startup", $"InboxDock {AppVersion} starting");

            var settingsStore = new SettingsStore();
            var load = settingsStore.LoadAsync().GetAwaiter().GetResult();
            if (load.Settings is null)
            {
                OpenOnboarding(settingsStore);
            }
            else
            {
                OpenMainWindow(load.Settings);
            }

            // 启动命名管道服务器，监听第二实例的呼出请求。
            singleInstance.StartServer(OnSingleInstanceActivated);
        }
        catch (Exception exception)
        {
            log?.Error("Startup", exception);
            System.Windows.MessageBox.Show(exception.Message, "InboxDock 无法启动", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void OnSingleInstanceActivated()
    {
        log?.Info("SingleInstance", "Second instance signaled, activating window");
        Dispatcher.Invoke(() => mainWindow?.BringToForeground());
    }

    private void OpenOnboarding(SettingsStore settingsStore)
    {
        var viewModel = new OnboardingViewModel(settingsStore, new VaultDiscovery());
        viewModel.Completed += settings =>
        {
            log?.Info("Onboarding", "Completed");
            onboardingWindow = null;
            OpenMainWindow(settings);
            return Task.CompletedTask;
        };
        viewModel.Cancelled += () =>
        {
            log?.Info("Onboarding", "Cancelled");
            Shutdown(0);
        };

        onboardingWindow = new OnboardingWindow(viewModel);
        onboardingWindow.Show();
        log?.Info("Onboarding", "Shown");
    }

    private void OpenMainWindow(Core.Configuration.AppSettings settings)
    {
        mainWindow = new MainWindow();
        MainWindow = mainWindow;
        if (mainWindow.DataContext is MainViewModel vm)
        {
            _ = vm.InitializeWithSettingsAsync(settings);
        }
        mainWindow.Show();
        log?.Info("MainWindow", "Shown");

        // 非阻塞更新检查：启动后在后台检查 GitHub 最新发布。
        updateChecker = new GitHubUpdateService(log: log);
        updateChecker.UpdateAvailable += OnUpdateAvailable;
        _ = updateChecker.CheckAsync(AppVersion);
    }

    private void OnUpdateAvailable(InboxDock.Core.UpdateChecking.UpdateCheckResult result)
    {
        Dispatcher.Invoke(() =>
        {
            if (mainWindow is not null)
            {
                mainWindow.ShowUpdateNotification(result.LatestVersion, result.ReleaseUrl ?? string.Empty);
            }
        });
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        log?.Info("Shutdown", $"Exit code {e.ApplicationExitCode}");
        updateChecker?.Dispose();
        singleInstance?.Dispose();
        log?.Dispose();
        base.OnExit(e);
    }

    private static string GetAppVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
