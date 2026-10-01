using System.Text.Json;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.WorkItems;

// The one WorkItemsBatch request. IDs are sent in chunks of the endpoint's size and the returned
// items are reconciled by ID (§9.1), never by position: an absent ID is simply not returned.
// Callers choose the fields or the relation expansion and map the items themselves.
internal static class WorkItemBatchReader
{
    internal static Task<IReadOnlyList<WorkItemDto>> ReadAsync(AdoHttpPipeline pipeline, IReadOnlyList<int> ids,
        string[]? fields, bool expandRelations, CultureInfo culture, CancellationToken cancellationToken, Action? batchCompleted = null)
    {
        EndpointDefinition endpoint = EndpointRegistry.WorkItemsBatch;
        return IdChunks.FetchAsync(ids, endpoint.ChunkSize, async (chunk, token) =>
        {
            byte[] body = JsonSerializer.SerializeToUtf8Bytes(
                new WorkItemBatchRequestDto { Ids = chunk, Fields = fields, Expand = expandRelations ? "relations" : null },
                AdoJsonContext.Default.WorkItemBatchRequestDto);
            IReadOnlyList<WorkItemDto> items = await pipeline.ExecuteAsync(endpoint, null, null, body, culture, async (response, requestToken) =>
            {
                try
                {
                    string json = await ResponseJson.ReadAsync(response, requestToken).ConfigureAwait(false);
                    return (IReadOnlyList<WorkItemDto>)(JsonSerializer.Deserialize(json, AdoJsonContext.Default.WorkItemBatchDto)?.Value
                        ?? throw new JsonException());
                }
                catch (JsonException error) { throw FormatError(culture, error); }
            }, token).ConfigureAwait(false);
            batchCompleted?.Invoke();
            return items;
        }, static item => item.Id, culture, cancellationToken);
    }

    internal static AdoResponseFormatException FormatError(CultureInfo culture, Exception? error = null) =>
        new(Messages.Get(AdoMessage.ResponseFormat, culture), error) { Operation = EndpointRegistry.WorkItemsBatch.Name };
}
