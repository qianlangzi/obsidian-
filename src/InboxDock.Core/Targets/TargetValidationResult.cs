namespace InboxDock.Core.Targets;

/// <summary>目标路径解析结果。</summary>
public sealed record TargetValidationResult
{
    public bool IsValid { get; init; }

    public string Message { get; init; } = string.Empty;

    public ResolvedTargetPaths? ResolvedPaths { get; init; }

    public static TargetValidationResult Success(ResolvedTargetPaths paths) => new()
    {
        IsValid = true,
        Message = "路径校验通过。",
        ResolvedPaths = paths,
    };

    public static TargetValidationResult Failed(string message) => new()
    {
        IsValid = false,
        Message = message,
    };
}

/// <summary>解析器输入的附件信息。</summary>
public sealed record AttachmentInput(string OriginalName, long SizeBytes);
