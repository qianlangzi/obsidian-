namespace InboxDock.Core.SystemIntegration;

/// <summary>
/// 登录启动项的可测试纯逻辑。负责生成、解析和比较注册表命令字符串，
/// 不触碰 Windows 注册表。注册表操作由 App 层的 LaunchAtSignInService 完成。
/// </summary>
public static class LaunchAtSignInCommand
{
    /// <summary>
    /// 将可执行文件路径包装为带引号的命令字符串，处理路径含空格的情况。
    /// 不附加任何参数，保证卸载时能精确匹配。
    /// </summary>
    public static string BuildValue(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("可执行文件路径不能为空。", nameof(executablePath));
        }

        var trimmed = executablePath.Trim();
        // 已带引号时不再重复包装。
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            return trimmed;
        }

        return $"\"{trimmed}\"";
    }

    /// <summary>
    /// 从注册表值字符串中提取可执行文件路径。无法解析时返回 null。
    /// 支持带引号和不带引号的形式。
    /// </summary>
    public static string? ExtractPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        if (trimmed.Length == 0) return null;

        if (trimmed[0] == '"')
        {
            var closing = trimmed.IndexOf('"', 1);
            if (closing <= 0) return null;
            return trimmed.Substring(1, closing - 1);
        }

        // 不带引号时取第一个空格前的内容，或整串。
        var space = trimmed.IndexOf(' ');
        return space < 0 ? trimmed : trimmed.Substring(0, space);
    }

    /// <summary>
    /// 判断已注册路径是否与当前可执行路径一致。便携版移动后会返回 false。
    /// 比较前规范化大小写和目录分隔符。
    /// </summary>
    public static bool IsCurrentPath(string? registeredValue, string currentExecutablePath)
    {
        var registered = ExtractPath(registeredValue);
        if (string.IsNullOrEmpty(registered)) return false;
        if (string.IsNullOrWhiteSpace(currentExecutablePath)) return false;

        return NormalizePath(registered) == NormalizePath(currentExecutablePath);
    }

    /// <summary>
    /// 计算当前启动项状态。便携版移动后返回 NeedsRepair。
    /// </summary>
    public static LaunchAtSignInStatus ResolveStatus(
        bool enabledInSettings,
        string? registeredValue,
        string currentExecutablePath)
    {
        if (!enabledInSettings && string.IsNullOrEmpty(registeredValue))
        {
            return LaunchAtSignInStatus.Disabled;
        }

        if (enabledInSettings && !string.IsNullOrEmpty(registeredValue)
            && IsCurrentPath(registeredValue, currentExecutablePath))
        {
            return LaunchAtSignInStatus.Enabled;
        }

        // 设置中开启但未注册、或注册路径与当前不一致：需要修复。
        if (enabledInSettings || !string.IsNullOrEmpty(registeredValue))
        {
            return LaunchAtSignInStatus.NeedsRepair;
        }

        return LaunchAtSignInStatus.Disabled;
    }

    private static string NormalizePath(string path)
    {
        var full = Path.GetFullPath(path.Trim());
        return full.Replace('/', '\\').TrimEnd('\\').ToUpperInvariant();
    }
}
