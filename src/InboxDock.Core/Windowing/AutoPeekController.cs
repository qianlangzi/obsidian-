namespace InboxDock.Core.Windowing;

/// <summary>
/// 控制贴边自动收回的倒计时。支持可配置延时、永不收回、图钉暂停
/// 以及写入成功后的独立短延时收回。
/// </summary>
public sealed class AutoPeekController
{
    private TimeSpan idleDuration;
    private DateTimeOffset lastActivity;
    private bool autoPeekDisabled;
    private bool pinned;
    private DateTimeOffset? postSuccessDeadline;

    public AutoPeekController(TimeSpan idleDuration, DateTimeOffset startedAt)
    {
        if (idleDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(idleDuration));
        this.idleDuration = idleDuration;
        lastActivity = startedAt;
    }

    public bool IsPaused { get; private set; }

    public bool IsPeeking { get; private set; }

    /// <summary>图钉开启时为 true，暂停自动收边。</summary>
    public bool IsPinned => pinned;

    /// <summary>当前是否完全禁用自动收边（"永不"设置）。</summary>
    public bool IsDisabled => autoPeekDisabled;

    public void RecordActivity(DateTimeOffset now)
    {
        lastActivity = now;
        postSuccessDeadline = null;
    }

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
        postSuccessDeadline = null;
    }

    /// <summary>
    /// 更新自动收回延时。null 表示永不自动收回，保留手动收边。
    /// 变更后重新开始完整倒计时。
    /// </summary>
    public void UpdateIdleDuration(TimeSpan? duration, DateTimeOffset now)
    {
        if (duration is { } d && d <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        idleDuration = duration ?? TimeSpan.Zero;
        autoPeekDisabled = duration is null;
        lastActivity = now;
        postSuccessDeadline = null;
    }

    /// <summary>图钉开启时暂停自动收边，关闭后重新开始完整倒计时。</summary>
    public void SetPinned(bool value, DateTimeOffset now)
    {
        pinned = value;
        if (!value)
        {
            lastActivity = now;
        }
    }

    /// <summary>
    /// 写入成功后安排一次独立的短延时收回。延时内若有任何用户输入会被取消。
    /// 失败、撤销悬停和批量处理中应不调用此方法。
    /// </summary>
    public void SchedulePostSuccessPeek(TimeSpan delay, DateTimeOffset now)
    {
        if (delay <= TimeSpan.Zero) return;
        postSuccessDeadline = now + delay;
    }

    public bool ShouldPeek(DateTimeOffset now)
    {
        if (IsPaused || pinned || autoPeekDisabled || IsPeeking) return false;

        if (postSuccessDeadline is { } deadline)
        {
            if (now >= deadline) return true;
            // 短延时期内仍然尊重常规空闲计时，取两者中较早到期者。
            if (idleDuration > TimeSpan.Zero && now - lastActivity >= idleDuration) return true;
            return false;
        }

        return idleDuration > TimeSpan.Zero && now - lastActivity >= idleDuration;
    }
}
