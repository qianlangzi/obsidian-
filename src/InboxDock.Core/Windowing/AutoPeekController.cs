namespace InboxDock.Core.Windowing;

public sealed class AutoPeekController
{
    private readonly TimeSpan idleDuration;
    private DateTimeOffset lastActivity;

    public AutoPeekController(TimeSpan idleDuration, DateTimeOffset startedAt)
    {
        if (idleDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(idleDuration));
        this.idleDuration = idleDuration;
        lastActivity = startedAt;
    }

    public bool IsPaused { get; private set; }

    public bool IsPeeking { get; private set; }

    public void RecordActivity(DateTimeOffset now) => lastActivity = now;

    public void Pause() => IsPaused = true;

    public void Resume(DateTimeOffset now)
    {
        IsPaused = false;
        lastActivity = now;
    }

    public void SetPeeking(bool value, DateTimeOffset now)
    {
        IsPeeking = value;
        lastActivity = now;
    }

    public bool ShouldPeek(DateTimeOffset now) =>
        !IsPaused && !IsPeeking && now - lastActivity >= idleDuration;
}
