using System.Globalization;
using System.Text;
using InboxDock.Core.Capture;

namespace InboxDock.Core.Markdown;

public static class InboxMarkdown
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg",
    };

    public static string ForText(string title, string content, Guid captureId, DateTimeOffset createdAt)
    {
        return $"""
            ---
            type: inbox
            status: unprocessed
            created: {createdAt:yyyy-MM-ddTHH:mm:sszzz}
            source: inboxdock
            capture_id: {captureId:D}
            ---

            # {title}

            ## 内容

            {content.Trim()}
            """ + Environment.NewLine;
    }

    public static string ForFiles(
        string title,
        IReadOnlyList<CapturedAttachment> files,
        Guid captureId,
        DateTimeOffset createdAt,
        string? note = null)
    {
        var builder = new StringBuilder(ForText(title, "", captureId, createdAt));
        var contentHeading = builder.ToString().LastIndexOf("## 内容", StringComparison.Ordinal);
        builder.Remove(contentHeading, builder.Length - contentHeading);
        if (!string.IsNullOrWhiteSpace(note))
        {
            builder.AppendLine("## 备注")
                .AppendLine()
                .AppendLine(note.Trim())
                .AppendLine();
        }

        builder.AppendLine("## 附件").AppendLine();

        foreach (var file in files)
        {
            var prefix = ImageExtensions.Contains(Path.GetExtension(file.OriginalName)) ? "!" : string.Empty;
            builder.Append("- ")
                .Append(prefix)
                .Append("[[")
                .Append(file.VaultRelativePath.Replace('\\', '/'))
                .Append("]] · ")
                .Append(FormatBytes(file.SizeBytes))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes.ToString(CultureInfo.InvariantCulture)} B";
        }

        var kilobytes = bytes / 1024d;
        return kilobytes < 1024
            ? $"{kilobytes:0.#} KB"
            : $"{kilobytes / 1024d:0.#} MB";
    }
}
