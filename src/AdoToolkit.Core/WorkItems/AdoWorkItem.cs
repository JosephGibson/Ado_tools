namespace AdoToolkit.Core.WorkItems;

public sealed class AdoWorkItem
{
    public int Id { get; init; }
    public int Rev { get; init; }
    public required string WorkItemType { get; init; }
    public required string Title { get; init; }
    public required string State { get; init; }
    public required string TeamProject { get; init; }
    public required string AreaPath { get; init; }
    public required string IterationPath { get; init; }
    public DateTimeOffset ChangedDate { get; init; }
    public required IReadOnlyDictionary<string, object?> Fields { get; init; }
    public IReadOnlyList<AdoWorkItemRelation>? Relations { get; init; }
    public required Uri WebUrl { get; init; }
    public required Uri CollectionUri { get; init; }
}
