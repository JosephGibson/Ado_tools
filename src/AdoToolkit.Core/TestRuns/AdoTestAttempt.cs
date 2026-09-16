using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.TestRuns;

// Every field other than Number, RunId, ResultId and Outcome is optional (§15.11).
public sealed class AdoTestAttempt
{
    public required int Number { get; init; }
    public AdoTestAttemptSource Source { get; init; }
    public required int RunId { get; init; }
    public required int ResultId { get; init; }
    public int? SubResultId { get; init; }
    public required string Outcome { get; init; }
    public AdoTestOutcomeClass OutcomeClass { get; init; }
    public string? ErrorMessage { get; init; }
    public string? StackTrace { get; init; }
    public DateTimeOffset? StartedDate { get; init; }
    public DateTimeOffset? CompletedDate { get; init; }
    public TimeSpan? Duration { get; init; }
    public string? ComputerName { get; init; }
    public AdoIdentityRef? RunBy { get; init; }
    public string? FailureType { get; init; }
    public string? ResolutionState { get; init; }
    public string? Comment { get; init; }
    public int? FailingSinceBuildId { get; init; }
    public IReadOnlyList<int> AssociatedBugIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<AdoTestSubResult> SubResults { get; init; } = Array.Empty<AdoTestSubResult>();
    public IReadOnlyList<AdoTestIteration> Iterations { get; init; } = Array.Empty<AdoTestIteration>();
    public IReadOnlyDictionary<string, object?> CustomFields { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, object?> AdditionalFields { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);
    // Attachments of this attempt's result and of its iteration sub-results; SubResultId names the origin.
    public IReadOnlyList<AdoTestAttachment> Attachments { get; init; } = Array.Empty<AdoTestAttachment>();
    public Uri? WebUrl { get; init; }

    // Export works on copies so a piped set keeps its retrieved attachment state.
    internal AdoTestAttempt WithAttachments(IReadOnlyList<AdoTestAttachment> attachments) => new()
    {
        Number = Number, Source = Source, RunId = RunId, ResultId = ResultId, SubResultId = SubResultId,
        Outcome = Outcome, OutcomeClass = OutcomeClass, ErrorMessage = ErrorMessage, StackTrace = StackTrace,
        StartedDate = StartedDate, CompletedDate = CompletedDate, Duration = Duration, ComputerName = ComputerName,
        RunBy = RunBy, FailureType = FailureType, ResolutionState = ResolutionState, Comment = Comment,
        FailingSinceBuildId = FailingSinceBuildId, AssociatedBugIds = AssociatedBugIds, SubResults = SubResults,
        Iterations = Iterations, CustomFields = CustomFields, AdditionalFields = AdditionalFields,
        Attachments = attachments, WebUrl = WebUrl,
    };
}
