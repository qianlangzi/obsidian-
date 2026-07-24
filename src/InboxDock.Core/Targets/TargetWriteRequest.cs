namespace InboxDock.Core.Targets;

/// <summary>通用目标写入请求。</summary>
public sealed record TargetWriteRequest
{
    /// <summary>收集目标。</summary>
    public required CaptureTarget Target { get; init; }

    /// <summary>写入预览（已校验的路径和 Markdown）。</summary>
    public required CapturePreview Preview { get; init; }

    /// <summary>暂存附件的源文件绝对路径，按 Preview.ResolvedAttachments 顺序对应。</summary>
    public IReadOnlyList<string> SourceFiles { get; init; } = [];

    /// <summary>本次写入的唯一标识，用于安全撤销。未指定时自动生成。</summary>
    public Guid CaptureId { get; init; } = Guid.NewGuid();
}
