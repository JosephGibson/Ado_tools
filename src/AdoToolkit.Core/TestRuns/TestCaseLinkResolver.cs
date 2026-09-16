using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit.Core.TestRuns;

// One WorkItemsBatch pass over the distinct valid Test Case IDs, reconciled by ID (§9.1).
// Requested IDs are never matched to response positions; an absent ID keeps its link.
internal sealed class TestCaseLinkResolver
{
    private static readonly string[] Fields = ["System.Id", "System.Rev", "System.Title", "System.State", "System.WorkItemType"];
    private readonly AdoConnection connection;
    private readonly AdoHttpPipeline pipeline;
    private readonly Dictionary<int, AdoTestCaseLink> cache = [];

    internal TestCaseLinkResolver(HttpClient client, AdoConnection connection, IAdoLog? log, RequestCounter? counter)
    {
        this.connection = connection;
        pipeline = new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds),
            log, counter: counter);
    }

    // Parses testCase.id, which arrives as a string [Verify V-21]. Null means "no reference";
    // false means a reference that is not a positive integer.
    internal static bool TryParseReference(string? value, out int id)
    {
        id = 0;
        return !string.IsNullOrWhiteSpace(value)
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id >= 1;
    }

    internal async Task<IReadOnlyDictionary<int, AdoTestCaseLink>> ResolveAsync(IReadOnlyList<int> ids, string project,
        CultureInfo culture, List<AdoDiagnostic> diagnostics, IAdoLog progress, CancellationToken cancellationToken)
    {
        int[] missing = ids.Distinct().Where(id => !cache.ContainsKey(id)).OrderBy(static id => id).ToArray();
        if (missing.Length == 0) return cache;
        EndpointDefinition endpoint = EndpointRegistry.WorkItemsBatch;
        int batches = 0;
        IReadOnlyList<WorkItemDto> returned = await IdChunks.FetchAsync(missing, endpoint.ChunkSize,
            async (chunk, token) =>
            {
                byte[] body = JsonSerializer.SerializeToUtf8Bytes(new WorkItemBatchRequestDto { Ids = chunk, Fields = Fields },
                    AdoJsonContext.Default.WorkItemBatchRequestDto);
                IReadOnlyList<WorkItemDto> page = await pipeline.ExecuteAsync(endpoint, null, null, body, culture,
                    async (response, requestToken) =>
                    {
                        try
                        {
                            byte[] bytes = await response.Content.ReadAsByteArrayAsync(requestToken).ConfigureAwait(false);
                            return (IReadOnlyList<WorkItemDto>)(JsonSerializer.Deserialize(bytes, AdoJsonContext.Default.WorkItemBatchDto)?.Value
                                ?? throw new JsonException());
                        }
                        catch (JsonException error)
                        {
                            throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture), error)
                            { Operation = endpoint.Name };
                        }
                    }, token).ConfigureAwait(false);
                progress.Progress(new AdoProgress { Phase = AdoProgressPhase.TestCaseLinks, Completed = ++batches });
                return page;
            }, static item => item.Id, culture, cancellationToken).ConfigureAwait(false);
        Dictionary<int, WorkItemDto> resolved = [];
        foreach (WorkItemDto item in returned)
            if (!resolved.TryAdd(item.Id, item))
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = endpoint.Name };
        foreach (int id in missing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Uri webUrl = AdoWebLinks.WorkItem(connection.CollectionUri, project, id);
            if (!resolved.TryGetValue(id, out WorkItemDto? item))
            {
                diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.UnresolvedTestCase, culture, id,
                    [id.ToString(CultureInfo.InvariantCulture)]));
                cache[id] = new AdoTestCaseLink { Id = id, WebUrl = webUrl, IsResolved = false };
                continue;
            }
            IReadOnlyDictionary<string, object?> fields = FieldValueMapper.MapFields(item.Fields
                ?? throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = endpoint.Name });
            cache[id] = new AdoTestCaseLink
            {
                Id = id,
                Title = Text(fields, "System.Title"),
                State = Text(fields, "System.State"),
                Rev = item.Rev >= 1 ? item.Rev : null,
                WebUrl = webUrl,
                IsResolved = true,
            };
        }
        return cache;
    }

    private static string? Text(IReadOnlyDictionary<string, object?> fields, string name) =>
        fields.TryGetValue(name, out object? value) ? value as string : null;
}
