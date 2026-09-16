namespace AdoToolkit.Core.TestManagement;

public sealed class TestCaseResult
{
    public IReadOnlyList<AdoTestCase> TestCases { get; init; } = Array.Empty<AdoTestCase>();
    public IReadOnlyList<int> MissingIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<AdoDiagnostic> InputDiagnostics { get; init; } = Array.Empty<AdoDiagnostic>();
}
