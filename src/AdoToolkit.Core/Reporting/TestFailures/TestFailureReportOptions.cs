namespace AdoToolkit.Core.Reporting.TestFailures;

public sealed class TestFailureReportOptions
{
    public const int DefaultAttachmentWindowDays = 7;
    public string? Culture { get; init; }
    public string? ConfiguredCulture { get; init; }
    public required CultureInfo SessionCulture { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required string ToolkitVersion { get; init; }
    // Runs that started before GeneratedAt minus this many days keep their attempts but lose their
    // attachments; a run without a start date counts as outside the window.
    public int AttachmentWindowDays { get; init; } = DefaultAttachmentWindowDays;
    // Flaky tests are left out unless requested; FlakyCount still counts them.
    public bool IncludeFlaky { get; init; }
}
