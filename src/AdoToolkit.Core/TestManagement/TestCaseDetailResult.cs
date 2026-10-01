namespace AdoToolkit.Core.TestManagement;

public sealed class TestCaseDetailResult
{
    // One entry per requested Test Case ID.
    public required IReadOnlyDictionary<int, AdoTestCaseDetail> Details { get; init; }
    // Every lookup problem once, for the caller's warnings; each detail repeats the ones that concern it.
    public IReadOnlyList<AdoDiagnostic> Diagnostics { get; init; } = Array.Empty<AdoDiagnostic>();
}
