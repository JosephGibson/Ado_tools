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
}
