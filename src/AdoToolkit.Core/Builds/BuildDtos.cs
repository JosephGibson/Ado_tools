using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.Builds;

internal sealed class BuildDefinitionPageDto { public List<BuildDefinitionDto>? Value { get; init; } }
internal sealed class BuildDefinitionDto
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Path { get; init; }
    public int Revision { get; init; }
    public string? QueueStatus { get; init; }
}
internal sealed class BuildPageDto { public List<BuildDto>? Value { get; init; } }
internal sealed class BuildDto
{
    public int Id { get; init; }
    public string? BuildNumber { get; init; }
    public BuildDefinitionDto? Definition { get; init; }
    public string? SourceBranch { get; init; }
    public string? SourceVersion { get; init; }
    public BuildRepositoryDto? Repository { get; init; }
    public string? Status { get; init; }
    public string? Result { get; init; }
    public string? Reason { get; init; }
    public IdentityDto? RequestedFor { get; init; }
    public DateTimeOffset? QueueTime { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? FinishTime { get; init; }
    public Uri? Uri { get; init; }
}
internal sealed class BuildRepositoryDto
{
    public string? Type { get; init; }
    public string? Id { get; init; }
}
internal sealed class TimelineDto { public List<TimelineRecordDto>? Records { get; init; } }
internal sealed class TimelineRecordDto
{
    public Guid Id { get; init; }
    public Guid? ParentId { get; init; }
    public string? Type { get; init; }
    public string? Name { get; init; }
    public int Order { get; init; }
    public string? State { get; init; }
    public string? Result { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? FinishTime { get; init; }
    public int Attempt { get; init; } = 1;
    public string? Identifier { get; init; }
    public List<AdoTimelineAttempt>? PreviousAttempts { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public List<AdoTimelineIssue>? Issues { get; init; }
    public BuildLogDto? Log { get; init; }
}
internal sealed class BuildLogPageDto { public List<BuildLogDto>? Value { get; init; } }
internal sealed class BuildLogDto
{
    public int Id { get; init; }
    public long? LineCount { get; init; }
}
