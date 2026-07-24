namespace InboxDock.Core.Configuration;

/// <summary>窗口在屏幕上的持久状态。</summary>
public sealed record WindowState
{
    public double? Left { get; init; }

    public double? Top { get; init; }

    public double? Width { get; init; }

    public double? Height { get; init; }

    /// <summary>贴靠边缘，null 表示未贴靠。</summary>
    public string? DockEdge { get; init; }

    public static WindowState Empty { get; } = new();
}
