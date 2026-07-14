namespace InboxDock.Core.Windowing;

public static class PointerGesture
{
    public static bool IsClick(
        double startX,
        double startY,
        double endX,
        double endY,
        double threshold)
    {
        if (threshold < 0) throw new ArgumentOutOfRangeException(nameof(threshold));

        var deltaX = endX - startX;
        var deltaY = endY - startY;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) < threshold;
    }
}
