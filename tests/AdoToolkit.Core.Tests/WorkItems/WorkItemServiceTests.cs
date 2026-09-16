using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit.Core.Tests.WorkItems;

[Trait("Acceptance", "S1-1")]
public sealed class WorkItemServiceTests
{
    private static readonly AdoConnection Connection = new() { CollectionUri = new Uri("https://ado.example.test/Collection"), DefaultProject = "Wrong project" };
    private static readonly int[] OneId = [1];
    private static readonly int[] InvalidId = [0];
    private static readonly int[] RepeatedIds = [1, 2, 3, 4, 2, 1];
    private static readonly int[] PresentIds = [1, 3];
    private static readonly int[] UniqueIds = [1, 2, 3, 4];
    private static readonly string[] ExtraFields = ["Custom.Nested", "system.title"];
    private static readonly string[] TitleField = ["System.Title"];

    [Theory]
    [InlineData("workitems-null.json")]
    [InlineData("workitems-compact.json")]
    public async Task ReconcilesByIdAcrossBothOmissionShapes(string fixture)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture(fixture));
        using HttpClient client = new(handler);
        IReadOnlyList<AdoWorkItem> items = await new WorkItemService(client, Connection)
            .GetWorkItemsAsync(RepeatedIds, CultureInfo.CurrentCulture, TestContext.Current.CancellationToken);
        Assert.Equal(PresentIds, items.Select(item => item.Id));
        using JsonDocument body = JsonDocument.Parse(Assert.Single(handler.Requests).Body!);
        Assert.Equal(UniqueIds, body.RootElement.GetProperty("ids").EnumerateArray().Select(v => v.GetInt32()));
    }

    [Theory]
    [InlineData("{\"value\":[{\"id\":1},{\"id\":1}]}")]
    [InlineData("{\"value\":[{\"id\":9}]}")]
    [InlineData("{\"value\":[{\"id\":0}]}")]
    [InlineData("{\"value\":[{\"id\":1,\"rev\":1,\"fields\":{}}]}")]
    [InlineData("{\"value\":[1]}")]
    [InlineData("{\"value\":null}")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("broken")]
    public async Task InvalidResponsesFailWithoutRetry(string body)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(body));
        using HttpClient client = new(handler);
        AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() => new WorkItemService(client, Connection)
            .GetWorkItemsAsync(OneId, CultureInfo.CurrentCulture, TestContext.Current.CancellationToken));
        Assert.Equal("WorkItemsBatch", error.Operation);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MapsTypedImmutableFieldsAndBuildsLinksFromTheOwningProject(bool relations)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("workitem-fields.json"));
        using HttpClient client = new(handler);
        AdoWorkItem item = Assert.Single(await new WorkItemService(client, Connection).GetWorkItemsAsync(OneId,
            CultureInfo.CurrentCulture, TestContext.Current.CancellationToken, relations ? null : ExtraFields, relations));
        Assert.Equal(1, item.Id);
        Assert.Equal(3, item.Rev);
        Assert.Equal("Task", item.WorkItemType);
        Assert.Equal("Écrire le guide", item.Title);
        Assert.Equal("Active", item.State);
        Assert.Equal("Équipe\\Docs", item.AreaPath);
        Assert.Equal("Équipe\\Sprint 1", item.IterationPath);
        Assert.Equal(DateTimeOffset.Parse("2026-09-15T13:20:30Z", CultureInfo.InvariantCulture), item.ChangedDate);
        Assert.Equal(TimeSpan.Zero, item.ChangedDate.Offset);
        Assert.Equal(item.ChangedDate, item.Fields["system.changeddate"]);
        Assert.Equal(42L, Assert.IsType<long>(item.Fields["custom.integer"]));
        Assert.Equal(2.5, Assert.IsType<double>(item.Fields["Custom.Double"]));
        Assert.True(Assert.IsType<bool>(item.Fields["Custom.Boolean"]));
        Assert.Null(item.Fields["Custom.Null"]);
        Assert.IsType<string>(item.Fields["Custom.DateText"]);
        Assert.IsType<string>(item.Fields["Custom.IdentityText"]);
        AdoIdentityRef identity = Assert.IsType<AdoIdentityRef>(item.Fields["System.AssignedTo"]);
        Assert.Equal("opaque:sample", identity.Id);
        Assert.Equal("Zoé Exemple", identity.DisplayName);
        Assert.Equal("sample@example.test", identity.UniqueName);
        var dictionary = Assert.IsAssignableFrom<IDictionary<string, object?>>(item.Fields);
        Assert.Throws<NotSupportedException>(() => dictionary.Add("new", 1));
        var nested = Assert.IsAssignableFrom<IDictionary<string, object?>>(item.Fields["Custom.Nested"]);
        var values = Assert.IsAssignableFrom<IList<object?>>(nested["values"]);
        Assert.Throws<NotSupportedException>(() => values.Add(2));
        Assert.Throws<NotSupportedException>(() => nested["values"] = null);
        var leaf = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(values[3]);
        Assert.Equal("<script>untrusted</script>", leaf["text"]);
        Assert.IsType<string>(leaf["date"]);
        Assert.Equal(Connection.CollectionUri, item.CollectionUri);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20%2F%20Web/_workitems/edit/1", item.WebUrl.AbsoluteUri);
        using JsonDocument body = JsonDocument.Parse(Assert.Single(handler.Requests).Body!);
        Assert.Equal("omit", body.RootElement.GetProperty("errorPolicy").GetString());
        if (relations)
        {
            Assert.False(body.RootElement.TryGetProperty("fields", out _));
            Assert.Equal("relations", body.RootElement.GetProperty("$expand").GetString());
            AdoWorkItemRelation relation = Assert.Single(item.Relations!);
            Assert.Equal("System.LinkTypes.Related", relation.Rel);
            Assert.Equal("Related", relation.Attributes["name"]);
            Assert.False(Assert.IsType<bool>(relation.Attributes["isLocked"]));
            Assert.Throws<NotSupportedException>(() => ((IList<AdoWorkItemRelation>)item.Relations!).Clear());
        }
        else
        {
            Assert.Null(item.Relations);
            Assert.False(body.RootElement.TryGetProperty("$expand", out _));
            string?[] fields = body.RootElement.GetProperty("fields").EnumerateArray().Select(v => v.GetString()).ToArray();
            Assert.Equal(8, fields.Length);
            Assert.Contains("System.TeamProject", fields);
            Assert.Contains("System.ChangedDate", fields);
            Assert.Contains("Custom.Nested", fields);
            Assert.DoesNotContain("System.Id", fields);
            Assert.DoesNotContain("System.Rev", fields);
        }
    }

    [Theory]
    [InlineData("System.ChangedDate", "not a date")]
    [InlineData("System.TeamProject", "")]
    public async Task InvalidConvenienceFieldsAreTypedFormatFailures(string field, string value)
    {
        JsonNode response = JsonNode.Parse(File.ReadAllText(Path.Combine(TestDirectory.RepositoryRoot, "tests/Fixtures/Rest/workitem-fields.json")))!;
        response["value"]![0]!["fields"]![field] = value;
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(response.ToJsonString()));
        using HttpClient client = new(handler);
        await Assert.ThrowsAsync<AdoResponseFormatException>(() => new WorkItemService(client, Connection)
            .GetWorkItemsAsync(OneId, CultureInfo.CurrentCulture, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void UnknownObjectsPreserveCaseDistinctKeysAndStringValues()
    {
        using JsonDocument document = JsonDocument.Parse("""{"Name":"2026-01-01","name":"Sample <sample@example.test>"}""");
        var values = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(FieldValueMapper.MapValue(document.RootElement));
        Assert.Equal(2, values.Count);
        Assert.Equal("2026-01-01", Assert.IsType<string>(values["Name"]));
        Assert.Equal("Sample <sample@example.test>", Assert.IsType<string>(values["name"]));
    }

    [Fact]
    public async Task SafeBatchPostRetriesWithFreshRequestsAndTheSameBody()
    {
        using FakeHttpMessageHandler handler = new();
        HttpResponseMessage unavailable = FakeHttpMessageHandler.Response("{}", 503);
        unavailable.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
        handler.Enqueue(unavailable);
        handler.Enqueue(FakeHttpMessageHandler.Fixture("workitem-fields.json"));
        using HttpClient client = new(handler);
        Assert.Single(await new WorkItemService(client, Connection).GetWorkItemsAsync(OneId,
            CultureInfo.CurrentCulture, TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.Requests.Count);
        Assert.NotSame(handler.Requests[0].Request, handler.Requests[1].Request);
        Assert.Equal(handler.Requests[0].Body, handler.Requests[1].Body);
        Assert.All(handler.Requests, request => Assert.Equal("POST", request.Method));
    }

    [Fact]
    public async Task EmptyIdsDoNotRequestAndInvalidArgumentsFailLocally()
    {
        using FakeHttpMessageHandler handler = new();
        using HttpClient client = new(handler);
        WorkItemService service = new(client, Connection);
        Assert.Empty(await service.GetWorkItemsAsync(Array.Empty<int>(), CultureInfo.CurrentCulture, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetWorkItemsAsync(InvalidId, CultureInfo.CurrentCulture, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetWorkItemsAsync(OneId, CultureInfo.CurrentCulture, TestContext.Current.CancellationToken, TitleField, true));
        Assert.Empty(handler.Requests);
    }
}
