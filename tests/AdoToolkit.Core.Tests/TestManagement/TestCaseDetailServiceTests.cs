using System.Net.Http;
using System.Text.Json.Nodes;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.TestRuns;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.TestManagement;

// The requests of Export-AdoTestCase -IncludeDetail. The shapes are assumptions until V-31 (fields
// and relation attributes of a Test Case) and V-32 (the test points query) pass at work.
public sealed class TestCaseDetailServiceTests
{
    private static readonly Uri Collection = new("https://ado.example.test/Collection/");
    private const string Project = "Équipe / Web";

    [Fact]
    public async Task ThreeRequestsReadTheFieldsLinksAndPointsOfATestCase()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("testcase-detail.json"));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("testcase-detail-linked.json"));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("testpoints.json"));
        TestCaseDetailResult result = await Get(handler, Case(3));

        Assert.Equal(3, handler.Requests.Count);
        // 1. The Test Case with its relations: no field list can be sent with them (V-10).
        Assert.Equal("POST", handler.Requests[0].Method);
        Assert.Equal("https://ado.example.test/Collection/_apis/wit/workitemsbatch?api-version=6.0", handler.Requests[0].Uri.AbsoluteUri);
        Assert.Equal("{\"ids\":[3],\"errorPolicy\":\"omit\",\"$expand\":\"relations\"}", handler.Requests[0].Body);
        // 2. The linked work items, once each, with the five fields that are shown.
        Assert.Equal("https://ado.example.test/Collection/_apis/wit/workitemsbatch?api-version=6.0", handler.Requests[1].Uri.AbsoluteUri);
        Assert.Equal("{\"ids\":[3001,3050,3099],\"errorPolicy\":\"omit\",\"fields\":[\"System.Id\",\"System.Title\",\"System.State\",\"System.WorkItemType\",\"System.TeamProject\"]}",
            handler.Requests[1].Body);
        // 3. The test points of the project of the Test Case.
        Assert.Equal("POST", handler.Requests[2].Method);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20%2F%20Web/_apis/test/points?%24top=1000&%24skip=0&api-version=6.0-preview.2",
            handler.Requests[2].Uri.AbsoluteUri);
        Assert.Equal("{\"pointsFilter\":{\"testcaseIds\":[3]}}", handler.Requests[2].Body);

        AdoTestCaseDetail detail = Assert.Single(result.Details).Value;
        Assert.True(detail.IsResolved);
        Assert.Equal("Checks the order form.\n- Uses the sandbox\n- Unsafe", detail.Description);
        Assert.StartsWith("<div><p>Checks the <b>order</b> form.</p>", detail.DescriptionSource, StringComparison.Ordinal);
        Assert.Equal(["Smoke", "Été <b>"], detail.Tags);
        Assert.Equal("Fictional Author", detail.CreatedBy!.DisplayName);
        Assert.Equal("author@example.test", detail.CreatedBy.UniqueName);
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 8, 30, 0, TimeSpan.Zero), detail.CreatedDate);
        Assert.Equal("Synthetic.Orders.OrderTests.Creates", detail.AutomatedTestName);
        Assert.Equal("Synthetic.Orders.Tests.dll", detail.AutomatedTestStorage);
        Assert.Equal("Unit Test", detail.AutomatedTestType);

        // Work item links of any type, without the Test Case itself, a repeated link or an unreadable URL.
        Assert.Equal([(3099, "Contoso.LinkTypes.Blocks-Forward", null), (3001, "System.LinkTypes.Related", "Related"), (3050, "Microsoft.VSTS.Common.TestedBy-Reverse", "Tests")],
            detail.Links.Select(static link => (link.Id, link.LinkType, link.LinkName)));
        AdoLinkedWorkItem bug = detail.Links[1];
        Assert.Equal(("Le total ignore la remise", "Closed", "Bug", "Autre / équipe", "See <also>", true), (bug.Title, bug.State, bug.WorkItemType, bug.TeamProject, bug.Comment, bug.IsResolved));
        // Links are built from the connection, the project that the work item reports and its ID.
        Assert.Equal("https://ado.example.test/Collection/Autre%20%2F%20%C3%A9quipe/_workitems/edit/3001", bug.WebUrl.AbsoluteUri);
        AdoLinkedWorkItem missing = detail.Links[0];
        Assert.False(missing.IsResolved);
        Assert.Null(missing.Title);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20%2F%20Web/_workitems/edit/3099", missing.WebUrl.AbsoluteUri);
        Assert.Equal([("https://wiki.example.test/orders", "Specification"), ("javascript:alert(1)", null)], detail.Hyperlinks.Select(static link => (link.Url, link.Comment)));
        AdoTestCaseAttachment attachment = Assert.Single(detail.Attachments);
        Assert.Equal(("capture <1>.png", 20480L, "Screen"), (attachment.Name, attachment.Size, attachment.Comment));

        Assert.Equal([1, 2, 3, 4], detail.Points!.Select(static point => point.Id));
        AdoTestPoint passed = detail.Points![0];
        Assert.Equal((812, 813, "Windows 11", "Passed", AdoTestOutcomeClass.Pass, true, 701, 100000), (passed.PlanId, passed.SuiteId, passed.ConfigurationName, passed.Outcome,
            passed.OutcomeClass, passed.HasRun, passed.LastRunId, passed.LastResultId));
        // Names that the response leaves out come from the suite that the case was retrieved through.
        Assert.Equal(("Plan retrieved", "Suite retrieved"), (passed.PlanName, passed.SuiteName));
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20%2F%20Web/_testPlans/define?planId=812&suiteId=813", passed.SuiteWebUrl.AbsoluteUri);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20%2F%20Web/_testManagement/runs?_a=runCharts&runId=701", passed.LastRunWebUrl!.AbsoluteUri);
        Assert.Equal(new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero), passed.LastUpdated);
        AdoTestPoint failed = detail.Points[1];
        Assert.Equal(("Plan « Été »", "Paiement <b>", "Failed", AdoTestOutcomeClass.Failure, "Fictional Tester", "tester@example.test"),
            (failed.PlanName, failed.SuiteName, failed.Outcome, failed.OutcomeClass, failed.Tester!.DisplayName, failed.Tester.UniqueName));
        // A point without plan and suite references is placed by the IDs in its path; odd values are dropped one by one.
        AdoTestPoint blocked = detail.Points[2];
        Assert.Equal((820, 821, "Blocked", AdoTestOutcomeClass.Other, true, "Fictional Text Tester"), (blocked.PlanId, blocked.SuiteId, blocked.Outcome, blocked.OutcomeClass, blocked.HasRun, blocked.Tester!.DisplayName));
        Assert.Null(blocked.LastRunId);
        Assert.Null(blocked.LastRunWebUrl);
        Assert.Null(blocked.LastUpdated);
        AdoTestPoint notRun = detail.Points[3];
        Assert.Equal((false, null, "Ready"), (notRun.HasRun, notRun.Outcome, notRun.State));
        Assert.Null(notRun.Tester);

        // The one problem: a linked work item that the batch did not return.
        AdoDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal((DiagnosticCodes.UnresolvedLinkedWorkItem, AdoDiagnosticSeverity.Warning, 3), (diagnostic.Code, diagnostic.Severity, diagnostic.WorkItemId));
        Assert.Equal(["3099"], diagnostic.Arguments);
        Assert.Same(diagnostic, Assert.Single(detail.Diagnostics));
        Assert.DoesNotContain("untrusted.example.test", string.Join(' ', detail.Links.Select(static link => link.WebUrl.AbsoluteUri)
            .Concat(detail.Points.Select(static point => point.PlanWebUrl.AbsoluteUri + point.SuiteWebUrl.AbsoluteUri + point.LastRunWebUrl?.AbsoluteUri))), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACaseInSeveralSuitesIsReadOnceAndCasesAreQueriedByProject()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(Batch(Item(3), Item(4), Item(5))));
        handler.Enqueue(FakeHttpMessageHandler.Response(Points(Point(1, 3, 812, 813, "Passed"), Point(2, 4, 812, 813, "Failed"))));
        handler.Enqueue(FakeHttpMessageHandler.Response(Points()));
        TestCaseDetailResult result = await Get(handler, Case(3), Case(4), Case(3), Case(5, "Other"));
        Assert.Equal("{\"ids\":[3,4,5],\"errorPolicy\":\"omit\",\"$expand\":\"relations\"}", handler.Requests[0].Body);
        // No link, so no second batch; one points query per project.
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(["/Collection/%C3%89quipe%20%2F%20Web/_apis/test/points", "/Collection/Other/_apis/test/points"], handler.Requests.Skip(1).Select(static request => request.Uri.AbsolutePath));
        Assert.Equal(["{\"pointsFilter\":{\"testcaseIds\":[3,4]}}", "{\"pointsFilter\":{\"testcaseIds\":[5]}}"], handler.Requests.Skip(1).Select(static request => request.Body));
        Assert.Equal([3, 4, 5], result.Details.Keys.Order());
        Assert.Equal([1], result.Details[3].Points!.Select(static point => point.Id));
        Assert.Equal([2], result.Details[4].Points!.Select(static point => point.Id));
        // In no suite of its project: an empty list, which is not the same as points that could not be read.
        Assert.Empty(result.Details[5].Points!);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task PointsArePagedUntilAShortPageAndInChunksOfFiftyCases()
    {
        using FakeHttpMessageHandler handler = new();
        AdoTestCase[] cases = Enumerable.Range(1, 51).Select(id => Case(id)).ToArray();
        handler.Enqueue(FakeHttpMessageHandler.Response(Batch(cases.Select(static item => Item(item.Id)).ToArray())));
        // A full page of 1,000 points, then a short one; then the second chunk.
        handler.Enqueue(FakeHttpMessageHandler.Response(Points(Enumerable.Range(1, 1000).Select(id => Point(id, 1 + (id % 50), 812, 813, "Passed")).ToArray())));
        handler.Enqueue(FakeHttpMessageHandler.Response(Points(Point(1001, 1, 812, 813, "Failed"))));
        handler.Enqueue(FakeHttpMessageHandler.Response(Points(Point(2000, 51, 812, 813, "Passed"))));
        TestCaseDetailResult result = await Get(handler, cases);
        Assert.Equal(["%24top=1000&%24skip=0", "%24top=1000&%24skip=1000", "%24top=1000&%24skip=0"],
            handler.Requests.Skip(1).Select(static request => request.Uri.Query.TrimStart('?').Replace("&api-version=6.0-preview.2", "", StringComparison.Ordinal)));
        Assert.Equal(50, JsonNode.Parse(handler.Requests[1].Body!)!["pointsFilter"]!["testcaseIds"]!.AsArray().Count);
        Assert.Equal("{\"pointsFilter\":{\"testcaseIds\":[51]}}", handler.Requests[3].Body);
        Assert.Equal(1002, result.Details.Values.Sum(static detail => detail.Points!.Count));
        Assert.Contains(result.Details[1].Points!, static point => point.Id == 1001);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task AServerThatIgnoresSkipEndsThePagingInsteadOfRepeatingPoints()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(Batch(Item(3))));
        string page = Points(Enumerable.Range(1, 1000).Select(id => Point(id, 3, 812, 813, "Passed")).ToArray());
        handler.Enqueue(FakeHttpMessageHandler.Response(page));
        handler.Enqueue(FakeHttpMessageHandler.Response(page));
        TestCaseDetailResult result = await Get(handler, Case(3));
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(1000, result.Details[3].Points!.Count);
    }

    [Theory]
    [InlineData(500, "{\"message\":\"synthetic\"}")]
    [InlineData(404, "{\"message\":\"synthetic\"}")]
    [InlineData(200, "{\"value\":[]}")]
    [InlineData(200, "{\"points\":null}")]
    [InlineData(200, "{\"points\":[null]}")]
    [InlineData(200, "{\"points\":[{\"id\":1,\"outcome\":\"Passed\",\"testCase\":{\"id\":\"3\"}}]}")]
    [InlineData(200, "{\"points\":[{\"id\":1,\"testCase\":{\"id\":\"999\"},\"testPlan\":{\"id\":\"1\"},\"suite\":{\"id\":\"2\"}}]}")]
    [InlineData(200, "{\"points\":[{\"id\":\"x\"}]}")]
    [InlineData(200, "not json")]
    public async Task PointsThatCannotBeReadOrPlacedAreAWarningAndTheRestIsKept(int status, string body)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(Batch(Item(3))));
        // One points request in every row: 500 is not retried.
        handler.Enqueue(FakeHttpMessageHandler.Response(body, status));
        TestCaseDetailResult result = await Get(handler, Case(3));
        Assert.Equal(2, handler.Requests.Count);
        AdoTestCaseDetail detail = result.Details[3];
        Assert.True(detail.IsResolved);
        // Unknown, not empty: the report leaves the section out instead of saying that there is no point.
        Assert.Null(detail.Points);
        AdoDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal((DiagnosticCodes.TestPointsUnavailable, AdoDiagnosticSeverity.Warning), (diagnostic.Code, diagnostic.Severity));
        Assert.Equal([Project], diagnostic.Arguments);
        Assert.Same(diagnostic, Assert.Single(detail.Diagnostics));
    }

    [Fact]
    public async Task AFailedDetailBatchLeavesTheFieldsOutAndStillReadsThePoints()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response("{\"message\":\"synthetic\"}", 400));
        handler.Enqueue(FakeHttpMessageHandler.Response(Points(Point(1, 3, 812, 813, "Passed"))));
        TestCaseDetailResult result = await Get(handler, Case(3));
        Assert.Equal(2, handler.Requests.Count);
        AdoTestCaseDetail detail = result.Details[3];
        Assert.False(detail.IsResolved);
        Assert.Null(detail.Description);
        Assert.Empty(detail.Links);
        Assert.Single(detail.Points!);
        Assert.Equal(DiagnosticCodes.TestCaseDetailUnavailable, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(DiagnosticCodes.TestCaseDetailUnavailable, Assert.Single(detail.Diagnostics).Code);
    }

    [Fact]
    public async Task ATestCaseThatTheBatchOmitsAndAFailedLinkBatchEachGetTheirWarning()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response(Batch(Item(3, Relation("System.LinkTypes.Related", 3001, "Related")))));
        handler.Enqueue(FakeHttpMessageHandler.Response("{\"message\":\"synthetic\"}", 404));
        handler.Enqueue(FakeHttpMessageHandler.Response(Points()));
        TestCaseDetailResult result = await Get(handler, Case(3), Case(4));
        Assert.Equal([DiagnosticCodes.UnresolvedTestCaseDetail, DiagnosticCodes.LinkedWorkItemsUnavailable], result.Diagnostics.Select(static diagnostic => diagnostic.Code));
        Assert.Equal(["4"], result.Diagnostics[0].Arguments);
        Assert.False(result.Details[4].IsResolved);
        Assert.Equal(DiagnosticCodes.UnresolvedTestCaseDetail, Assert.Single(result.Details[4].Diagnostics).Code);
        // The link is kept by its ID, through the project of its Test Case; a link that was not read is not reported as missing.
        AdoLinkedWorkItem link = Assert.Single(result.Details[3].Links);
        Assert.Equal((3001, false, "Related"), (link.Id, link.IsResolved, link.LinkName));
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20%2F%20Web/_workitems/edit/3001", link.WebUrl.AbsoluteUri);
        Assert.Equal(DiagnosticCodes.LinkedWorkItemsUnavailable, Assert.Single(result.Details[3].Diagnostics).Code);
    }

    [Theory]
    [InlineData(401, typeof(AdoAuthenticationException))]
    [InlineData(403, typeof(AdoAuthorizationException))]
    public async Task AuthenticationAndAuthorizationFailuresStopTheLookup(int status, Type expected)
    {
        foreach (int failing in new[] { 0, 1 })
        {
            using FakeHttpMessageHandler handler = new();
            if (failing == 1) handler.Enqueue(FakeHttpMessageHandler.Response(Batch(Item(3))));
            handler.Enqueue(FakeHttpMessageHandler.Response("{\"message\":\"synthetic\"}", status));
            await Assert.ThrowsAsync(expected, () => Get(handler, Case(3)));
        }
    }

    [Fact]
    public async Task CancellationAndAForeignCaseAreNotTurnedIntoWarnings()
    {
        using FakeHttpMessageHandler handler = new();
        using CancellationTokenSource cancellation = new();
        handler.Enqueue((_, _) => { cancellation.Cancel(); return Task.FromResult(FakeHttpMessageHandler.Response(Batch(Item(3)))); });
        using HttpClient client = new(handler);
        TestCaseDetailService service = new(client, new AdoConnection { CollectionUri = Collection });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync([Case(3)], CultureInfo.InvariantCulture, cancellation.Token));
        AdoTestCase foreign = new()
        {
            Id = 9, Title = "Foreign", WorkItemType = "Test Case", TeamProject = Project, State = "Ready",
            WebUrl = Collection, CollectionUri = new Uri("https://other.example.test/Collection/"),
        };
        await Assert.ThrowsAsync<AdoConnectionMismatchException>(() => service.GetAsync([foreign], CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task WarningsAreWrittenInTheRequestedCulture()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response("{\"message\":\"synthetic\"}", 400));
        handler.Enqueue(FakeHttpMessageHandler.Response("{\"message\":\"synthetic\"}", 400));
        using HttpClient client = new(handler);
        TestCaseDetailResult result = await new TestCaseDetailService(client, new AdoConnection { CollectionUri = Collection })
            .GetAsync([Case(3)], CultureInfo.GetCultureInfo("fr-CA"), TestContext.Current.CancellationToken);
        Assert.StartsWith("Les détails des cas de test n’ont pas pu être lus", result.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.StartsWith("Les points de test du projet Équipe / Web n’ont pas pu être lus", result.Diagnostics[1].Message, StringComparison.Ordinal);
        Assert.Equal("fr-CA", handler.Requests[0].Language);
    }

    private static async Task<TestCaseDetailResult> Get(FakeHttpMessageHandler handler, params AdoTestCase[] cases)
    {
        using HttpClient client = new(handler);
        return await new TestCaseDetailService(client, new AdoConnection { CollectionUri = Collection })
            .GetAsync(cases, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
    }

    private static AdoTestCase Case(int id, string project = Project) => new()
    {
        Id = id, Rev = 3, Title = "Synthetic " + id.ToString(CultureInfo.InvariantCulture), WorkItemType = "Test Case", TeamProject = project, State = "Ready",
        WebUrl = Collection, CollectionUri = Collection,
        Suite = new AdoTestSuiteRef { PlanId = 812, SuiteId = 813, PlanName = "Plan retrieved", SuiteName = "Suite retrieved", TeamProject = project, CollectionUri = Collection },
    };

    private static string Batch(params JsonObject[] items) => new JsonObject { ["value"] = new JsonArray([.. items]) }.ToJsonString();

    private static JsonObject Item(int id, params JsonObject[] relations) => new()
    {
        ["id"] = id, ["rev"] = 3,
        ["fields"] = new JsonObject { ["System.Title"] = "Synthetic", ["System.TeamProject"] = Project },
        ["relations"] = new JsonArray([.. relations]),
    };

    private static JsonObject Relation(string rel, int id, string name) => new()
    {
        ["rel"] = rel, ["url"] = "https://ado.example.test/Collection/_apis/wit/workItems/" + id.ToString(CultureInfo.InvariantCulture),
        ["attributes"] = new JsonObject { ["name"] = name },
    };

    private static string Points(params JsonObject[] points) => new JsonObject { ["points"] = new JsonArray([.. points]) }.ToJsonString();

    private static JsonObject Point(int id, int testCase, int plan, int suite, string outcome) => new()
    {
        ["id"] = id, ["outcome"] = outcome, ["testCase"] = new JsonObject { ["id"] = testCase.ToString(CultureInfo.InvariantCulture) },
        ["testPlan"] = new JsonObject { ["id"] = plan.ToString(CultureInfo.InvariantCulture) }, ["suite"] = new JsonObject { ["id"] = suite.ToString(CultureInfo.InvariantCulture) },
    };
}
