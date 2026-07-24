using InboxDock.Core.Capture;
using InboxDock.Core.Templates;
using InboxDock.Core.Targets;

namespace InboxDock.Core.Staging;

/// <summary>单个材料批量确认结果。</summary>
public sealed record StagedBatchItemResult(
    Guid MaterialId,
    bool IsSuccess,
    string? ErrorMessage,
    TargetWriteResult? Result);

/// <summary>批量确认汇总。成功项已移除，失败项保留并记录错误。</summary>
public sealed record StagedBatchResult(IReadOnlyList<StagedBatchItemResult> Items)
{
    public int SuccessCount => Items.Count(i => i.IsSuccess);
    public int FailureCount => Items.Count(i => !i.IsSuccess);
}

public sealed class StagedCaptureService
{
    private readonly MaterialStagingService staging;
    private readonly InboxCaptureService inbox;
    private readonly CapturePreviewService? previewService;
    private readonly TargetWriteService? writeService;

    public StagedCaptureService(
        MaterialStagingService staging,
        InboxCaptureService inbox,
        CapturePreviewService? previewService = null,
        TargetWriteService? writeService = null)
    {
        this.staging = staging;
        this.inbox = inbox;
        this.previewService = previewService;
        this.writeService = writeService;
    }

    public async Task<CaptureResult> ConfirmAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var material = staging.GetRequired(id);
        if (material.Status == StagedMaterialStatus.Capturing)
        {
            throw new InvalidOperationException("这份材料正在收集中，请稍候。");
        }

        material = await staging.UpdateAsync(
            id,
            item => item with { Status = StagedMaterialStatus.Capturing, LastError = null },
            cancellationToken);

        try
        {
            var result = material.Kind switch
            {
                StagedMaterialKind.Files => await inbox.CaptureFilesAsync(
                    material.Files.Select(file => file.StagedPath).ToArray(),
                    material.Note,
                    cancellationToken),
                StagedMaterialKind.Link or StagedMaterialKind.Text => await inbox.CaptureTextAsync(
                    material.Content ?? throw new InvalidDataException("文字材料缺少内容。"),
                    cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(material.Kind)),
            };

            await staging.RemoveAsync(id, deleteOwnedFiles: true, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            await staging.UpdateAsync(
                id,
                item => item with { Status = StagedMaterialStatus.Failed, LastError = ex.Message },
                CancellationToken.None);
            throw;
        }
    }

    public Task<CaptureResult> RetryAsync(Guid id, CancellationToken cancellationToken = default) =>
        ConfirmAsync(id, cancellationToken);

    /// <summary>
    /// 生成目标写入预览，不执行任何写入。UI 用此结果决定是否弹出预览确认。
    /// </summary>
    public CapturePreview PreviewTarget(
        Guid id,
        CaptureTarget target,
        int? lastConfirmedRevision = null)
    {
        var preview = previewService ?? throw new InvalidOperationException("未配置写入预览服务。");
        var material = staging.GetRequired(id);
        var context = BuildContext(material);
        var attachments = BuildAttachments(material);
        return preview.Preview(target, context, attachments, lastConfirmedRevision);
    }

