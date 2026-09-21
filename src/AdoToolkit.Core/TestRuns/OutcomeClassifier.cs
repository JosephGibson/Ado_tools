namespace AdoToolkit.Core.TestRuns;

// Outcome classes follow the Q-23 default. Unknown strings are kept verbatim and classified Other.
internal static class OutcomeClassifier
{
    internal const string RerunGroup = "Rerun";
    private static readonly HashSet<string> FailureOutcomes =
        new(["Failed", "Error", "Timeout", "Aborted"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> PassOutcomes = new(["Passed"], StringComparer.OrdinalIgnoreCase);
    // Group types that are never attempts (§15.10).
    private static readonly HashSet<string> NonAttemptGroups =
        new(["DataDriven", "OrderedTest", "Generic"], StringComparer.OrdinalIgnoreCase);

    internal static AdoTestOutcomeClass Classify(string? outcome) => outcome is null ? AdoTestOutcomeClass.Other
        : FailureOutcomes.Contains(outcome) ? AdoTestOutcomeClass.Failure
        : PassOutcomes.Contains(outcome) ? AdoTestOutcomeClass.Pass
        : AdoTestOutcomeClass.Other;

    internal static bool IsRerunGroup(string? resultGroupType) =>
        string.Equals(resultGroupType, RerunGroup, StringComparison.OrdinalIgnoreCase);

    internal static bool IsNonAttemptGroup(string? resultGroupType) =>
        resultGroupType is not null && NonAttemptGroups.Contains(resultGroupType);

    // The deciding outcome takes each pipeline group's last attempt, then the worst of them: a
    // failure in any group decides Failure, then Other; Pass only when every group ends passing.
    // With one group this is the last attempt, as §15.10 describes.
    internal static AdoTestOutcomeClass Deciding(IEnumerable<(string Group, AdoTestOutcomeClass Outcome)> ordered)
    {
        Dictionary<string, AdoTestOutcomeClass> last = new(StringComparer.Ordinal);
        foreach ((string group, AdoTestOutcomeClass outcome) in ordered) last[group] = outcome;
        if (last.ContainsValue(AdoTestOutcomeClass.Failure)) return AdoTestOutcomeClass.Failure;
        return last.Count == 0 || last.ContainsValue(AdoTestOutcomeClass.Other) ? AdoTestOutcomeClass.Other : AdoTestOutcomeClass.Pass;
    }

    // §15.10 classification table applied to the deciding attempt.
    internal static AdoTestFailureClassification Classify(AdoTestOutcomeClass last) =>
        last == AdoTestOutcomeClass.Pass ? AdoTestFailureClassification.Flaky : AdoTestFailureClassification.Failed;

    // Bar bucket and cell agree by construction: Flaky is exactly the reported subset of Passed (§15.12).
    internal static AdoTestHistoryOutcome Cell(AdoTestOutcomeClass last, bool reported) => last switch
    {
        AdoTestOutcomeClass.Pass => reported ? AdoTestHistoryOutcome.Flaky : AdoTestHistoryOutcome.Passed,
        AdoTestOutcomeClass.Failure => AdoTestHistoryOutcome.Failed,
        _ => AdoTestHistoryOutcome.Other,
    };
}
