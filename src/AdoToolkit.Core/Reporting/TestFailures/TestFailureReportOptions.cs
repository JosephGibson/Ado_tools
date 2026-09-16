namespace AdoToolkit.Core.Reporting.TestFailures;

public sealed class TestFailureReportOptions
{
    public string? Culture { get; init; }
    public string? ConfiguredCulture { get; init; }
    public required CultureInfo SessionCulture { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string ToolkitVersion { get; init; }
}
