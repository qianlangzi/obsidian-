namespace InboxDock.Core.Templates;

/// <summary>模板变量替换使用的附件文件信息。</summary>
public sealed record TemplateAttachmentFile(
    string OriginalName,
    string VaultRelativePath,
    long SizeBytes);

/// <summary>模板渲染上下文，提供有限变量所需的数据。</summary>
public sealed record TemplateContext
{
    public string? Content { get; init; }

    public string? Title { get; init; }

    public string? Url { get; init; }

    public string? Note { get; init; }

    public IReadOnlyList<TemplateAttachmentFile>? Files { get; init; }

    /// <summary>用于日期、时间和时间戳变量。测试可传入固定时钟。</summary>
    public DateTimeOffset Now { get; init; } = DateTimeOffset.Now;

    /// <summary>材料来源（如 clipboard、drag、manual）。</summary>
    public string? Source { get; init; }

    /// <summary>目标名称。</summary>
    public string? Target { get; init; }

    public static TemplateContext Empty { get; } = new();
}
