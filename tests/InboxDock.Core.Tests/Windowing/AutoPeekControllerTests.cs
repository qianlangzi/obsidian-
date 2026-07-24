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

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(30)]
    public void UpdateIdleDuration_BoundarySeconds_RecomputesCountdown(int seconds)
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new AutoPeekController(TimeSpan.FromSeconds(10), now);

        controller.UpdateIdleDuration(TimeSpan.FromSeconds(seconds), now);

        Assert.False(controller.ShouldPeek(now.AddSeconds(seconds - 1)));
        Assert.True(controller.ShouldPeek(now.AddSeconds(seconds)));
    }

    [Fact]
    public void UpdateIdleDuration_NullDisablesAutoPeekButKeepsManual()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new AutoPeekController(TimeSpan.FromSeconds(10), now);

        controller.UpdateIdleDuration(null, now);

        Assert.True(controller.IsDisabled);
        Assert.False(controller.ShouldPeek(now.AddHours(1)));
    }

    [Fact]
    public void SetPinned_PausesAutoPeek_UnpinRestartsFullCountdown()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new AutoPeekController(TimeSpan.FromSeconds(10), now);

        controller.SetPinned(true, now);
        Assert.True(controller.IsPinned);
        Assert.False(controller.ShouldPeek(now.AddMinutes(1)));

        var unpinnedAt = now.AddMinutes(1);
        controller.SetPinned(false, unpinnedAt);
        Assert.False(controller.ShouldPeek(unpinnedAt.AddSeconds(9)));
        Assert.True(controller.ShouldPeek(unpinnedAt.AddSeconds(10)));
    }

    [Fact]
    public void Resume_AfterPause_RecomputesFullCountdown()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new AutoPeekController(TimeSpan.FromSeconds(10), now);
        controller.Pause();

        var resumedAt = now.AddMinutes(1);
        controller.Resume(resumedAt);

        Assert.False(controller.ShouldPeek(resumedAt.AddSeconds(9)));
        Assert.True(controller.ShouldPeek(resumedAt.AddSeconds(10)));
    }

    [Fact]
    public void SchedulePostSuccessPeek_TriggersAfterShortDelay()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new AutoPeekController(TimeSpan.FromMinutes(5), now);

        controller.SchedulePostSuccessPeek(TimeSpan.FromSeconds(2), now);

        Assert.False(controller.ShouldPeek(now.AddSeconds(1)));
        Assert.True(controller.ShouldPeek(now.AddSeconds(2)));
    }

    [Fact]
    public void SchedulePostSuccessPeek_CancelledByUserInput()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new AutoPeekController(TimeSpan.FromMinutes(5), now);
        controller.SchedulePostSuccessPeek(TimeSpan.FromSeconds(2), now);

        controller.RecordActivity(now.AddSeconds(1));

        Assert.False(controller.ShouldPeek(now.AddSeconds(5)));
    }

    [Fact]
    public void SchedulePostSuccessPeek_NotTriggeredWhenPinned()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new AutoPeekController(TimeSpan.FromMinutes(5), now);
        controller.SetPinned(true, now);

        controller.SchedulePostSuccessPeek(TimeSpan.FromSeconds(2), now);

        Assert.False(controller.ShouldPeek(now.AddSeconds(5)));
    }

    [Fact]
    public void SchedulePostSuccessPeek_NotTriggeredWhenDisabled()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new AutoPeekController(TimeSpan.FromSeconds(10), now);
        controller.UpdateIdleDuration(null, now);

        controller.SchedulePostSuccessPeek(TimeSpan.FromSeconds(2), now);

        Assert.False(controller.ShouldPeek(now.AddSeconds(5)));
    }
}
