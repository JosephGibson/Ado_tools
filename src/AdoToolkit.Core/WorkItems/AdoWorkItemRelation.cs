namespace AdoToolkit.Core.WorkItems;

public sealed class AdoWorkItemRelation
{
    public required string Rel { get; init; }
    public required Uri Url { get; init; }
    public required IReadOnlyDictionary<string, object?> Attributes { get; init; }
}
