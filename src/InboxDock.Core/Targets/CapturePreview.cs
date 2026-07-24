namespace InboxDock.Core.Targets;

/// <summary>
/// 写入预览结果。展示最终路径、附件位置和 Markdown，但不执行任何写入。
/// </summary>
public sealed record CapturePreview
{
    /// <summary>目标显示名称。</summary>
    public string TargetName { get; init; } = string.Empty;

    /// <summary>笔记绝对路径，StagingOnly 时为 null。</summary>
    public string? NotePath { get; init; }

    /// <summary>笔记 Vault 相对路径（正斜杠），用于 obsidian:// 链接。</summary>
    public string? RelativeNotePath { get; init; }

    /// <summary>附件绝对路径列表。</summary>
    public IReadOnlyList<string> AttachmentPaths { get; init; } = [];

    /// <summary>各附件解析详情。</summary>
    public IReadOnlyList<ResolvedAttachment> ResolvedAttachments { get; init; } = [];

    /// <summary>将写入的 Markdown 正文。</summary>
    public string Markdown { get; init; } = string.Empty;

    /// <summary>是否必须显示确认面板。</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>要求确认的原因。</summary>
    public string? ConfirmationReason { get; init; }

    /// <summary>预览是否有效（可写入）。</summary>
    public bool IsValid { get; init; }

    /// <summary>用户可读错误信息，UI 只显示此字段。</summary>
    public string? UserErrorMessage { get; init; }

    public static CapturePreview Invalid(string targetName, string error) => new()
    {
        TargetName = targetName,
        IsValid = false,
        UserErrorMessage = error,
    };
}
