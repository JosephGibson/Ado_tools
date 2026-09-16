using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.TestManagement;

[Trait("Acceptance", "S1-6")]
public sealed class SharedStepResolverRequestCountTests
{
    [Fact]
    public async Task MissingAndNonTestRootsReferencedByAnotherRootAreNotFetchedAgain()
    {
        JsonObject root = ExpansionFixture.Item(1, "<steps><compref ref=\"2\"/><compref ref=\"3\"/></steps>");
        JsonObject other = ExpansionFixture.Item(2, null, "Task");
        other["fields"]!.AsObject().Remove("Microsoft.VSTS.TCM.Steps");
        using var handler = ExpansionFixture.Handler(root, other);
        using HttpClient client = new(handler);
        TestCaseResult result = await new TestCaseService(client, ExpansionFixture.Connection, new()).GetTestCasesAsync(Values6,
            CultureInfo.CurrentCulture, TestContext.Current.CancellationToken);
        Assert.Equal(3, Assert.Single(result.MissingIds));
        Assert.Equal(2, Assert.Single(result.InputDiagnostics).WorkItemId);
        Assert.Equal(DiagnosticCodes.SharedStepHasNoSteps, Assert.Single(result.TestCases).Steps[0].DiagnosticCode);
        Assert.Equal(DiagnosticCodes.UnresolvedSharedStep, result.TestCases[0].Steps[1].DiagnosticCode);
        Assert.Single(handler.Requests, request => request.Method == "POST");
        Assert.Equal(2, handler.Requests.Count(request => request.Method == "GET"));
    }

    private static readonly int[] Values1 = [1, 2, 1];
    private static readonly int[] Values2 = [1, 2];
    private static readonly int[] Values3 = [3, 4];
    private static readonly int[] Values4 = [1, 200, 200, 50];
    private static readonly int[] Values5 = [1, 2, 3];
    private static readonly int[] Values6 = [1, 2, 3];
    [Fact]
    public async Task ThreeLevelsIssueExactlyFourBatchesAndNoCategoryRequests()
    {
        JsonArray rounds = JsonNode.Parse(ParserFixture.Read("Rest/shared-expansion-sequence.json"))!.AsArray();
        using FakeHttpMessageHandler handler = new();
        foreach (JsonNode? round in rounds) handler.Enqueue(FakeHttpMessageHandler.Response(round!.ToJsonString()));
        AdoTestCase result = await ExpansionFixture.Retrieve(handler);
        Assert.Equal(4, result.Steps.Count);
        Assert.Equal(4, handler.Requests.Count(request => request.Method == "POST"));
        Assert.DoesNotContain(handler.Requests, request => request.Method == "GET");
        for (int index = 0; index < 4; index++)
        {
            using JsonDocument body = JsonDocument.Parse(handler.Requests[index].Body!);
            Assert.Equal(index + 1, Assert.Single(body.RootElement.GetProperty("ids").EnumerateArray()).GetInt32());
            Assert.Equal("omit", body.RootElement.GetProperty("errorPolicy").GetString());
            Assert.Contains(body.RootElement.GetProperty("fields").EnumerateArray(), field => field.GetString() == "Microsoft.VSTS.TCM.Steps");
        }
    }

    [Fact]
    public async Task InvocationRootsAreSeededAndSharedParameterSetsJoinTheFirstRound()
    {
        JsonObject root = ExpansionFixture.Item(1, "<steps><compref ref=\"2\"/><compref ref=\"3\"/></steps>");
        root["fields"]!["Microsoft.VSTS.TCM.Parameters"] = "<parameters><param name=\"user\"/></parameters>";
        root["fields"]!["Microsoft.VSTS.TCM.LocalDataSource"] = "{\"user\":4}";
        JsonObject set = ExpansionFixture.Item(4, null);
        set["fields"]!["Microsoft.VSTS.TCM.Parameters"] = "<Data><Row><user>Zoé</user></Row></Data>";
        using var handler = ExpansionFixture.Handler(root, ExpansionFixture.Item(2, ExpansionFixture.Xml("01-direct.xml")),
            ExpansionFixture.Item(3, ExpansionFixture.Xml("01-direct.xml")), set);
        using HttpClient client = new(handler);
        TestCaseResult result = await new TestCaseService(client, ExpansionFixture.Connection, new()).GetTestCasesAsync(Values1,
            CultureInfo.CurrentCulture, TestContext.Current.CancellationToken);
        Assert.Equal(Values2, result.TestCases.Select(item => item.Id));
        Assert.Equal(2, handler.Requests.Count);
        using JsonDocument body = JsonDocument.Parse(handler.Requests[1].Body!);
        Assert.Equal(Values3, body.RootElement.GetProperty("ids").EnumerateArray().Select(id => id.GetInt32()));
        Assert.Equal("Zoé", Assert.Single(result.TestCases[0].Parameters.Rows)["user"]);
    }

    [Fact]
    public async Task DistinctReferencesAreChunkedAtTwoHundred()
    {
        string references = string.Concat(Enumerable.Range(2, 450).Select(id => FormattableString.Invariant($"<compref ref=\"{id}\"/>")));
        var children = Enumerable.Range(2, 450).Select(id => ExpansionFixture.Item(id, ExpansionFixture.Xml("01-direct.xml")));
        using var handler = ExpansionFixture.Handler(new[] { ExpansionFixture.Item(1, "<steps>" + references + "</steps>") }.Concat(children).ToArray());
        AdoTestCase result = await ExpansionFixture.Retrieve(handler);
        Assert.Equal(450, result.StepCount);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal(Values4, handler.Requests.Select(request =>
        {
            using JsonDocument body = JsonDocument.Parse(request.Body!);
            return body.RootElement.GetProperty("ids").GetArrayLength();
        }));
    }

    [Fact]
    public async Task CategoriesAreCountedSeparatelyAndEachCategoryIsFetchedOncePerProjectPerSession()
    {
        using var handler = ExpansionFixture.Handler();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("testcase-no-steps.json"));
        using HttpClient client = new(handler);
        SessionCache<IReadOnlyList<string>> cache = new();
        TestCaseService service = new(client, ExpansionFixture.Connection, cache);
        TestCaseResult first = await service.GetTestCasesAsync(Values5, CultureInfo.CurrentCulture, TestContext.Current.CancellationToken);
        handler.Enqueue(FakeHttpMessageHandler.Fixture("testcase-no-steps.json"));
        TestCaseResult second = await service.GetTestCasesAsync(Values6, CultureInfo.CurrentCulture, TestContext.Current.CancellationToken);
        Assert.Equal(2, first.TestCases.Count);
        Assert.Equal(2, second.TestCases.Count);
        Assert.Equal(DiagnosticCodes.NotAStepContainer, Assert.Single(first.InputDiagnostics).Code);
        Assert.All(first.TestCases, item => Assert.Equal(DiagnosticCodes.EmptySteps, Assert.Single(item.Diagnostics).Code));
        Assert.Equal(2, handler.Requests.Count(request => request.Method == "POST"));
        Assert.Equal(2, handler.Requests.Count(request => request.Method == "GET"));
        Assert.All(handler.Requests.Where(request => request.Method == "GET").GroupBy(request => request.Uri), group => Assert.Single(group));
        Assert.All(handler.Requests.Where(request => request.Method == "GET"), request =>
            Assert.StartsWith("https://ado.example.test/Collection/%C3%89quipe%20%2F%20Web/", request.Uri.AbsoluteUri, StringComparison.Ordinal));
    }
}
