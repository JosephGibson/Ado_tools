using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Reporting;

public sealed class ReportModelOptions
{
    public required CultureInfo Culture { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string ToolkitVersion { get; init; }
    // Details by Test Case ID, shown by the HTML report only; null when they were not requested.
    public IReadOnlyDictionary<int, AdoTestCaseDetail>? Details { get; init; }
}
