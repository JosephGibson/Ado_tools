namespace AdoToolkit.Core.Builds;

public sealed class AdoBuildFailure
{
    public int BuildId { get; init; }
    public required string BuildNumber { get; init; }
    public required AdoBuildDefinitionRef Definition { get; init; }
    public string? Branch { get; init; }
    public required string Path { get; init; }
    public required string RecordType { get; init; }
    public required string RecordName { get; init; }
    public required string Result { get; init; }
    public IReadOnlyList<AdoTimelineIssue> ErrorIssues { get; init; } = Array.Empty<AdoTimelineIssue>();
    public int? LogId { get; init; }
    public int? LogLineCount { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? FinishTime { get; init; }
    public int Attempt { get; init; }
    public required Uri CollectionUri { get; init; }
}
