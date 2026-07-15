using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using InboxDock.Core.Capture;
using InboxDock.Core.Configuration;
using InboxDock.Core.Daily;
using InboxDock.Core.History;
using InboxDock.Core.Staging;
using InboxDock.Core.Vault;

namespace InboxDock.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly SettingsStore settingsStore;
    private readonly MaterialStagingService staging;
    private InboxCaptureService? inbox;
    private StagedCaptureService? stagedCapture;
    private DailyCaptureService? daily;
    private CaptureResult? last;
    private CancellationTokenSource? draftSaveCancellation;
    private bool restoringDraft;

    [ObservableProperty] private string dailyText = string.Empty;
    [ObservableProperty] private string draftText = string.Empty;
    [ObservableProperty] private CategoryOption selectedCategory;
    [ObservableProperty] private string statusText = "请先选择 Obsidian Vault";
    [ObservableProperty] private bool canUndo;
    [ObservableProperty] private bool hasVault;
    [ObservableProperty] private bool isConfirmationOpen;
    [ObservableProperty] private bool isDeleteConfirmation;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private StagedMaterial? selectedMaterial;
    [ObservableProperty] private string vaultPath = string.Empty;

    public MainViewModel()
        : this(new SettingsStore(), CreateDefaultStaging())
    {
    }

    internal MainViewModel(SettingsStore settingsStore, MaterialStagingService staging)
    {
        this.settingsStore = settingsStore;
        this.staging = staging;
        selectedCategory = Categories.Single(item => item.Value == DailyCategory.Learning);
    }

    public ObservableCollection<StagedMaterial> StagedItems { get; } = [];

    public IReadOnlyList<CategoryOption> Categories { get; } = Enum.GetValues<DailyCategory>()
        .Select(value => new CategoryOption(value, value.DisplayName()))
        .ToArray();

    public bool HasStagedItems => StagedItems.Count > 0;

    public async Task InitializeAsync()
    {
        var settings = await settingsStore.LoadAsync();
        if (settings.Settings is not null) Configure(settings.Settings);

        var staged = await staging.LoadAsync();
        restoringDraft = true;
        DraftText = staged.Snapshot.DraftText;
        restoringDraft = false;
        RefreshStagedItems();
        if (staged.Error is not null) StatusText = staged.Error;
    }

    public async Task SetVaultAsync(string path)
    {
        var check = VaultValidator.Validate(path);
        if (!check.IsValid)
        {
            StatusText = check.Message;
            return;
        }

        var settings = AppSettings.CreateDefault(check.CanonicalPath!);
        await settingsStore.SaveAsync(settings);
        Configure(settings);
        StatusText = "Vault 已连接";
    }

    public async Task StageFilesAsync(IReadOnlyList<string> files)
    {
        await Run(async () =>
        {
            var material = await staging.StageFilesAsync(files);
            RefreshStagedItems();
            SelectMaterial(material);
            StatusText = files.Count == 1 ? "文件已暂存，请确认" : $"{files.Count} 个文件已暂存，请确认";
        });
    }

    public async Task<bool> StagePastedLinkAsync(string value)
    {
        var recognized = false;
        await Run(async () =>
        {
            var material = await staging.StagePastedLinkAsync(value);
            if (material is null) return;
            recognized = true;
            RefreshStagedItems();
            SelectMaterial(material);
            StatusText = "链接已暂存，请确认";
        });
        return recognized;
    }

    public async Task SubmitDraftAsync()
    {
        if (string.IsNullOrWhiteSpace(DraftText)) return;
        CancelPendingDraftSave();
        await Run(async () =>
        {
            var material = await staging.StageDraftAsync(DraftText);
            restoringDraft = true;
            DraftText = string.Empty;
            restoringDraft = false;
            RefreshStagedItems();
            SelectMaterial(material);
            StatusText = "文字已暂存，请确认";
        });
    }

    public async Task ConfirmSelectedAsync()
    {
        if (SelectedMaterial is null) return;
        if (stagedCapture is null)
        {
            StatusText = "请先在设置中连接 Obsidian Vault";
            return;
        }

        var id = SelectedMaterial.Id;
        await Run(async () =>
        {
            IsBusy = true;
            IsConfirmationOpen = false;
            last = await stagedCapture.ConfirmAsync(id);
            CanUndo = true;
            StatusText = "已收进 Inbox";
            RefreshStagedItems();
            SelectedMaterial = null;
        });
        IsBusy = false;
    }

    public async Task DeferSelectedAsync()
    {
        if (SelectedMaterial is null)
        {
            IsConfirmationOpen = false;
            return;
        }

        await Run(async () =>
        {
            var deferred = stagedCapture is not null
                ? await stagedCapture.DeferAsync(SelectedMaterial.Id)
                : await staging.UpdateAsync(
                    SelectedMaterial.Id,
                    item => item with { Status = StagedMaterialStatus.Deferred, LastError = null });
            RefreshStagedItems();
            SelectedMaterial = deferred;
            IsConfirmationOpen = false;
            StatusText = "已留在材料桶，稍后再处理";
        });
    }

    public async Task RemoveSelectedAsync()
    {
        if (SelectedMaterial is null) return;
        var id = SelectedMaterial.Id;
        await Run(async () =>
        {
            if (stagedCapture is not null)
            {
                await stagedCapture.RemoveAsync(id);
            }
            else
            {
                var material = staging.GetRequired(id);
                if (material.Status == StagedMaterialStatus.Capturing)
                {
                    throw new InvalidOperationException("正在收集的材料不能删除。");
                }
                await staging.RemoveAsync(id, deleteOwnedFiles: true);
            }

            RefreshStagedItems();
            SelectedMaterial = null;
            IsConfirmationOpen = false;
            IsDeleteConfirmation = false;
            StatusText = "已从材料桶移除";
        });
    }

    public void RequestRemove(StagedMaterial material)
    {
        SelectMaterial(material);
        IsDeleteConfirmation = true;
        IsConfirmationOpen = true;
    }

    public void CancelDelete()
    {
        IsDeleteConfirmation = false;
        IsConfirmationOpen = false;
    }

    public void SelectMaterial(StagedMaterial? material)
    {
        if (material is null) return;
        SelectedMaterial = StagedItems.SingleOrDefault(item => item.Id == material.Id) ?? material;
        IsDeleteConfirmation = false;
        IsConfirmationOpen = material.Status != StagedMaterialStatus.Capturing;
    }

    public async Task AppendDailyAsync()
    {
        if (daily is null || string.IsNullOrWhiteSpace(DailyText)) return;
        await Run(async () =>
        {
            last = await daily.AppendAsync(SelectedCategory.Value, DailyText);
            DailyText = string.Empty;
            StatusText = "已追加到今天的 Daily";
            CanUndo = true;
        });
    }

    public async Task UndoAsync()
    {
        if (last is null) return;
        var result = await new UndoService().UndoAsync(last);
        StatusText = result.Message;
        if (!result.IsSuccess) return;
        last = null;
        CanUndo = false;
    }

    public void OpenInbox()
    {
        if (inbox is null || string.IsNullOrWhiteSpace(VaultPath)) return;
        var uri = $"obsidian://open?vault={Uri.EscapeDataString(Path.GetFileName(VaultPath))}&file={Uri.EscapeDataString("00 Inbox收件箱")}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
    }

    partial void OnDraftTextChanged(string value)
    {
        if (restoringDraft) return;
        CancelPendingDraftSave();
        draftSaveCancellation = new CancellationTokenSource();
        _ = SaveDraftAfterDelayAsync(value, draftSaveCancellation.Token);
    }

    private void CancelPendingDraftSave()
    {
        draftSaveCancellation?.Cancel();
        draftSaveCancellation?.Dispose();
        draftSaveCancellation = null;
    }

    private async Task SaveDraftAfterDelayAsync(string value, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(300, cancellationToken);
            await staging.SaveDraftAsync(value, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusText = $"草稿保存失败：{ex.Message}";
        }
    }

    private void Configure(AppSettings settings)
    {
        var layout = new VaultLayout(settings);
        inbox = new InboxCaptureService(layout);
        stagedCapture = new StagedCaptureService(staging, inbox);
        daily = new DailyCaptureService(layout);
        VaultPath = layout.RootDirectory;
        HasVault = true;
        StatusText = "拖入文件、粘贴链接，或写下一条文字";
    }

    private void RefreshStagedItems()
    {
        var selectedId = SelectedMaterial?.Id;
        StagedItems.Clear();
        foreach (var item in staging.Snapshot.Items.OrderByDescending(item => item.CreatedAt))
        {
            StagedItems.Add(item);
        }

        OnPropertyChanged(nameof(HasStagedItems));
        if (selectedId is not null)
        {
            SelectedMaterial = StagedItems.SingleOrDefault(item => item.Id == selectedId);
        }
    }

    private async Task Run(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            RefreshStagedItems();
        }
    }

    private static MaterialStagingService CreateDefaultStaging()
    {
        var store = new StagingStore();
        return new MaterialStagingService(store, new FileStagingService(store));
    }
}

public sealed record CategoryOption(DailyCategory Value, string Label);
