namespace InboxDock.Core.Windowing;

/// <summary>
/// 把待处理数量格式化为贴边把手上显示的短文本。数量超过 99 时使用 99+，
/// 避免在狭窄的把手上溢出。
/// </summary>
public static class PendingCountFormatter
{
    /// <summary>数量大于 99 时返回 "99+"，否则返回数量本身。0 返回空字符串。</summary>
    public static string Format(int count)
    {
        if (count <= 0) return string.Empty;
        if (count > 99) return "99+";
        return count.ToString();
    }

    /// <summary>是否需要显示徽标（数量大于 0）。</summary>
    public static bool ShouldShow(int count) => count > 0;
}
