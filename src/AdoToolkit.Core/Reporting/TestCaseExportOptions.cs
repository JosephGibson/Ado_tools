using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Reporting;

public sealed class TestCaseExportOptions
{
    public ReportFormat Format { get; init; } = ReportFormat.Html;
    public string? Culture { get; init; }
    public string? ConfiguredCulture { get; init; }
    public required CultureInfo SessionCulture { get; init; }
    public string? Path { get; init; }
    public bool NoClobber { get; init; }
    public bool IncludeSource { get; init; }
    public bool Open { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string ToolkitVersion { get; init; }
    // Reads the details by Test Case ID, for the HTML format only. Called once, and only for a
    // report that is going to be written.
    public Func<IReadOnlyDictionary<int, AdoTestCaseDetail>>? ReadDetails { get; init; }
}
