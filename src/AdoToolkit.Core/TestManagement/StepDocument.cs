namespace AdoToolkit.Core.TestManagement;

public sealed class StepDocument
{
    public int WorkItemId { get; init; }
    public int Rev { get; init; }
    public IReadOnlyList<StepNode> Nodes { get; init; } = Array.Empty<StepNode>();
    public IReadOnlyList<AdoDiagnostic> Diagnostics { get; init; } = Array.Empty<AdoDiagnostic>();
}
