using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.Tests.Http;

internal sealed class FakeClock : ISystemClock
{
    public DateTimeOffset UtcNow { get; private set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    internal List<TimeSpan> Delays { get; } = [];
    internal double Jitter { get; set; } = 0.5;
    public double NextJitter() => Jitter;
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Delays.Add(delay);
        UtcNow += delay;
        return Task.CompletedTask;
    }
}
