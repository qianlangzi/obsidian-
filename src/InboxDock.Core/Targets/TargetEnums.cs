namespace InboxDock.Core.Targets;

/// <summary>收集目标的写入方式。v0.3.0 只支持这四种。</summary>
public enum TargetWriteMode
{
    /// <summary>追加到固定的 Markdown 文件。</summary>
    AppendToFile,

    /// <summary>按照日期路径追加到周期笔记。</summary>
    AppendToPeriodicFile,

    /// <summary>每次收集创建一篇新笔记。</summary>
    CreateNote,

    /// <summary>只保留在 InboxDock 暂存区，不写入 Vault。</summary>
    StagingOnly,
}

/// <summary>附件在 Vault 中的存放策略。</summary>
public enum AttachmentPolicyKind
{
    /// <summary>沿用可安全读取到的 Obsidian 附件设置。</summary>
    FollowObsidian,

    /// <summary>固定 Vault 相对目录。</summary>
    FixedDirectory,

    /// <summary>带日期变量的 Vault 相对目录。</summary>
    DatedDirectory,

    /// <summary>放在目标笔记旁的指定子目录。</summary>
    BesideNote,

    /// <summary>附件暂不进入 Vault。</summary>
    StagingOnly,
}

/// <summary>内容写入固定文件时的插入位置。</summary>
public enum TargetInsertionMode
{
    /// <summary>追加到文件末尾。</summary>
    Append,

    /// <summary>插入到指定标题下方。</summary>
    UnderHeading,
}

/// <summary>写入成功后对暂存材料的处理方式。</summary>
public enum PostCaptureBehavior
{
    /// <summary>保留暂存材料，由用户手动清理。</summary>
    KeepStaged,

    /// <summary>写入成功后移除暂存材料。</summary>
    RemoveStaged,
}
