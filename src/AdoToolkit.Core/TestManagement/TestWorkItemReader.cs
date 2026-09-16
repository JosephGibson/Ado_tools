using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit.Core.TestManagement;

internal sealed class TestWorkItemReader(HttpClient client, AdoConnection connection, IAdoLog? log)
{
    private readonly AdoHttpPipeline pipeline = new(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log);

    internal async Task<IReadOnlyList<TestWorkItem>> ReadAsync(IReadOnlyList<int> ids, string[] fields,
        CultureInfo culture, CancellationToken token, Action? batchCompleted = null)
    {
        IReadOnlyList<WorkItemDto> items = await IdChunks.FetchAsync(ids, 200, async (chunk, cancellation) =>
        {
            byte[] body = JsonSerializer.SerializeToUtf8Bytes(new WorkItemBatchRequestDto { Ids = chunk, Fields = fields }, AdoJsonContext.Default.WorkItemBatchRequestDto);
            IReadOnlyList<WorkItemDto> returned = await pipeline.ExecuteAsync(EndpointRegistry.WorkItemsBatch, null, null, body, culture, async (response, requestToken) =>
            {
                try
                {
                    byte[] bytes = await response.Content.ReadAsByteArrayAsync(requestToken).ConfigureAwait(false);
                    return (IReadOnlyList<WorkItemDto>)(JsonSerializer.Deserialize(bytes, AdoJsonContext.Default.WorkItemBatchDto)?.Value ?? throw new JsonException());
                }
                catch (JsonException error) { throw FormatError(culture, error); }
            }, cancellation).ConfigureAwait(false);
            batchCompleted?.Invoke();
            return returned;
        }, item => item.Id, culture, token).ConfigureAwait(false);
        try
        {
            List<TestWorkItem> mapped = [];
            foreach (WorkItemDto item in items)
            {
                token.ThrowIfCancellationRequested();
                if (item.Rev < 1 || item.Fields is null) throw new JsonException();
                mapped.Add(TestWorkItem.FromDto(item));
            }
            return mapped.AsReadOnly();
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or ArgumentException)
        { throw FormatError(culture, error); }
    }

    private static AdoResponseFormatException FormatError(CultureInfo culture, Exception error) =>
        new(Messages.Get(AdoMessage.ResponseFormat, culture), error) { Operation = "WorkItemsBatch" };
}
