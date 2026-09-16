using AdoToolkit.Core.Builds;

namespace AdoToolkit.Core.TestRuns;

public sealed class AdoBuildTestFailureSet
{
    public required AdoBuild Build { get; init; }
    public IReadOnlyList<AdoTestRun> Runs { get; init; } = Array.Empty<AdoTestRun>();
    public required AdoBuildTestSummary Summary { get; init; }
    // Oldest first, current build last.
    public IReadOnlyList<AdoBuildTestSummary> History { get; init; } = Array.Empty<AdoBuildTestSummary>();
    public IReadOnlyList<AdoTestFailure> Failures { get; init; } = Array.Empty<AdoTestFailure>();
    public int FailedCount { get; init; }
    public int FlakyCount { get; init; }
    public AdoTestFailureStatus Status { get; init; }
    public IReadOnlyList<AdoDiagnostic> Diagnostics { get; init; } = Array.Empty<AdoDiagnostic>();
    public required DateTimeOffset RetrievedAt { get; init; }
    public required Uri CollectionUri { get; init; }
}
