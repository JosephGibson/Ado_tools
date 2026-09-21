namespace AdoToolkit.Core.Reporting.TestFailures;

// Downloaded attachment files for one render. Links use FolderName, the final relative folder
// (§13.4 step 1); inline previews and validation read the files from SourceFolder.
public sealed class TestFailureLocalAttachments
{
    public required string FolderName { get; init; }
    public required string SourceFolder { get; init; }
    // Toolkit file name → bytes written.
    public required IReadOnlyDictionary<string, long> Files { get; init; }
    // Per-file and per-report limits on JSON and text shown, and so searchable, in the report.
    public required long MaximumInlineJsonBytes { get; init; }
    public long MaximumInlineTotalBytes { get; init; } = 8388608;
}
