using InboxDock.Core.Capture;
using InboxDock.Core.IO;
using InboxDock.Core.Markdown;
using InboxDock.Core.Vault;

namespace InboxDock.Core.Daily;

public sealed class DailyCaptureService(VaultLayout layout)
{
    public async Task<CaptureResult> AppendAsync(DailyCategory category, string entry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entry)) throw new ArgumentException("请输入日记内容。", nameof(entry));
        var now = DateTimeOffset.Now;
        var id = Guid.NewGuid();
        Directory.CreateDirectory(layout.DailyDirectory);
        var path = Path.Combine(layout.DailyDirectory, $"{now:yyyy-MM-dd}.md");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var exists = File.Exists(path);
            var original = exists ? await File.ReadAllTextAsync(path, cancellationToken) : await InitialContentAsync(now, cancellationToken);
            var stamp = exists ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            var updated = DailyMarkdown.Append(original, category, entry, id, TimeOnly.FromDateTime(now.LocalDateTime));
            if (exists && File.GetLastWriteTimeUtc(path) != stamp) { await Task.Delay(100 * (attempt + 1), cancellationToken); continue; }
            if (exists) await AtomicFile.ReplaceTextAsync(path, updated, cancellationToken); else await AtomicFile.WriteTextAsync(path, updated, cancellationToken);
            var marker = $"<!-- inboxdock:{id:D} -->";
            return new CaptureResult(id, now, null, [], path, updated.Split('\n').First(line => line.Contains(marker, StringComparison.Ordinal)).Trim());
        }
        throw new IOException("Daily 正在被其他程序修改，请稍后重试。");
    }

    private async Task<string> InitialContentAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (File.Exists(layout.DailyTemplatePath))
            return DailyMarkdown.CreateFromTemplate(await File.ReadAllTextAsync(layout.DailyTemplatePath, cancellationToken), DateOnly.FromDateTime(now.LocalDateTime));
        return $"# {now:yyyy-MM-dd}{Environment.NewLine}";
    }
}
