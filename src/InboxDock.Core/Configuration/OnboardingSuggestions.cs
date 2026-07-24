using InboxDock.Core.Targets;
using InboxDock.Core.Vault;

namespace InboxDock.Core.Configuration;

/// <summary>
/// 根据发现的 Obsidian 配置生成首次配置建议的目标列表。
/// 只产出建议，不强制使用，调用方决定是否启用。
/// </summary>
public static class OnboardingSuggestions
{
    /// <summary>
    /// 根据写入方式偏好和 Vault 发现结果生成默认目标。
    /// </summary>
    /// <param name="preferredMode">用户在第二步选择的默认写入方式。</param>
    /// <param name="discovery">Vault 发现结果，可为 null。</param>
    public static IReadOnlyList<CaptureTarget> BuildDefaultTargets(
        TargetWriteMode preferredMode,
        VaultDiscoveryResult? discovery = null)
    {
        var targets = new List<CaptureTarget>();

        var inbox = preferredMode switch
        {
            TargetWriteMode.AppendToFile => BuildInboxTarget(discovery),
            TargetWriteMode.CreateNote => BuildCreateNoteTarget(discovery),
            TargetWriteMode.StagingOnly => BuildStagingOnlyTarget(),
            _ => BuildInboxTarget(discovery),
        };
        targets.Add(inbox with { IsDefault = true });

        if (discovery?.DailyNotesFolder is not null)
        {
            targets.Add(BuildDailyNotesTarget(discovery));
        }

        return targets;
    }

    private static CaptureTarget BuildInboxTarget(VaultDiscoveryResult? discovery)
    {
        var attachmentFolder = ResolveAttachmentFolder(discovery);
        return new CaptureTarget
        {
            Name = "收件箱",
            Icon = "📥",
            WriteMode = TargetWriteMode.AppendToFile,
            PathTemplate = "00 Inbox收件箱/收件箱.md",
            AttachmentPolicy = BuildAttachmentPolicy(attachmentFolder),
        };
    }

    private static CaptureTarget BuildCreateNoteTarget(VaultDiscoveryResult? discovery)
    {
        var attachmentFolder = ResolveAttachmentFolder(discovery);
        return new CaptureTarget
        {
            Name = "新建笔记",
            Icon = "📝",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "00 Inbox收件箱",
            FileNameTemplate = "{{timestamp}}-{{title}}",
            AttachmentPolicy = BuildAttachmentPolicy(attachmentFolder),
        };
    }

    private static CaptureTarget BuildStagingOnlyTarget() => new()
    {
        Name = "只暂存",
        Icon = "🗃️",
        WriteMode = TargetWriteMode.StagingOnly,
        AttachmentPolicy = AttachmentPolicy.StagingOnly,
    };

    private static CaptureTarget BuildDailyNotesTarget(VaultDiscoveryResult discovery)
    {
        var folder = discovery.DailyNotesFolder!;
        var fileNameTemplate = string.IsNullOrWhiteSpace(discovery.DailyNotesFormat)
            ? "{{date:yyyy-MM-dd}}.md"
            : ConvertMomentFormat(discovery.DailyNotesFormat);
        return new CaptureTarget
        {
            Name = "今日日记",
            Icon = "📅",
            WriteMode = TargetWriteMode.AppendToPeriodicFile,
            PathTemplate = folder,
            FileNameTemplate = fileNameTemplate,
            AttachmentPolicy = BuildAttachmentPolicy(ResolveAttachmentFolder(discovery)),
        };
    }

    private static string? ResolveAttachmentFolder(VaultDiscoveryResult? discovery)
    {
        if (discovery is null) return null;
        // 模式 1：使用指定目录；模式 3：笔记旁子目录。其他模式不提供建议，由 UI 使用默认。
        if (discovery.AttachmentLocationMode == 1 && !string.IsNullOrWhiteSpace(discovery.AttachmentFolder))
        {
            return discovery.AttachmentFolder;
        }

        return null;
    }

    private static AttachmentPolicy BuildAttachmentPolicy(string? discoveredFolder)
    {
        if (!string.IsNullOrWhiteSpace(discoveredFolder))
        {
            return new AttachmentPolicy
            {
                Kind = AttachmentPolicyKind.FixedDirectory,
                DirectoryTemplate = discoveredFolder,
            };
        }

        return AttachmentPolicy.DefaultDated;
    }

    /// <summary>
    /// 把 Obsidian 的 Moment.js 日期格式转换为 .NET 格式。只覆盖常见格式，未识别字符原样保留。
    /// </summary>
    private static string ConvertMomentFormat(string momentFormat) => momentFormat
        .Replace("YYYY", "yyyy")
        .Replace("MM", "MM")
        .Replace("DD", "dd")
        .Replace("HH", "HH")
        .Replace("mm", "mm");
}
