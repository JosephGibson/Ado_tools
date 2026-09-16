namespace AdoToolkit.Core.Builds;

public sealed class AdoBuildDefinition
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string? Path { get; init; }
    public int Revision { get; init; }
    public string? QueueStatus { get; init; }
    public required string TeamProject { get; init; }
    public required Uri WebUrl { get; init; }
    public required Uri CollectionUri { get; init; }
}
