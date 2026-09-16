namespace AdoToolkit.Core.TestRuns;

// One pass-1 result listing entry, with its run's ordering key resolved (§15.9 step 3).
internal sealed class TestResultRecord
{
    internal required int RunId { get; init; }
    internal required int ResultId { get; init; }
    internal required int RunOrder { get; init; }
    internal string? Outcome { get; init; }
    internal string? AutomatedTestName { get; init; }
    internal string? AutomatedTestStorage { get; init; }
    internal string? TestCaseTitle { get; init; }
    internal string? ResultGroupType { get; init; }
    internal DateTimeOffset? StartedDate { get; init; }
    internal AdoTestOutcomeClass OutcomeClass => OutcomeClassifier.Classify(Outcome);
    internal bool IsRerunGroup => OutcomeClassifier.IsRerunGroup(ResultGroupType);
}
