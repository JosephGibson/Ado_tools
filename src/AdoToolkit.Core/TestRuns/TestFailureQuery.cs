using AdoToolkit.Core.Configuration;

namespace AdoToolkit.Core.TestRuns;

public sealed class TestFailureQuery
{
    public int HistoryCount { get; init; } = 10;
    public AdoTestHistoryScope HistoryScope { get; init; } = AdoTestHistoryScope.SameBranch;
    public int MaximumReportedFailures { get; init; } = 1000;
    public int MaximumHistoryRequests { get; init; } = 400;
    // Requests in flight at once, 1 to 16. One sends every request after the previous one, in the
    // order of §15.9, with the history read last.
    public int MaximumConcurrentRequests { get; init; } = TestResultOptions.DefaultMaximumConcurrentRequests;
}
