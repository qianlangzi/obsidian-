using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InboxDock.App.SystemIntegration;
using InboxDock.Core.Configuration;
using InboxDock.Core.SystemIntegration;
using InboxDock.Core.Targets;

namespace InboxDock.App.ViewModels;

public sealed record AutoHideOption(TimeSpan? Duration, string Label);

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore settingsStore;
    private readonly LaunchAtSignInService launchAtSignInService = new();

    public SettingsViewModel(SettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        AutoHideOptions =
        [
            new(TimeSpan.FromSeconds(5), "5 秒"),
            new(TimeSpan.FromSeconds(10), "10 秒"),
            new(TimeSpan.FromSeconds(30), "30 秒"),
            new(null, "永不"),
        ];
        selectedAutoHideOption = AutoHideOptions[0];
    }

    public ObservableCollection<CaptureTarget> Targets { get; } = [];

    public IReadOnlyList<AutoHideOption> AutoHideOptions { get; }

    [ObservableProperty] private string vaultName = string.Empty;
    [ObservableProperty] private string vaultPath = string.Empty;
    [ObservableProperty] private CaptureTarget? selectedTarget;
    [ObservableProperty] private CaptureTarget? defaultTarget;
    [ObservableProperty] private bool isEditorOpen;
    [ObservableProperty] private CaptureTargetEditorViewModel? editor;
    [ObservableProperty] private AutoHideOption selectedAutoHideOption;

    [ObservableProperty] private string globalHotkey = HotkeyGesture.Default.ToDisplayString();
    [ObservableProperty] private bool isCapturingHotkey;
    [ObservableProperty] private string? hotkeyCaptureError;

    [ObservableProperty] private bool launchAtSignIn;
    [ObservableProperty] private string? launchAtSignInNote;

    public CaptureTargetEditorViewModel? PendingEditor => Editor;

    /// <summary>配置有未保存修改时为 true，保存后重置。</summary>
    [ObservableProperty] private bool hasUnsavedChanges;

    public event Action? ConfigurationSaved;

    public void Load(AppSettings settings)
    {
        Targets.Clear();
        var profile = settings.CurrentProfile;
        if (profile is null) return;

        VaultName = profile.Name;
        VaultPath = profile.VaultPath;
        DefaultTarget = profile.CaptureTargets.FirstOrDefault(t => t.Id == profile.DefaultTargetId);
        foreach (var target in profile.CaptureTargets.OrderBy(t => t.SortOrder))
        {
            Targets.Add(target);
        }

        SelectedAutoHideOption = AutoHideOptions.FirstOrDefault(o => o.Duration == profile.AutoHideDelay)
            ?? AutoHideOptions[0];

        GlobalHotkey = string.IsNullOrWhiteSpace(profile.GlobalHotkey)
            ? HotkeyGesture.Default.ToDisplayString()
            : HotkeyGesture.TryParse(profile.GlobalHotkey)?.ToDisplayString()
              ?? HotkeyGesture.Default.ToDisplayString();

        LaunchAtSignIn = profile.LaunchAtSignIn;
        RefreshLaunchAtSignInStatus();

        HasUnsavedChanges = false;
    }

    /// <summary>刷新开机自启动状态提示。便携版移动后显示"需要修复"。</summary>
    public void RefreshLaunchAtSignInStatus()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            LaunchAtSignInNote = null;
            return;
        }

        var status = launchAtSignInService.GetStatus(LaunchAtSignIn, exePath);
        LaunchAtSignInNote = status switch
        {
            LaunchAtSignInStatus.Enabled => null,
            LaunchAtSignInStatus.NeedsRepair => "注册路径与当前不一致，点击下方开关修复。",
            _ => null,
        };
    }

    [RelayCommand]
    public void AddTarget()
    {
        var editor = new CaptureTargetEditorViewModel();
        editor.LoadFrom(new CaptureTarget
        {
            Name = "新目标",
            PathTemplate = "00 Inbox收件箱",
            FileNameTemplate = "{{timestamp}}-{{title}}",
        });
        editor.IsEditing = false;
        Editor = editor;
        IsEditorOpen = true;
    }

    [RelayCommand]
    public void EditTarget(CaptureTarget target)
    {
        var editor = new CaptureTargetEditorViewModel();
        editor.LoadFrom(target);
        Editor = editor;
        IsEditorOpen = true;
    }

    [RelayCommand]
    public void CommitEditor()
    {
        if (Editor is null) return;
        var target = Editor.BuildTarget(incrementRevision: Editor.IsEditing);
        var existing = Targets.FirstOrDefault(t => t.Id == target.Id);
        if (existing is null)
        {
            target = target with { SortOrder = Targets.Count };
            Targets.Add(target);
        }
        else
        {
            var index = Targets.IndexOf(existing);
            Targets[index] = target with { SortOrder = existing.SortOrder, IsDefault = existing.IsDefault };
        }
        Editor = null;
        IsEditorOpen = false;
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    public void CancelEditor()
    {
        Editor = null;
        IsEditorOpen = false;
    }

    [RelayCommand]
    public void DeleteTarget(CaptureTarget target)
    {
        var wasDefault = target.Id == DefaultTarget?.Id;
        Targets.Remove(target);
        if (wasDefault)
        {
            DefaultTarget = Targets.FirstOrDefault();
        }
        for (var i = 0; i < Targets.Count; i++)
        {
            Targets[i] = Targets[i] with { IsDefault = Targets[i].Id == DefaultTarget?.Id };
        }
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    public void SetDefault(CaptureTarget target)
    {
        DefaultTarget = target;
        for (var i = 0; i < Targets.Count; i++)
        {
            Targets[i] = Targets[i] with { IsDefault = Targets[i].Id == target.Id };
        }
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    public void MoveUp(CaptureTarget target)
    {
        var index = Targets.IndexOf(target);
        if (index <= 0) return;
        Targets.Move(index, index - 1);
        Reorder();
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    public void MoveDown(CaptureTarget target)
    {
        var index = Targets.IndexOf(target);
        if (index < 0 || index >= Targets.Count - 1) return;
        Targets.Move(index, index + 1);
        Reorder();
        HasUnsavedChanges = true;
    }

    /// <summary>开始捕获快捷键。View 进入捕获模式，等待用户按下组合键。</summary>
    [RelayCommand]
    public void StartCaptureHotkey()
    {
        IsCapturingHotkey = true;
        HotkeyCaptureError = null;
    }

    /// <summary>取消快捷键捕获，保持原值。</summary>
    [RelayCommand]
    public void CancelCaptureHotkey()
    {
        IsCapturingHotkey = false;
        HotkeyCaptureError = null;
    }

    /// <summary>
    /// 应用捕获到的快捷键组合。由 View 调用，传入标准化后的手势字符串。
    /// 无效组合时设置错误提示。
    /// </summary>
    public bool ApplyCapturedHotkey(string gestureText)
    {
        var gesture = HotkeyGesture.TryParse(gestureText);
        if (gesture is null || !gesture.IsValid)
        {
            HotkeyCaptureError = "无效的快捷键组合，请包含至少一个修饰键和一个按键。";
            return false;
        }

        GlobalHotkey = gesture.ToDisplayString();
        IsCapturingHotkey = false;
        HotkeyCaptureError = null;
        HasUnsavedChanges = true;
        return true;
    }

    /// <summary>重置快捷键为默认值 Ctrl+Shift+Space。</summary>
    [RelayCommand]
    public void ResetHotkey()
    {
        GlobalHotkey = HotkeyGesture.Default.ToDisplayString();
        IsCapturingHotkey = false;
        HotkeyCaptureError = null;
        HasUnsavedChanges = true;
    }

    /// <summary>切换开机自启动开关。注册成功后才更新设置。</summary>
    [RelayCommand]
    public void ToggleLaunchAtSignIn()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            LaunchAtSignInNote = "无法获取当前程序路径。";
            return;
        }

        if (!LaunchAtSignIn)
        {
            // 开启：先检查路径存在，再注册。
            if (!System.IO.File.Exists(exePath))
            {
                LaunchAtSignInNote = "当前程序路径无效。";
                return;
            }

            if (launchAtSignInService.Enable(exePath))
            {
                LaunchAtSignIn = true;
                HasUnsavedChanges = true;
                RefreshLaunchAtSignInStatus();
            }
            else
            {
                LaunchAtSignInNote = "注册开机自启动失败，请检查权限。";
            }
        }
        else
        {
            // 关闭：只移除 InboxDock 创建的启动项。
            if (launchAtSignInService.Disable())
            {
                LaunchAtSignIn = false;
                HasUnsavedChanges = true;
                RefreshLaunchAtSignInStatus();
            }
            else
            {
                LaunchAtSignInNote = "移除开机自启动失败，请检查权限。";
            }
        }
    }

    /// <summary>日志目录路径，供 UI 打开。</summary>
    public string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InboxDock",
        "Logs");

    /// <summary>暂存目录路径，供 UI 打开。</summary>
    public string StagingDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InboxDock",
        "Staging");

    /// <summary>打开日志目录。失败时返回错误提示。</summary>
    public string? OpenLogDirectory()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LogDirectory)
            {
                UseShellExecute = true,
            });
            return null;
        }
        catch (Exception ex)
        {
            return $"打开日志目录失败：{ex.Message}";
        }
    }

    /// <summary>打开暂存目录。失败时返回错误提示。</summary>
    public string? OpenStagingDirectory()
    {
        try
        {
            Directory.CreateDirectory(StagingDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(StagingDirectory)
            {
                UseShellExecute = true,
            });
            return null;
        }
        catch (Exception ex)
        {
            return $"打开暂存目录失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 生成诊断信息文本并复制到剪贴板。返回 null 表示成功，否则返回错误提示。
    /// 诊断信息不包含笔记正文、URL 查询内容、文件内容和剪贴板内容。
    /// </summary>
    public string? CopyDiagnostics()
    {
        try
        {
            var snapshot = InboxDock.Core.Diagnostics.DiagnosticSnapshot.Capture(
                appVersion: App.AppVersion,
                vaultPath: string.IsNullOrEmpty(VaultPath) ? null : VaultPath,
                vaultExists: !string.IsNullOrEmpty(VaultPath) && Directory.Exists(VaultPath),
                vaultWritable: IsVaultWritable(),
                stagedItemCount: 0,
                lastErrorType: null);

            var text = snapshot.ToClipboardText();
            System.Windows.Clipboard.SetText(text);
            return null;
        }
        catch (Exception ex)
        {
            return $"复制诊断信息失败：{ex.Message}";
        }
    }

    private bool IsVaultWritable()
    {
        if (string.IsNullOrEmpty(VaultPath) || !Directory.Exists(VaultPath)) return false;
        try
        {
            var testFile = Path.Combine(VaultPath, $".inboxdock-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (Targets.Count == 0)
        {
            throw new InvalidOperationException("至少需要一个收集目标。");
        }

        if (DefaultTarget is null)
        {
            DefaultTarget = Targets[0];
        }

        var targets = Targets
            .Select((t, i) => t with { SortOrder = i, IsDefault = t.Id == DefaultTarget.Id })
            .ToList();

        var profile = new VaultProfile
        {
            Name = VaultName,
            VaultPath = VaultPath,
            CaptureTargets = targets,
            DefaultTargetId = DefaultTarget.Id,
            AutoHideDelay = SelectedAutoHideOption.Duration,
            GlobalHotkey = GlobalHotkey,
            LaunchAtSignIn = LaunchAtSignIn,
        };

        var validation = profile.Validate();
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Message);
        }

        var settings = new AppSettings
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            CurrentProfile = profile,
            VaultPath = VaultPath,
        };

        await settingsStore.SaveAsync(settings);
        HasUnsavedChanges = false;
        ConfigurationSaved?.Invoke();
    }

    private void Reorder()
    {
        for (var i = 0; i < Targets.Count; i++)
        {
            Targets[i] = Targets[i] with { SortOrder = i };
        }
    }

    partial void OnSelectedAutoHideOptionChanged(AutoHideOption value)
    {
        HasUnsavedChanges = true;
    }
}
