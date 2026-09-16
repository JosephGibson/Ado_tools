namespace AdoToolkit.Core.TestRuns;

// Files maps each toolkit file name in the generation folder to the number of bytes written.
internal sealed record AttachmentDownloadResult(
    IReadOnlyList<AdoTestFailure> Failures,
    IReadOnlyList<AdoDiagnostic> Diagnostics,
    IReadOnlyDictionary<string, long> Files);
