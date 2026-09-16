using System.Net.Http;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.TestManagement;

// Testplan-area wire shapes and preview versions are [V-04] assumptions.
[Trait("Acceptance", "S3-1")]
public sealed class TestPlanPagingTests
{
    private const string Collection = "https://ado.example.test/Collection";
    private const string Project = "Équipe Web";
    private static readonly AdoConnection Connection = new() { CollectionUri = new Uri(Collection), DefaultProject = "Wrong project" };
    private static readonly int[] PlanIds = [812, 820, 830];
    private static readonly int[] SuiteIds = [813, 814, 816, 818, 815, 817];
    private static readonly int[] CaseIds = [1203, 1201, 1202];
    private static readonly int[] CaseOrders = [1, 2, 3];
    private static readonly string[] NestedPath = ["Tâches de régression", "Connexion", "Écran d’accueil"];

    [Fact]
    public async Task PlansContinueThroughShortAndEmptyTokenPagesInOrder()
    {
        using FakeHttpMessageHandler handler = ThreePages("testplans-first.json", "testplans-last.json");
        using HttpClient client = new(handler);
        IReadOnlyList<AdoTestPlan> plans = await new TestPlanService(client, Connection)
            .GetPlansAsync(Project, CultureInfo.GetCultureInfo("fr-CA"), TestContext.Current.CancellationToken);
        Assert.Equal(PlanIds, plans.Select(plan => plan.Id));
        AssertTokenSequence(handler, Collection + "/%C3%89quipe%20Web/_apis/testplan/plans?api-version=6.0-preview.1");
        AdoTestPlan first = plans[0];
        Assert.Equal("Tâches de régression", first.Name);
        Assert.Equal(813, first.RootSuiteId);
        Assert.Equal(Project, first.TeamProject);
        Assert.Equal(Connection.CollectionUri, first.CollectionUri);
        // Links are constructed from the connection, never copied from response text.
        Assert.Equal(Collection + "/%C3%89quipe%20Web/_testPlans/define?planId=812", first.WebUrl!.AbsoluteUri);
        Assert.All(handler.Requests, request => Assert.Equal("fr-CA", request.Language));
    }

    [Fact]
    public async Task SuitesContinueThroughShortAndEmptyTokenPagesAndBuildTheTreeAcrossPages()
    {
        using FakeHttpMessageHandler handler = ThreePages("testsuites-first.json", "testsuites-last.json");
        using HttpClient client = new(handler);
        IReadOnlyList<AdoTestSuite> suites = await new TestSuiteService(client, Connection)
            .GetSuitesAsync(Project, 812, null, true, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(SuiteIds, suites.Select(suite => suite.Id));
        AssertTokenSequence(handler, Collection + "/%C3%89quipe%20Web/_apis/testplan/Plans/812/suites?api-version=6.0-preview.1");
        AdoTestSuite nested = suites[2];
        Assert.Equal(NestedPath, nested.SuitePath);
        Assert.Equal(814, nested.ParentSuiteId);
        Assert.Equal(812, nested.PlanId);
        Assert.Null(suites[0].ParentSuiteId);
        Assert.Equal(Collection + "/%C3%89quipe%20Web/_testPlans/define?planId=812&suiteId=816", nested.WebUrl!.AbsoluteUri);
    }

    [Fact]
    public async Task SuiteTestCasesContinueThroughShortAndEmptyTokenPagesInOrder()
    {
        using FakeHttpMessageHandler handler = ThreePages("suitetestcases-first.json", "suitetestcases-last.json");
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, Connection.CollectionUri, TimeSpan.FromSeconds(5));
        IReadOnlyList<SuiteTestCaseDto> cases = await pipeline.GetPagesAsync(EndpointRegistry.SuiteTestCaseList,
            AdoJsonContext.Default.SuiteTestCasePageDto, static page => page.Value, static item => item.WorkItem?.Id.ToString(CultureInfo.InvariantCulture) ?? "",
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken,
            new Dictionary<string, string> { ["project"] = Project, ["planId"] = "812", ["suiteId"] = "814" });
        Assert.Equal(CaseIds, cases.Select(item => item.WorkItem!.Id));
        Assert.Equal(CaseOrders, cases.Select(item => item.Order));
        AssertTokenSequence(handler, Collection + "/%C3%89quipe%20Web/_apis/testplan/Plans/812/Suites/814/TestCase?api-version=6.0-preview.2");
    }

