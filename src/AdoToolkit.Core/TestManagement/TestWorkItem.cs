using AdoToolkit.Core.Connections;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit.Core.TestManagement;

internal sealed class TestWorkItem
{
    internal required int Id { get; init; }
    internal required int Rev { get; init; }
    internal required IReadOnlyDictionary<string, object?> Fields { get; init; }
    internal object? Value(string field) => Fields.GetValueOrDefault(field);
    internal string? Text(string field) => Value(field) as string;
    internal AdoSharedStepInfo Info(Uri collection, int count = 0) => new()
    {
        Id = Id, Rev = Rev, Title = Text("System.Title"), TeamProject = Text("System.TeamProject"), ReferenceCount = count,
        WebUrl = Text("System.TeamProject") is { Length: > 0 } project ? AdoWebLinks.WorkItem(collection, project, Id) : null,
    };
    internal static TestWorkItem FromDto(WorkItemDto dto) => new()
    { Id = dto.Id, Rev = dto.Rev, Fields = FieldValueMapper.MapFields(dto.Fields!) };
}
