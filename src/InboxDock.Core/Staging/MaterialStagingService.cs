namespace InboxDock.Core.Staging;

public sealed class MaterialStagingService
{
    private readonly StagingStore store;
    private readonly FileStagingService files;
    private readonly Func<DateTimeOffset> clock;
    private readonly SemaphoreSlim gate = new(1, 1);

    public MaterialStagingService(
        StagingStore store,
        FileStagingService files,
        Func<DateTimeOffset>? clock = null)
    {
        this.store = store;
        this.files = files;
        this.clock = clock ?? (() => DateTimeOffset.Now);
    }

    public StagingSnapshot Snapshot { get; private set; } = StagingSnapshot.Empty;

    public string RootDirectory => store.RootDirectory;

    public async Task<StagingLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var result = await store.LoadAsync(cancellationToken);
            Snapshot = MarkMissingFiles(result.Snapshot, out var changed);
            if (changed && result.Error is null) await store.SaveAsync(Snapshot, cancellationToken);
            return result with { Snapshot = Snapshot };
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<StagedMaterial> StageFilesAsync(
        IReadOnlyList<string> sourcePaths,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var result = await files.StageFilesAsync(sourcePaths, Snapshot, cancellationToken);
            Snapshot = result.Snapshot;
            return result.Material;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<StagedMaterial?> StagePastedLinkAsync(
        string value,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeHttpUrl(value, out var normalized)) return null;
        var uri = new Uri(normalized, UriKind.Absolute);
        return await AddTextualMaterialAsync(
            StagedMaterialKind.Link,
            uri.Host,
            normalized,
            clearDraft: false,
            cancellationToken);
    }

    public Task<StagedMaterial> StageDraftAsync(string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("请输入要暂存的文字。", nameof(value));
        var content = value.Trim();
        var firstLine = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "文字笔记";
        var title = firstLine.Length <= 28 ? firstLine : firstLine[..28] + "…";
        return AddTextualMaterialAsync(StagedMaterialKind.Text, title, content, clearDraft: true, cancellationToken);
    }

    public async Task SaveDraftAsync(string value, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            Snapshot = Snapshot with { DraftText = value ?? string.Empty };
            await store.SaveAsync(Snapshot, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<StagedMaterial> UpdateNoteAsync(
        Guid id,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return UpdateAsync(
            id,
            item => item.Kind == StagedMaterialKind.Files
                ? item with { Note = normalized }
                : throw new InvalidOperationException("只有文件材料可以添加备注。"),
            cancellationToken);
    }

    /// <summary>
    /// 更新材料的首选收集目标。传入 null 清除选择，用于目标被删除后要求重新选择。
    /// 不会校验目标是否存在；调用方在删除目标时应主动清除引用。
    /// </summary>
    public Task<StagedMaterial> UpdatePreferredTargetAsync(
        Guid id,
        Guid? targetId,
        CancellationToken cancellationToken = default)
    {
        return UpdateAsync(
            id,
            item => item with { PreferredTargetId = targetId },
            cancellationToken);
    }

    public async Task<StagedMaterial> UpdateAsync(
        Guid id,
        Func<StagedMaterial, StagedMaterial> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = GetRequired(id);
            var updated = update(current);
            Snapshot = Snapshot with
            {
                Items = Snapshot.Items.Select(item => item.Id == id ? updated : item).ToArray(),
            };
            await store.SaveAsync(Snapshot, cancellationToken);
            return updated;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RemoveAsync(Guid id, bool deleteOwnedFiles, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = GetRequired(id);
            Snapshot = Snapshot with { Items = Snapshot.Items.Where(item => item.Id != id).ToArray() };
            await store.SaveAsync(Snapshot, cancellationToken);
            if (deleteOwnedFiles && current.Kind == StagedMaterialKind.Files)
            {
                var directory = Path.Combine(store.FilesDirectory, current.Id.ToString("N"));
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public StagedMaterial GetRequired(Guid id) =>
        Snapshot.Items.SingleOrDefault(item => item.Id == id)
        ?? throw new KeyNotFoundException("暂存材料不存在或已经处理。 ");

    public static bool TryNormalizeHttpUrl(string? input, out string normalized)
    {
        normalized = string.Empty;
        var candidate = input?.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;
        normalized = candidate!;
        return true;
    }

    private async Task<StagedMaterial> AddTextualMaterialAsync(
        StagedMaterialKind kind,
        string title,
        string content,
        bool clearDraft,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var material = new StagedMaterial(
                Guid.NewGuid(),
                kind,
                title,
                clock(),
                StagedMaterialStatus.AwaitingConfirmation,
                [],
                content);
            Snapshot = new StagingSnapshot(
                [.. Snapshot.Items, material],
                clearDraft ? string.Empty : Snapshot.DraftText);
            await store.SaveAsync(Snapshot, cancellationToken);
            return material;
        }
        finally
        {
            gate.Release();
        }
    }

    private static StagingSnapshot MarkMissingFiles(StagingSnapshot snapshot, out bool changed)
    {
        changed = false;
        var items = new List<StagedMaterial>(snapshot.Items.Count);
        foreach (var item in snapshot.Items)
        {
            if (item.Kind == StagedMaterialKind.Files && item.Files.Any(file => !File.Exists(file.StagedPath)))
            {
                changed = true;
                items.Add(item with
                {
                    Status = StagedMaterialStatus.Failed,
                    LastError = "暂存文件缺失，请移除此卡片后重新拖入。",
                });
            }
            else
            {
                items.Add(item);
            }
        }

        return snapshot with { Items = items };
    }
}
