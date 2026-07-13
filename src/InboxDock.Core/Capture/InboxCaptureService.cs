using InboxDock.Core.IO;
using InboxDock.Core.Markdown;
using InboxDock.Core.Vault;

namespace InboxDock.Core.Capture;

public sealed class InboxCaptureService(VaultLayout layout)
{
    public async Task<CaptureResult> CaptureTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("请输入要收集的内容。", nameof(text));
        var now = DateTimeOffset.Now;
        var id = Guid.NewGuid();
        Directory.CreateDirectory(layout.InboxDirectory);
        var stem = $"{now:yyyy-MM-dd-HHmmss}-{SafeName.FromText(text)}";
        var name = SafeName.AvailableFileName(stem + ".md", candidate => File.Exists(Path.Combine(layout.InboxDirectory, candidate)));
        var path = Path.Combine(layout.InboxDirectory, name);
        var title = Path.GetFileNameWithoutExtension(name)[18..];
        await AtomicFile.WriteTextAsync(path, InboxMarkdown.ForText(title, text, id, now), cancellationToken);
        return new CaptureResult(id, now, path, []);
    }

    public async Task<CaptureResult> CaptureFilesAsync(IReadOnlyList<string> sourcePaths, CancellationToken cancellationToken = default)
    {
        if (sourcePaths.Count == 0) throw new ArgumentException("请拖入至少一个文件。", nameof(sourcePaths));
        var now = DateTimeOffset.Now;
        var id = Guid.NewGuid();
        var datedDirectory = Path.Combine(layout.AttachmentsDirectory, now.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(datedDirectory);
        Directory.CreateDirectory(layout.InboxDirectory);
        var copied = new List<string>();
        var attachments = new List<CapturedAttachment>();
        try
        {
            foreach (var source in sourcePaths)
            {
                if (!File.Exists(source)) throw new FileNotFoundException("源文件不存在。", source);
                var desired = Path.GetFileName(source);
                var name = SafeName.AvailableFileName(desired, candidate => File.Exists(Path.Combine(datedDirectory, candidate)));
                var destination = Path.Combine(datedDirectory, name);
                var temporary = destination + $".{id:N}.tmp";
                await using (var input = File.OpenRead(source))
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    await input.CopyToAsync(output, cancellationToken);
                File.Move(temporary, destination);
                copied.Add(destination);
                var relative = Path.GetRelativePath(layout.RootDirectory, destination).Replace('\\', '/');
                attachments.Add(new CapturedAttachment(desired, relative, new FileInfo(destination).Length));
            }

            var noteName = SafeName.AvailableFileName($"{now:yyyy-MM-dd-HHmmss}-材料 {attachments.Count} 项.md", candidate => File.Exists(Path.Combine(layout.InboxDirectory, candidate)));
            var notePath = Path.Combine(layout.InboxDirectory, noteName);
            await AtomicFile.WriteTextAsync(notePath, InboxMarkdown.ForFiles($"材料 {attachments.Count} 项", attachments, id, now), cancellationToken);
            return new CaptureResult(id, now, notePath, copied);
        }
        catch
        {
            foreach (var path in copied.Where(File.Exists)) File.Delete(path);
            throw;
        }
    }
}
