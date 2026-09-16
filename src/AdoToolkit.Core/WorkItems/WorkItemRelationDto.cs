using System.Text.Json;

namespace AdoToolkit.Core.WorkItems;

internal sealed class WorkItemRelationDto
{
    public string? Rel { get; init; }
    public string? Url { get; init; }
    public Dictionary<string, JsonElement>? Attributes { get; init; }
}
