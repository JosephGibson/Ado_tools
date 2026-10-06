namespace AdoToolkit.Core.TestRuns;

// What one history build says of a test's errors (D-8): the distinct starts of the messages of its
// failed results, at most MaximumStarts in record order, each with the pipeline key of its first
// result, which tells in which stage or job it failed, and whether starts were dropped: past
// MaximumStarts, or past the MaximumCharacters that one build keeps of distinct starts.
internal sealed class HistoryErrors
{
    internal const int MaximumStarts = 5;
    // An invocation caches every history build it reads, so an outage with thousands of distinct
    // messages must stay small: 4 MB of text per build.
    internal const int MaximumCharacters = 2_000_000;

    internal required IReadOnlyList<string> Starts { get; init; }
    // One per start, in the same order.
    internal required IReadOnlyList<string> Keys { get; init; }
    internal bool Dropped { get; init; }
}
