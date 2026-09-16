using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Reporting;

public sealed class ReportRow
{
    public int Sequence { get; init; }
    public required string Number { get; init; }
    public AdoTestStepKind Kind { get; init; }
    public required string Action { get; init; }
    public required string ExpectedResult { get; init; }
    public string? ActionSource { get; init; }
    public string? ExpectedResultSource { get; init; }
    public AdoSharedStepInfo? SharedStep { get; init; }
    public bool IsExpanded { get; init; }
    public int SourceWorkItemId { get; init; }
    public int SourceRev { get; init; }
    public int? SourceStepId { get; init; }
    public required IReadOnlyList<AdoSharedStepInfo> SharedStepPath { get; init; }
    public int Depth => SharedStepPath.Count;
    public string? DiagnosticCode { get; init; }
    public string? DiagnosticMessage { get; init; }
}
