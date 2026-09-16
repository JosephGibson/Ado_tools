namespace AdoToolkit.Core.TestRuns;

public sealed class AdoTestActionResult
{
    public string? ActionPath { get; init; }
    public string? StepIdentifier { get; init; }
    public string? Outcome { get; init; }
    public string? ErrorMessage { get; init; }
}
