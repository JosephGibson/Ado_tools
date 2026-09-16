namespace AdoToolkit.Core.Connections;

public sealed class CollectionUrlNormalizationResult
{
    public required Uri CollectionUri { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
