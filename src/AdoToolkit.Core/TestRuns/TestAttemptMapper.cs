using System.Collections.ObjectModel;
using System.Text.Json;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit.Core.TestRuns;

// Maps §15.11 attempt fields from detailed results. Field names are [Verify V-21]; every field
// other than Number, RunId, ResultId and Outcome is optional and stays null or empty when absent.
internal static class TestAttemptMapper
{
    internal const int MaximumSubResultDepth = 3;

    internal static AdoTestAttempt FromResult(TestResultDto result, int runId, int number,
        AdoTestAttemptSource source, Uri? webUrl, CancellationToken cancellationToken) => new()
        {
            Number = number,
            Source = source,
            RunId = runId,
            ResultId = result.Id,
            Outcome = result.Outcome ?? "",
            OutcomeClass = OutcomeClassifier.Classify(result.Outcome),
            ErrorMessage = result.ErrorMessage,
            StackTrace = result.StackTrace,
            StartedDate = result.StartedDate?.ToUniversalTime(),
            CompletedDate = result.CompletedDate?.ToUniversalTime(),
            Duration = Duration(result.DurationInMs),
            ComputerName = result.ComputerName,
            RunBy = result.RunBy?.ToDomain(),
            FailureType = result.FailureType,
            ResolutionState = result.ResolutionState,
            Comment = result.Comment,
            FailingSinceBuildId = FailingSinceBuild(result),
            AssociatedBugIds = Bugs(result),
            // Rerun children are attempts, so only non-attempt groups nest here (§15.10).
            SubResults = SubResults(result.SubResults, 1, cancellationToken),
            Iterations = Iterations(result, cancellationToken),
            CustomFields = CustomFields(result.CustomFields),
            AdditionalFields = AdditionalFields(result),
            WebUrl = webUrl,
        };

    // A rerun sub-result attempt carries its own outcome and text. Result-level metadata that
    // Server 2020 records on the parent only is inherited [Verify V-21, V-22].
    internal static AdoTestAttempt FromSubResult(TestSubResultDto sub, TestResultDto parent, int runId, int number,
        Uri? webUrl, CancellationToken cancellationToken) => new()
        {
            Number = number,
            Source = AdoTestAttemptSource.Rerun,
            RunId = runId,
            ResultId = parent.Id,
            SubResultId = sub.Id,
            Outcome = sub.Outcome ?? "",
            OutcomeClass = OutcomeClassifier.Classify(sub.Outcome),
            ErrorMessage = sub.ErrorMessage,
            StackTrace = sub.StackTrace,
            StartedDate = sub.StartedDate?.ToUniversalTime(),
            CompletedDate = sub.CompletedDate?.ToUniversalTime(),
            Duration = Duration(sub.DurationInMs),
            ComputerName = sub.ComputerName ?? parent.ComputerName,
            RunBy = parent.RunBy?.ToDomain(),
            FailureType = parent.FailureType,
            ResolutionState = parent.ResolutionState,
            Comment = sub.Comment,
            FailingSinceBuildId = FailingSinceBuild(parent),
            AssociatedBugIds = Bugs(parent),
            SubResults = SubResults(sub.SubResults, 2, cancellationToken),
            CustomFields = CustomFields(sub.CustomFields, parent.CustomFields),
            AdditionalFields = AdditionalFields(parent),
            WebUrl = webUrl,
        };

    internal static IReadOnlyList<TestSubResultDto> RerunAttempts(TestResultDto result) =>
        (result.SubResults ?? [])
            .Where(static sub => sub is not null && sub.Id >= 1)
            .OrderBy(static sub => sub.SequenceId ?? 0)
            .ThenBy(static sub => sub.StartedDate ?? DateTimeOffset.MinValue)
            .ThenBy(static sub => sub.Id)
            .ToArray();

    private static TimeSpan? Duration(double? milliseconds) =>
        milliseconds is double value && value >= 0 && double.IsFinite(value) && value <= TimeSpan.MaxValue.TotalMilliseconds
            ? TimeSpan.FromMilliseconds(value) : null;

