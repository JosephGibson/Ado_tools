using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.WorkItems;

public sealed class WorkItemService
{
    private static readonly string[] RequiredFields =
    [
        "System.WorkItemType", "System.Title", "System.State", "System.TeamProject",
        "System.AreaPath", "System.IterationPath", "System.ChangedDate",
    ];
    private readonly AdoConnection connection;
    private readonly AdoHttpPipeline pipeline;

    public WorkItemService(HttpClient client, AdoConnection connection, IAdoLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(connection.RequestTimeoutSeconds);
        this.connection = connection;
        pipeline = new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log);
    }

    public async Task<IReadOnlyList<AdoWorkItem>> GetWorkItemsAsync(IReadOnlyList<int> ids, CultureInfo culture,
        CancellationToken cancellationToken, IReadOnlyList<string>? fields = null, bool includeRelations = false)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(culture);
        if (includeRelations && fields is not null) throw new ArgumentException(Messages.Get(AdoMessage.FieldRelationsConflict, culture), nameof(fields));
        foreach (int id in ids) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        if (fields is not null)
            foreach (string field in fields) ArgumentException.ThrowIfNullOrWhiteSpace(field);
        int[] unique = ids.Distinct().ToArray();
        string[]? requestedFields = includeRelations ? null
            : RequiredFields.Concat(fields ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        EndpointDefinition endpoint = EndpointRegistry.WorkItemsBatch;
        IReadOnlyList<WorkItemDto> returned = await IdChunks.FetchAsync(unique, endpoint.ChunkSize,
            async (chunk, token) =>
            {
                byte[] body = JsonSerializer.SerializeToUtf8Bytes(new WorkItemBatchRequestDto
                { Ids = chunk, Fields = requestedFields, Expand = includeRelations ? "relations" : null }, AdoJsonContext.Default.WorkItemBatchRequestDto);
                return await pipeline.ExecuteAsync(endpoint, null, null, body, culture, async (response, requestToken) =>
                {
                    try
                    {
                        byte[] bytes = await response.Content.ReadAsByteArrayAsync(requestToken).ConfigureAwait(false);
                        return (IReadOnlyList<WorkItemDto>)(JsonSerializer.Deserialize(bytes, AdoJsonContext.Default.WorkItemBatchDto)?.Value
                            ?? throw new JsonException());
                    }
                    catch (JsonException error) { throw FormatError(culture, error); }
                }, token).ConfigureAwait(false);
            }, static item => item.Id, culture, cancellationToken).ConfigureAwait(false);
        List<AdoWorkItem> result = [];
        foreach (WorkItemDto item in returned)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { result.Add(Map(item, includeRelations)); }
            catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or ArgumentException)
            { throw FormatError(culture, error); }
        }
        return result.AsReadOnly();
    }

    private AdoWorkItem Map(WorkItemDto item, bool includeRelations)
    {
        if (item.Rev < 1 || item.Fields is null) throw new JsonException();
        IReadOnlyDictionary<string, object?> fields = FieldValueMapper.MapFields(item.Fields);
        string Required(string name) => fields.TryGetValue(name, out object? value) && value is string text ? text : throw new JsonException();
        string project = Required("System.TeamProject");
        if (!fields.TryGetValue("System.ChangedDate", out object? date) || date is not DateTimeOffset changed) throw new JsonException();
        List<AdoWorkItemRelation>? relations = includeRelations ? [] : null;
        if (includeRelations && item.Relations is not null)
            foreach (WorkItemRelationDto relation in item.Relations)
            {
                if (relation is null || string.IsNullOrEmpty(relation.Rel) || !Uri.TryCreate(relation.Url, UriKind.Absolute, out Uri? url)) throw new JsonException();
                relations!.Add(new AdoWorkItemRelation { Rel = relation.Rel, Url = url, Attributes = FieldValueMapper.MapObject(relation.Attributes ?? []) });
            }
        return new AdoWorkItem
        {
            Id = item.Id, Rev = item.Rev, WorkItemType = Required("System.WorkItemType"), Title = Required("System.Title"),
            State = Required("System.State"), TeamProject = project, AreaPath = Required("System.AreaPath"),
            IterationPath = Required("System.IterationPath"), ChangedDate = changed, Fields = fields,
            Relations = relations?.AsReadOnly(), CollectionUri = connection.CollectionUri,
            WebUrl = AdoWebLinks.WorkItem(connection.CollectionUri, project, item.Id),
        };
    }

    private static AdoResponseFormatException FormatError(CultureInfo culture, Exception error) =>
        new(Messages.Get(AdoMessage.ResponseFormat, culture), error) { Operation = "WorkItemsBatch" };
}
