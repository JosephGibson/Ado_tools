namespace AdoToolkit.Core.TestManagement;

public sealed class AdoTestSuite
{
    // Suite ID; pipeline binding maps it to SuiteId, never to a test case ID.
    public int Id { get; init; }
    public int PlanId { get; init; }
    public int? ParentSuiteId { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<string> SuitePath { get; init; } = Array.Empty<string>();
    public required string TeamProject { get; init; }
    public required Uri CollectionUri { get; init; }
    public Uri? WebUrl { get; init; }
}
