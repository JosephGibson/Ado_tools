namespace AdoToolkit.Core.Connections;

public sealed class AdoProject
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? State { get; init; }
    public required Uri CollectionUri { get; init; }
}
