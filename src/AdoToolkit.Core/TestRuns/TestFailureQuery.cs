namespace AdoToolkit.Core.TestRuns;

public sealed class TestFailureQuery
{
    public int HistoryCount { get; init; } = 10;
    public AdoTestHistoryScope HistoryScope { get; init; } = AdoTestHistoryScope.SameBranch;
    public int MaximumReportedFailures { get; init; } = 1000;
    public int MaximumHistoryRequests { get; init; } = 400;
}
