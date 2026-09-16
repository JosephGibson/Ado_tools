using System.Text.Json;

namespace AdoToolkit.Core.WorkItems;

internal sealed class WorkItemDto
{
    public int Id { get; init; }
    public int Rev { get; init; }
    public Dictionary<string, JsonElement>? Fields { get; init; }
    public List<WorkItemRelationDto>? Relations { get; init; }
}
