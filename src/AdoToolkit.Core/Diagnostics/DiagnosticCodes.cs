namespace AdoToolkit.Core.Diagnostics;

public static class DiagnosticCodes
{
    public const string NotAStepContainer = nameof(NotAStepContainer);
    public const string EmptySteps = nameof(EmptySteps);
    public const string MalformedStepsXml = nameof(MalformedStepsXml);
    public const string UnknownStepElement = nameof(UnknownStepElement);
    public const string UnknownStepType = nameof(UnknownStepType);
    public const string UnexpectedComprefChildren = nameof(UnexpectedComprefChildren);
    public const string InvalidSharedStepReference = nameof(InvalidSharedStepReference);
    public const string UnresolvedSharedStep = nameof(UnresolvedSharedStep);
    public const string SharedStepHasNoSteps = nameof(SharedStepHasNoSteps);
    public const string CircularSharedStepReference = nameof(CircularSharedStepReference);
    public const string MaximumDepthExceeded = nameof(MaximumDepthExceeded);
    public const string ExpansionLimitExceeded = nameof(ExpansionLimitExceeded);
    public const string ResolutionLimitExceeded = nameof(ResolutionLimitExceeded);
    public const string NestedEncodingDecoded = nameof(NestedEncodingDecoded);
    public const string MalformedParameterData = nameof(MalformedParameterData);
    public const string UnresolvedSharedParameter = nameof(UnresolvedSharedParameter);
    public const string NoTestRuns = nameof(NoTestRuns);
    public const string TestRunInProgress = nameof(TestRunInProgress);
    public const string FailureLimitExceeded = nameof(FailureLimitExceeded);
    public const string UngroupedTestResult = nameof(UngroupedTestResult);
    public const string InvalidTestCaseReference = nameof(InvalidTestCaseReference);
    public const string UnresolvedTestCase = nameof(UnresolvedTestCase);
    public const string BugLookupFailed = nameof(BugLookupFailed);
    public const string UnresolvedBug = nameof(UnresolvedBug);
    public const string BugMetadataUnavailable = nameof(BugMetadataUnavailable);
    public const string HistoryUnavailable = nameof(HistoryUnavailable);
    public const string HistoryLimitExceeded = nameof(HistoryLimitExceeded);
    public const string TestTextTruncated = nameof(TestTextTruncated);
    public const string AttachmentTooLarge = nameof(AttachmentTooLarge);
    public const string AttachmentBudgetExceeded = nameof(AttachmentBudgetExceeded);
    public const string AttachmentDownloadFailed = nameof(AttachmentDownloadFailed);
    public const string AttachmentContentMismatch = nameof(AttachmentContentMismatch);

    public static AdoDiagnosticSeverity GetSeverity(string code) => code switch
    {
        EmptySteps or NestedEncodingDecoded or NoTestRuns or TestTextTruncated => AdoDiagnosticSeverity.Info,
        UnknownStepElement or UnknownStepType or UnexpectedComprefChildren or SharedStepHasNoSteps
            or MalformedParameterData or UnresolvedSharedParameter or TestRunInProgress or UngroupedTestResult
            or InvalidTestCaseReference or UnresolvedTestCase or BugLookupFailed or UnresolvedBug
            or BugMetadataUnavailable or HistoryUnavailable
            or HistoryLimitExceeded or AttachmentTooLarge or AttachmentBudgetExceeded
            or AttachmentDownloadFailed or AttachmentContentMismatch => AdoDiagnosticSeverity.Warning,
        NotAStepContainer or MalformedStepsXml or InvalidSharedStepReference or UnresolvedSharedStep
            or CircularSharedStepReference or MaximumDepthExceeded or ExpansionLimitExceeded
            or ResolutionLimitExceeded or FailureLimitExceeded => AdoDiagnosticSeverity.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };
}
