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
        IReadOnlyList<WorkItemDto> items = await WorkItemBatchReader.ReadAsync(pipeline, ids, fields, false, culture, token, batchCompleted)
            .ConfigureAwait(false);
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
        { throw WorkItemBatchReader.FormatError(culture, error); }
    }
}
