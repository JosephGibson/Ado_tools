namespace AdoToolkit.Core.Builds;

public sealed class AdoTimelineAttempt
{
    public int Attempt { get; init; }
    public Guid TimelineId { get; init; }
    public Guid RecordId { get; init; }
}
