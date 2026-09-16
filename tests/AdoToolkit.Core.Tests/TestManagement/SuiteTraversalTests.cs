using System.Net.Http;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.TestManagement;

// Suite sibling order and testplan shapes are provisional [V-04].
[Trait("Acceptance", "S3-2")]
public sealed class SuiteTraversalTests
{
    private static readonly string[] ConnexionPath = ["Tâches de régression", "Connexion"];
    private static readonly string[] AccueilPath = ["Tâches de régression", "Connexion", "Écran d’accueil"];
    private static readonly string[] PipedPath = ["Piped", "Path"];

    [Fact]
    public async Task RecursionIsDepthFirstInSuiteOrderAndCasesFollowOrderAcrossPages()
    {
        BulkFixture fixture = new BulkFixture()
            .Page(813, BulkFixture.Entry(1100, 1))
            .Page(814, BulkFixture.Entry(1203, 3), BulkFixture.Entry(1201, 1))
            .Page(814, BulkFixture.Entry(1202, 2))
            .Page(816, BulkFixture.Entry(1201, 1))
            .Page(815, BulkFixture.Entry(1500, 1))
            .Page(817, BulkFixture.Entry(1700, 1));
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        CapturingLog log = new();
        IReadOnlyList<TestCaseMembership> memberships = await new SuiteMembershipService(client, BulkFixture.Connection, log).GetMembershipsAsync(
            new TestSuiteSelection { Project = BulkFixture.Project, PlanId = 812, SuiteId = 813, Recurse = true },
            CultureInfo.GetCultureInfo("fr-CA"), TestContext.Current.CancellationToken);

        // Tree order 813, 814, 816, 818, 815, 817; within 814 the order field wins over page order.
        Assert.Equal([1100, 1201, 1202, 1203, 1201, 1500, 1700], memberships.Select(item => item.TestCaseId));
        Assert.Equal([813, 814, 814, 814, 816, 815, 817], memberships.Select(item => item.Suite!.SuiteId));
        AdoTestSuiteRef connexion = memberships[1].Suite!;
        Assert.Equal(812, connexion.PlanId);
        Assert.Equal("Tâches de régression", connexion.PlanName);
        Assert.Equal("Connexion", connexion.SuiteName);
        Assert.Equal(ConnexionPath, connexion.SuitePath);
        Assert.Equal(AccueilPath, memberships[4].Suite!.SuitePath);
        Assert.Equal(BulkFixture.Project, connexion.TeamProject);
        Assert.Equal(BulkFixture.Connection.CollectionUri, connexion.CollectionUri);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_testPlans/define?planId=812", connexion.PlanWebUrl!.AbsoluteUri);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_testPlans/define?planId=812&suiteId=814", connexion.WebUrl!.AbsoluteUri);
        Assert.Same(memberships[1].Suite, memberships[3].Suite);

