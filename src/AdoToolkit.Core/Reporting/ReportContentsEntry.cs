using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Reporting;

// Table-of-contents data for one case occurrence, built without the full case model.
public sealed class ReportContentsEntry
{
    public int Id { get; init; }
    public required string Title { get; init; }
    // tc-<id> for the first occurrence, tc-<id>-<k> for the k-th (§12.5).
    public required string Anchor { get; init; }
    public AdoTestCaseStatus Status { get; init; }
    public AdoTestSuiteRef? Suite { get; init; }
    public int RowCount { get; init; }
    public int StepCount { get; init; }
}
