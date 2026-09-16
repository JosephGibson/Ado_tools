namespace AdoToolkit.Core.Connections;

public sealed class AdoConnection
{
    // One day; per-request cancellation timers cannot represent much larger values.
    public const int MaximumRequestTimeoutSeconds = 86400;

    public required Uri CollectionUri { get; init; }
    public string? DefaultProject { get; init; }
    public string Authentication { get; init; } = "WindowsIntegrated";
    public int RequestTimeoutSeconds { get; init; } = 100;
}
