namespace InboxDock.Core.Configuration;

/// <summary>
/// v1 旧版配置结构，仅用于迁移判断。独立于 <see cref="AppSettings"/>，
/// 避免新模型字段默认值掩盖迁移判断。
/// </summary>
public sealed record LegacyAppSettings
{
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
}
