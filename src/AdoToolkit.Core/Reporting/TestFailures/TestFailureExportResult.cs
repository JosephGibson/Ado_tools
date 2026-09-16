namespace AdoToolkit.Core.Reporting.TestFailures;

public sealed class TestFailureExportResult
{
    public required FileInfo Report { get; init; }
    // Set only when a generation folder was created.
    public DirectoryInfo? AttachmentDirectory { get; init; }
    // Attachment diagnostics, in the session culture; the report shows them in its own culture.
    public IReadOnlyList<AdoDiagnostic> Diagnostics { get; init; } = Array.Empty<AdoDiagnostic>();
}
