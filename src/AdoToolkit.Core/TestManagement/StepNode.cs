namespace AdoToolkit.Core.TestManagement;

public sealed class StepNode
{
    public AdoTestStepKind Kind { get; init; }
    public int? SourceStepId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string ExpectedResult { get; init; } = string.Empty;
    public string? ActionSource { get; init; }
    public string? ExpectedResultSource { get; init; }
    public int? SharedStepId { get; init; }
    public string? DiagnosticCode { get; init; }
}
