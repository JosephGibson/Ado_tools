namespace AdoToolkit.Core.TestRuns;

// One test identity in one build, with its pass-1 records in attempt order.
internal sealed class TestIdentityGroup
{
    internal required TestIdentity Identity { get; init; }
    internal required IReadOnlyList<TestResultRecord> Records { get; init; }
    internal string? Storage => Records[0].AutomatedTestStorage;
    internal string? Name => string.IsNullOrEmpty(Records[0].AutomatedTestName) ? null : Records[0].AutomatedTestName;
    internal string? Title => Records.Select(static record => record.TestCaseTitle).FirstOrDefault(static title => !string.IsNullOrEmpty(title));
    internal int FirstResultId => Records.Min(static record => record.ResultId);
    // A rerun parent is always a detail candidate: its failed attempts live in sub-results (§15.10).
    internal bool IsCandidate => Records.Any(static record => record.IsRerunGroup || record.OutcomeClass == AdoTestOutcomeClass.Failure);
    internal AdoTestOutcomeClass LastOutcomeClass =>
        OutcomeClassifier.Deciding(Records.Select(static record => (record.PipelineKey, record.OutcomeClass)));

    // Provisional classification from pass-1 data only, used for report order and the detail limit.
    // A rerun parent that did not fail counts as passing: its failed attempts live in sub-results.
    internal AdoTestFailureClassification ProvisionalClassification => OutcomeClassifier.Classify(OutcomeClassifier.Deciding(
        Records.Select(static record => (record.PipelineKey,
            record.IsRerunGroup && record.OutcomeClass != AdoTestOutcomeClass.Failure ? AdoTestOutcomeClass.Pass : record.OutcomeClass))));

    internal AdoTestHistoryOutcome Cell => OutcomeClassifier.Cell(LastOutcomeClass, IsCandidate);
}
