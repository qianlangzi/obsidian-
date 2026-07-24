using System.Text.RegularExpressions;

namespace InboxDock.Core.Diagnostics;

/// <summary>
/// 遮蔽诊断信息中的私人路径。保留文件名和顶层目录结构，
/// 但替换用户名和完整中间路径，防止泄露私人信息。
/// </summary>
public static class DiagnosticRedactor
{
    /// <summary>遮蔽单个路径字符串。</summary>
    public static string RedactPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        // 遮蔽 Windows 用户目录：C:\Users\用户名\...
        var redacted = Regex.Replace(
            path,
            @"^[A-Za-z]:\\Users\\[^\\]+\\",
            "C:\\Users\\<user>\\",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        // 遮蔽 Unix 风格 home：/home/用户名/... 或 /Users/用户名/...
        redacted = Regex.Replace(
            redacted,
            @"^/(home|Users)/[^/]+/",
            "/<home>/",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        // 遮蔽 LOCALAPPDATA 路径中的用户名
        redacted = Regex.Replace(
            redacted,
            @"\\AppData\\Local\\[^\\]+\\",
            "\\AppData\\Local\\<app>\\",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        return redacted;
    }

    /// <summary>遮蔽多行文本中出现的所有绝对路径。</summary>
    public static string RedactPaths(string text, IEnumerable<string> knownPaths)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = text;
        // 按长度降序排列，先替换最长的路径，避免短路径破坏长路径替换。
        foreach (var path in knownPaths.OrderByDescending(p => p.Length).Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrEmpty(path)) continue;
            var redacted = RedactPath(path);
            if (redacted != path)
            {
                result = result.Replace(path, redacted, StringComparison.Ordinal);
            }
        }
        return result;
    }

    /// <summary>截断超长错误消息，防止日志爆炸。</summary>
    public static string TruncateError(string? message, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(message)) return string.Empty;
        if (message.Length <= maxLength) return message;
        return message[..maxLength] + "…[已截断]";
    }
}
