namespace InboxDock.Core.SystemIntegration;

/// <summary>
/// 开机自启动状态。由纯逻辑计算，不依赖 Windows API。
/// </summary>
public enum LaunchAtSignInStatus
{
    /// <summary>未启用且注册表无残留。</summary>
    Disabled,

    /// <summary>已启用且注册路径与当前可执行路径一致。</summary>
    Enabled,

    /// <summary>设置中开启但注册表缺失，或便携版移动后路径不一致，需要修复。</summary>
    NeedsRepair,
}
