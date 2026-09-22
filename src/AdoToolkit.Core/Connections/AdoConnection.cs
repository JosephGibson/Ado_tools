using AdoToolkit.Core.Builds;

namespace AdoToolkit.Core.Connections;

public sealed class AdoConnection
{
    // One day; per-request cancellation timers cannot represent much larger values.
    public const int MaximumRequestTimeoutSeconds = 86400;

    public required Uri CollectionUri { get; init; }
    public string? DefaultProject { get; init; }
    // Profile defaults for parameters a command leaves out; a URL connection has none.
    public string? DefaultBranch { get; init; }
    public BuildDefinitionSelector? DefaultBuildDefinition { get; init; }
    public int? DefaultTestPlanId { get; init; }
    public int? DefaultTestSuiteId { get; init; }
    public string Authentication { get; init; } = "WindowsIntegrated";
    public int RequestTimeoutSeconds { get; init; } = 100;
}
