using System.Text.RegularExpressions;
using InboxDock.Core.Capture;

namespace InboxDock.Core.Markdown;

public static class DailyMarkdown
{
    public const string SectionHeading = "## InboxDock 快速记录";

    public static string CreateFromTemplate(string template, DateOnly date)
    {
        var value = date.ToString("yyyy-MM-dd");
        return template
            .Replace("{{date:YYYY-MM-DD}}", value, StringComparison.Ordinal)
            .Replace("{{title}}", value, StringComparison.Ordinal);
    }

    public static string Append(
        string content,
        DailyCategory category,
        string entry,
        Guid captureId,
        TimeOnly time)
    {
        var normalized = content.TrimEnd();
        if (!normalized.Contains(SectionHeading, StringComparison.Ordinal))
        {
            normalized += $"{Environment.NewLine}{Environment.NewLine}{SectionHeading}";
        }

        var label = category switch
        {
            DailyCategory.Done => "完成",
            DailyCategory.Learning => "学习",
            DailyCategory.Problem => "问题",
            DailyCategory.Idea => "灵感",
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };

        return $"{normalized}{Environment.NewLine}{Environment.NewLine}- {time:HH:mm} · {label} · {entry.Trim()} <!-- inboxdock:{captureId:D} -->{Environment.NewLine}";
    }

    public static string Remove(string content, Guid captureId)
    {
        var marker = Regex.Escape($"<!-- inboxdock:{captureId:D} -->");
        return Regex.Replace(
            content,
            $@"(?m)^.*{marker}.*(?:\r?\n)?$",
            string.Empty,
            RegexOptions.CultureInvariant);
    }
}
