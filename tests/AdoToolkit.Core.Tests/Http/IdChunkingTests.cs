using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit.Core.Tests.Http;

[Trait("Acceptance", "S1-1")]
public sealed class IdChunkingTests
{
    private static readonly int[] ExpectedChunkSizes = [200, 200, 50];
    [Fact]
    public async Task Requests450IdsSequentiallyInThreeChunksAndRestoresFirstOccurrenceOrder()
    {
        using FakeHttpMessageHandler handler = new();
        int active = 0;
        handler.Fallback = async (request, token) =>
        {
            Assert.Equal(1, Interlocked.Increment(ref active));
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            int[] chunk = body.RootElement.GetProperty("ids").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            Assert.InRange(chunk.Length, 1, 200);
            JsonArray values = [];
            foreach (int id in chunk.Reverse())
            {
                JsonNode item = JsonNode.Parse(File.ReadAllText(Path.Combine(TestDirectory.RepositoryRoot, "tests/Fixtures/Rest/workitem-fields.json")))!["value"]![0]!.DeepClone();
                item["id"] = id;
                values.Add(item);
            }
            await Task.Yield();
            Interlocked.Decrement(ref active);
            return FakeHttpMessageHandler.Response(new JsonObject { ["value"] = values }.ToJsonString());
        };
        using HttpClient client = new(handler);
        WorkItemService service = new(client, new AdoConnection { CollectionUri = new Uri("https://ado.example.test/Collection") });
        int[] ids = Enumerable.Range(1, 450).Reverse().ToArray();
        IReadOnlyList<AdoWorkItem> result = await service.GetWorkItemsAsync(ids.Concat(ids.Take(5)).ToArray(), CultureInfo.GetCultureInfo("fr-CA"), TestContext.Current.CancellationToken);
        Assert.Equal(ids, result.Select(item => item.Id));
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(ExpectedChunkSizes, handler.Requests.Select(r => JsonNode.Parse(r.Body!)!["ids"]!.AsArray().Count));
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal("POST", request.Method);
            Assert.Equal("https://ado.example.test/Collection/_apis/wit/workitemsbatch?api-version=6.0", request.Uri.AbsoluteUri);
            Assert.Equal("fr-CA", request.Language);
        });
        Assert.Equal(PagingStrategy.IdChunks, EndpointRegistry.WorkItemsBatch.Paging);
        Assert.Equal(200, EndpointRegistry.WorkItemsBatch.ChunkSize);
        Assert.Equal(TimeoutClass.Query, EndpointRegistry.WorkItemsBatch.Timeout);
        Assert.True(EndpointRegistry.WorkItemsBatch.IsSafeToRetry);
    }

    [Fact]
    public async Task CancellationStopsBeforeTheNextChunk()
    {
        using CancellationTokenSource cancellation = new();
        int calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => IdChunks.FetchAsync(Enumerable.Range(1, 450).ToArray(), 200,
            (chunk, _) =>
            {
                calls++;
                cancellation.Cancel();
                return Task.FromResult<IReadOnlyList<WorkItemDto>>(Array.Empty<WorkItemDto>());
            }, item => item.Id, CultureInfo.InvariantCulture, cancellation.Token));
        Assert.Equal(1, calls);
    }
}
