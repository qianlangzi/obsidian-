namespace InboxDock.Core.Targets;

/// <summary>
/// 写入内容的唯一标记，用于安全撤销。追加模式包裹整个写入段，
/// 新建笔记在文件末尾添加标记，撤销时据此定位和移除本次写入。
/// </summary>
public static class WriteMarker
{
    /// <summary>追加段开始标记。</summary>
    public static string Start(Guid captureId) => $"<!-- inboxdock:start:{captureId:D} -->";

    /// <summary>追加段结束标记。</summary>
    public static string End(Guid captureId) => $"<!-- inboxdock:end:{captureId:D} -->";

    /// <summary>新建笔记末尾标记。</summary>
    public static string Footprint(Guid captureId) => $"{Environment.NewLine}<!-- inboxdock:{captureId:D} -->";

    /// <summary>把追加内容用开始和结束标记包裹。</summary>
    public static string WrapAppend(string content, Guid captureId)
    {
        var normalized = content.TrimEnd();
        return $"{Start(captureId)}{Environment.NewLine}{normalized}{Environment.NewLine}{End(captureId)}";
    }

    /// <summary>在新建笔记正文末尾添加标记。</summary>
    public static string StampCreateNote(string content, Guid captureId)
    {
        var normalized = content.TrimEnd();
        return $"{normalized}{Footprint(captureId)}";
    }
}
