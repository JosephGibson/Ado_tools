using System.Net.Http;
using System.Text.Json.Nodes;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.TestManagement;

// Sibling order comes from the server response and is provisional [V-04].
public sealed class TestSuiteServiceTests
{
    private static readonly AdoConnection Connection = new() { CollectionUri = new Uri("https://ado.example.test/Collection") };

    [Theory]
    [InlineData(null, false, "813")]
    [InlineData(null, true, "813,814,816,818,815,817")]
    [InlineData(814, false, "814")]
    [InlineData(814, true, "814,816,818")]
    [InlineData(817, true, "817")]
    public async Task SelectsTheStartSuiteOrItsDepthFirstSubtree(int? suiteId, bool recurse, string expected)
    {
        IReadOnlyList<AdoTestSuite> suites = await Fetch(suiteId, recurse);
        Assert.Equal(expected, string.Join(',', suites.Select(suite => suite.Id.ToString(CultureInfo.InvariantCulture))));
        Assert.All(suites, suite =>
        {
            Assert.Equal(suite.ParentSuiteId is null, suite.SuitePath.Count == 1);
            Assert.Equal(suite.Name, suite.SuitePath[^1]);
            Assert.Equal("Tâches de régression", suite.SuitePath[0]);
        });
    }

    [Fact]
    public async Task MissingSuiteIsNotFoundAfterOneListing()
    {
        using FakeHttpMessageHandler handler = TestPlanPagingTests.ThreePages("testsuites-first.json", "testsuites-last.json");
        using HttpClient client = new(handler);
        AdoNotFoundException error = await Assert.ThrowsAsync<AdoNotFoundException>(() => new TestSuiteService(client, Connection)
            .GetSuitesAsync("Équipe Web", 812, 999, true, CultureInfo.GetCultureInfo("fr-CA"), TestContext.Current.CancellationToken));
        Assert.Equal(Messages.Get(AdoMessage.TestSuiteNotFound, CultureInfo.GetCultureInfo("fr-CA"), "999", "812"), error.Message);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Theory]
    [InlineData("""{"value":[{"id":1,"name":"Root"},{"id":2,"name":"Child","parentSuite":{"id":9}}]}""")]
    [InlineData("""{"value":[{"id":1,"name":"Root"},{"id":2,"name":"A","parentSuite":{"id":3}},{"id":3,"name":"B","parentSuite":{"id":2}}]}""")]
    [InlineData("""{"value":[{"id":1,"name":"Root"},{"id":1,"name":"Again"}]}""")]
    [InlineData("""{"value":[{"id":1,"name":""}]}""")]
    [InlineData("""{"value":[{"id":1,"name":"Root","parentSuite":{"id":0}}]}""")]
    [InlineData("""{"value":[{"id":0,"name":"Root"}]}""")]
    public async Task InconsistentTreesAreFormatErrors(string body)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(body));
        using HttpClient client = new(handler);
        AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() => new TestSuiteService(client, Connection)
            .GetSuitesAsync("P", 1, null, true, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Equal("TestSuitesForPlan", error.Operation);
    }

    [Fact]
    public async Task DeepTreesAreTraversedWithoutRecursion()
    {
        JsonArray values = [new JsonObject { ["id"] = 1, ["name"] = "S1" }];
        for (int id = 2; id <= 5000; id++)
            values.Add(new JsonObject { ["id"] = id, ["name"] = "S" + id.ToString(CultureInfo.InvariantCulture), ["parentSuite"] = new JsonObject { ["id"] = id - 1 } });
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(new JsonObject { ["value"] = values }.ToJsonString()));
        using HttpClient client = new(handler);
        IReadOnlyList<AdoTestSuite> suites = await new TestSuiteService(client, Connection)
            .GetSuitesAsync("P", 1, 4990, true, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(Enumerable.Range(4990, 11), suites.Select(suite => suite.Id));
        Assert.Equal(5000, suites[^1].SuitePath.Count);
    }

    [Fact]
    public async Task InvalidArgumentsFailBeforeAnyRequest()
    {
        using FakeHttpMessageHandler handler = new();
        using HttpClient client = new(handler);
        TestSuiteService service = new(client, Connection);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetSuitesAsync("P", 0, null, false, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetSuitesAsync("P", 1, 0, false, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetSuitesAsync(" ", 1, null, false, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    private static async Task<IReadOnlyList<AdoTestSuite>> Fetch(int? suiteId, bool recurse)
    {
        using FakeHttpMessageHandler handler = TestPlanPagingTests.ThreePages("testsuites-first.json", "testsuites-last.json");
        using HttpClient client = new(handler);
        return await new TestSuiteService(client, Connection)
            .GetSuitesAsync("Équipe Web", 812, suiteId, recurse, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
    }
}
