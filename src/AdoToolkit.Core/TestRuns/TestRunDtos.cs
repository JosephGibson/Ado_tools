using System.Text.Json;
using System.Text.Json.Serialization;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.TestRuns;

// Test-area wire shapes are assumptions until V-19 to V-23 pass; every field is optional.
internal sealed class TestRunPageDto { public List<TestRunDto>? Value { get; init; } }

internal sealed class TestRunDto
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? State { get; init; }
    public bool IsAutomated { get; init; }
    public DateTimeOffset? StartedDate { get; init; }
    public DateTimeOffset? CompletedDate { get; init; }
    public int? TotalTests { get; init; }
    public NamedReferenceDto? Build { get; init; }
    // Read and ignored: links are rebuilt from the connection and numeric IDs (§18 item 6).
    public Uri? Url { get; init; }
    public PipelineReferenceDto? PipelineReference { get; init; }
    public List<RunStatisticDto>? RunStatistics { get; init; }
}

internal sealed class PipelineReferenceDto { public int? PipelineAttempt { get; init; } }

internal sealed class RunStatisticDto
{
    public string? Outcome { get; init; }
    public int Count { get; init; }
}

internal sealed class TestResultPageDto { public List<TestResultDto>? Value { get; init; } }

internal sealed class TestResultDto
{
    public int Id { get; init; }
    public string? Outcome { get; init; }
    public string? AutomatedTestName { get; init; }
    public string? AutomatedTestStorage { get; init; }
    public string? TestCaseTitle { get; init; }
    public string? ResultGroupType { get; init; }
    public DateTimeOffset? StartedDate { get; init; }
    public DateTimeOffset? CompletedDate { get; init; }
    public double? DurationInMs { get; init; }
    public string? ErrorMessage { get; init; }
    public string? StackTrace { get; init; }
    public string? ComputerName { get; init; }
    public string? FailureType { get; init; }
    public string? ResolutionState { get; init; }
    public string? Comment { get; init; }
    public int? Priority { get; init; }
    public IdentityDto? Owner { get; init; }
    public IdentityDto? RunBy { get; init; }
    public NamedReferenceDto? TestRun { get; init; }
    // Read and ignored, so an untrusted response URL never reaches AdditionalFields.
    public Uri? Url { get; init; }
    public NamedReferenceDto? FailingSince { get; init; }
    // testCase.id arrives as a string [Verify V-21]; a non-integer is a diagnostic, never a parse failure.
    public TestCaseReferenceDto? TestCase { get; init; }
    public List<AssociatedBugDto>? AssociatedBugs { get; init; }
    public List<CustomFieldDto>? CustomFields { get; init; }
    public List<TestSubResultDto>? SubResults { get; init; }
    public List<TestIterationDto>? IterationDetails { get; init; }
    // Unknown scalar top-level fields become AdditionalFields (§15.11).
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

internal sealed class TestCaseReferenceDto { public string? Id { get; init; } }

internal sealed class AssociatedBugDto { public int Id { get; init; } }

internal sealed class CustomFieldDto
{
    public string? FieldName { get; init; }
    public JsonElement Value { get; init; }
}

internal sealed class TestSubResultDto
{
    public int Id { get; init; }
    public int? SequenceId { get; init; }
    public string? DisplayName { get; init; }
    public string? ResultGroupType { get; init; }
    public string? Outcome { get; init; }
    public string? ErrorMessage { get; init; }
    public string? StackTrace { get; init; }
    public string? Comment { get; init; }
    public string? ComputerName { get; init; }
    public DateTimeOffset? StartedDate { get; init; }
    public DateTimeOffset? CompletedDate { get; init; }
    public double? DurationInMs { get; init; }
    public List<TestSubResultDto>? SubResults { get; init; }
}

internal sealed class TestIterationDto
{
    public int Id { get; init; }
    public string? Outcome { get; init; }
    public string? ErrorMessage { get; init; }
    public List<TestIterationParameterDto>? Parameters { get; init; }
    public List<TestActionResultDto>? ActionResults { get; init; }
}

internal sealed class TestIterationParameterDto
{
    public string? ParameterName { get; init; }
    public string? Value { get; init; }
}

internal sealed class TestActionResultDto
{
    public string? ActionPath { get; init; }
    public string? StepIdentifier { get; init; }
    public string? Outcome { get; init; }
    public string? ErrorMessage { get; init; }
}

internal sealed class TestAttachmentPageDto { public List<TestAttachmentDto>? Value { get; init; } }

internal sealed class TestAttachmentDto
{
    public int Id { get; init; }
    public string? FileName { get; init; }
    public string? Comment { get; init; }
    public long? Size { get; init; }
    public string? AttachmentType { get; init; }
}
