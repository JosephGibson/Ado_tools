using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit.Core.TestRuns;

// One WorkItemsBatch pass over the distinct valid Test Case IDs, reconciled by ID (§9.1).
// Requested IDs are never matched to response positions; an absent ID keeps its link.
// Relations are expanded so the bug lookup knows every linked work item; the API returns all
// fields with them, of which only the title and state are read.
internal sealed class TestCaseLinkResolver
{
    // Relations that are not work item links, whatever their URL.
    private static readonly string[] ResourceRelations = ["ArtifactLink", "Hyperlink", "AttachedFile"];
    private readonly AdoConnection connection;
    private readonly AdoHttpPipeline pipeline;
    private readonly Dictionary<int, AdoTestCaseLink> cache = [];
    private readonly Dictionary<int, IReadOnlyList<int>> linked = [];

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
                byte[] body = JsonSerializer.SerializeToUtf8Bytes(new WorkItemBatchRequestDto { Ids = chunk, Expand = "relations" },
                    AdoJsonContext.Default.WorkItemBatchRequestDto);
                IReadOnlyList<WorkItemDto> page = await pipeline.ExecuteAsync(endpoint, null, null, body, culture,
                    async (response, requestToken) =>
                    {
                        try
                        {
                            string bytes = await ResponseJson.ReadAsync(response, requestToken).ConfigureAwait(false);
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
            Dictionary<string, JsonElement> fields = item.Fields
                ?? throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = endpoint.Name };
            cache[id] = new AdoTestCaseLink
            {
                Id = id,
                Title = Text(fields, "System.Title"),
                State = Text(fields, "System.State"),
                Rev = item.Rev >= 1 ? item.Rev : null,
                WebUrl = webUrl,
                IsResolved = true,
            };
            linked[id] = LinkedIds(id, item.Relations);
        }
        return cache;
    }

    // The work items linked to a resolved Test Case by any link type; empty for any other ID.
    internal IReadOnlyList<int> LinkedWorkItems(int id) => linked.TryGetValue(id, out IReadOnlyList<int>? ids) ? ids : [];

    // Only the title and state are read, so no other field of the expanded response can fail the lookup.
    internal static string? Text(Dictionary<string, JsonElement> fields, string name)
    {
        foreach ((string key, JsonElement value) in fields)
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return null;
    }

    // A work item link names its target as <collection>/_apis/wit/workItems/<id> whatever the link type,
    // including custom types; artifact links, hyperlinks and attachments are skipped. Relations are
    // untrusted data: an unexpected shape is ignored, never followed.
    internal static IReadOnlyList<int> LinkedIds(int self, List<WorkItemRelationDto>? relations)
    {
        SortedSet<int> ids = [];
        foreach (WorkItemRelationDto relation in relations ?? [])
        {
            if (relation?.Rel is not { Length: > 0 } rel || ResourceRelations.Contains(rel, StringComparer.OrdinalIgnoreCase)
                || !Uri.TryCreate(relation.Url, UriKind.Absolute, out Uri? url)) continue;
            string[] segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 4 && string.Equals(segments[^4], "_apis", StringComparison.OrdinalIgnoreCase)
                && string.Equals(segments[^3], "wit", StringComparison.OrdinalIgnoreCase)
                && string.Equals(segments[^2], "workItems", StringComparison.OrdinalIgnoreCase)
                && TryParseReference(segments[^1], out int id) && id != self)
                ids.Add(id);
        }
        return Array.AsReadOnly(ids.ToArray());
    }
}
