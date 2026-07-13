using InboxDock.Core.IO;
using InboxDock.Core.Markdown;

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
}
