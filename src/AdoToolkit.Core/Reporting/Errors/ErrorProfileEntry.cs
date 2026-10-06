namespace AdoToolkit.Core.Reporting.Errors;

// One error of one test: how many of its attempts had it, in which groups, and its latest attempt,
// whose message the report shows.
internal sealed class ErrorProfileEntry
{
    internal required ErrorClass Class { get; init; }
    internal required int Count { get; init; }
    // The numbers of those attempts, in order.
    internal IReadOnlyList<int> Attempts { get; init; } = [];
    // The pipeline groups of those attempts, in group order; empty in a build without groups.
    internal required IReadOnlyList<int> Groups { get; init; }
    internal required ErrorOccurrence Latest { get; init; }
}
