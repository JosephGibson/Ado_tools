namespace AdoToolkit.Core.Configuration;

public sealed class TestCaseOptions
{
    public int MaximumSharedStepDepth { get; init; } = 10;
    public int MaximumExpandedSteps { get; init; } = 5000;
    public int MaximumResolvedWorkItems { get; init; } = 10000;
}
