using System.Net.Http;
using System.Text.Json.Nodes;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.Tests.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

// Bugs of reported tests: associated with a result, or of a Bug-category type linked to the Test Case
// by any work item link. Open means the state category is neither Completed nor Removed.
public sealed class TestBugResolutionTests
{
    private const string CustomType = "/workitemtypes/D%C3%A9faut%20de%20production/states";
    private const string CustomStates = """{"count":2,"value":[{"name":"Nouveau","color":"b2b2b2","category":"Proposed"},{"name":"Livré","color":"339933","category":"Completed"}]}""";

    [Fact]
    public async Task BugLinkedOnlyToTheTestCaseIsFoundThroughAnyWorkItemLinkType()
    {
        TestRunFixture fixture = Scenario(Detail(101, "1010"), Detail(102), Detail(103))
            .RouteBatch(TestRunFixture.BugBatch, Items(Catalog(3001, 3002, 3050, 3060, 3080)))
            .Route("workitemtypecategory-bug.json", "/workitemtypecategories/Microsoft.BugCategory")
            .Route("workitemtype-states-bug.json", "/workitemtypes/Bug/states")
            .RouteBody(CustomStates, CustomType);
        using FakeHttpMessageHandler handler = fixture.Handler();
        AdoBuildTestFailureSet set = await RetrieveAsync(handler);
        AdoTestFailure valid = Failure(set, "Valid");
        // The User Story and Shared Steps are linked too, but only Bug-category types are bugs.
        Assert.Equal([3001, 3002, 3080], valid.Bugs.Select(static bug => bug.Id));
        AdoTestBug tested = valid.Bugs[0];
        Assert.Equal("Le total ignore la remise", tested.Title);
        Assert.Equal("Active", tested.State);
        Assert.Equal("Bug", tested.WorkItemType);
        Assert.Equal("Équipe Web", tested.TeamProject);
        Assert.Equal("InProgress", tested.StateCategory);
        Assert.True(tested.IsOpen);
        Assert.True(tested.IsResolved);
        Assert.True(tested.IsLinkedToTestCase);
        Assert.False(tested.IsAssociatedWithResult);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_workitems/edit/3001", tested.WebUrl.AbsoluteUri);
        Assert.False(valid.Bugs[1].IsOpen);
        Assert.Equal("Completed", valid.Bugs[1].StateCategory);
        // A custom link type to a custom type in the Bug category still counts.
        Assert.Equal("Défaut de production", valid.Bugs[2].WorkItemType);
        Assert.Equal("Proposed", valid.Bugs[2].StateCategory);
        Assert.True(valid.Bugs[2].IsOpen);
        Assert.True(valid.HasOpenBug);
        Assert.All(set.Failures.Where(static failure => failure.ShortName != "Valid"), static failure => Assert.Empty(failure.Bugs));
        Assert.Empty(set.Diagnostics);
        // Relations come with the Test Case read, which cannot also name fields.
        string testCases = Assert.Single(handler.Requests, static request => request.Body?.Contains(TestRunFixture.TestCaseBatch, StringComparison.Ordinal) == true).Body!;
        Assert.DoesNotContain("\"fields\"", testCases, StringComparison.Ordinal);
        // Hyperlinks, artifact links, attachments and unreadable URLs are never requested.
        string bugs = Assert.Single(handler.Requests, static request => request.Body?.Contains(TestRunFixture.BugBatch, StringComparison.Ordinal) == true).Body!;
        Assert.Contains("\"ids\":[3001,3002,3050,3060,3080]", bugs, StringComparison.Ordinal);
        Assert.Contains("\"fields\":[\"System.Id\",\"System.Title\",\"System.State\",\"System.WorkItemType\",\"System.TeamProject\"]", bugs, StringComparison.Ordinal);
        Assert.Single(handler.Requests, static request => request.Uri.AbsolutePath.EndsWith("/workitemtypecategories/Microsoft.BugCategory", StringComparison.Ordinal));
        Assert.All(handler.Requests.Where(static request => request.Uri.AbsolutePath.EndsWith("/states", StringComparison.Ordinal)),
            static request => Assert.EndsWith("api-version=6.0-preview.1", request.Uri.Query, StringComparison.Ordinal));
    }

