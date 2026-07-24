namespace InboxDock.Core.Targets;

/// <summary>
/// 附件在 Vault 中的存放策略。任何渲染后的路径必须为非空 Vault 相对路径，
/// 解析后仍位于 Vault 根目录内，且不覆盖已有文件。
/// </summary>
public sealed record AttachmentPolicy
{
    /// <summary>附件策略类型。</summary>
    public AttachmentPolicyKind Kind { get; init; } = AttachmentPolicyKind.DatedDirectory;

    /// <summary>
    /// Vault 相对目录模板，用于 <see cref="AttachmentPolicyKind.FixedDirectory"/>、
    /// <see cref="AttachmentPolicyKind.DatedDirectory"/> 和
    /// <see cref="AttachmentPolicyKind.BesideNote"/>。允许日期变量。
    /// </summary>
    public string DirectoryTemplate { get; init; } = "Attachments/{{date:yyyy-MM-dd}}";

    /// <summary>
    /// 跟随 Obsidian 时使用的 Vault 相对目录，仅在发现服务解析到 Obsidian 附件设置后填充。
    /// 解析器不直接读取 .obsidian，此字段为已解析结果。
    /// </summary>
    public string? FollowObsidianDirectory { get; init; }

    public static AttachmentPolicy DefaultDated { get; } = new()
    {
        Kind = AttachmentPolicyKind.DatedDirectory,
        DirectoryTemplate = "Attachments/{{date:yyyy-MM-dd}}",
    };

    public static AttachmentPolicy StagingOnly { get; } = new()
    {
        Kind = AttachmentPolicyKind.StagingOnly,
        DirectoryTemplate = string.Empty,
    };
}