    /// <summary>
    /// 通过通用目标写入服务确认单个材料。预览无效或写入失败时不抛出，
    /// 返回失败的 <see cref="TargetWriteResult"/> 并把材料标记为 Failed。
    /// 成功后按目标 <see cref="PostCaptureBehavior"/> 移除或保留材料。
    /// </summary>
    public async Task<TargetWriteResult> ConfirmToTargetAsync(
        Guid id,
        CaptureTarget target,
        string vaultRoot,
        int? lastConfirmedRevision = null,
        CancellationToken cancellationToken = default)
    {
        var preview = previewService ?? throw new InvalidOperationException("未配置写入预览服务。");
        var writer = writeService ?? throw new InvalidOperationException("未配置目标写入服务。");

        var material = staging.GetRequired(id);
        if (material.Status == StagedMaterialStatus.Capturing)
        {
            throw new InvalidOperationException("这份材料正在收集中，请稍候。");
        }

        material = await staging.UpdateAsync(
            id,
            item => item with { Status = StagedMaterialStatus.Capturing, LastError = null },
            cancellationToken);

        try
        {
            var context = BuildContext(material);
            var attachments = BuildAttachments(material);
            var capturePreview = preview.Preview(target, context, attachments, lastConfirmedRevision);
            if (!capturePreview.IsValid)
            {
                await staging.UpdateAsync(
                    id,
                    item => item with
                    {
                        Status = StagedMaterialStatus.Failed,
                        LastError = capturePreview.UserErrorMessage ?? "预览无效，无法写入。",
                    },
                    CancellationToken.None);
                return TargetWriteResult.Failed(Guid.NewGuid(), capturePreview.UserErrorMessage ?? "预览无效，无法写入。");
            }

            var request = new TargetWriteRequest
            {
                Target = target,
                Preview = capturePreview,
                SourceFiles = material.Files.Select(file => file.StagedPath).ToArray(),
            };
            var result = await writer.WriteAsync(request, vaultRoot, cancellationToken);

            if (!result.IsSuccess)
            {
                await staging.UpdateAsync(
                    id,
                    item => item with { Status = StagedMaterialStatus.Failed, LastError = result.ErrorMessage },
                    CancellationToken.None);
                return result;
            }

            if (target.PostCaptureBehavior == PostCaptureBehavior.RemoveStaged)
            {
                await staging.RemoveAsync(id, deleteOwnedFiles: true, cancellationToken);
            }
            else
            {
                await staging.UpdateAsync(
                    id,
                    item => item with { Status = StagedMaterialStatus.AwaitingConfirmation, LastError = null },
                    CancellationToken.None);
            }

            return result;
        }
        catch (Exception ex)
        {
            await staging.UpdateAsync(
                id,
                item => item with { Status = StagedMaterialStatus.Failed, LastError = ex.Message },
                CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// 批量确认多个材料到同一目标。逐项写入，失败项保留且不影响其他卡片。
    /// </summary>
    public async Task<StagedBatchResult> ConfirmBatchAsync(
        IReadOnlyList<Guid> ids,
        CaptureTarget target,
        string vaultRoot,
        int? lastConfirmedRevision = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(target);

        var results = new List<StagedBatchItemResult>(ids.Count);
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await ConfirmToTargetAsync(id, target, vaultRoot, lastConfirmedRevision, cancellationToken);
                results.Add(new StagedBatchItemResult(id, result.IsSuccess, result.IsSuccess ? null : result.ErrorMessage, result));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new StagedBatchItemResult(id, false, ex.Message, null));
            }
        }

        return new StagedBatchResult(results);
    }

    public Task<StagedMaterial> DeferAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var material = staging.GetRequired(id);
        if (material.Status == StagedMaterialStatus.Capturing)
        {
            throw new InvalidOperationException("正在收集的材料不能暂存。");
        }

        return staging.UpdateAsync(
            id,
            item => item with { Status = StagedMaterialStatus.Deferred, LastError = null },
            cancellationToken);
    }

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var material = staging.GetRequired(id);
        if (material.Status == StagedMaterialStatus.Capturing)
        {
            throw new InvalidOperationException("正在收集的材料不能删除。");
        }

        return staging.RemoveAsync(id, deleteOwnedFiles: true, cancellationToken);
    }

    private static TemplateContext BuildContext(StagedMaterial material) => new()
    {
        Content = material.Content,
        Title = material.Title,
        Note = material.Note,
        Now = DateTimeOffset.Now,
        Source = "staged",
    };

    private static IReadOnlyList<AttachmentInput>? BuildAttachments(StagedMaterial material)
    {
        if (material.Kind != StagedMaterialKind.Files || material.Files.Count == 0)
        {
            return null;
        }

        return material.Files
            .Select(file => new AttachmentInput(file.OriginalName, file.SizeBytes))
            .ToArray();
    }
}
