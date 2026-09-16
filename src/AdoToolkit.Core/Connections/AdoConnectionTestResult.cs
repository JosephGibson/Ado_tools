namespace AdoToolkit.Core.Connections;

public sealed class AdoConnectionTestResult
{
    public bool Success { get; init; }
    public TimeSpan Elapsed { get; init; }
    public required string ApiVersion { get; init; }
    public string? Hint { get; init; }
    public required Uri CollectionUri { get; init; }
}