        string[] uris = handler.Requests.Select(request => request.Uri.AbsoluteUri).ToArray();
        Assert.Single(uris, uri => uri.Contains("/_apis/testplan/plans?", StringComparison.Ordinal));
        Assert.Single(uris, uri => uri.Contains("/suites?", StringComparison.Ordinal));
        Assert.Equal(2, uris.Count(uri => uri.Contains("/Plans/812/Suites/814/TestCase?", StringComparison.Ordinal)));
        Assert.Contains(uris, uri => uri.EndsWith("/Plans/812/Suites/814/TestCase?continuationToken=1&api-version=6.0-preview.2", StringComparison.Ordinal));
        Assert.All(handler.Requests, request => Assert.Equal("GET", request.Method));
        Assert.Equal(6, log.ProgressEvents.Count);
        Assert.All(log.ProgressEvents, item => Assert.Equal(AdoProgressPhase.Enumeration, item.Phase));
        Assert.Equal([1, 2, 3, 4, 5, 6], log.ProgressEvents.Select(item => item.Completed));
        Assert.All(log.ProgressEvents, item => Assert.Equal(6, item.Total));
    }

    [Fact]
    public async Task SelectionsOfOnePlanShareOnePlanListingAndOneTreeAndPipedSuitesNeedNoTree()
    {
        BulkFixture fixture = new BulkFixture().Page(814, BulkFixture.Entry(1201, 1)).Page(816, BulkFixture.Entry(1202, 1));
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        SuiteMembershipService service = new(client, BulkFixture.Connection);
        AdoTestSuite piped = new()
        {
            Id = 816, PlanId = 812, ParentSuiteId = 814, Name = "Piped name", SuitePath = ["Piped", "Path"],
            TeamProject = BulkFixture.Project, CollectionUri = BulkFixture.Connection.CollectionUri,
        };
        IReadOnlyList<TestCaseMembership> alone = await service.GetMembershipsAsync(
            new TestSuiteSelection { Project = BulkFixture.Project, PlanId = 812, SuiteId = 816, Suite = piped }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(handler.Requests, request => request.Uri.AbsolutePath.EndsWith("/suites", StringComparison.Ordinal));
        Assert.Equal("Piped name", Assert.Single(alone).Suite!.SuiteName);
        Assert.Equal(PipedPath, alone[0].Suite!.SuitePath);

        IReadOnlyList<TestCaseMembership> first = await service.GetMembershipsAsync(
            new TestSuiteSelection { Project = BulkFixture.Project, PlanId = 812, SuiteId = 814 }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        IReadOnlyList<TestCaseMembership> second = await service.GetMembershipsAsync(
            new TestSuiteSelection { Project = BulkFixture.Project, PlanId = 812, SuiteId = 814, Recurse = true }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal([1201], first.Select(item => item.TestCaseId));
        Assert.Equal([1201, 1202], second.Select(item => item.TestCaseId));
        Assert.Single(handler.Requests, request => request.Uri.AbsolutePath.EndsWith("/testplan/plans", StringComparison.Ordinal));
        Assert.Single(handler.Requests, request => request.Uri.AbsolutePath.EndsWith("/suites", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(999, 814, "TestPlansList")]
    [InlineData(812, 999, "TestSuitesForPlan")]
    public async Task MissingPlanOrSuiteIsNotFoundBeforeMembershipRequests(int planId, int suiteId, string operation)
    {
        using FakeHttpMessageHandler handler = new BulkFixture().Handler();
        using HttpClient client = new(handler);
        AdoNotFoundException error = await Assert.ThrowsAsync<AdoNotFoundException>(() => new SuiteMembershipService(client, BulkFixture.Connection)
            .GetMembershipsAsync(new TestSuiteSelection { Project = BulkFixture.Project, PlanId = planId, SuiteId = suiteId }, CultureInfo.GetCultureInfo("fr-CA"),
                TestContext.Current.CancellationToken));
        Assert.Equal(operation, error.Operation);
        Assert.Equal(planId == 999 ? Messages.Get(AdoMessage.TestPlanNotFound, CultureInfo.GetCultureInfo("fr-CA"), "999")
            : Messages.Get(AdoMessage.TestSuiteNotFound, CultureInfo.GetCultureInfo("fr-CA"), "999", "812"), error.Message);
        Assert.DoesNotContain(handler.Requests, request => request.Uri.AbsolutePath.EndsWith("/TestCase", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{"value":[{"order":1}]}""")]
    [InlineData("""{"value":[{"workItem":{"id":0},"order":1}]}""")]
    [InlineData("""{}""")]
    public async Task InvalidMembershipShapesAreFormatErrors(string body)
    {
        BulkFixture fixture = new();
        using FakeHttpMessageHandler handler = fixture.Handler();
        HttpClient client = new(handler, disposeHandler: false);
        using (client)
        {
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> fallback = handler.Fallback!;
            handler.Fallback = (request, token) => request.RequestUri!.AbsolutePath.EndsWith("/TestCase", StringComparison.Ordinal)
                ? Task.FromResult(FakeHttpMessageHandler.Response(body)) : fallback(request, token);
            AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() => new SuiteMembershipService(client, BulkFixture.Connection)
                .GetMembershipsAsync(new TestSuiteSelection { Project = BulkFixture.Project, PlanId = 812, SuiteId = 814 }, CultureInfo.InvariantCulture,
                    TestContext.Current.CancellationToken));
            Assert.Equal("SuiteTestCaseList", error.Operation);
        }
    }

    [Fact]
    public async Task ForeignOrInconsistentSuiteObjectsFailBeforeAnyRequest()
    {
        using FakeHttpMessageHandler handler = new BulkFixture().Handler();
        using HttpClient client = new(handler);
        SuiteMembershipService service = new(client, BulkFixture.Connection);
        AdoTestSuite foreign = new() { Id = 814, PlanId = 812, Name = "S", TeamProject = BulkFixture.Project, CollectionUri = new Uri("https://other.example.test/Collection") };
        await Assert.ThrowsAsync<AdoConnectionMismatchException>(() => service.GetMembershipsAsync(
            new TestSuiteSelection { Project = BulkFixture.Project, PlanId = 812, SuiteId = 814, Suite = foreign }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        AdoTestSuite local = new() { Id = 814, PlanId = 812, Name = "S", TeamProject = BulkFixture.Project, CollectionUri = BulkFixture.Connection.CollectionUri };
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetMembershipsAsync(
            new TestSuiteSelection { Project = BulkFixture.Project, PlanId = 812, SuiteId = 815, Suite = local }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetMembershipsAsync(
            new TestSuiteSelection { Project = BulkFixture.Project, PlanId = 812, SuiteId = 0 }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Empty(handler.Requests);
    }
}