    [Fact]
    public void TestplanEndpointsUseTheExactPinnedVersionsAndContinuationPaging()
    {
        Assert.Equal("6.0-preview.1", EndpointRegistry.TestPlansList.ApiVersion);
        Assert.Equal("6.0-preview.1", EndpointRegistry.TestSuitesForPlan.ApiVersion);
        Assert.Equal("6.0-preview.2", EndpointRegistry.SuiteTestCaseList.ApiVersion);
        foreach (EndpointDefinition endpoint in new[] { EndpointRegistry.TestPlansList, EndpointRegistry.TestSuitesForPlan, EndpointRegistry.SuiteTestCaseList })
        {
            Assert.Equal(PagingStrategy.ContinuationHeader, endpoint.Paging);
            Assert.Equal(HttpMethod.Get, endpoint.Method);
            Assert.True(endpoint.IsSafeToRetry);
        }
    }

    [Theory]
    [InlineData("{\"value\":[{\"id\":0,\"name\":\"P\",\"rootSuite\":{\"id\":2}}]}")]
    [InlineData("{\"value\":[{\"id\":1,\"name\":\"\",\"rootSuite\":{\"id\":2}}]}")]
    [InlineData("{\"value\":[{\"id\":1,\"name\":\"P\"}]}")]
    [InlineData("{\"value\":[{\"id\":1,\"name\":\"P\",\"rootSuite\":{\"id\":2}},{\"id\":1,\"name\":\"Q\",\"rootSuite\":{\"id\":3}}]}")]
    [InlineData("{\"value\":[null]}")]
    [InlineData("{}")]
    [InlineData("{\"value\":[{\"id\":\"1\",\"name\":\"P\",\"rootSuite\":{\"id\":2}}]}")]
    public async Task InvalidPlanShapesAreFormatErrors(string body)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(body));
        using HttpClient client = new(handler);
        AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() => new TestPlanService(client, Connection)
            .GetPlansAsync(Project, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Equal("TestPlansList", error.Operation);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RepeatedPlanTokenFailsInsteadOfLooping()
    {
        using FakeHttpMessageHandler handler = new();
        for (int i = 0; i < 2; i++)
        {
            HttpResponseMessage response = FakeHttpMessageHandler.Fixture(i == 0 ? "testplans-first.json" : "testplans-last.json");
            response.Headers.TryAddWithoutValidation("x-ms-continuationtoken", "same");
            handler.Enqueue(response);
        }
        using HttpClient client = new(handler);
        await Assert.ThrowsAsync<AdoResponseFormatException>(() => new TestPlanService(client, Connection)
            .GetPlansAsync(Project, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.Requests.Count);
    }

    internal static FakeHttpMessageHandler ThreePages(string first, string last)
    {
        FakeHttpMessageHandler handler = new();
        HttpResponseMessage page = FakeHttpMessageHandler.Fixture(first);
        page.Headers.TryAddWithoutValidation("x-ms-continuationtoken", "page 2+/=");
        handler.Enqueue(page);
        HttpResponseMessage empty = FakeHttpMessageHandler.Fixture("testplan-empty-page.json");
        empty.Headers.TryAddWithoutValidation("x-ms-continuationtoken", "page3");
        handler.Enqueue(empty);
        handler.Enqueue(FakeHttpMessageHandler.Fixture(last));
        return handler;
    }

    private static void AssertTokenSequence(FakeHttpMessageHandler handler, string firstUri)
    {
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(firstUri, handler.Requests[0].Uri.AbsoluteUri);
        Assert.Contains("continuationToken=page%202%2B%2F%3D", handler.Requests[1].Uri.Query, StringComparison.Ordinal);
        Assert.Contains("continuationToken=page3", handler.Requests[2].Uri.Query, StringComparison.Ordinal);
        Assert.All(handler.Requests, request => Assert.Equal("GET", request.Method));
    }
}