    [Fact]
    public async Task BugFoundOnlyOnTheTestResultIsKeptWithoutATypeCheck()
    {
        TestRunFixture fixture = Scenario(Detail(101, null, 2001, 2002, 2001), Detail(102), Detail(103)).RouteBugs();
        using FakeHttpMessageHandler handler = fixture.Handler();
        AdoBuildTestFailureSet set = await RetrieveAsync(handler);
        AdoTestFailure valid = Failure(set, "Valid");
        Assert.Equal([2001, 2002], valid.Bugs.Select(static bug => bug.Id));
        AdoTestBug open = valid.Bugs[0];
        Assert.Equal("Le panier perd un article", open.Title);
        Assert.True(open.IsAssociatedWithResult);
        Assert.False(open.IsLinkedToTestCase);
        Assert.True(open.IsOpen);
        Assert.True(valid.HasOpenBug);
        Assert.Null(valid.TestCase);
        Assert.Empty(set.Diagnostics);
        // With no linked work items there is nothing to check against the Bug category.
        Assert.DoesNotContain(handler.Requests, static request => request.Uri.AbsolutePath.Contains("/workitemtypecategories/", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Requests, static request => request.Body?.Contains(TestRunFixture.TestCaseBatch, StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ClosedAndRemovedCategoriesAreNotOpenWhateverTheStateName()
    {
        const string States = """{"count":4,"value":[{"name":"Nouveau","category":"Proposed"},{"name":"Corrigé","category":"Completed"},{"name":"Obsolète","category":"Removed"},{"name":"Vérifié","category":"Resolved"}]}""";
        TestRunFixture fixture = Scenario(Detail(101, null, 4001, 4002), Detail(102, null, 4003), Detail(103))
            .RouteBatch(TestRunFixture.BugBatch, Items(Item(4001, "Corrigé hier", "Corrigé"), Item(4002, "Doublon", "Obsolète"),
                Item(4003, "Vérifié en recette", "Vérifié")))
            .RouteBody(States, "/workitemtypes/Bug/states");
        using FakeHttpMessageHandler handler = fixture.Handler();
        AdoBuildTestFailureSet set = await RetrieveAsync(handler);
        AdoTestFailure valid = Failure(set, "Valid");
        Assert.Equal(["Completed", "Removed"], valid.Bugs.Select(static bug => bug.StateCategory));
        Assert.All(valid.Bugs, static bug => Assert.False(bug.IsOpen));
        Assert.False(valid.HasOpenBug);
        // Resolved is not Completed, so a resolved bug is still open.
        AdoTestBug resolved = Assert.Single(Failure(set, "Missing").Bugs);
        Assert.Equal("Resolved", resolved.StateCategory);
        Assert.True(resolved.IsOpen);
        Assert.Empty(set.Diagnostics);

        TestFailureReportModel model = TestFailureReportModelBuilder.Build(set, TestFailureReportFixture.Options("en-US"));
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        // The overview and by-error rows mark only the test with an open bug.
        Assert.Equal(2, Count(html, ">Missing</a> <a class=\"open-bug-marker\" rel=\"noreferrer\" href=\"https://ado.example.test/Collection/%C3%89quipe%20Web/_workitems/edit/4003\">Open bug</a>"));
        Assert.Equal(2, Count(html, "class=\"open-bug-marker\""));
        Assert.Contains("<li data-bug=\"4001\"><a rel=\"noreferrer\" href=\"https://ado.example.test/Collection/%C3%89quipe%20Web/_workitems/edit/4001\">#4001",
            html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bug-title\">Corrigé hier</span> <span class=\"bug-state\">Corrigé</span></li>", html, StringComparison.Ordinal);
        Assert.Contains("<li data-bug=\"4003\" data-open-bug>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bug-state\">Vérifié</span> <span class=\"bug-open\">Open</span></li>", html, StringComparison.Ordinal);
        Assert.Equal(1, Count(html, "class=\"bug-open\""));
    }

    [Fact]
    public async Task FailedBugLookupAddsAWarningAndTheReportStillRenders()
    {
        TestRunFixture fixture = Scenario(Detail(101, "1010", 2001), Detail(102), Detail(103))
            .RouteBatch(TestRunFixture.BugBatch, "{\"message\":\"Synthetic server failure.\"}", 500);
        using FakeHttpMessageHandler handler = fixture.Handler();
        AdoBuildTestFailureSet set = await RetrieveAsync(handler);
        AdoDiagnostic diagnostic = Assert.Single(set.Diagnostics);
        Assert.Equal(DiagnosticCodes.BugLookupFailed, diagnostic.Code);
        Assert.Equal(AdoDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(["401"], diagnostic.Arguments);
        Assert.Equal(AdoTestFailureStatus.Complete, set.Status);
        Assert.Equal(3, set.Failures.Count);
        AdoTestFailure valid = Failure(set, "Valid");
        Assert.True(valid.TestCase!.IsResolved);
        // The associated ID keeps its link; linked work items cannot be told apart from bugs.
        AdoTestBug kept = Assert.Single(valid.Bugs);
        Assert.Equal(2001, kept.Id);
        Assert.False(kept.IsResolved);
        Assert.Null(kept.IsOpen);
        Assert.Null(kept.Title);
        Assert.Null(kept.State);
        Assert.True(kept.IsAssociatedWithResult);
        Assert.False(valid.HasOpenBug);
        Assert.DoesNotContain(handler.Requests, static request => request.Uri.AbsolutePath.Contains("/workitemtype", StringComparison.Ordinal));

        TestFailureReportModel model = TestFailureReportModelBuilder.Build(set, TestFailureReportFixture.Options("fr-CA"));
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.Contains("data-diagnostic=\"BugLookupFailed\"", html, StringComparison.Ordinal);
        Assert.Contains("Les bogues des tests de la build 401 n’ont pas pu être lus", html, StringComparison.Ordinal);
        Assert.Contains("<li data-bug=\"2001\"><a rel=\"noreferrer\" href=\"https://ado.example.test/Collection/%C3%89quipe%20Web/_workitems/edit/2001\">#2001",
            html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"open-bug-marker\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"bug-open\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-open-bug", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnreadableMetadataFallsBackToTheBugNameAndDefaultClosedStates()
    {
        TestRunFixture fixture = Scenario(Detail(101, "1010"), Detail(102), Detail(103))
            .RouteBatch(TestRunFixture.BugBatch, Items(Catalog(3001, 3002, 3050, 3060, 3080)))
            .RouteStatus(404, "{\"message\":\"Not found.\"}", "/workitemtypecategories/")
            .RouteStatus(404, "{\"message\":\"Not found.\"}", "/workitemtypes/");
        using FakeHttpMessageHandler handler = fixture.Handler();
        AdoBuildTestFailureSet set = await RetrieveAsync(handler);
        AdoTestFailure valid = Failure(set, "Valid");
        // Only the type named Bug counts, so the custom bug type is not recognised.
        Assert.Equal([3001, 3002], valid.Bugs.Select(static bug => bug.Id));
        Assert.All(valid.Bugs, static bug => Assert.Null(bug.StateCategory));
        Assert.True(valid.Bugs[0].IsOpen);
        Assert.False(valid.Bugs[1].IsOpen);
        AdoDiagnostic diagnostic = Assert.Single(set.Diagnostics);
        Assert.Equal(DiagnosticCodes.BugMetadataUnavailable, diagnostic.Code);
        Assert.Equal(AdoDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(["Équipe Web"], diagnostic.Arguments);
        Assert.Equal(AdoTestFailureStatus.Complete, set.Status);
    }

    [Fact]
    public async Task UnreadableAssociatedBugKeepsItsLinkWithAWarning()
    {
        TestRunFixture fixture = Scenario(Detail(101, null, 2001, 2003), Detail(102), Detail(103))
            .RouteBatch(TestRunFixture.BugBatch, Items(Catalog(2001)))
            .Route("workitemtype-states-bug.json", "/workitemtypes/Bug/states");
        using FakeHttpMessageHandler handler = fixture.Handler();
        AdoBuildTestFailureSet set = await RetrieveAsync(handler);
        AdoTestFailure valid = Failure(set, "Valid");
        Assert.Equal([2001, 2003], valid.Bugs.Select(static bug => bug.Id));
        AdoTestBug missing = valid.Bugs[1];
        Assert.False(missing.IsResolved);
        Assert.Null(missing.IsOpen);
        Assert.Null(missing.TeamProject);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_workitems/edit/2003", missing.WebUrl.AbsoluteUri);
        AdoDiagnostic diagnostic = Assert.Single(set.Diagnostics);
        Assert.Equal(DiagnosticCodes.UnresolvedBug, diagnostic.Code);
        Assert.Equal(2003, diagnostic.WorkItemId);
        Assert.Equal(["2003"], diagnostic.Arguments);
    }

    [Fact]
    public async Task BugsOfEveryTestTravelInOneBatchAndMetadataIsReadOncePerType()
    {
        TestRunFixture fixture = Scenario(Detail(101, "1010", 2001), Detail(102, null, 2001, 2002), Detail(103, "1010"))
            .RouteBatch(TestRunFixture.BugBatch, Items(Catalog(2001, 2002, 3001, 3002, 3050, 3060, 3080)))
            .Route("workitemtypecategory-bug.json", "/workitemtypecategories/Microsoft.BugCategory")
            .Route("workitemtype-states-bug.json", "/workitemtypes/Bug/states")
            .RouteBody(CustomStates, CustomType);
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        TestFailureRetrievalService service = TestRunFixture.Service(client);
        AdoBuildTestFailureSet set = await service.GetAsync(TestRunFixture.Build(), new TestFailureQuery { HistoryCount = 1 },
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal([2001, 3001, 3002, 3080], Failure(set, "Valid").Bugs.Select(static bug => bug.Id));
        Assert.Equal([2001, 2002], Failure(set, "Missing").Bugs.Select(static bug => bug.Id));
        Assert.Equal([3001, 3002, 3080], Failure(set, "Invalid").Bugs.Select(static bug => bug.Id));
        // Source flags describe each test's own relation to the bug.
        Assert.False(Failure(set, "Valid").Bugs[0].IsLinkedToTestCase);
        Assert.True(Failure(set, "Valid").Bugs[1].IsLinkedToTestCase);
        Assert.False(Failure(set, "Invalid").Bugs[0].IsAssociatedWithResult);
        RequestSnapshot bugs = Assert.Single(handler.Requests, static request => request.Body?.Contains(TestRunFixture.BugBatch, StringComparison.Ordinal) == true);
        Assert.Contains("\"ids\":[2001,2002,3001,3002,3050,3060,3080]", bugs.Body!, StringComparison.Ordinal);
        Assert.Single(handler.Requests, static request => request.Body?.Contains(TestRunFixture.TestCaseBatch, StringComparison.Ordinal) == true);
        Assert.Single(handler.Requests, static request => request.Uri.AbsolutePath.Contains("/workitemtypecategories/", StringComparison.Ordinal));
        Assert.Single(handler.Requests, static request => request.Uri.AbsolutePath.EndsWith("/workitemtypes/Bug/states", StringComparison.Ordinal));
        Assert.Single(handler.Requests, static request => request.Uri.AbsolutePath.EndsWith(CustomType, StringComparison.Ordinal));
        Assert.Equal(handler.Requests.Count, service.RequestCount);
        Assert.Empty(set.Diagnostics);
    }

    [Fact]
    public async Task BugInAnotherProjectUsesThatProjectsStatesAndLink()
    {
        const string States = """{"count":2,"value":[{"name":"Actif","category":"InProgress"},{"name":"Fermé","category":"Completed"}]}""";
        TestRunFixture fixture = Scenario(Detail(101, null, 2001, 5001), Detail(102), Detail(103))
            .RouteBatch(TestRunFixture.BugBatch, Items([.. Catalog(2001), Item(5001, "Écran mobile vide", "Fermé", project: "Équipe Mobile")]))
            .Route("workitemtype-states-bug.json", "/%C3%89quipe%20Web/_apis/wit/workitemtypes/Bug/states")
            .RouteBody(States, "/%C3%89quipe%20Mobile/_apis/wit/workitemtypes/Bug/states");
        using FakeHttpMessageHandler handler = fixture.Handler();
        AdoBuildTestFailureSet set = await RetrieveAsync(handler);
        AdoTestBug other = Failure(set, "Valid").Bugs[1];
        Assert.Equal("Équipe Mobile", other.TeamProject);
        Assert.Equal("Completed", other.StateCategory);
        Assert.False(other.IsOpen);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Mobile/_workitems/edit/5001", other.WebUrl.AbsoluteUri);
        Assert.Empty(set.Diagnostics);
    }

    // System.TeamProject is server data: an empty, blank or dot-segment value must neither fail the
    // retrieval nor send a metadata request or link outside the collection.
    [Fact]
    public async Task UnusableBugProjectFallsBackToTheBuildProject()
    {
        TestRunFixture fixture = Scenario(Detail(101, null, 5001, 5002, 5003), Detail(102), Detail(103))
            .RouteBatch(TestRunFixture.BugBatch, Items(Item(5001, "Sans projet", "Active", project: ""),
                Item(5002, "Projet vide", "Active", project: "  "), Item(5003, "Hors collection", "Closed", project: "..")))
            .Route("workitemtype-states-bug.json", "/Collection/%C3%89quipe%20Web/_apis/wit/workitemtypes/Bug/states");
        using FakeHttpMessageHandler handler = fixture.Handler();
        AdoBuildTestFailureSet set = await RetrieveAsync(handler);
        AdoTestFailure valid = Failure(set, "Valid");
        Assert.Equal([5001, 5002, 5003], valid.Bugs.Select(static bug => bug.Id));
        Assert.All(valid.Bugs, static bug =>
        {
            Assert.True(bug.IsResolved);
            Assert.Null(bug.TeamProject);
            Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_workitems/edit/" + bug.Id.ToString(CultureInfo.InvariantCulture),
                bug.WebUrl.AbsoluteUri);
        });
        Assert.Equal([true, true, false], valid.Bugs.Select(static bug => bug.IsOpen));
        Assert.All(handler.Requests, static request => Assert.StartsWith("/Collection/", request.Uri.AbsolutePath, StringComparison.Ordinal));
        Assert.Single(handler.Requests, static request => request.Uri.AbsolutePath.EndsWith("/workitemtypes/Bug/states", StringComparison.Ordinal));
        Assert.Empty(set.Diagnostics);

        TestFailureReportModel model = TestFailureReportModelBuilder.Build(set, TestFailureReportFixture.Options("en-US"));
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.Contains("<li data-bug=\"5003\"><a rel=\"noreferrer\" href=\"https://ado.example.test/Collection/%C3%89quipe%20Web/_workitems/edit/5003\">#5003",
            html, StringComparison.Ordinal);
    }

    // The report can leave only the tests that no open bug tracks yet. The filter appears only when
    // some test has an open bug, and the script reads the card's data-open-bug marker.
    [Theory]
    [InlineData("en-US", "Without an open bug")]
    [InlineData("fr-CA", "Sans bogue ouvert")]
    public async Task WithoutOpenBugFilterAppearsOnlyWhenSomeTestHasAnOpenBug(string culture, string label)
    {
        TestRunFixture open = Scenario(Detail(101, null, 2001), Detail(102, null, 2002), Detail(103)).RouteBugs();
        using FakeHttpMessageHandler handler = open.Handler();
        TestFailureReportModel model = TestFailureReportModelBuilder.Build(await RetrieveAsync(handler), TestFailureReportFixture.Options(culture));
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.Contains("<label><input type=\"checkbox\" data-toggle=\"untracked\">" + label + "</label>", html, StringComparison.Ordinal);
        System.Text.RegularExpressions.Match card = Assert.Single(System.Text.RegularExpressions.Regex.Matches(html,
            "<article class=\"card failure-card\" id=\"f-([0-9]+)\"[^>]* data-open-bug(?:\\s[^>]*)?>"));
        Assert.Equal(model.Failures.Single(static failure => failure.HasOpenBug).Ordinal.ToString(CultureInfo.InvariantCulture), card.Groups[1].Value);

        TestRunFixture closed = Scenario(Detail(101, null, 2002), Detail(102), Detail(103)).RouteBugs();
        using FakeHttpMessageHandler closedHandler = closed.Handler();
        string none = TestFailureReportFixture.Render(TestFailureReportModelBuilder.Build(await RetrieveAsync(closedHandler),
            TestFailureReportFixture.Options(culture)));
        Assert.DoesNotContain("data-toggle=\"untracked\"", none, StringComparison.Ordinal);
        Assert.DoesNotContain(" data-open-bug>", none, StringComparison.Ordinal);
    }

    private static async Task<AdoBuildTestFailureSet> RetrieveAsync(FakeHttpMessageHandler handler)
    {
        using HttpClient client = new(handler, disposeHandler: false);
        return await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), new TestFailureQuery { HistoryCount = 1 },
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
    }

    private static AdoTestFailure Failure(AdoBuildTestFailureSet set, string name) => Assert.Single(set.Failures, failure => failure.ShortName == name);

    private static int Count(string text, string value)
    {
        int count = 0;
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    // Three failures of run 201 with generated details; only the details carry Test Case references and bugs.
    private static TestRunFixture Scenario(string valid, string missing, string invalid) => new TestRunFixture()
        .Route("runs-two.json", "/test/runs", "%24skip=0&")
        .Route("results-testcases.json", "/Runs/201/results", "%24skip=0&")
        .RouteBody(TestRunFixture.EmptyPage, "/Runs/202/results")
        .RouteBody(valid, "/Runs/201/results/101?")
        .RouteBody(missing, "/Runs/201/results/102?")
        .RouteBody(invalid, "/Runs/201/results/103?")
        .Route("attachments-empty.json", "/attachments")
        .RouteBatch(TestRunFixture.TestCaseBatch, TestRunFixture.Read("workitems-testcase-links.json"));

    private static string Detail(int id, string? testCase = null, params int[] bugs) =>
        "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"outcome\":\"Failed\","
        + "\"automatedTestStorage\":\"Contoso.Web.Tests.dll\",\"automatedTestName\":\"Contoso.Web.Tests.LinkTests."
        + id switch { 101 => "Valid", 102 => "Missing", _ => "Invalid" } + "\""
        + (testCase is null ? "" : ",\"testCase\":{\"id\":\"" + testCase + "\"}")
        + ",\"associatedBugs\":[" + string.Join(',', bugs.Select(static bug => "{\"id\":\"" + bug.ToString(CultureInfo.InvariantCulture) + "\"}")) + "]"
        + ",\"errorMessage\":\"Bug check failed.\"}";

    // Work items from the bug and linked fixtures, in the order given, so a batch returns only requested IDs.
    private static JsonNode[] Catalog(params int[] ids)
    {
        Dictionary<int, JsonNode> items = [];
        foreach (string name in new[] { "workitems-bugs.json", "workitems-linked.json" })
            foreach (JsonNode? item in JsonNode.Parse(TestRunFixture.Read(name))!["value"]!.AsArray())
                items.Add((int)item!["id"]!, item);
        return [.. ids.Select(id => items[id].DeepClone())];
    }

    private static JsonObject Item(int id, string title, string state, string type = "Bug", string project = TestRunFixture.Project) => new()
    {
        ["id"] = id,
        ["rev"] = 1,
        ["fields"] = new JsonObject
        {
            ["System.Id"] = id, ["System.Title"] = title, ["System.State"] = state, ["System.WorkItemType"] = type, ["System.TeamProject"] = project,
        },
    };

    private static string Items(params JsonNode[] items) => new JsonObject { ["count"] = items.Length, ["value"] = new JsonArray(items) }.ToJsonString();
}
