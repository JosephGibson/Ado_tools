namespace AdoToolkit.Core.TestManagement;

public sealed class AdoTestPlan
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public int RootSuiteId { get; init; }
    public required string TeamProject { get; init; }
    public required Uri CollectionUri { get; init; }
    public Uri? WebUrl { get; init; }
}