    private static int? FailingSinceBuild(TestResultDto result) =>
        result.FailingSince?.Build is { Id: > 0 } build ? build.Id : null;

    private static ReadOnlyCollection<int> Bugs(TestResultDto result) => Array.AsReadOnly((result.AssociatedBugs ?? [])
        .Where(static bug => bug is not null && bug.Id > 0).Select(static bug => bug.Id).Distinct().ToArray());

    private static IReadOnlyList<AdoTestSubResult> SubResults(List<TestSubResultDto>? values, int depth,
        CancellationToken cancellationToken)
    {
        if (values is null || depth > MaximumSubResultDepth) return Array.Empty<AdoTestSubResult>();
        List<AdoTestSubResult> result = [];
        foreach (TestSubResultDto value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value is null || value.Id < 1) continue;
            result.Add(new AdoTestSubResult
            {
                Id = value.Id,
                SequenceId = value.SequenceId,
                DisplayName = value.DisplayName,
                ResultGroupType = value.ResultGroupType,
                Outcome = value.Outcome ?? "",
                OutcomeClass = OutcomeClassifier.Classify(value.Outcome),
                ErrorMessage = value.ErrorMessage,
                StackTrace = value.StackTrace,
                Comment = value.Comment,
                ComputerName = value.ComputerName,
                StartedDate = value.StartedDate?.ToUniversalTime(),
                CompletedDate = value.CompletedDate?.ToUniversalTime(),
                Duration = Duration(value.DurationInMs),
                SubResults = SubResults(value.SubResults, depth + 1, cancellationToken),
            });
        }
        return result.AsReadOnly();
    }

    private static ReadOnlyCollection<AdoTestIteration> Iterations(TestResultDto result, CancellationToken cancellationToken)
    {
        List<AdoTestIteration> iterations = [];
        foreach (TestIterationDto value in result.IterationDetails ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value is null) continue;
            iterations.Add(new AdoTestIteration
            {
                Id = value.Id,
                Outcome = value.Outcome,
                ErrorMessage = value.ErrorMessage,
                Parameters = Array.AsReadOnly((value.Parameters ?? [])
                    .Where(static parameter => parameter is not null && !string.IsNullOrEmpty(parameter.ParameterName))
                    .Select(static parameter => new AdoTestIterationParameter { Name = parameter.ParameterName!, Value = parameter.Value })
                    .ToArray()),
                ActionResults = Array.AsReadOnly((value.ActionResults ?? [])
                    .Where(static action => action is not null)
                    .Select(static action => new AdoTestActionResult
                    {
                        ActionPath = action.ActionPath,
                        StepIdentifier = action.StepIdentifier,
                        Outcome = action.Outcome,
                        ErrorMessage = action.ErrorMessage,
                    }).ToArray()),
            });
        }
        return iterations.AsReadOnly();
    }

    private static ReadOnlyDictionary<string, object?> CustomFields(List<CustomFieldDto>? values, List<CustomFieldDto>? fallback = null)
    {
        Dictionary<string, object?> fields = new(StringComparer.Ordinal);
        // An attempt's own values take precedence; missing fields inherit result metadata.
        foreach (CustomFieldDto field in (values ?? []).Concat(fallback ?? []))
            if (field is not null && !string.IsNullOrEmpty(field.FieldName) && field.Value.ValueKind != JsonValueKind.Undefined)
                fields.TryAdd(field.FieldName, FieldValueMapper.MapValue(field.Value));
        return new ReadOnlyDictionary<string, object?>(fields);
    }

    // Scalars only: arrays and objects of unknown fields are not projected (§15.11).
    private static ReadOnlyDictionary<string, object?> AdditionalFields(TestResultDto result)
    {
        Dictionary<string, object?> fields = new(StringComparer.Ordinal);
        foreach ((string name, JsonElement value) in result.Extra ?? [])
            if (value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True
                or JsonValueKind.False or JsonValueKind.Null)
                fields.TryAdd(name, FieldValueMapper.MapValue(value));
        return new ReadOnlyDictionary<string, object?>(fields);
    }
}
