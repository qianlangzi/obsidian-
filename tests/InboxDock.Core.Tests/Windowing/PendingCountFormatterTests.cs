using InboxDock.Core.Windowing;

namespace InboxDock.Core.Tests.Windowing;

public sealed class PendingCountFormatterTests
{
    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "1")]
    [InlineData(9, "9")]
    [InlineData(50, "50")]
    [InlineData(99, "99")]
    [InlineData(100, "99+")]
    [InlineData(9999, "99+")]
    public void Format_ReturnsExpectedText(int count, string expected)
    {
        Assert.Equal(expected, PendingCountFormatter.Format(count));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    public void ShouldShow_ReturnsWhetherBadgeIsNeeded(int count, bool expected)
    {
        Assert.Equal(expected, PendingCountFormatter.ShouldShow(count));
    }
}
