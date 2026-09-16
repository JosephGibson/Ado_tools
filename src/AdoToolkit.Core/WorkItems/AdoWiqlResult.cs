namespace AdoToolkit.Core.WorkItems;

public sealed class AdoWiqlResult
{
    public IReadOnlyList<int> Ids { get; init; } = Array.Empty<int>();
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
    // Provenance only: hydration uses current revisions (Q-05).
    public DateTimeOffset AsOf { get; init; }
    public required string TeamProject { get; init; }
    public required Uri CollectionUri { get; init; }
    public int? LimitApplied { get; init; }
}
