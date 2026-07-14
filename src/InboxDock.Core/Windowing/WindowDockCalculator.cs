namespace InboxDock.Core.Windowing;

public static class WindowDockCalculator
{
    public static DockEdge NearestEdge(WindowRect current, WindowRect workArea)
    {
        var candidates = new[]
        {
            (DockEdge.Left, Math.Abs(current.Left - workArea.Left)),
            (DockEdge.Right, Math.Abs(workArea.Right - current.Right)),
            (DockEdge.Top, Math.Abs(current.Top - workArea.Top)),
            (DockEdge.Bottom, Math.Abs(workArea.Bottom - current.Bottom)),
        };

        return candidates.MinBy(candidate => candidate.Item2).Item1;
    }

    public static WindowRect TargetRect(
        DockEdge edge,
        WindowRect current,
        WindowRect workArea,
        double width,
        double height,
        double margin)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (margin < 0) throw new ArgumentOutOfRangeException(nameof(margin));

        var minimumLeft = workArea.Left + margin;
        var maximumLeft = Math.Max(minimumLeft, workArea.Right - width - margin);
        var minimumTop = workArea.Top + margin;
        var maximumTop = Math.Max(minimumTop, workArea.Bottom - height - margin);
        var clampedLeft = Math.Clamp(current.Left, minimumLeft, maximumLeft);
        var clampedTop = Math.Clamp(current.Top, minimumTop, maximumTop);

        return edge switch
        {
            DockEdge.Left => new WindowRect(minimumLeft, clampedTop, width, height),
            DockEdge.Right => new WindowRect(maximumLeft, clampedTop, width, height),
            DockEdge.Top => new WindowRect(clampedLeft, minimumTop, width, height),
            DockEdge.Bottom => new WindowRect(clampedLeft, maximumTop, width, height),
            _ => throw new ArgumentOutOfRangeException(nameof(edge)),
        };
    }
}
