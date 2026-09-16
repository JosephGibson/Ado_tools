using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit.Core.Tests.WorkItems;

// The server cap failure shape is a [V-06] assumption.
[Trait("Acceptance", "S3-3")]
public sealed class WiqlServiceTests
{
    private const string Project = "Équipe Web";
    private const string Query = "SELECT [System.Id] FROM WorkItems WHERE [System.Title] = 'marqueur-confidentiel-7Q'";
    private static readonly AdoConnection Connection = new() { CollectionUri = new Uri("https://ado.example.test/Collection"), DefaultProject = "Wrong project" };
    private static readonly int[] FlatIds = [1203, 1201, 1202];
    private static readonly string[] FlatColumns = ["System.Id", "System.Title", "Microsoft.VSTS.Common.Priority"];
    private static readonly int[] FirstTwo = [1203, 1201];

    [Fact]
    public async Task FlatQueryPostsOnlyTheQueryAndNeverLogsIt()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("wiql-flat.json"));
        using HttpClient client = new(handler);
        CapturingLog log = new();
        AdoWiqlResult result = await new WiqlService(client, Connection, log)
            .QueryAsync(Project, Query, null, CultureInfo.GetCultureInfo("fr-CA"), TestContext.Current.CancellationToken);
        Assert.Equal(FlatIds, result.Ids);
        Assert.Equal(FlatColumns, result.Columns);
        Assert.Equal(DateTimeOffset.Parse("2026-09-15T16:20:30.123Z", CultureInfo.InvariantCulture), result.AsOf);
        Assert.Equal(TimeSpan.Zero, result.AsOf.Offset);
        Assert.Null(result.LimitApplied);
        Assert.Equal(Project, result.TeamProject);
        Assert.Equal(Connection.CollectionUri, result.CollectionUri);
        RequestSnapshot request = Assert.Single(handler.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_apis/wit/wiql?api-version=6.0", request.Uri.AbsoluteUri);
        using JsonDocument body = JsonDocument.Parse(request.Body!);
        Assert.Equal(Query, body.RootElement.GetProperty("query").GetString());
        Assert.Single(body.RootElement.EnumerateObject());
        Assert.NotEmpty(log.Messages);
        Assert.DoesNotContain(log.Messages, message => message.Contains("marqueur", StringComparison.Ordinal) || message.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExplicitTopIsSentAsQueryParameterAndRecorded()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("wiql-flat.json"));
        using HttpClient client = new(handler);
        AdoWiqlResult result = await new WiqlService(client, Connection)
            .QueryAsync(Project, Query, 2, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(FirstTwo, result.Ids);
        Assert.Equal(2, result.LimitApplied);
        RequestSnapshot request = Assert.Single(handler.Requests);
        Assert.Equal("?%24top=2&api-version=6.0", request.Uri.Query);
        using JsonDocument body = JsonDocument.Parse(request.Body!);
        Assert.Single(body.RootElement.EnumerateObject());
    }

    [Theory]
    [InlineData(20000, null, true)]
    [InlineData(20001, null, true)]
    [InlineData(19999, null, false)]
    [InlineData(20000, 20000, false)]
    public async Task ReachingTheCapWithoutTopRaisesInsteadOfTruncating(int count, int? top, bool fails)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(FlatResponse(Enumerable.Range(1, count))));
        using HttpClient client = new(handler);
        CultureInfo culture = CultureInfo.GetCultureInfo("fr-CA");
        Task<AdoWiqlResult> query = new WiqlService(client, Connection).QueryAsync(Project, Query, top, culture, TestContext.Current.CancellationToken);
        if (fails)
        {
            AdoRequestException error = await Assert.ThrowsAsync<AdoRequestException>(() => query);
            Assert.Equal("Wiql", error.Operation);
            Assert.False(error.IsRetryable);
            Assert.Contains(Messages.Get(AdoMessage.WiqlLimitHint, culture), error.Message, StringComparison.Ordinal);
            Assert.Contains(20000.ToString("N0", culture), error.Message, StringComparison.Ordinal);
        }
        else
        {
            AdoWiqlResult result = await query;
            Assert.Equal(count, result.Ids.Count);
            Assert.Equal(top, result.LimitApplied);
        }
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ServerLimitErrorIsSurfacedOnceWithTheNarrowingHint()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("wiql-limit-error.json", 400));
        using HttpClient client = new(handler);
        CultureInfo culture = CultureInfo.GetCultureInfo("en-US");
        AdoRequestException error = await Assert.ThrowsAsync<AdoRequestException>(() => new WiqlService(client, Connection)
            .QueryAsync(Project, Query, null, culture, TestContext.Current.CancellationToken));
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("Wiql", error.Operation);
        Assert.StartsWith("VS402337", error.Message, StringComparison.Ordinal);
        Assert.EndsWith(Messages.Get(AdoMessage.WiqlLimitHint, culture), error.Message, StringComparison.Ordinal);
        Assert.IsType<AdoRequestException>(error.InnerException);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task OtherBadRequestsKeepTheirOwnMessage()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response("{\"message\":\"TF51005: syntax error.\"}", 400));
        using HttpClient client = new(handler);
        AdoRequestException error = await Assert.ThrowsAsync<AdoRequestException>(() => new WiqlService(client, Connection)
            .QueryAsync(Project, Query, null, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(Messages.Get(AdoMessage.WiqlLimitHint, CultureInfo.InvariantCulture), error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData("tree", "workItemLink", true)]
    [InlineData("oneHop", "workItemLink", true)]
    [InlineData("flat", "workItem", true)]
    [InlineData("oneHop", null, false)]
    [InlineData(null, "workItemLink", false)]
    public async Task LinkAndTreeQueriesAreNotSupported(string? queryType, string? resultType, bool relations)
    {
        JsonObject response = JsonNode.Parse(File.ReadAllText(Path.Combine(TestDirectory.RepositoryRoot, "tests/Fixtures/Rest/wiql-tree.json")))!.AsObject();
        response["queryType"] = queryType;
        response["queryResultType"] = resultType;
        if (!relations) response.Remove("workItemRelations");
        response["workItems"] = new JsonArray(new JsonObject { ["id"] = 1201 });
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(response.ToJsonString()));
        using HttpClient client = new(handler);
        CultureInfo culture = CultureInfo.GetCultureInfo("fr-CA");
        NotSupportedException error = await Assert.ThrowsAsync<NotSupportedException>(() => new WiqlService(client, Connection)
            .QueryAsync(Project, Query, null, culture, TestContext.Current.CancellationToken));
        Assert.Equal(Messages.Get(AdoMessage.WiqlNotFlat, culture), error.Message);
    }

    [Theory]
    [InlineData("""{"queryType":"flat","asOf":"2026-09-15T00:00:00Z"}""")]
    [InlineData("""{"queryType":"flat","workItems":[]}""")]
    [InlineData("""{"queryType":"flat","asOf":"2026-09-15T00:00:00Z","workItems":[{"id":0}]}""")]
    [InlineData("""{"queryType":"flat","asOf":"2026-09-15T00:00:00Z","workItems":[{"id":1},{"id":1}]}""")]
    [InlineData("""{"queryType":"flat","asOf":"2026-09-15T00:00:00Z","workItems":[null]}""")]
    [InlineData("""{"queryType":"flat","asOf":"2026-09-15T00:00:00Z","workItems":[],"columns":[{"name":"ID"}]}""")]
    [InlineData("""{"queryType":"flat","asOf":"not a date","workItems":[]}""")]
    [InlineData("null")]
    public async Task InvalidFlatShapesAreFormatErrors(string body)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(body));
        using HttpClient client = new(handler);
        AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() => new WiqlService(client, Connection)
            .QueryAsync(Project, Query, null, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Equal("Wiql", error.Operation);
    }

    [Fact]
    public async Task HydratePreservesQueryOrderAcrossChunksWithColumnsAndConvenienceFields()
    {
        int[] ids = Enumerable.Range(1, 450).Select(i => (i * 7919 % 450) + 1).ToArray();
        Assert.Equal(450, ids.Distinct().Count());
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(FlatResponse(ids)));
        handler.Fallback = async (request, token) =>
        {
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            JsonArray values = [];
            foreach (int id in body.RootElement.GetProperty("ids").EnumerateArray().Select(v => v.GetInt32()).Reverse())
            {
                JsonNode item = JsonNode.Parse(File.ReadAllText(Path.Combine(TestDirectory.RepositoryRoot, "tests/Fixtures/Rest/workitem-fields.json")))!["value"]![0]!.DeepClone();
                item["id"] = id;
                values.Add(item);
            }
            return FakeHttpMessageHandler.Response(new JsonObject { ["value"] = values }.ToJsonString());
        };
        using HttpClient client = new(handler);
        WiqlService service = new(client, Connection);
        AdoWiqlResult result = await service.QueryAsync(Project, Query, null, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        IReadOnlyList<AdoWorkItem> items = await service.HydrateAsync(result, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(ids, items.Select(item => item.Id));
        Assert.Equal(4, handler.Requests.Count);
        Assert.All(handler.Requests.Skip(1), request =>
            Assert.Equal("https://ado.example.test/Collection/_apis/wit/workitemsbatch?api-version=6.0", request.Uri.AbsoluteUri));
        string?[] fields = JsonNode.Parse(handler.Requests[1].Body!)!["fields"]!.AsArray().Select(v => v!.GetValue<string>()).ToArray();
        Assert.Contains("System.Title", fields);
        Assert.Contains("Microsoft.VSTS.Common.Priority", fields);
        Assert.Contains("System.ChangedDate", fields);
    }

    [Fact]
    public async Task HydrateRejectsAResultFromAnotherCollectionWithoutRequests()
    {
        using FakeHttpMessageHandler handler = new();
        using HttpClient client = new(handler);
        AdoWiqlResult foreign = new() { Ids = FlatIds, TeamProject = Project, CollectionUri = new Uri("https://other.example.test/Collection") };
        await Assert.ThrowsAsync<AdoConnectionMismatchException>(() => new WiqlService(client, Connection)
            .HydrateAsync(foreign, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task InvalidArgumentsFailBeforeAnyRequest()
    {
        using FakeHttpMessageHandler handler = new();
        using HttpClient client = new(handler);
        WiqlService service = new(client, Connection);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.QueryAsync(Project, Query, 0, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.QueryAsync(Project, Query, 20001, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => service.QueryAsync(Project, " ", null, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => service.QueryAsync("", Query, null, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
        Assert.Equal("6.0", EndpointRegistry.Wiql.ApiVersion);
        Assert.Equal(PagingStrategy.None, EndpointRegistry.Wiql.Paging);
        Assert.True(EndpointRegistry.Wiql.IsSafeToRetry);
    }

    private static string FlatResponse(IEnumerable<int> ids) => new JsonObject
    {
        ["queryType"] = "flat",
        ["queryResultType"] = "workItem",
        ["asOf"] = "2026-09-15T16:20:30Z",
        ["columns"] = new JsonArray(new JsonObject { ["referenceName"] = "System.Id" }, new JsonObject { ["referenceName"] = "Microsoft.VSTS.Common.Priority" }),
        ["workItems"] = new JsonArray(ids.Select(id => (JsonNode)new JsonObject { ["id"] = id }).ToArray()),
    }.ToJsonString();
}
