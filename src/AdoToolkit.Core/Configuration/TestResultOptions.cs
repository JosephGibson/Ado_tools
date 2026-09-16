namespace AdoToolkit.Core.Configuration;

public sealed class TestResultOptions
{
    public int HistoryCount { get; init; } = 10;
    public string HistoryScope { get; init; } = "SameBranch";
    public int MaximumReportedFailures { get; init; } = 1000;
    public int MaximumHistoryRequests { get; init; } = 400;
    public long MaximumAttachmentBytes { get; init; } = 52428800;
    public long MaximumTotalAttachmentBytes { get; init; } = 524288000;
    public long MaximumInlineJsonBytes { get; init; } = 262144;
}
