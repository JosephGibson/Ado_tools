using System.Text.Json;

namespace AdoToolkit.Core.WorkItems;

internal sealed class WiqlResponseDto
{
    public string? QueryType { get; init; }
    public string? QueryResultType { get; init; }
    public DateTimeOffset? AsOf { get; init; }
    public WiqlColumnDto[]? Columns { get; init; }
    public WiqlWorkItemReferenceDto[]? WorkItems { get; init; }
    public JsonElement? WorkItemRelations { get; init; }
}
