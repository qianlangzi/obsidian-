using InboxDock.Core.Windowing;

namespace InboxDock.Core.Tests.Windowing;

public sealed class AutoPeekControllerTests
{
    [Fact]
    public void ShouldPeek_AfterTenIdleSeconds_ReturnsTrue()
    {
        var now = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        var controller = new AutoPeekController(TimeSpan.FromSeconds(10), now);

        Assert.False(controller.ShouldPeek(now.AddSeconds(9)));
        Assert.True(controller.ShouldPeek(now.AddSeconds(10)));
    }

    [Fact]
    public void RecordActivity_RestartsIdleWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new AutoPeekController(TimeSpan.FromSeconds(10), now);

        controller.RecordActivity(now.AddSeconds(8));

        Assert.False(controller.ShouldPeek(now.AddSeconds(17)));
        Assert.True(controller.ShouldPeek(now.AddSeconds(18)));
    }

    [Fact]
    public void Resume_StartsAFreshIdleWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new AutoPeekController(TimeSpan.FromSeconds(10), now);
        controller.Pause();

        Assert.False(controller.ShouldPeek(now.AddMinutes(1)));

        controller.Resume(now.AddMinutes(1));

        Assert.False(controller.ShouldPeek(now.AddMinutes(1).AddSeconds(9)));
        Assert.True(controller.ShouldPeek(now.AddMinutes(1).AddSeconds(10)));
    }

    [Fact]
    public void Peeking_DisablesIdleRequestUntilExpanded()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new AutoPeekController(TimeSpan.FromSeconds(10), now);

        controller.SetPeeking(true, now.AddSeconds(10));
        Assert.False(controller.ShouldPeek(now.AddMinutes(1)));

        controller.SetPeeking(false, now.AddMinutes(1));
        Assert.False(controller.ShouldPeek(now.AddMinutes(1).AddSeconds(9)));
        Assert.True(controller.ShouldPeek(now.AddMinutes(1).AddSeconds(10)));
    }
}
