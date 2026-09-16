namespace AdoToolkit.Core.TestManagement;

// A suite to read memberships from: explicit IDs, or a suite object already retrieved.
public sealed class TestSuiteSelection
{
    public required string Project { get; init; }
    public int PlanId { get; init; }
    public int SuiteId { get; init; }
    public bool Recurse { get; init; }
    // Name and path are taken from this object; without Recurse no suite listing is needed.
    public AdoTestSuite? Suite { get; init; }
}
