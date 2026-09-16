using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.TestManagement;

public sealed class AdoTestCase
{
    public int Id { get; init; }
    public int Rev { get; init; }
    public required string Title { get; init; }
    public required string WorkItemType { get; init; }
    public required string TeamProject { get; init; }
    public required string State { get; init; }
    public int? Priority { get; init; }
    public string? AutomationStatus { get; init; }
    public string? AreaPath { get; init; }
    public string? IterationPath { get; init; }
    public AdoIdentityRef? AssignedTo { get; init; }
    public AdoIdentityRef? ChangedBy { get; init; }
    public DateTimeOffset ChangedDate { get; init; }
    public required Uri WebUrl { get; init; }
    public IReadOnlyList<AdoTestStep> Steps { get; init; } = Array.Empty<AdoTestStep>();
    public int StepCount => Steps.Count(step => step.Kind is AdoTestStepKind.Action or AdoTestStepKind.Validate);
    public AdoTestParameters Parameters { get; init; } = new();
    public IReadOnlyList<AdoSharedStepInfo> SharedSteps { get; init; } = Array.Empty<AdoSharedStepInfo>();
    public AdoTestCaseStatus Status => Diagnostics.Any(d => d.Severity == AdoDiagnosticSeverity.Error) ? AdoTestCaseStatus.Partial : AdoTestCaseStatus.Complete;
    public IReadOnlyList<AdoDiagnostic> Diagnostics { get; init; } = Array.Empty<AdoDiagnostic>();
    public AdoTestSuiteRef? Suite { get; init; }
    public DateTimeOffset RetrievedAt { get; init; }
    public required Uri CollectionUri { get; init; }
}
