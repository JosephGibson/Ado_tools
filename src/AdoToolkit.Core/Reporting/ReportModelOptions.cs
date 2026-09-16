namespace AdoToolkit.Core.Reporting;

public sealed class ReportModelOptions
{
    public required CultureInfo Culture { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string ToolkitVersion { get; init; }
}
