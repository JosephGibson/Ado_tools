using System.Text.Json.Serialization;

namespace AdoToolkit.Core.WorkItems;

internal sealed class WorkItemBatchRequestDto
{
    public required int[] Ids { get; init; }
    public string ErrorPolicy { get; init; } = "omit";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Fields { get; init; }
    [JsonPropertyName("$expand")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Expand { get; init; }
}
