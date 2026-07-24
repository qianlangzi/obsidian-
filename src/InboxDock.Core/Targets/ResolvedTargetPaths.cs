namespace InboxDock.Core.Targets;

/// <summary>解析后的单个附件路径信息。</summary>
public sealed record ResolvedAttachment(
    string OriginalName,
    string AbsolutePath,
    string VaultRelativePath,
    long SizeBytes);

/// <summary>收集目标解析后的最终路径。不创建任何目录或文件。</summary>
public sealed record ResolvedTargetPaths
{
    /// <summary>笔记绝对路径。StagingOnly 或无笔记时为 null。</summary>
    public string? NotePath { get; init; }

    /// <summary>笔记的 Vault 相对路径（正斜杠分隔），用于 obsidian:// 链接。</summary>
    public string? RelativeNotePath { get; init; }

    /// <summary>附件目录绝对路径。无附件或暂存时为 null。</summary>
    public string? AttachmentDirectory { get; init; }

    /// <summary>各附件的冲突安全解析路径。</summary>
    public IReadOnlyList<ResolvedAttachment> ResolvedAttachments { get; init; } = [];

    /// <summary>目标显示名称。</summary>
    public string TargetDisplayName { get; init; } = string.Empty;

    /// <summary>解析时是否发生了文件名或附件重名冲突（已自动生成后缀）。</summary>
    public bool HadNameCollision { get; init; }

    public static ResolvedTargetPaths Empty(string displayName) => new() { TargetDisplayName = displayName };
}
