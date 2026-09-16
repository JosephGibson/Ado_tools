namespace AdoToolkit.Core.TestRuns;

public sealed class AdoTestCaseLink
{
    public required int Id { get; init; }
    public string? Title { get; init; }
    public string? State { get; init; }
    public int? Rev { get; init; }
    public required Uri WebUrl { get; init; }
    public bool IsResolved { get; init; }
}
