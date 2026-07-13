using System.Text;

namespace InboxDock.Core.IO;

public static class AtomicFile
{
    public static async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), cancellationToken);
        File.Move(temporary, path, overwrite: false);
    }

    public static async Task ReplaceTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), cancellationToken);
        File.Move(temporary, path, overwrite: true);
    }
}
