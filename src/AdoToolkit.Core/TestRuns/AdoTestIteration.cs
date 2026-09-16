namespace AdoToolkit.Core.TestRuns;

public sealed class AdoTestIteration
{
    public required int Id { get; init; }
    public string? Outcome { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<AdoTestIterationParameter> Parameters { get; init; } = Array.Empty<AdoTestIterationParameter>();
    public IReadOnlyList<AdoTestActionResult> ActionResults { get; init; } = Array.Empty<AdoTestActionResult>();
}
