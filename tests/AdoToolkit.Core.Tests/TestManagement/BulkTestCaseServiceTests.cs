using System.Net.Http;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.TestManagement;

[Trait("Acceptance", "S3-2")]
public sealed class BulkTestCaseServiceTests
{
    private const string SharedReference = "<steps><compref id=\"5\" ref=\"2001\"/><step id=\"6\" type=\"ActionStep\"><parameterizedString>After</parameterizedString><parameterizedString/></step></steps>";
    private static readonly int[] RootBatch = [1201, 1203, 1202];
    private static readonly int[] SharedBatch = [2001];
    private static readonly int[] EmittedIds = [1201, 1203, 1201, 1202];
    private static readonly int?[] EmittedSuites = [814, 814, 816, null];
    private static readonly int[] ExpansionCounts = [1, 2, 3];
    private static readonly string[] Numbers = ["1", "1.1", "2"];

    [Fact]
    public async Task CaseInTwoSuitesIsFetchedAndExpandedOnceAndEmittedPerMembership()
    {
        BulkFixture fixture = new BulkFixture()
            .Page(814, BulkFixture.Entry(1201, 1), BulkFixture.Entry(1203, 2))
            .Page(816, BulkFixture.Entry(1201, 1))
            .Item(ExpansionFixture.Item(1201, SharedReference)).Item(ExpansionFixture.Item(1203, SharedReference))
            .Item(ExpansionFixture.Item(1202, ExpansionFixture.Xml("01-direct.xml")))
            .Item(ExpansionFixture.Item(2001, ExpansionFixture.Xml("01-direct.xml")));
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        CapturingLog log = new();
        SuiteMembershipService suites = new(client, BulkFixture.Connection, log);
        List<TestCaseMembership> memberships = [];
        foreach (int suiteId in new[] { 814, 816 })
            memberships.AddRange(await suites.GetMembershipsAsync(new TestSuiteSelection { Project = BulkFixture.Project, PlanId = 812, SuiteId = suiteId },
                CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        memberships.Add(new TestCaseMembership { TestCaseId = 1202 });
        log.ProgressEvents.Clear();

        TestCaseResult result = await new TestCaseService(client, BulkFixture.Connection, new(), log)
            .GetTestCasesForMembershipsAsync(memberships, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);

        RequestSnapshot[] batches = handler.Requests.Where(request => request.Method == "POST").ToArray();
        Assert.Equal(2, batches.Length);
        Assert.Equal(RootBatch, BulkFixture.BatchIds(batches[0]));
        Assert.Equal(SharedBatch, BulkFixture.BatchIds(batches[1]));
        Assert.Equal(EmittedIds, result.TestCases.Select(item => item.Id));
        Assert.Equal(EmittedSuites, result.TestCases.Select(item => item.Suite?.SuiteId));
        Assert.NotSame(result.TestCases[0], result.TestCases[2]);
        Assert.Same(result.TestCases[0].Steps, result.TestCases[2].Steps);
        Assert.Equal(Numbers, result.TestCases[2].Steps.Select(step => step.Number));
        Assert.Equal(2, result.TestCases[2].StepCount);
        Assert.Equal("Connexion", result.TestCases[0].Suite!.SuiteName);
        Assert.Equal("Écran d’accueil", result.TestCases[2].Suite!.SuiteName);
        Assert.All(result.TestCases, item => Assert.Equal(result.TestCases.First(other => other.Id == item.Id).RetrievedAt, item.RetrievedAt));
        Assert.Empty(result.MissingIds);

        Assert.Equal(AdoProgressPhase.Fetch, log.ProgressEvents[0].Phase);
        Assert.Equal((1, (int?)1), (log.ProgressEvents[0].Completed, log.ProgressEvents[0].Total));
        Assert.Equal((2, (int?)null), (log.ProgressEvents[1].Completed, log.ProgressEvents[1].Total));
        AdoProgress[] expansion = log.ProgressEvents.Where(item => item.Phase == AdoProgressPhase.Expansion).ToArray();
        Assert.Equal(ExpansionCounts, expansion.Select(item => item.Completed));
        Assert.All(expansion, item => Assert.Equal(3, item.Total));
    }

    [Fact]
    public async Task MissingAndRejectedRootsAreSkippedInEveryMembershipAndReportedOnce()
    {
        System.Text.Json.Nodes.JsonObject task = ExpansionFixture.Item(1203, null, "Task");
        task["fields"]!.AsObject().Remove("Microsoft.VSTS.TCM.Steps");
        BulkFixture fixture = new BulkFixture().Item(ExpansionFixture.Item(1201, ExpansionFixture.Xml("01-direct.xml"))).Item(task);
        using FakeHttpMessageHandler handler = fixture.Handler();
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> fallback = handler.Fallback!;
        handler.Fallback = (request, token) => request.RequestUri!.AbsolutePath.Contains("workitemtypecategories", StringComparison.Ordinal)
            ? Task.FromResult(FakeHttpMessageHandler.Fixture("test-category.json")) : fallback(request, token);
        using HttpClient client = new(handler);
        AdoTestSuiteRef first = Suite(814);
        AdoTestSuiteRef second = Suite(816);
        TestCaseMembership[] memberships =
        [
            new() { TestCaseId = 1299, Suite = first }, new() { TestCaseId = 1201, Suite = first }, new() { TestCaseId = 1203, Suite = first },
            new() { TestCaseId = 1299, Suite = second }, new() { TestCaseId = 1201, Suite = second },
        ];
        TestCaseResult result = await new TestCaseService(client, BulkFixture.Connection, new())
            .GetTestCasesForMembershipsAsync(memberships, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { first, second }, result.TestCases.Select(item => item.Suite));
        Assert.Equal(1299, Assert.Single(result.MissingIds));
        Assert.Equal(1203, Assert.Single(result.InputDiagnostics).WorkItemId);
        Assert.Single(handler.Requests, request => request.Method == "POST");
    }

    [Fact]
    public async Task MismatchedSuiteProvenanceOrInvalidIdsFailBeforeAnyRequest()
    {
        using FakeHttpMessageHandler handler = new BulkFixture().Handler();
        using HttpClient client = new(handler);
        TestCaseService service = new(client, BulkFixture.Connection, new());
        AdoTestSuiteRef foreign = new()
        {
            PlanId = 1, SuiteId = 2, PlanName = "P", SuiteName = "S", TeamProject = "T", CollectionUri = new Uri("https://other.example.test/Collection"),
        };
        await Assert.ThrowsAsync<AdoConnectionMismatchException>(() => service.GetTestCasesForMembershipsAsync(
            [new TestCaseMembership { TestCaseId = 1, Suite = foreign }], CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetTestCasesForMembershipsAsync(
            [new TestCaseMembership { TestCaseId = 0 }], CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }

    private static AdoTestSuiteRef Suite(int suiteId) => new()
    {
        PlanId = 812, SuiteId = suiteId, PlanName = "Plan", SuiteName = "Suite " + suiteId.ToString(CultureInfo.InvariantCulture),
        TeamProject = BulkFixture.Project, CollectionUri = BulkFixture.Connection.CollectionUri,
    };
}
