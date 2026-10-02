namespace AdoToolkit.Core.Configuration;

public sealed class TestResultOptions
{
    public const int DefaultMaximumConcurrentRequests = 6;
    public const int MaximumConcurrentRequestsLimit = 16;

    public int HistoryCount { get; init; } = 10;
    public string HistoryScope { get; init; } = "SameBranch";
    public int MaximumReportedFailures { get; init; } = 1000;
    public int MaximumHistoryRequests { get; init; } = 400;
    public long MaximumAttachmentBytes { get; init; } = 52428800;
    public long MaximumTotalAttachmentBytes { get; init; } = 524288000;
    // Largest JSON or text attachment inlined, and so searchable, in a report.
    public long MaximumInlineJsonBytes { get; init; } = 262144;
    // Inlined attachment bytes per report; later attachments are linked only.
    public long MaximumInlineTotalBytes { get; init; } = 8388608;
    // Requests that failed-test retrieval, and files that the export, has in flight at once: 1 to 16.
    public int MaximumConcurrentRequests { get; init; } = DefaultMaximumConcurrentRequests;
}
