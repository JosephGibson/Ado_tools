using AdoToolkit.Core.Connections;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Reporting;

public sealed class TestCaseReportModel
{
    public required CultureInfo Culture { get; init; }
    public required Uri ServerUri { get; init; }
    public required Uri CollectionUri { get; init; }
    public required string Project { get; init; }
    public int Id { get; init; }
    public int Rev { get; init; }
    public required string Title { get; init; }
    public required string WorkItemType { get; init; }
    public required string State { get; init; }
    public int? Priority { get; init; }
    public string? AutomationStatus { get; init; }
    public string? AreaPath { get; init; }
    public string? IterationPath { get; init; }
    public AdoIdentityRef? AssignedTo { get; init; }
    public AdoIdentityRef? ChangedBy { get; init; }
    public DateTimeOffset ChangedDate { get; init; }
    public DateTimeOffset RetrievedAt { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public required string ToolkitVersion { get; init; }
    public required Uri WebUrl { get; init; }
    public AdoTestSuiteRef? Suite { get; init; }
    public required IReadOnlyList<ReportRow> Rows { get; init; }
    public int StepCount => Rows.Count(row => row.Kind is AdoTestStepKind.Action or AdoTestStepKind.Validate);
    public AdoTestCaseStatus Status { get; init; }
    public required IReadOnlyList<AdoDiagnostic> Diagnostics { get; init; }
    public int ErrorCount => Diagnostics.Count(diagnostic => diagnostic.Severity == AdoDiagnosticSeverity.Error);
    public int WarningCount => Diagnostics.Count(diagnostic => diagnostic.Severity == AdoDiagnosticSeverity.Warning);
    public int InformationCount => Diagnostics.Count(diagnostic => diagnostic.Severity == AdoDiagnosticSeverity.Info);
    public required AdoTestParameters Parameters { get; init; }
    public required IReadOnlyList<AdoSharedStepInfo> SharedSteps { get; init; }
    public required IReadOnlyDictionary<string, string> Labels { get; init; }
}
