namespace AdoToolkit.Core.Connections;

internal sealed class IdentityDto
{
    public string? Id { get; set; }
    public string? DisplayName { get; set; }
    public string? UniqueName { get; set; }

    internal AdoIdentityRef ToDomain() => new() { Id = Id, DisplayName = DisplayName ?? "", UniqueName = UniqueName };
}
