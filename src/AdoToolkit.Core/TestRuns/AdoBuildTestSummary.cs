namespace AdoToolkit.Core.TestRuns;

// Counts are test identities by the outcome class of their last attempt; Flaky is a subset of Passed (§15.12).
public sealed class AdoBuildTestSummary
{
    public required int BuildId { get; init; }
    public required string BuildNumber { get; init; }
    public string? SourceBranch { get; init; }
    public DateTimeOffset? FinishTime { get; init; }
    public string? Result { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public int Flaky { get; init; }
    public int Other { get; init; }
    public bool IsAvailable { get; init; }
    public bool IsCurrent { get; init; }
    public required Uri WebUrl { get; init; }
}
