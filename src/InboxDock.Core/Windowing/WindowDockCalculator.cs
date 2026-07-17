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

    public static WindowRect PeekHandleRect(
        DockEdge edge,
        WindowRect current,
        WindowRect workArea,
        double longSide,
        double shortSide)
    {
        if (longSide <= 0) throw new ArgumentOutOfRangeException(nameof(longSide));
        if (shortSide <= 0) throw new ArgumentOutOfRangeException(nameof(shortSide));

        var centerX = current.Left + (current.Width / 2);
        var centerY = current.Top + (current.Height / 2);

        if (edge is DockEdge.Left or DockEdge.Right)
        {
            var top = Math.Clamp(
                centerY - (longSide / 2),
                workArea.Top,
                Math.Max(workArea.Top, workArea.Bottom - longSide));
            var left = edge == DockEdge.Left ? workArea.Left : workArea.Right - shortSide;
            return new WindowRect(left, top, shortSide, longSide);
        }

        var horizontal = Math.Clamp(
            centerX - (longSide / 2),
            workArea.Left,
            Math.Max(workArea.Left, workArea.Right - longSide));
        var vertical = edge == DockEdge.Top ? workArea.Top : workArea.Bottom - shortSide;
        return new WindowRect(horizontal, vertical, longSide, shortSide);
    }
}
