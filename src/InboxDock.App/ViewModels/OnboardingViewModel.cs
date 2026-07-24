using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InboxDock.Core.Configuration;
using InboxDock.Core.Targets;
using InboxDock.Core.Vault;

namespace InboxDock.App.ViewModels;

public sealed record OnboardingWriteModeOption(TargetWriteMode Mode, string Label, string Description);

public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly SettingsStore settingsStore;
    private readonly VaultDiscovery discovery;

    public OnboardingViewModel(SettingsStore settingsStore, VaultDiscovery discovery)
    {
        this.settingsStore = settingsStore;
        this.discovery = discovery;
        WriteModes =
        [
            new OnboardingWriteModeOption(TargetWriteMode.AppendToFile, "追加到收件箱", "所有内容追加到同一个 Markdown 文件"),
            new OnboardingWriteModeOption(TargetWriteMode.CreateNote, "新建笔记", "每次收集在指定目录创建新笔记"),
            new OnboardingWriteModeOption(TargetWriteMode.StagingOnly, "只暂存", "只保留在 InboxDock，稍后再处理"),
        ];
        SelectedWriteMode = WriteModes[0];
    }

    public IReadOnlyList<OnboardingWriteModeOption> WriteModes { get; }

    [ObservableProperty]
    private int step = 1;

    [ObservableProperty]
    private string vaultPath = string.Empty;

    [ObservableProperty]
    private string vaultValidationMessage = "请选择 Obsidian Vault 文件夹";

    [ObservableProperty]
    private bool isVaultValid;

    [ObservableProperty]
    private OnboardingWriteModeOption selectedWriteMode;

    [ObservableProperty]
    private string dailyNotesHint = string.Empty;

    [ObservableProperty]
    private bool hasDailyNotesSuggestion;

    [ObservableProperty]
    private bool includeDailyNotesTarget = true;

    [ObservableProperty]
    private string summary = string.Empty;

    [ObservableProperty]
    private bool canFinish;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    /// <summary>完成配置后触发，参数为最终保存的 AppSettings。</summary>
    public event Func<AppSettings, Task>? Completed;

    /// <summary>用户取消后触发。</summary>
    public event Action? Cancelled;

    private VaultDiscoveryResult? discoveryResult;

    [RelayCommand]
    public async Task PickVaultAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            IsVaultValid = false;
            VaultValidationMessage = "请选择 Vault 文件夹";
            return;
        }

        var validation = VaultValidator.Validate(path);
        if (!validation.IsValid)
        {
            IsVaultValid = false;
            VaultValidationMessage = validation.Message;
            discoveryResult = null;
            return;
        }

        VaultPath = validation.CanonicalPath!;
        discoveryResult = await discovery.DiscoverAsync(VaultPath);

        IsVaultValid = true;
        VaultValidationMessage = "Vault 可用";

        UpdateDailyNotesSuggestion();
    }

    [RelayCommand]
    public void GoToStep2()
    {
        if (!IsVaultValid) return;
        Step = 2;
        ErrorMessage = string.Empty;
        UpdateDailyNotesSuggestion();
    }

    [RelayCommand]
    public void BackToStep1()
    {
        Step = 1;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    public async Task FinishAsync()
    {
        if (!IsVaultValid)
        {
            ErrorMessage = "Vault 未验证，无法完成。";
            return;
        }

        try
        {
            var targets = BuildTargets();
            if (targets.Count == 0)
            {
                ErrorMessage = "未创建任何目标，请检查配置。";
                return;
            }

            var profile = new VaultProfile
            {
                Name = Path.GetFileName(VaultPath.TrimEnd('\\', '/')),
                VaultPath = VaultPath,
                CaptureTargets = targets,
                DefaultTargetId = targets[0].Id,
            };

            var validation = profile.Validate();
            if (!validation.IsValid)
            {
                ErrorMessage = validation.Message;
                return;
            }

            var settings = new AppSettings
            {
                SchemaVersion = AppSettings.CurrentSchemaVersion,
                CurrentProfile = profile,
                VaultPath = VaultPath,
            };
            await settingsStore.SaveAsync(settings);
            Summary = $"已创建 {targets.Count} 个目标，默认为 {targets[0].Name}。";
            CanFinish = true;
            if (Completed is not null) await Completed.Invoke(settings);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"保存配置失败：{ex.Message}";
            CanFinish = false;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        Cancelled?.Invoke();
    }

    private IReadOnlyList<CaptureTarget> BuildTargets()
    {
        var targets = OnboardingSuggestions.BuildDefaultTargets(SelectedWriteMode.Mode, discoveryResult).ToList();
        if (!IncludeDailyNotesTarget)
        {
            targets.RemoveAll(t => t.Name == "今日日记");
        }

        return targets;
    }

    private void UpdateDailyNotesSuggestion()
    {
        if (discoveryResult?.DailyNotesFolder is not null)
        {
            HasDailyNotesSuggestion = true;
            DailyNotesHint = $"检测到 Obsidian Daily Notes 目录：{discoveryResult.DailyNotesFolder}。建议额外创建今日日记目标。";
        }
        else
        {
            HasDailyNotesSuggestion = false;
            DailyNotesHint = string.Empty;
        }
    }
}
