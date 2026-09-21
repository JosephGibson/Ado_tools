namespace AdoToolkit.Core.Reporting.TestFailures;

public sealed class TestFailureExportOptions
{
    public string? Culture { get; init; }
    public string? ConfiguredCulture { get; init; }
    public required CultureInfo SessionCulture { get; init; }
    // A resolved FileSystem path: an .html file or a directory. Null means Downloads.
    public string? Path { get; init; }
    // Path names a directory that may not exist yet; it is created when the export runs, never in Prepare.
    public bool CreateDirectory { get; init; }
    public bool NoClobber { get; init; }
    public bool SkipAttachments { get; init; }
    public bool AllRunAttachments { get; init; }
    // Attachments are listed and downloaded only for runs started this many days before GeneratedAt.
    public int AttachmentWindowDays { get; init; } = TestFailureReportOptions.DefaultAttachmentWindowDays;
    public bool IncludeFlaky { get; init; }
    public bool Open { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string ToolkitVersion { get; init; }
}
