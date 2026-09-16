namespace AdoToolkit.Core.Builds;

public sealed class AdoTimelineRecord
{
    public Guid Id { get; init; }
    public Guid? ParentId { get; init; }
    public required string Type { get; init; }
    public required string Name { get; init; }
    public int Order { get; init; }
    public string? State { get; init; }
    public string? Result { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? FinishTime { get; init; }
    public int Attempt { get; init; }
    public string? Identifier { get; init; }
    public IReadOnlyList<AdoTimelineAttempt> PreviousAttempts { get; init; } = Array.Empty<AdoTimelineAttempt>();
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public IReadOnlyList<AdoTimelineIssue> Issues { get; init; } = Array.Empty<AdoTimelineIssue>();
    public int? LogId { get; init; }
    public int BuildId { get; init; }
    public required Uri CollectionUri { get; init; }
}
