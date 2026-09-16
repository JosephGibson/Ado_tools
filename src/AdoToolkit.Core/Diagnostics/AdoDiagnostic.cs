namespace AdoToolkit.Core.Diagnostics;

public sealed class AdoDiagnostic
{
    public required string Code { get; init; }
    public AdoDiagnosticSeverity Severity { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    public required string Message { get; init; }
    public int? WorkItemId { get; init; }
    public string? StepNumber { get; init; }
    public IReadOnlyList<int> ReferenceChain { get; init; } = Array.Empty<int>();
}
