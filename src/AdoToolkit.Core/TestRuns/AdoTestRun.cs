namespace AdoToolkit.Core.TestRuns;

public sealed class AdoTestRun
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required int BuildId { get; init; }
    public required string State { get; init; }
    public bool IsAutomated { get; init; }
    public DateTimeOffset? StartedDate { get; init; }
    public DateTimeOffset? CompletedDate { get; init; }
    // Job attempt, when exposed in pipelineReference.jobReference [Verify V-19].
    public int? PipelineAttempt { get; init; }
    internal int? StageAttempt { get; init; }
    internal int? PhaseAttempt { get; init; }
    public int? TotalTests { get; init; }
    // Server run aggregates, kept in the server's own terms. UnanalyzedTests counts results that
    // did not pass and are not yet analyzed; it is not a result outcome.
    public int? PassedTests { get; init; }
    public int? NotApplicableTests { get; init; }
    public int? UnanalyzedTests { get; init; }
    public int? IncompleteTests { get; init; }
    // Result outcomes from runStatistics only; empty when the server sends no statistics.
    public IReadOnlyDictionary<string, int> OutcomeCounts { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);
    public Uri? WebUrl { get; init; }
    public required string TeamProject { get; init; }
    public required Uri CollectionUri { get; init; }
}
