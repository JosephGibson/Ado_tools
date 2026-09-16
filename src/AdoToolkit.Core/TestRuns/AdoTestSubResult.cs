namespace AdoToolkit.Core.TestRuns;

// A non-attempt result group (DataDriven, OrderedTest, Generic) inside one attempt (§15.10).
public sealed class AdoTestSubResult
{
    public required int Id { get; init; }
    public int? SequenceId { get; init; }
    public string? DisplayName { get; init; }
    public string? ResultGroupType { get; init; }
    public required string Outcome { get; init; }
    public AdoTestOutcomeClass OutcomeClass { get; init; }
    public string? ErrorMessage { get; init; }
    public string? StackTrace { get; init; }
    public string? Comment { get; init; }
    public string? ComputerName { get; init; }
    public DateTimeOffset? StartedDate { get; init; }
    public DateTimeOffset? CompletedDate { get; init; }
    public TimeSpan? Duration { get; init; }
    public IReadOnlyList<AdoTestSubResult> SubResults { get; init; } = Array.Empty<AdoTestSubResult>();
}
