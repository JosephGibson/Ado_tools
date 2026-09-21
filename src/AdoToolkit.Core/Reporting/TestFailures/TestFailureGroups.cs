using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

// A failure's attempts in render order: one list when the build is not grouped, otherwise one
// list per pipeline group in group order, each in attempt order. The validator uses the same order.
internal static class TestFailureGroups
{
    internal static IReadOnlyList<(int? Group, IReadOnlyList<AdoTestAttempt> Attempts)> Of(AdoTestFailure failure, PipelineGrouping grouping)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(grouping);
        if (!grouping.IsGrouped) return [(null, failure.Attempts)];
        return [.. failure.Attempts.GroupBy(attempt => grouping.GroupOf(attempt.RunId) ?? int.MaxValue).OrderBy(group => group.Key)
            .Select(group => (group.Key == int.MaxValue ? (int?)null : group.Key, (IReadOnlyList<AdoTestAttempt>)[.. group.OrderBy(a => a.Number)]))];
    }

    // A group ends failed when its last attempt failed, and is flaky when it failed, then passed.
    internal static AdoTestHistoryOutcome Status(IReadOnlyList<AdoTestAttempt> attempts) => attempts.Count == 0 ? AdoTestHistoryOutcome.NotRun
        : attempts[^1].OutcomeClass switch
        {
            AdoTestOutcomeClass.Failure => AdoTestHistoryOutcome.Failed,
            AdoTestOutcomeClass.Pass => attempts.Any(a => a.OutcomeClass == AdoTestOutcomeClass.Failure) ? AdoTestHistoryOutcome.Flaky : AdoTestHistoryOutcome.Passed,
            _ => AdoTestHistoryOutcome.Other,
        };
}
