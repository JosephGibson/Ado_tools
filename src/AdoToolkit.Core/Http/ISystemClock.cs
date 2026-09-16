namespace AdoToolkit.Core.Http;

internal interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
    double NextJitter();
}
