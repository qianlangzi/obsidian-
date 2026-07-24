using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using InboxDock.App.Diagnostics;
using InboxDock.App.SystemIntegration;
using InboxDock.App.UpdateChecking;
using InboxDock.App.ViewModels;
using InboxDock.App.Views;
using InboxDock.Core.Configuration;
using InboxDock.Core.Diagnostics;
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

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        // 兜底：注册全局未处理异常处理器，确保任何崩溃都能写入日志。
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);

        // 先创建日志，确保后续所有步骤（包括单实例判断）都能记录。
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InboxDock",
            "Logs");
        log = new AppLog(logDirectory);
        LogInstance = log;
        log.Info("Startup", $"InboxDock {AppVersion} starting");

        // 单实例检查：第二实例通知第一实例后退出。
        singleInstance = new SingleInstanceService();
        if (!singleInstance.TryAcquire())
        {
            log.Info("Startup", "Another instance is running, signaling and exiting");
            SingleInstanceService.SignalFirstInstance();
            Shutdown(0);
            return;
        }

        try
        {
            var settingsStore = new SettingsStore();
            log.Info("Startup", "Settings store created");
            // 必须用 await 而非 .GetAwaiter().GetResult()，
            // 否则 UI 线程阻塞后 async 续体无法回到 UI 线程，造成死锁。
            var load = await settingsStore.LoadAsync();
            log.Info("Startup", $"Settings loaded: {(load.Settings is null ? "null" : "present")}");

            if (load.Settings is null)
            {
                log.Info("Startup", "Opening onboarding");
                OpenOnboarding(settingsStore);
            }
            else
            {
                log.Info("Startup", "Opening main window");
                OpenMainWindow(load.Settings);
            }

            // 启动命名管道服务器，监听第二实例的呼出请求。
            singleInstance.StartServer(OnSingleInstanceActivated);
            log.Info("Startup", "Pipe server started");
        }
        catch (Exception exception)
        {
            log?.Error("Startup", exception);
            WriteCrashLog(exception);
            try
            {
                System.Windows.MessageBox.Show(
                    $"{exception.Message}\n\n详细错误已写入崩溃日志。",
                    "InboxDock 无法启动",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            catch
            {
                // MessageBox 也失败时，至少写入了崩溃日志。
            }
            Shutdown(-1);
        }
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            log?.Error("Unhandled", ex);
            WriteCrashLog(ex);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        log?.Error("Dispatcher", e.Exception);
        WriteCrashLog(e.Exception);
        e.Handled = false;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        log?.Error("Task", e.Exception);
        WriteCrashLog(e.Exception);
    }

    /// <summary>写入崩溃日志到 AppData\InboxDock\crash.log，便于事后排查。</summary>
    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "InboxDock");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "crash.log");
            var timestamp = DateTimeOffset.Now.ToString("O");
            var entry = $"==== {timestamp} ===={Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(path, entry);
        }
        catch
        {
            // 兜底写入也失败时彻底放弃。
        }
    }

    private void OnSingleInstanceActivated()
    {
        log?.Info("SingleInstance", "Second instance signaled, activating window");
        Dispatcher.Invoke(() => mainWindow?.BringToForeground());
    }

    private void OpenOnboarding(SettingsStore settingsStore)
    {
        log?.Info("Onboarding", "Creating view model");
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

        log?.Info("Onboarding", "Constructing window");
        onboardingWindow = new OnboardingWindow(viewModel);
        log?.Info("Onboarding", "Showing window");
        onboardingWindow.Show();
        log?.Info("Onboarding", "Shown");
    }

    private void OpenMainWindow(Core.Configuration.AppSettings settings)
    {
        log?.Info("MainWindow", "Constructing");
        mainWindow = new MainWindow();
        MainWindow = mainWindow;
        if (mainWindow.DataContext is MainViewModel vm)
        {
            _ = vm.InitializeWithSettingsAsync(settings);
        }
        log?.Info("MainWindow", "Showing");
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
