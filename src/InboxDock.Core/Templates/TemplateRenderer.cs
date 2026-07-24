using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InboxDock.Core.Templates;

/// <summary>
/// 渲染有限模板变量。支持 content、title、url、note、files、date、time、timestamp、source、target。
/// 不支持循环、条件、函数、环境变量和文件读取。替换值不会再次解析。
/// </summary>
public static class TemplateRenderer
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg",
    };

    /// <summary>已知的日期/时间格式说明符。格式中出现的 ASCII 字母必须是其中之一，否则视为非法格式。</summary>
    private static readonly HashSet<char> ValidFormatSpecifiers =
    [
        'y', 'M', 'd', 'H', 'h', 'm', 's', 'f', 'F', 't', 'K', 'z', 'g',
    ];

    /// <summary>匹配 {{var}} 或 {{var:format}}，变量名仅允许字母数字下划线。</summary>
    private static readonly Regex VariablePattern = new(
        @"\{\{\s*(\w+)\s*(?::\s*([^}]+?))?\s*\}\}",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    public static TemplateRenderResult Render(string template, TemplateContext context)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(context);

        var errors = new List<TemplateError>();
        var result = new StringBuilder(template.Length + 64);

        var lastEnd = 0;
        foreach (Match match in VariablePattern.Matches(template))
        {
            result.Append(template, lastEnd, match.Index - lastEnd);

            var variable = match.Groups[1].Value;
            var format = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null;
            var value = TryResolve(variable, format, context, match.Index, errors);
            result.Append(value);

            lastEnd = match.Index + match.Length;
        }

        if (lastEnd < template.Length)
        {
            result.Append(template, lastEnd, template.Length - lastEnd);
        }

        return errors.Count == 0
            ? TemplateRenderResult.Success(result.ToString())
            : TemplateRenderResult.Failed(result.ToString(), errors);
    }

    private static string TryResolve(
        string variable,
        string? format,
        TemplateContext context,
        int position,
        List<TemplateError> errors)
    {
        switch (variable)
        {
            case "content":
                return context.Content ?? string.Empty;
            case "title":
                return context.Title ?? string.Empty;
            case "url":
                return context.Url ?? string.Empty;
            case "note":
                return context.Note ?? string.Empty;
            case "source":
                return context.Source ?? string.Empty;
            case "target":
                return context.Target ?? string.Empty;
            case "date":
                return FormatDate(context.Now.LocalDateTime, format ?? "yyyy-MM-dd", variable, position, errors);
            case "time":
                return FormatDate(context.Now.LocalDateTime, format ?? "HH:mm", variable, position, errors);
            case "timestamp":
                return FormatDate(context.Now.LocalDateTime, format ?? "yyyy-MM-dd-HHmmss", variable, position, errors);
            case "files":
                return RenderFiles(context, variable, position, errors);
            default:
                errors.Add(new TemplateError(variable, position, $"未知变量：{variable}。"));
                return string.Empty;
        }
    }

    private static string FormatDate(
        DateTime now,
        string format,
        string variable,
        int position,
        List<TemplateError> errors)
    {
        if (!IsValidDateFormat(format))
        {
            errors.Add(new TemplateError(variable, position, $"日期格式无效：{format}。"));
            return string.Empty;
        }

        try
        {
            return now.ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            errors.Add(new TemplateError(variable, position, $"日期格式无效：{format}。"));
            return string.Empty;
        }
    }

    private static bool IsValidDateFormat(string format)
    {
        foreach (var c in format)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
            {
                if (!ValidFormatSpecifiers.Contains(c)) return false;
            }
        }

        return true;
    }

    private static string RenderFiles(
        TemplateContext context,
        string variable,
        int position,
        List<TemplateError> errors)
    {
        var files = context.Files;
        if (files is null || files.Count == 0)
        {
            errors.Add(new TemplateError(variable, position, "材料不包含任何文件，无法渲染附件列表。"));
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var file in files)
        {
            var prefix = ImageExtensions.Contains(Path.GetExtension(file.OriginalName)) ? "!" : string.Empty;
            builder.Append("- ")
                .Append(prefix)
                .Append("[[")
                .Append(file.VaultRelativePath.Replace('\\', '/'))
                .Append("]] · ")
                .Append(FormatBytes(file.SizeBytes))
                .AppendLine();
        }

        // 移除末尾换行，保持与内联替换一致的稳定性。
        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes.ToString(CultureInfo.InvariantCulture)} B";
        var kilobytes = bytes / 1024d;
        return kilobytes < 1024
            ? $"{kilobytes:0.#} KB"
            : $"{kilobytes / 1024d:0.#} MB";
    }
}
