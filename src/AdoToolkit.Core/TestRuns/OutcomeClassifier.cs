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

    // §15.10 classification table applied to the deciding (last) attempt.
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
