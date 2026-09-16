namespace AdoToolkit.Core.Builds;

public sealed class AdoTimelineIssue
{
    public required string Type { get; init; }
    public string? Category { get; init; }
    public required string Message { get; init; }
}
