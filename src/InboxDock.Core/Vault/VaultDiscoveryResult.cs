namespace InboxDock.Core.Vault;

/// <summary>
/// 只读发现 Obsidian 配置的结果。只包含可安全读取到的建议，
/// 不包含笔记正文，不会修改 .obsidian。缺失项为 null，UI 自行决定如何提示。
/// </summary>
public sealed record VaultDiscoveryResult
{
    /// <summary>是否成功读取配置（Vault 存在即可，即使 .obsidian 配置缺失也返回 true）。</summary>
    public bool IsValid { get; init; }

    /// <summary>用户可读的发现说明，用于诊断。</summary>
    public string? Message { get; init; }

    /// <summary>Obsidian 设置的默认附件目录（Vault 相对）。未配置或禁用时为 null。</summary>
    public string? AttachmentFolder { get; init; }

    /// <summary>Obsidian 附件存放方式：0=Vault 根，1=指定目录，2=同笔记目录，3=笔记旁子目录。</summary>
    public int? AttachmentLocationMode { get; init; }

    /// <summary>Daily Notes 启用时日记所在目录（Vault 相对）。禁用或未配置时为 null。</summary>
    public string? DailyNotesFolder { get; init; }

    /// <summary>Daily Notes 文件名格式（如 yyyy-MM-dd）。未配置时为 null。</summary>
    public string? DailyNotesFormat { get; init; }

    /// <summary>Daily Notes 模板路径（Vault 相对）。未配置时为 null。</summary>
    public string? DailyNotesTemplate { get; init; }

    public static VaultDiscoveryResult Success() => new() { IsValid = true };
}
