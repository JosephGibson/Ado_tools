namespace AdoToolkit.Core.Connections;

// A collection URL that ended with a project name, split into the parts Connect-Ado expects.
public sealed class CollectionUrlSuggestion
{
    public required Uri CollectionUri { get; init; }
    public required string Project { get; init; }
}
