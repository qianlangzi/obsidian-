using InboxDock.Core.IO;
using InboxDock.Core.Markdown;

namespace InboxDock.Core.History;

public sealed record UndoResult(bool IsSuccess, string Message);

public sealed class UndoService
{
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

        if (capture.InboxNotePath is not null && File.Exists(capture.InboxNotePath)) File.Delete(capture.InboxNotePath);
        foreach (var path in capture.AttachmentPaths.Where(File.Exists)) File.Delete(path);
        return new UndoResult(true, "已撤销收集内容。");
    }
}
