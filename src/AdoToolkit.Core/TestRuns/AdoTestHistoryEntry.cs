namespace AdoToolkit.Core.TestRuns;

public sealed class AdoTestHistoryEntry
{
    public required int BuildId { get; init; }
    public required string BuildNumber { get; init; }
    public AdoTestHistoryOutcome Outcome { get; init; }
    public bool IsCurrent { get; init; }
    public required Uri WebUrl { get; init; }
}
