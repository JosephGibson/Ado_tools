namespace AdoToolkit.Core.Tests.Http;

// A clock that moves only when a test advances it. A timer fires, on the advancing thread, once the
// clock reaches its due time; a disposed or changed timer never fires for its old due time.
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock gate = new();
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // The system's zone unless a test sets one, so that a local time it formats is known.
    internal TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Local;

    public override TimeZoneInfo LocalTimeZone => Zone;

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate) return now;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ManualTimer timer = new(this, callback, state);
        lock (gate) timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    internal void Advance(TimeSpan time)
    {
        List<ManualTimer> due;
        lock (gate)
        {
            now += time;
            due = [.. timers.Where(timer => timer.Due <= now)];
            foreach (ManualTimer timer in due) timer.Due = null;
        }
        foreach (ManualTimer timer in due) timer.Fire();
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        internal DateTimeOffset? Due { get; set; }

        internal void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner.gate) Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner.now + dueTime;
            return true;
        }

        public void Dispose()
        {
            lock (owner.gate) owner.timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
