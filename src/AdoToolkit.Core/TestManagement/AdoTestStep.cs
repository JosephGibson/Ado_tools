namespace AdoToolkit.Core.TestManagement;

public sealed class AdoTestStep
{
    public int Sequence { get; init; }
    public string Number { get; init; } = string.Empty;
    public AdoTestStepKind Kind { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ExpectedResult { get; init; } = string.Empty;
    public string? ActionSource { get; init; }
    public string? ExpectedResultSource { get; init; }
    public AdoSharedStepInfo? SharedStep { get; init; }
    public bool IsExpanded { get; init; }
    public int SourceWorkItemId { get; init; }
    public int SourceRev { get; init; }
    public int? SourceStepId { get; init; }
    public IReadOnlyList<AdoSharedStepInfo> SharedStepPath { get; init; } = Array.Empty<AdoSharedStepInfo>();
    public int Depth => SharedStepPath.Count;
    public string? DiagnosticCode { get; init; }
}
