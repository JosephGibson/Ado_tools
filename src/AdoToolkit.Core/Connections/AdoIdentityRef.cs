namespace AdoToolkit.Core.Connections;

public sealed class AdoIdentityRef
{
    public string? Id { get; init; }
    public required string DisplayName { get; init; }
    public string? UniqueName { get; init; }

    // An identity held as a field value is shown through its text (a table cell, a report value).
    public override string ToString() => string.IsNullOrEmpty(UniqueName) ? DisplayName : DisplayName + " <" + UniqueName + ">";
}
