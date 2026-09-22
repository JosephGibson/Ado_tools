namespace AdoToolkit.Core.Builds;

// Names a build definition by a positive ID or by exact name, as -Definition does.
public sealed record BuildDefinitionSelector
{
    private BuildDefinitionSelector(int? id, string? name) => (Id, Name) = (id, name);

    public int? Id { get; }
    public string? Name { get; }

    public static BuildDefinitionSelector FromId(int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        return new BuildDefinitionSelector(id, null);
    }

    public static BuildDefinitionSelector FromName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new BuildDefinitionSelector(null, name);
    }

    public override string ToString() => Name ?? Id!.Value.ToString(CultureInfo.InvariantCulture);
}
