namespace InboxDock.Core.Capture;

public enum DailyCategory
{
    Done,
    Learning,
    Problem,
    Idea,
}

public static class DailyCategoryExtensions
{
    public static string DisplayName(this DailyCategory category) => category switch
    {
        DailyCategory.Done => "完成",
        DailyCategory.Learning => "学习",
        DailyCategory.Problem => "问题",
        DailyCategory.Idea => "灵感",
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };
}

public sealed record CapturedAttachment(string OriginalName, string VaultRelativePath, long SizeBytes);

public sealed record CaptureResult(
    Guid CaptureId,
    DateTimeOffset CreatedAt,
    string? InboxNotePath,
    IReadOnlyList<string> AttachmentPaths,
    string? DailyNotePath = null,
    string? DailyRecord = null);
