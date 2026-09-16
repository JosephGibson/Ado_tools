namespace AdoToolkit.Core.Connections;

public sealed class AdoIdentityRef
{
    public string? Id { get; init; }
    public required string DisplayName { get; init; }
    public string? UniqueName { get; init; }
}
