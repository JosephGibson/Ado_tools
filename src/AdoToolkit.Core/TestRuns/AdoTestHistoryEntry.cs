namespace AdoToolkit.Core.TestRuns;

public sealed class AdoTestHistoryEntry
{
    public required int BuildId { get; init; }
    public required string BuildNumber { get; init; }
    public AdoTestHistoryOutcome Outcome { get; init; }
    public bool IsCurrent { get; init; }
    public required Uri WebUrl { get; init; }
    // The distinct starts of the error messages that the build's failed results of the test listed,
    // at most five, as the listing sent them: their first 20 lines, at most 8,192 characters. Empty
    // for the current build, for a build where the test did not fail, when the listing carries no
    // message (V-39), and for the tests read after a build's starts reach 2 million characters.
    public IReadOnlyList<string> ErrorMessages { get; init; } = [];
    // More than five distinct starts were listed, so one that is not kept might match.
    internal bool ErrorMessagesDropped { get; init; }
    // The pipeline key of the first result of each start, in the same order: where it failed.
    internal IReadOnlyList<string> ErrorMessageKeys { get; init; } = [];
}
