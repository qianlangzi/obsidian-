using InboxDock.Core.Targets;

namespace InboxDock.Core.Configuration;

/// <summary>
/// 把 v1 旧版配置转换成 v2 <see cref="AppSettings"/>。纯逻辑，不执行文件 I/O。
/// 迁移结果同时保留旧字段（兼容现有入口）并填充 <see cref="AppSettings.CurrentProfile"/>。
/// </summary>
public static class SettingsMigration
{
    /// <summary>迁移生成的“收件箱”目标固定 Id，便于幂等识别。</summary>
    public static readonly Guid InboxTargetId = new("11111111-1111-1111-1111-111111111111");

    /// <summary>迁移生成的“今日日记”目标固定 Id，便于幂等识别。</summary>
    public static readonly Guid DailyTargetId = new("22222222-2222-2222-2222-222222222222");

    /// <summary>把旧版配置转换成新版 <see cref="AppSettings"/>。</summary>
    public static AppSettings Migrate(LegacyAppSettings legacy)
    {
        ArgumentNullException.ThrowIfNull(legacy);

        var inboxTarget = CreateInboxTarget(legacy);
        var dailyTarget = CreateDailyTarget(legacy);

        var profile = new VaultProfile
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(legacy.VaultPath.TrimEnd('\\', '/')),
            VaultPath = legacy.VaultPath,
            DefaultTargetId = InboxTargetId,
            DefaultAttachmentPolicy = inboxTarget.AttachmentPolicy,
            CaptureTargets = [inboxTarget, dailyTarget],
            Theme = legacy.Theme,
            AlwaysOnTop = legacy.AlwaysOnTop,
            LaunchAtSignIn = legacy.LaunchAtSignIn,
            AutoHideDelay = TimeSpan.FromSeconds(5),
            GlobalHotkey = "Ctrl+Shift+Space",
            WindowState = new WindowState
            {
                Left = legacy.WindowLeft,
                Top = legacy.WindowTop,
            },
        };

        return new AppSettings
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            CurrentProfile = profile,
            VaultPath = legacy.VaultPath,
            InboxPath = legacy.InboxPath,
            DailyPath = legacy.DailyPath,
            DailyTemplatePath = legacy.DailyTemplatePath,
            AttachmentsPath = legacy.AttachmentsPath,
            AlwaysOnTop = legacy.AlwaysOnTop,
            LaunchAtSignIn = legacy.LaunchAtSignIn,
            Theme = legacy.Theme,
            WindowLeft = legacy.WindowLeft,
            WindowTop = legacy.WindowTop,
        };
    }

    private static CaptureTarget CreateInboxTarget(LegacyAppSettings legacy)
    {
        var attachmentTemplate = string.IsNullOrWhiteSpace(legacy.AttachmentsPath)
            ? "Attachments/{{date:yyyy-MM-dd}}"
            : NormalizeRelative(legacy.AttachmentsPath) + "/{{date:yyyy-MM-dd}}";

        return new CaptureTarget
        {
            Id = InboxTargetId,
            Name = "收件箱",
            Icon = "📥",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = NormalizeRelative(legacy.InboxPath),
            FileNameTemplate = "{{timestamp}}-{{title}}",
            ContentTemplate = string.Empty,
            AttachmentPolicy = new AttachmentPolicy
            {
                Kind = AttachmentPolicyKind.DatedDirectory,
                DirectoryTemplate = attachmentTemplate,
            },
            PostCaptureBehavior = PostCaptureBehavior.RemoveStaged,
            IsDefault = true,
            SortOrder = 0,
        };
    }

    private static CaptureTarget CreateDailyTarget(LegacyAppSettings legacy)
    {
        return new CaptureTarget
        {
            Id = DailyTargetId,
            Name = "今日日记",
            Icon = "📅",
            WriteMode = TargetWriteMode.AppendToPeriodicFile,
            PathTemplate = NormalizeRelative(legacy.DailyPath),
            FileNameTemplate = "{{date:yyyy-MM-dd}}",
            ContentTemplate = string.Empty,
            AttachmentPolicy = AttachmentPolicy.StagingOnly,
            InsertionMode = TargetInsertionMode.Append,
            PostCaptureBehavior = PostCaptureBehavior.RemoveStaged,
            IsDefault = false,
            SortOrder = 1,
        };
    }

    private static string NormalizeRelative(string path)
    {
        var trimmed = path?.Trim().Replace('\\', '/');
        return string.IsNullOrEmpty(trimmed) ? string.Empty : trimmed;
    }
}
