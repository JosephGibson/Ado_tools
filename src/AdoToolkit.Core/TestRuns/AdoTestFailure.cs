using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.TestRuns;

public sealed class AdoTestFailure
{
    public required int Ordinal { get; init; }
    public AdoTestFailureClassification Classification { get; init; }
    public string? TestName { get; init; }
    // Last name segment, display only.
    public required string ShortName { get; init; }
    public string? Storage { get; init; }
    public string? Title { get; init; }
    public IReadOnlyList<AdoTestAttempt> Attempts { get; init; } = Array.Empty<AdoTestAttempt>();
    public AdoTestCaseLink? TestCase { get; init; }
    // Bugs of its test results and of its Test Case, ordered by ID.
    public IReadOnlyList<AdoTestBug> Bugs { get; init; } = Array.Empty<AdoTestBug>();
    public bool HasOpenBug => Bugs.Any(static bug => bug.IsOpen == true);
    // One cell per history entry, oldest first, current last.
    public IReadOnlyList<AdoTestHistoryEntry> History { get; init; } = Array.Empty<AdoTestHistoryEntry>();
    public AdoIdentityRef? Owner { get; init; }
    public int? Priority { get; init; }
    public required Uri CollectionUri { get; init; }

    internal AdoTestFailure WithAttempts(IReadOnlyList<AdoTestAttempt> attempts) => new()
    {
        Ordinal = Ordinal, Classification = Classification, TestName = TestName, ShortName = ShortName,
        Storage = Storage, Title = Title, Attempts = attempts, TestCase = TestCase, Bugs = Bugs, History = History,
        Owner = Owner, Priority = Priority, CollectionUri = CollectionUri,
    };
}
