using InboxDock.Core.IO;
using InboxDock.Core.Markdown;
using InboxDock.Core.Targets;

namespace InboxDock.Core.History;

public sealed record UndoResult(bool IsSuccess, string Message);

public sealed class UndoService(string? recoveryRoot = null)
{
    private readonly string recoveryRoot = recoveryRoot ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InboxDock",
        "Recovery");

    public async Task<UndoResult> UndoAsync(Capture.CaptureResult capture, CancellationToken cancellationToken = default)
    {
        if (capture.DailyNotePath is not null && File.Exists(capture.DailyNotePath))
        {
            var content = await File.ReadAllTextAsync(capture.DailyNotePath, cancellationToken);
            if (!content.Contains($"<!-- inboxdock:{capture.CaptureId:D} -->", StringComparison.Ordinal))
                return new UndoResult(false, "记录已被修改，无法安全撤销。");
            await AtomicFile.ReplaceTextAsync(capture.DailyNotePath, DailyMarkdown.Remove(content, capture.CaptureId), cancellationToken);
            return new UndoResult(true, "已撤销 Daily 记录。");
        }

        var operationRecovery = Path.Combine(recoveryRoot, capture.CaptureId.ToString("D"));
        Directory.CreateDirectory(operationRecovery);
        var createdPaths = capture.AttachmentPaths
            .Concat(capture.InboxNotePath is null ? [] : [capture.InboxNotePath])
            .Where(File.Exists)
            .ToArray();

        foreach (var path in createdPaths)
        {
            var target = Path.Combine(operationRecovery, Path.GetFileName(path));
            var index = 2;
            while (File.Exists(target))
            {
                target = Path.Combine(
                    operationRecovery,
                    $"{Path.GetFileNameWithoutExtension(path)}-{index++}{Path.GetExtension(path)}");
            }
            File.Move(path, target);
        }

        return new UndoResult(true, "已撤销，文件已移到 InboxDock 恢复区。");
    }

    /// <summary>
    /// 撤销通过 <see cref="TargetWriteService"/> 完成的写入。
    /// 追加目标只移除本次标记段；新建笔记和附件移动到恢复区，不永久删除。
    /// 用户修改过标记区域时拒绝危险撤销。
    /// </summary>
    public async Task<UndoResult> UndoWriteAsync(TargetWriteResult result, CancellationToken cancellationToken = default)
    {
        if (!result.IsSuccess) return new UndoResult(false, "写入未成功，无需撤销。");

        if (result.WriteMode == TargetWriteMode.StagingOnly)
        {
            return new UndoResult(true, "只暂存目标无需撤销。");
        }

        var operationRecovery = Path.Combine(recoveryRoot, result.CaptureId.ToString("D"));

        if (result.WriteMode is TargetWriteMode.AppendToFile or TargetWriteMode.AppendToPeriodicFile)
        {
            if (result.NotePath is null || !File.Exists(result.NotePath))
            {
                return new UndoResult(false, "笔记文件不存在，无法撤销。");
            }

            var content = await File.ReadAllTextAsync(result.NotePath, cancellationToken);
            var startMarker = WriteMarker.Start(result.CaptureId);
            var endMarker = WriteMarker.End(result.CaptureId);

            if (!content.Contains(startMarker, StringComparison.Ordinal)
                || !content.Contains(endMarker, StringComparison.Ordinal))
            {
                return new UndoResult(false, "标记区域已被修改，拒绝危险撤销。");
            }

            var cleaned = RemoveMarkedBlock(content, startMarker, endMarker);
            await AtomicFile.ReplaceTextAsync(result.NotePath, cleaned, cancellationToken);

            await MoveAttachmentsToRecoveryAsync(result.AttachmentPaths, operationRecovery);
            return new UndoResult(true, "已撤销本次追加，附件移到恢复区。");
        }

        if (result.WriteMode == TargetWriteMode.CreateNote)
        {
            if (result.NotePath is null || !File.Exists(result.NotePath))
            {
                return new UndoResult(false, "笔记文件不存在，无法撤销。");
            }

            var content = await File.ReadAllTextAsync(result.NotePath, cancellationToken);
            var footprint = WriteMarker.Footprint(result.CaptureId);
            if (!content.Contains(footprint, StringComparison.Ordinal))
            {
                return new UndoResult(false, "笔记已被修改，拒绝危险撤销。");
            }

            Directory.CreateDirectory(operationRecovery);
            MoveFileToRecovery(result.NotePath, operationRecovery);
            await MoveAttachmentsToRecoveryAsync(result.AttachmentPaths, operationRecovery);
            return new UndoResult(true, "已撤销，笔记和附件移到恢复区。");
        }

        return new UndoResult(false, $"不支持的写入方式：{result.WriteMode}。");
    }

    private static string RemoveMarkedBlock(string content, string startMarker, string endMarker)
    {
        var startIndex = content.IndexOf(startMarker, StringComparison.Ordinal);
        var endIndex = content.IndexOf(endMarker, StringComparison.Ordinal) + endMarker.Length;

        if (startIndex < 0 || endIndex < startIndex) return content;

        // 移除标记块及其前导的空行。
        var lineStart = startIndex;
        while (lineStart > 0 && content[lineStart - 1] is '\r' or '\n') lineStart--;

        var builder = new System.Text.StringBuilder(content.Length - (endIndex - lineStart));
        builder.Append(content, 0, lineStart);
        if (endIndex < content.Length) builder.Append(content, endIndex, content.Length - endIndex);
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static async Task MoveAttachmentsToRecoveryAsync(IReadOnlyList<string> attachmentPaths, string recoveryDir)
    {
        if (attachmentPaths.Count == 0) return;
        Directory.CreateDirectory(recoveryDir);
        foreach (var path in attachmentPaths.Where(File.Exists))
        {
            MoveFileToRecovery(path, recoveryDir);
            await Task.CompletedTask;
        }
    }

    private static void MoveFileToRecovery(string path, string recoveryDir)
    {
        var target = Path.Combine(recoveryDir, Path.GetFileName(path));
        var index = 2;
        while (File.Exists(target))
        {
            target = Path.Combine(recoveryDir,
                $"{Path.GetFileNameWithoutExtension(path)}-{index++}{Path.GetExtension(path)}");
        }
        File.Move(path, target);
    }
}
