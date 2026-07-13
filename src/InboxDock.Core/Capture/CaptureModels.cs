namespace InboxDock.Core.Capture;

public enum DailyCategory
{
    Done,
    Learning,
    Problem,
    Idea,
}

public sealed record CapturedAttachment(string OriginalName, string VaultRelativePath, long SizeBytes);

public sealed record CaptureResult(
    Guid CaptureId,
    DateTimeOffset CreatedAt,
    string? InboxNotePath,
    IReadOnlyList<string> AttachmentPaths,
    string? DailyNotePath = null,
    string? DailyRecord = null);
