namespace AdoToolkit.Core.TestManagement;

internal sealed class ExpansionContext
{
    internal List<AdoTestStep> Rows { get; } = [];
    internal List<AdoDiagnostic> Diagnostics { get; } = [];
    internal HashSet<int> DiagnosedDocuments { get; } = [];
    internal bool Stopped { get; set; }
    internal int Sequence { get; set; }
}
