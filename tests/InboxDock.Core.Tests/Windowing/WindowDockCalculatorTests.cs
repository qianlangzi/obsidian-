using InboxDock.Core.Windowing;

namespace InboxDock.Core.Tests.Windowing;

public sealed class WindowDockCalculatorTests
{
    [Theory]
    [InlineData(10, 300, DockEdge.Left)]
    [InlineData(940, 300, DockEdge.Right)]
    [InlineData(400, 10, DockEdge.Top)]
    [InlineData(400, 740, DockEdge.Bottom)]
    public void NearestEdge_SelectsClosestBoundary(double left, double top, DockEdge expected)
    {
        var current = new WindowRect(left, top, 52, 52);
        var workArea = new WindowRect(0, 0, 1000, 800);

        Assert.Equal(expected, WindowDockCalculator.NearestEdge(current, workArea));
    }

    [Fact]
    public void NearestEdge_OnTie_UsesStableEnumOrder()
    {
        var current = new WindowRect(374, 374, 52, 52);

        Assert.Equal(
            DockEdge.Left,
            WindowDockCalculator.NearestEdge(current, new WindowRect(0, 0, 800, 800)));
    }

    [Theory]
    [InlineData(DockEdge.Left, 8, 200)]
    [InlineData(DockEdge.Right, 688, 200)]
    [InlineData(DockEdge.Top, 400, 8)]
    [InlineData(DockEdge.Bottom, 400, 384)]
    public void TargetRect_GrowsInwardFromEveryEdge(DockEdge edge, double expectedLeft, double expectedTop)
    {
        var collapsed = edge switch
        {
            DockEdge.Left => new WindowRect(8, 200, 52, 52),
            DockEdge.Right => new WindowRect(940, 200, 52, 52),
            DockEdge.Top => new WindowRect(400, 8, 52, 52),
            _ => new WindowRect(400, 740, 52, 52),
        };

        var result = WindowDockCalculator.TargetRect(
            edge,
            collapsed,
            new WindowRect(0, 0, 1000, 800),
            304,
            408,
            8);

        Assert.Equal(new WindowRect(expectedLeft, expectedTop, 304, 408), result);
    }

    [Theory]
    [InlineData(DockEdge.Left, 8, 384)]
    [InlineData(DockEdge.Right, 688, 384)]
    [InlineData(DockEdge.Top, 688, 8)]
    [InlineData(DockEdge.Bottom, 688, 384)]
    public void TargetRect_ClampsPerpendicularPositionInsideWorkArea(
        DockEdge edge,
        double expectedLeft,
        double expectedTop)
    {
        var result = WindowDockCalculator.TargetRect(
            edge,
            new WindowRect(980, 780, 52, 52),
            new WindowRect(0, 0, 1000, 800),
            304,
            408,
            8);

        Assert.Equal(new WindowRect(expectedLeft, expectedTop, 304, 408), result);
    }

    [Fact]
    public void WindowRect_ExposesRightAndBottom()
    {
        var rect = new WindowRect(10, 20, 30, 40);

        Assert.Equal(40, rect.Right);
        Assert.Equal(60, rect.Bottom);
    }
}
