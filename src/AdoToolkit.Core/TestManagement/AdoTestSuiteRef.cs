namespace AdoToolkit.Core.TestManagement;

public sealed class AdoTestSuiteRef
{
    public int PlanId { get; init; }
    public int SuiteId { get; init; }
    public required string PlanName { get; init; }
    public required string SuiteName { get; init; }
    public IReadOnlyList<string> SuitePath { get; init; } = Array.Empty<string>();
    public Uri? PlanWebUrl { get; init; }
    public Uri? WebUrl { get; init; }
    public required string TeamProject { get; init; }
    public required Uri CollectionUri { get; init; }
}
