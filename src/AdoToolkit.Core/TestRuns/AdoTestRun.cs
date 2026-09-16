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
    // Pipeline attempt reference, when the run exposes one [Verify V-19].
    public int? PipelineAttempt { get; init; }
    public int? TotalTests { get; init; }
    public IReadOnlyDictionary<string, int> OutcomeCounts { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);
    public Uri? WebUrl { get; init; }
    public required string TeamProject { get; init; }
    public required Uri CollectionUri { get; init; }
}
