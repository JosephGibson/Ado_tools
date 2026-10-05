using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

// Whether a reported test fails for the first time or again, read from its history cells alone, so
// it costs no request. New: the build before this one ran the test, and it passed. A streak:
// this build and the ones just before it in which the test failed or was flaky, counted as the
// History by test table counts them. Nothing when there is no earlier build, or when the previous
// build is unavailable or did not run the test.
internal sealed record TestFailureSignal(bool IsNew, int Streak, string? Since, AdoTestHistoryOutcome Current)
{
    internal static TestFailureSignal? Of(IReadOnlyList<AdoTestHistoryEntry> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (history.Count < 2 || history[^2].Outcome == AdoTestHistoryOutcome.Unavailable) return null;
        int streak = StreakOf(history);
        if (streak >= 2) return new TestFailureSignal(false, streak, history[^streak].BuildNumber, history[^1].Outcome);
        return streak == 1 && history[^2].Outcome == AdoTestHistoryOutcome.Passed
            ? new TestFailureSignal(true, 1, null, history[^1].Outcome) : null;
    }

    // Builds in a row, ending with this one, in which the test failed or was flaky.
    internal static int StreakOf(IReadOnlyList<AdoTestHistoryEntry> history) =>
        history.Reverse().TakeWhile(static cell => cell.Outcome is AdoTestHistoryOutcome.Failed or AdoTestHistoryOutcome.Flaky).Count();
}
