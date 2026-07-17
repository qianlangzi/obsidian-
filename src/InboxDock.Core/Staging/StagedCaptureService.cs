using InboxDock.Core.Capture;

namespace InboxDock.Core.Staging;

public sealed class StagedCaptureService
{
    private readonly MaterialStagingService staging;
    private readonly InboxCaptureService inbox;

    public StagedCaptureService(MaterialStagingService staging, InboxCaptureService inbox)
    {
        this.staging = staging;
        this.inbox = inbox;
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
}
