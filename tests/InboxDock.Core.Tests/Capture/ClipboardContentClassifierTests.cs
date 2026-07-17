using InboxDock.Core.Capture;

namespace InboxDock.Core.Tests.Capture;

public sealed class ClipboardContentClassifierTests
{
    [Theory]
    [InlineData(true, true, "https://example.com", ClipboardContentKind.Files)]
    [InlineData(false, true, "https://example.com", ClipboardContentKind.Image)]
    [InlineData(false, false, "https://example.com", ClipboardContentKind.Link)]
    [InlineData(false, false, "普通文字", ClipboardContentKind.Text)]
    [InlineData(false, false, "   ", ClipboardContentKind.Empty)]
    [InlineData(false, false, null, ClipboardContentKind.Empty)]
    public void Classify_UsesStablePriority(
        bool hasFiles,
        bool hasImage,
        string? text,
        ClipboardContentKind expected)
    {
        Assert.Equal(expected, ClipboardContentClassifier.Classify(hasFiles, hasImage, text));
    }
}
