namespace AdoToolkit.Core.Builds;

public sealed class AdoBuild
{
    public required int Id { get; init; }
    public required string BuildNumber { get; init; }
    public required AdoBuildDefinitionRef Definition { get; init; }
    public string? SourceBranch { get; init; }
    public string? SourceVersion { get; init; }
    public string? RepositoryType { get; init; }
    public string? RepositoryId { get; init; }
    public string? Status { get; init; }
    public string? Result { get; init; }
    public string? Reason { get; init; }
    public AdoToolkit.Core.Connections.AdoIdentityRef? RequestedFor { get; init; }
    public DateTimeOffset? QueueTime { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? FinishTime { get; init; }
    public Uri? Uri { get; init; }
    public required string TeamProject { get; init; }
    public required Uri WebUrl { get; init; }
    public required Uri CollectionUri { get; init; }
}
