using AdoToolkit.Core.Builds;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

public sealed class TestFailureReportModel
{
    public required AdoBuild Build { get; init; }
    public required CultureInfo Culture { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string ToolkitVersion { get; init; }
    public required IReadOnlyList<AdoTestFailure> Failures { get; init; }
    public required IReadOnlyList<AdoBuildTestSummary> History { get; init; }
    public required IReadOnlyList<AdoTestRun> Runs { get; init; }
    public required IReadOnlyList<AdoDiagnostic> Diagnostics { get; init; }
    public required IReadOnlyDictionary<string, string> Labels { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public int FailedCount { get; init; }
    public int FlakyCount { get; init; }
    public AdoTestFailureStatus Status { get; init; }
    public required Uri BuildUrl { get; init; }
    public required Uri ResultsUrl { get; init; }
    public required Uri DefinitionUrl { get; init; }
    public Uri? CommitUrl { get; init; }
    // Null when nothing was downloaded; then the report contains no local links.
    public TestFailureLocalAttachments? LocalAttachments { get; init; }
    // Runs whose attachments are listed: those started at or after AttachmentWindowStart.
    public IReadOnlySet<int> AttachmentRunIds { get; init; } = new HashSet<int>();
    public DateTimeOffset AttachmentWindowStart { get; init; }
    // Attachments of reported attempts in runs outside the window, left out of Failures.
    public int OmittedAttachmentCount { get; init; }
    // True when flaky tests were left out of Failures; FlakyCount still counts them.
    public bool FlakyExcluded { get; init; }
    internal PipelineGrouping Grouping { get; init; } = PipelineGrouping.None;
}
