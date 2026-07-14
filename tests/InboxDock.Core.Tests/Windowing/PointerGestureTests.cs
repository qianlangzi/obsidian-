using InboxDock.Core.Windowing;

namespace InboxDock.Core.Tests.Windowing;

public sealed class PointerGestureTests
{
    [Theory]
    [InlineData(0, 0, 3, 0, true)]
    [InlineData(0, 0, 3.99, 0, true)]
    [InlineData(0, 0, 4, 0, false)]
    [InlineData(0, 0, 3, 3, false)]
    public void IsClick_UsesFourDipEuclideanThreshold(
        double startX,
        double startY,
        double endX,
        double endY,
        bool expected)
    {
        Assert.Equal(expected, PointerGesture.IsClick(startX, startY, endX, endY, 4));
    }
}
