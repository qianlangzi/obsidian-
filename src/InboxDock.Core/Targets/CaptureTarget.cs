namespace InboxDock.Core.Targets;

/// <summary>
/// 收集目标替代固定 Inbox、Daily 和 Project 概念。一个目标描述把材料写入 Vault 的方式、
/// 位置和模板，但不保存已解析的绝对路径。
/// </summary>
public sealed record CaptureTarget
{
    /// <summary>稳定唯一标识。</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>用户可见名称，不能为空。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>可选的 Emoji 或图标标识。</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>可选的主题色。</summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>写入方式。</summary>
    public TargetWriteMode WriteMode { get; init; } = TargetWriteMode.CreateNote;

    /// <summary>
    /// 目标路径模板。语义随写入方式变化：
    /// <list type="bullet">
    /// <item><see cref="TargetWriteMode.AppendToFile"/>：Vault 相对 Markdown 文件路径。</item>
    /// <item><see cref="TargetWriteMode.AppendToPeriodicFile"/>：Vault 相对目录，按日期渲染后追加到当日笔记。</item>
    /// <item><see cref="TargetWriteMode.CreateNote"/>：Vault 相对目录，新笔记写入此目录。</item>
    /// <item><see cref="TargetWriteMode.StagingOnly"/>：忽略。</item>
    /// </list>
    /// </summary>
    public string PathTemplate { get; init; } = string.Empty;

    /// <summary>新建笔记时使用的文件名模板。允许日期和内容变量。</summary>
    public string FileNameTemplate { get; init; } = "{{timestamp}}-{{title}}";

    /// <summary>生成笔记正文使用的模板。允许有限变量。</summary>
    public string ContentTemplate { get; init; } = string.Empty;

    /// <summary>附件策略。</summary>
    public AttachmentPolicy AttachmentPolicy { get; init; } = AttachmentPolicy.DefaultDated;

    /// <summary>追加到固定文件时的插入位置。</summary>
    public TargetInsertionMode InsertionMode { get; init; } = TargetInsertionMode.Append;

    /// <summary>插入到指定标题时使用的标题名称。</summary>
    public string? HeadingName { get; init; }

    /// <summary>写入成功后对暂存材料的处理方式。</summary>
    public PostCaptureBehavior PostCaptureBehavior { get; init; } = PostCaptureBehavior.RemoveStaged;

    /// <summary>是否为 Vault 默认目标。</summary>
    public bool IsDefault { get; init; }

    /// <summary>在目标列表中的排序顺序。</summary>
    public int SortOrder { get; init; }

    /// <summary>
    /// 修订号。每次目标配置被修改时递增。新目标或修改过的目标首次使用时强制显示写入预览。
    /// </summary>
    public int Revision { get; init; } = 1;

    /// <summary>创建一个带新 Id 的副本，并将 Revision 重置为 1。</summary>
    public CaptureTarget WithNewIdentity() => this with
    {
        Id = Guid.NewGuid(),
        Revision = 1,
    };
}
