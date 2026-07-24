namespace InboxDock.Core.Targets;

/// <summary>通用目标写入结果。</summary>
public sealed record TargetWriteResult
{
    /// <summary>本次写入唯一标识。</summary>
    public Guid CaptureId { get; init; }

    /// <summary>写入方式。</summary>
    public TargetWriteMode WriteMode { get; init; }

    /// <summary>笔记绝对路径，StagingOnly 时为 null。</summary>
    public string? NotePath { get; init; }

    /// <summary>笔记 Vault 相对路径。</summary>
    public string? RelativeNotePath { get; init; }

    /// <summary>已写入的附件绝对路径。</summary>
    public IReadOnlyList<string> AttachmentPaths { get; init; } = [];

    /// <summary>是否成功。</summary>
    public bool IsSuccess { get; init; }

    /// <summary>失败时的用户可读错误。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>回滚时无法自动清理的文件移入的恢复目录。</summary>
    public string? RecoveryDirectory { get; init; }

    public static TargetWriteResult Success(
        Guid captureId,
        TargetWriteMode writeMode,
        string? notePath,
        string? relativeNotePath,
        IReadOnlyList<string> attachments) => new()
        {
            CaptureId = captureId,
            WriteMode = writeMode,
            NotePath = notePath,
            RelativeNotePath = relativeNotePath,
            AttachmentPaths = attachments,
            IsSuccess = true,
        };

    public static TargetWriteResult Failed(Guid captureId, string error, string? recoveryDirectory = null) => new()
    {
        CaptureId = captureId,
        IsSuccess = false,
        ErrorMessage = error,
        RecoveryDirectory = recoveryDirectory,
    };
}
