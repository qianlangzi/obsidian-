using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InboxDock.Core.Capture;
using InboxDock.Core.Configuration;
using InboxDock.Core.Daily;
using InboxDock.Core.History;
using InboxDock.Core.Staging;
using InboxDock.Core.Targets;
using InboxDock.Core.Vault;
using InboxDock.Core.Windowing;

namespace InboxDock.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly SettingsStore settingsStore;
    private readonly MaterialStagingService staging;
    private readonly TargetConfirmationStore confirmationStore;
    private InboxCaptureService? inbox;
    private StagedCaptureService? stagedCapture;
    private DailyCaptureService? daily;
    private CaptureResult? last;
    private TargetWriteResult? lastTargetResult;
    private string? lastErrorMessage;
    private TargetConfirmationRecord confirmations = TargetConfirmationRecord.Empty;
    private AppSettings? currentSettings;
    private CancellationTokenSource? draftSaveCancellation;
    private CancellationTokenSource? noteSaveCancellation;
    private bool restoringDraft;
    private bool restoringNote;

    [ObservableProperty] private string dailyText = string.Empty;
    [ObservableProperty] private string draftText = string.Empty;
    [ObservableProperty] private CategoryOption selectedCategory;
    [ObservableProperty] private string statusText = "请先选择 Obsidian Vault";
    [ObservableProperty] private bool canUndo;
    [ObservableProperty] private bool canOpenLastNote;
    [ObservableProperty] private bool canCopyLastError;
    [ObservableProperty] private bool hasVault;
    [ObservableProperty] private bool isConfirmationOpen;
    [ObservableProperty] private bool isDeleteConfirmation;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isPreviewOpen;
    [ObservableProperty] private string previewMarkdown = string.Empty;
    [ObservableProperty] private string previewTargetName = string.Empty;
    [ObservableProperty] private string previewNotePath = string.Empty;
    [ObservableProperty] private string previewReason = string.Empty;
    [ObservableProperty] private StagedMaterial? selectedMaterial;
    [ObservableProperty] private string selectedNote = string.Empty;
    [ObservableProperty] private string vaultPath = string.Empty;
    [ObservableProperty] private CaptureTarget? selectedTarget;
    [ObservableProperty] private string confirmButtonText = "收进 Inbox";
    [ObservableProperty] private string pendingCountText = string.Empty;
    [ObservableProperty] private bool hasPendingCount;
    [ObservableProperty] private bool hasFailedItems;
    [ObservableProperty] private string listSummaryText = string.Empty;
    [ObservableProperty] private bool isBatchMode;
    [ObservableProperty] private int selectedBatchCount;
    [ObservableProperty] private bool isBatchProcessing;
    [ObservableProperty] private string batchSummaryText = string.Empty;

    /// <summary>批量选中材料 Id 集合。</summary>
    public HashSet<Guid> SelectedBatchIds { get; } = [];

    public MainViewModel()
        : this(new SettingsStore(), CreateDefaultStaging(), new TargetConfirmationStore())
    {
    }

    internal MainViewModel(
        SettingsStore settingsStore,
        MaterialStagingService staging,
        TargetConfirmationStore confirmationStore)
    {
        this.settingsStore = settingsStore;
        this.staging = staging;
        this.confirmationStore = confirmationStore;
        selectedCategory = Categories.Single(item => item.Value == DailyCategory.Learning);
    }

    public ObservableCollection<StagedMaterial> StagedItems { get; } = [];

    public ObservableCollection<CaptureTarget> CaptureTargets { get; } = [];

    public IReadOnlyList<CategoryOption> Categories { get; } = Enum.GetValues<DailyCategory>()
        .Select(value => new CategoryOption(value, value.DisplayName()))
        .ToArray();

    public bool HasStagedItems => StagedItems.Count > 0;

    /// <summary>当前已加载的设置，供窗口读取窗口相关配置。</summary>
    public AppSettings? CurrentSettings => currentSettings;

    public async Task InitializeAsync()
    {
        var settings = await settingsStore.LoadAsync();
        if (settings.Settings is not null)
        {
            currentSettings = settings.Settings;
            Configure(settings.Settings);
        }
        confirmations = await confirmationStore.LoadAsync();

        var staged = await staging.LoadAsync();
        restoringDraft = true;
        DraftText = staged.Snapshot.DraftText;
        restoringDraft = false;
        RefreshStagedItems();
        if (staged.Error is not null) StatusText = staged.Error;
    }

    /// <summary>使用已加载的设置初始化主界面，跳过再次读取 settings.json。</summary>
    public async Task InitializeWithSettingsAsync(AppSettings settings)
    {
        currentSettings = settings;
        Configure(settings);
        confirmations = await confirmationStore.LoadAsync();

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

    public async Task StageClipboardImageAsync(BitmapSource bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var clipboardDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InboxDock",
            "Clipboard");
        Directory.CreateDirectory(clipboardDirectory);
        var path = Path.Combine(
            clipboardDirectory,
            $"clipboard-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.png");

        try
        {
            var frame = BitmapFrame.Create(bitmap);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(frame);
            await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                encoder.Save(stream);
            }

            await Run(async () =>
            {
                var material = await staging.StageFilesAsync([path]);
                RefreshStagedItems();
                SelectMaterial(material);
                StatusText = "剪贴板图片已暂存，请确认";
            });
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
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

        var target = ResolveTargetForMaterial(SelectedMaterial);
        if (target is null)
        {
            StatusText = "没有可用的收集目标，请在设置中添加。";
            return;
        }

        SelectedTarget = target;
        UpdateConfirmButtonText(target);

        if (target.WriteMode == TargetWriteMode.StagingOnly)
        {
            await KeepStagedOnlyAsync(SelectedMaterial.Id, target);
            return;
        }

        var lastRevision = confirmations.ConfirmedRevisions.TryGetValue(target.Id, out var r) ? (int?)r : null;

        try
        {
            var preview = stagedCapture.PreviewTarget(SelectedMaterial.Id, target, lastRevision);
            if (!preview.IsValid)
            {
                StatusText = preview.UserErrorMessage ?? "预览无效，无法写入。";
                return;
            }

            if (preview.RequiresConfirmation)
            {
                ShowPreview(preview);
                return;
            }

            await ExecuteTargetWriteAsync(SelectedMaterial.Id, target, lastRevision);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            RefreshStagedItems();
        }
    }

    /// <summary>预览确认后执行写入。</summary>
    public async Task ConfirmPreviewAsync()
    {
        if (SelectedMaterial is null || SelectedTarget is null) return;
        IsPreviewOpen = false;
        var lastRevision = confirmations.ConfirmedRevisions.TryGetValue(SelectedTarget.Id, out var r) ? (int?)r : null;
        await ExecuteTargetWriteAsync(SelectedMaterial.Id, SelectedTarget, lastRevision);
    }

    public void CancelPreview()
    {
        IsPreviewOpen = false;
        StatusText = "已取消写入。";
    }

    private async Task ExecuteTargetWriteAsync(Guid id, CaptureTarget target, int? lastRevision)
    {
        if (stagedCapture is null) return;
        await Run(async () =>
        {
            await FlushSelectedNoteAsync();
            IsBusy = true;
            var result = await stagedCapture.ConfirmToTargetAsync(id, target, VaultPath, lastRevision);
            if (!result.IsSuccess)
            {
                IsBusy = false;
                // 简化错误信息，不暴露笔记正文和附件内容。
                lastErrorMessage = result.ErrorMessage ?? "写入失败";
                var shortError = lastErrorMessage.Length > 80
                    ? lastErrorMessage[..80] + "…"
                    : lastErrorMessage;
                StatusText = $"{shortError}（材料仍安全保存在 InboxDock）";
                CanCopyLastError = true;
                RefreshStagedItems();
                // 写入失败：保留卡片并重新打开确认面板，允许重试或换目标。
                if (SelectedMaterial is not null)
                {
                    var refreshed = staging.GetRequired(id);
                    SelectedMaterial = refreshed;
                    IsConfirmationOpen = refreshed.Status != StagedMaterialStatus.Capturing;
                }
                return;
            }

            IsConfirmationOpen = false;
            lastTargetResult = result;
            lastErrorMessage = null;
            CanCopyLastError = false;
            last = null;
            CanUndo = true;
            CanOpenLastNote = target.WriteMode != TargetWriteMode.StagingOnly
                && !string.IsNullOrEmpty(result.RelativeNotePath);
            await RecordTargetConfirmationAsync(target);
            await UpdateLastUsedTargetAsync(SelectedMaterial?.Kind, target);
            StatusText = target.WriteMode == TargetWriteMode.StagingOnly
                ? "已保留在材料桶"
                : $"已保存到 {target.Name}";
            RefreshStagedItems();
            SelectedMaterial = null;
            IsBusy = false;
            if (target.WriteMode != TargetWriteMode.StagingOnly)
            {
                PostSuccessPeekRequested?.Invoke(PostSuccessPeekDelay);
            }
        });
    }

    private async Task KeepStagedOnlyAsync(Guid id, CaptureTarget target)
    {
        await Run(async () =>
        {
            await FlushSelectedNoteAsync();
            var updated = await staging.UpdateAsync(
                id,
                item => item with { PreferredTargetId = target.Id, Status = StagedMaterialStatus.Deferred, LastError = null });
            await UpdateLastUsedTargetAsync(updated.Kind, target);
            ReplaceStagedItem(updated);
            IsConfirmationOpen = false;
            StatusText = "已保留在材料桶";
        });
    }

    private CaptureTarget? ResolveTargetForMaterial(StagedMaterial material)
    {
        if (material.PreferredTargetId is { } preferredId
            && CaptureTargets.FirstOrDefault(t => t.Id == preferredId) is { } preferred)
        {
            return preferred;
        }

        if (confirmations.LastUsedTargets.TryGetValue((int)material.Kind, out var lastUsedId)
            && CaptureTargets.FirstOrDefault(t => t.Id == lastUsedId) is { } lastUsed)
        {
            return lastUsed;
        }

        return CaptureTargets.FirstOrDefault(t => t.IsDefault)
            ?? CaptureTargets.FirstOrDefault();
    }

    private void UpdateConfirmButtonText(CaptureTarget target)
    {
        ConfirmButtonText = target.WriteMode == TargetWriteMode.StagingOnly
            ? "保留在材料桶"
            : $"保存到 {target.Name}";
    }

    private void ShowPreview(CapturePreview preview)
    {
        PreviewTargetName = preview.TargetName;
        PreviewNotePath = preview.RelativeNotePath ?? preview.NotePath ?? string.Empty;
        PreviewReason = preview.ConfirmationReason ?? "首次使用此目标，请确认写入位置。";
        PreviewMarkdown = preview.Markdown;
        IsPreviewOpen = true;
        StatusText = "请确认写入位置";
    }

    private async Task RecordTargetConfirmationAsync(CaptureTarget target)
    {
        var revisions = new Dictionary<Guid, int>(confirmations.ConfirmedRevisions)
        {
            [target.Id] = target.Revision,
        };
        confirmations = confirmations with { ConfirmedRevisions = revisions };
        try
        {
            await confirmationStore.SaveAsync(confirmations);
        }
        catch (IOException)
        {
            // 持久化失败不阻断写入流程，下次仍会提示确认。
        }
    }

    private async Task UpdateLastUsedTargetAsync(StagedMaterialKind? kind, CaptureTarget target)
    {
        if (kind is null) return;
        var lastUsed = new Dictionary<int, Guid>(confirmations.LastUsedTargets)
        {
            [(int)kind.Value] = target.Id,
        };
        confirmations = confirmations with { LastUsedTargets = lastUsed };
        try
        {
            await confirmationStore.SaveAsync(confirmations);
        }
        catch (IOException)
        {
        }
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
            await FlushSelectedNoteAsync();
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
        restoringNote = true;
        SelectedNote = SelectedMaterial.Note ?? string.Empty;
        restoringNote = false;
        IsDeleteConfirmation = false;
        IsConfirmationOpen = material.Status != StagedMaterialStatus.Capturing;

        var resolved = ResolveTargetForMaterial(SelectedMaterial);
        if (resolved is not null)
        {
            SelectedTarget = resolved;
            UpdateConfirmButtonText(resolved);
        }
    }

    public async Task AppendDailyAsync()
    {
        if (daily is null || string.IsNullOrWhiteSpace(DailyText)) return;
        await Run(async () =>
        {
            last = await daily.AppendAsync(SelectedCategory.Value, DailyText);
            lastTargetResult = null;
            lastErrorMessage = null;
            CanCopyLastError = false;
            DailyText = string.Empty;
            StatusText = "已追加到今天的 Daily";
            CanUndo = true;
            CanOpenLastNote = false;
        });
    }

    public async Task UndoAsync()
    {
        if (lastTargetResult is not null)
        {
            var result = await new UndoService().UndoWriteAsync(lastTargetResult);
            StatusText = result.Message;
            if (!result.IsSuccess) return;
            lastTargetResult = null;
            CanUndo = false;
            CanOpenLastNote = false;
            RefreshStagedItems();
            return;
        }

        if (last is null) return;
        var undoResult = await new UndoService().UndoAsync(last);
        StatusText = undoResult.Message;
        if (!undoResult.IsSuccess) return;
        last = null;
        CanUndo = false;
        RefreshStagedItems();
    }

    /// <summary>复制最近的简化错误信息到剪贴板。错误信息不包含笔记正文和附件内容。</summary>
    public string? CopyLastError()
    {
        return lastErrorMessage;
    }

    /// <summary>打开最近一次成功写入的笔记。</summary>
    public void OpenLastNote()
    {
        OpenInbox();
    }

    public void OpenInbox()
    {
        var relative = lastTargetResult?.RelativeNotePath;
        if (string.IsNullOrEmpty(relative) && last?.InboxNotePath is not null)
        {
            relative = Path.GetRelativePath(VaultPath, last.InboxNotePath).Replace('\\', '/');
        }

        if (string.IsNullOrEmpty(VaultPath)) return;
        var vaultName = Uri.EscapeDataString(Path.GetFileName(VaultPath.TrimEnd('\\', '/')));
        var file = string.IsNullOrEmpty(relative) ? Uri.EscapeDataString("00 Inbox收件箱") : Uri.EscapeDataString(relative);
        var uri = $"obsidian://open?vault={vaultName}&file={file}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
    }

    /// <summary>用户在确认面板手动切换目标时刷新按钮文案。</summary>
    partial void OnSelectedTargetChanged(CaptureTarget? value)
    {
        if (value is not null) UpdateConfirmButtonText(value);
    }

    partial void OnDraftTextChanged(string value)
    {
        if (restoringDraft) return;
        CancelPendingDraftSave();
        draftSaveCancellation = new CancellationTokenSource();
        _ = SaveDraftAfterDelayAsync(value, draftSaveCancellation.Token);
    }

    partial void OnSelectedNoteChanged(string value)
    {
        if (restoringNote || SelectedMaterial?.Kind != StagedMaterialKind.Files) return;
        CancelPendingNoteSave();
        noteSaveCancellation = new CancellationTokenSource();
        _ = SaveNoteAfterDelayAsync(SelectedMaterial.Id, value, noteSaveCancellation.Token);
    }

    private void CancelPendingDraftSave()
    {
        draftSaveCancellation?.Cancel();
        draftSaveCancellation?.Dispose();
        draftSaveCancellation = null;
    }

    private void CancelPendingNoteSave()
    {
        noteSaveCancellation?.Cancel();
        noteSaveCancellation?.Dispose();
        noteSaveCancellation = null;
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

    private async Task SaveNoteAfterDelayAsync(Guid id, string value, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(300, cancellationToken);
            var updated = await staging.UpdateNoteAsync(id, value, cancellationToken);
            ReplaceStagedItem(updated);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusText = $"备注保存失败：{ex.Message}";
        }
    }

    private async Task FlushSelectedNoteAsync()
    {
        CancelPendingNoteSave();
        if (SelectedMaterial?.Kind != StagedMaterialKind.Files) return;
        var updated = await staging.UpdateNoteAsync(SelectedMaterial.Id, SelectedNote);
        ReplaceStagedItem(updated);
        SelectedMaterial = updated;
    }

    private void Configure(AppSettings settings)
    {
        var layout = new VaultLayout(settings);
        inbox = new InboxCaptureService(layout);
        var profile = settings.CurrentProfile;
        var targets = profile?.CaptureTargets.OrderBy(t => t.SortOrder).ToList() ?? [];

        if (targets.Count > 0)
        {
            var resolver = new TargetPathResolver(layout.RootDirectory);
            var previewService = new CapturePreviewService(resolver);
            var writeService = new TargetWriteService();
            stagedCapture = new StagedCaptureService(staging, inbox, previewService, writeService);
        }
        else
        {
            stagedCapture = new StagedCaptureService(staging, inbox);
        }

        daily = new DailyCaptureService(layout);
        VaultPath = layout.RootDirectory;
        HasVault = true;
        StatusText = "拖入文件、粘贴链接，或写下一条文字";

        CaptureTargets.Clear();
        foreach (var target in targets) CaptureTargets.Add(target);
        SelectedTarget = CaptureTargets.FirstOrDefault(t => t.IsDefault) ?? CaptureTargets.FirstOrDefault();
    }

    private void RefreshStagedItems()
    {
        var selectedId = SelectedMaterial?.Id;
        var desired = staging.Snapshot.Items.OrderByDescending(item => item.CreatedAt).ToArray();
        for (var index = StagedItems.Count - 1; index >= 0; index--)
        {
            if (desired.All(item => item.Id != StagedItems[index].Id)) StagedItems.RemoveAt(index);
        }

        for (var index = 0; index < desired.Length; index++)
        {
            var currentIndex = StagedItems.ToList().FindIndex(item => item.Id == desired[index].Id);
            if (currentIndex < 0)
            {
                StagedItems.Insert(index, desired[index]);
            }
            else
            {
                if (currentIndex != index) StagedItems.Move(currentIndex, index);
                if (!Equals(StagedItems[index], desired[index])) StagedItems[index] = desired[index];
            }
        }

        OnPropertyChanged(nameof(HasStagedItems));
        if (selectedId is not null)
        {
            SelectedMaterial = StagedItems.SingleOrDefault(item => item.Id == selectedId);
        }
        UpdatePendingState();
    }

    private void UpdatePendingState()
    {
        var items = StagedItems;
        var pendingCount = items.Count;
        PendingCountText = PendingCountFormatter.Format(pendingCount);
        HasPendingCount = PendingCountFormatter.ShouldShow(pendingCount);

        HasFailedItems = items.Any(item => item.Status == StagedMaterialStatus.Failed);

        if (pendingCount == 0)
        {
            ListSummaryText = string.Empty;
        }
        else
        {
            var earliest = items.Min(item => item.CreatedAt);
            ListSummaryText = $"共 {pendingCount} 项 · 最早 {earliest:MM-dd HH:mm}";
        }
    }

    [RelayCommand]
    public void ToggleBatchMode()
    {
        IsBatchMode = !IsBatchMode;
        if (!IsBatchMode)
        {
            SelectedBatchIds.Clear();
            SelectedBatchCount = 0;
        }
    }

    [RelayCommand]
    public void ToggleBatchSelection(StagedMaterial material)
    {
        if (!SelectedBatchIds.Add(material.Id))
        {
            SelectedBatchIds.Remove(material.Id);
        }
        SelectedBatchCount = SelectedBatchIds.Count;
    }

    [RelayCommand]
    public void SelectAllBatch()
    {
        SelectedBatchIds.Clear();
        foreach (var item in StagedItems) SelectedBatchIds.Add(item.Id);
        SelectedBatchCount = SelectedBatchIds.Count;
    }

    [RelayCommand]
    public void ClearBatchSelection()
    {
        SelectedBatchIds.Clear();
        SelectedBatchCount = 0;
    }

    public bool IsBatchMaterialSelected(Guid id) => SelectedBatchIds.Contains(id);

    /// <summary>把批量选中项写入当前目标。批量处理中禁止自动收边，完成后总结成功与失败数量。</summary>
    public async Task SaveBatchToSelectedTargetAsync()
    {
        if (stagedCapture is null || SelectedTarget is null || SelectedBatchIds.Count == 0) return;

        var target = SelectedTarget;
        var ids = SelectedBatchIds.ToArray();
        var lastRevision = confirmations.ConfirmedRevisions.TryGetValue(target.Id, out var r) ? (int?)r : null;

        IsBatchProcessing = true;
        BatchSummaryText = string.Empty;
        try
        {
            var result = await stagedCapture.ConfirmBatchAsync(ids, target, VaultPath, lastRevision);
            var success = result.SuccessCount;
            var failure = result.FailureCount;
            await RecordTargetConfirmationAsync(target);
            BatchSummaryText = failure == 0
                ? $"已保存 {success} 项到 {target.Name}"
                : $"成功 {success} 项，失败 {failure} 项";
            StatusText = BatchSummaryText;
            RefreshStagedItems();
            SelectedBatchIds.Clear();
            SelectedBatchCount = 0;
        }
        catch (Exception ex)
        {
            StatusText = $"批量保存失败：{ex.Message}";
        }
        finally
        {
            IsBatchProcessing = false;
        }
    }

    /// <summary>批量移除选中项。只删除 InboxDock 暂存副本，不触碰源文件。</summary>
    public async Task RemoveBatchAsync()
    {
        if (SelectedBatchIds.Count == 0) return;
        var ids = SelectedBatchIds.ToArray();
        await Run(async () =>
        {
            foreach (var id in ids)
            {
                try
                {
                    if (stagedCapture is not null)
                    {
                        await stagedCapture.RemoveAsync(id);
                    }
                    else
                    {
                        var material = staging.GetRequired(id);
                        if (material.Status == StagedMaterialStatus.Capturing) continue;
                        await staging.RemoveAsync(id, deleteOwnedFiles: true);
                    }
                }
                catch (InvalidOperationException)
                {
                    // 正在收集的材料跳过。
                }
            }
            RefreshStagedItems();
            SelectedBatchIds.Clear();
            SelectedBatchCount = 0;
            StatusText = "已移除选中的暂存副本";
        });
    }

    private void ReplaceStagedItem(StagedMaterial updated)
    {
        var index = StagedItems.ToList().FindIndex(item => item.Id == updated.Id);
        if (index >= 0) StagedItems[index] = updated;
        if (SelectedMaterial?.Id == updated.Id) SelectedMaterial = updated;
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

    /// <summary>写入成功后触发，参数为建议的短延时。失败、撤销悬停和批量处理中不触发。</summary>
    public event Action<TimeSpan>? PostSuccessPeekRequested;

    /// <summary>建议的写入成功后收回延时。</summary>
    public static readonly TimeSpan PostSuccessPeekDelay = TimeSpan.FromSeconds(2);

    public void Dispose()
    {
        CancelPendingDraftSave();
        CancelPendingNoteSave();
    }
}

public sealed record CategoryOption(DailyCategory Value, string Label);
