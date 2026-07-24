namespace InboxDock.Core.Configuration;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public sealed record AppSettings
{
    /// <summary>配置架构版本。旧版缺失此字段时视为 1，v0.3.0 新版固定为 2。</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>当前生效版本号，用于迁移判断。</summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// v0.3.0 当前 Vault 配置。旧版配置迁移后填充；迁移完成前为 null。
    /// </summary>
    public VaultProfile? CurrentProfile { get; init; }

    // 以下属性为 v1 旧版字段，迁移期间保留作为迁移输入和兼容入口，
    // 待 MainViewModel 完全切换到 CurrentProfile 后再决定是否移除。

    public required string VaultPath { get; init; }

    public string InboxPath { get; init; } = "00 Inbox收件箱";

    public string DailyPath { get; init; } = "01 Daily日常";

    public string DailyTemplatePath { get; init; } = "10 Knowledge Hub/Templates/Daily.md";

    public string AttachmentsPath { get; init; } = "05 Resources/Attachments";

    public bool AlwaysOnTop { get; init; } = true;

    public bool LaunchAtSignIn { get; init; }

    public AppTheme Theme { get; init; } = AppTheme.System;

    public double? WindowLeft { get; init; }

    public double? WindowTop { get; init; }

    public static AppSettings CreateDefault(string vaultPath) => new()
    {
        VaultPath = vaultPath,
    };
}
