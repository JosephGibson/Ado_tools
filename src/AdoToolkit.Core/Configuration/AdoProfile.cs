using AdoToolkit.Core.Builds;

namespace AdoToolkit.Core.Configuration;

public sealed class AdoProfile
{
    public required string Name { get; init; }
    public required Uri CollectionUri { get; init; }
    public string CollectionUrl => CollectionUri.AbsoluteUri;
    public string? DefaultProject { get; init; }
    public string? DefaultBranch { get; init; }
    public BuildDefinitionSelector? DefaultBuildDefinition { get; init; }
    public int? DefaultTestPlanId { get; init; }
    public int? DefaultTestSuiteId { get; init; }
    public string Authentication { get; init; } = "WindowsIntegrated";
    public int RequestTimeoutSeconds { get; init; } = 100;
}
