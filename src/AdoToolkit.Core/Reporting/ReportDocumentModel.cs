using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Reporting;

public sealed class ReportDocumentModel
{
    public const int SchemaVersion = 1;
    public required CultureInfo Culture { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string ToolkitVersion { get; init; }
    public required Uri CollectionUri { get; init; }
    public Uri ServerUri => new(CollectionUri.GetLeftPart(UriPartial.Authority));
    public required string Project { get; init; }
    // TestCase for one case; for several: TestSuite (all from suites), Query (none), or Mixed.
    public string Source { get; init; } = "TestCase";
    public required IReadOnlyDictionary<string, string> Labels { get; init; }
    // Case models may be built on access; enumerate once per rendering pass.
    public required IReadOnlyList<TestCaseReportModel> Cases { get; init; }
    // One entry per case occurrence, in document order.
    public IReadOnlyList<ReportContentsEntry> Contents { get; init; } = Array.Empty<ReportContentsEntry>();
    public bool IsMultiCase => Cases.Count > 1;
    public int CompleteCount => Contents.Count(entry => entry.Status == AdoTestCaseStatus.Complete);
    public int PartialCount => Contents.Count(entry => entry.Status == AdoTestCaseStatus.Partial);
}
