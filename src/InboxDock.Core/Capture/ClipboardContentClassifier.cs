namespace InboxDock.Core.Capture;

public enum ClipboardContentKind
{
    Empty,
    Files,
    Image,
    Link,
    Text,
}

public static class ClipboardContentClassifier
{
    public static ClipboardContentKind Classify(bool hasFiles, bool hasImage, string? text)
    {
        if (hasFiles) return ClipboardContentKind.Files;
        if (hasImage) return ClipboardContentKind.Image;
        if (string.IsNullOrWhiteSpace(text)) return ClipboardContentKind.Empty;

        var value = text.Trim();
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && uri.Scheme is "http" or "https"
            ? ClipboardContentKind.Link
            : ClipboardContentKind.Text;
    }
}
