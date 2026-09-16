namespace AdoToolkit.Core.TestRuns;

// A build reduced to the window metadata history needs.
internal sealed class HistoryBuild
{
    internal required int Id { get; init; }
    internal required string BuildNumber { get; init; }
    internal string? SourceBranch { get; init; }
    internal DateTimeOffset? FinishTime { get; init; }
    internal string? Result { get; init; }
}

// One history build's derived counts and per-identity cells, from pass-1 data only (§15.12).
internal sealed class HistoryBuildData
{
    internal bool IsAvailable { get; init; }
    internal int Passed { get; init; }
    internal int Failed { get; init; }
    internal int Flaky { get; init; }
    internal int Other { get; init; }
    internal IReadOnlyDictionary<TestIdentity, AdoTestHistoryOutcome> Cells { get; init; }
        = new Dictionary<TestIdentity, AdoTestHistoryOutcome>();

    internal static HistoryBuildData Unavailable { get; } = new() { IsAvailable = false };

    // Bars are tallied from the same cells they display, so the two always agree.
    internal static HistoryBuildData FromGroups(IReadOnlyList<TestIdentityGroup> groups, CancellationToken cancellationToken)
    {
        Dictionary<TestIdentity, AdoTestHistoryOutcome> cells = [];
        int passed = 0, failed = 0, flaky = 0, other = 0;
        foreach (TestIdentityGroup group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AdoTestHistoryOutcome cell = group.Cell;
            cells[group.Identity] = cell;
            switch (cell)
            {
                case AdoTestHistoryOutcome.Flaky: passed++; flaky++; break;
                case AdoTestHistoryOutcome.Passed: passed++; break;
                case AdoTestHistoryOutcome.Failed: failed++; break;
                default: other++; break;
            }
        }
        return new HistoryBuildData
        {
            IsAvailable = true,
            Passed = passed,
            Failed = failed,
            Flaky = flaky,
            Other = other,
            Cells = cells,
        };
    }
}
