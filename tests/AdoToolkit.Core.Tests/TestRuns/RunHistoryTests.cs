using System.Net.Http;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

[Trait("Acceptance", "S5-3")]
public sealed class RunHistoryTests
{
    // Test results fixture 13.
    [Fact]
    public async Task WindowEndsAtTheCurrentBuildAndBarsAgreeWithCells()
    {
        TestRunFixture fixture = History();
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(),
            new TestFailureQuery { HistoryCount = 4 }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        // Oldest first, current last, and the current build is always present.
        Assert.Equal([398, 399, 400, 401], set.History.Select(static summary => summary.BuildId));
        Assert.Equal([false, false, false, true], set.History.Select(static summary => summary.IsCurrent));
        Assert.Equal([false, true, true, true], set.History.Select(static summary => summary.IsAvailable));
        Assert.Same(set.Summary, set.History[^1]);
        Assert.Equal("20260912.1", set.History[0].BuildNumber);
        Assert.Equal("refs/heads/main", set.History[1].SourceBranch);
        Assert.Equal("succeeded", set.History[2].Result);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_build/results?buildId=399",
            set.History[1].WebUrl.AbsoluteUri);
        // Per-test cells: unavailable, absent, then the identity's classification in that build.
        AdoTestFailure totals = Assert.Single(set.Failures, failure => failure.ShortName == "Totals");
        Assert.Equal([398, 399, 400, 401], totals.History.Select(static cell => cell.BuildId));
        Assert.Equal([AdoTestHistoryOutcome.Unavailable, AdoTestHistoryOutcome.NotRun, AdoTestHistoryOutcome.Failed,
            AdoTestHistoryOutcome.Failed], totals.History.Select(static cell => cell.Outcome));
        AdoTestFailure cart = Assert.Single(set.Failures, failure => failure.ShortName == "AddsItem");
        Assert.Equal([AdoTestHistoryOutcome.Unavailable, AdoTestHistoryOutcome.Failed, AdoTestHistoryOutcome.Passed,
            AdoTestHistoryOutcome.Failed], cart.History.Select(static cell => cell.Outcome));
        Assert.True(cart.History[^1].IsCurrent);
        // Each bar equals the tally of the cells it displays.
        AssertBarsMatchCells(set);
        AdoDiagnostic diagnostic = Assert.Single(set.Diagnostics, item => item.Code == DiagnosticCodes.HistoryUnavailable);
        Assert.Equal(AdoDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(["398"], diagnostic.Arguments);
        // An unreadable history build never changes Status.
        Assert.Equal(AdoTestFailureStatus.Complete, set.Status);
        Assert.Equal(2, set.Failures.Count);
    }

    [Fact]
    public async Task SameBranchScopeSendsTheBranchNameAndAllBranchesOmitsIt()
    {
        foreach (AdoTestHistoryScope scope in (AdoTestHistoryScope[])[AdoTestHistoryScope.SameBranch, AdoTestHistoryScope.AllBranches])
        {
            TestRunFixture fixture = History();
            using FakeHttpMessageHandler handler = fixture.Handler();
            using HttpClient client = new(handler);
            await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(),
                new TestFailureQuery { HistoryCount = 4, HistoryScope = scope }, CultureInfo.InvariantCulture,
                TestContext.Current.CancellationToken);
            RequestSnapshot window = handler.Requests.First(static request =>
                request.Uri.AbsolutePath.EndsWith("/_apis/build/builds", StringComparison.Ordinal));
            Assert.Contains("definitions=42", window.Uri.Query, StringComparison.Ordinal);
            Assert.Contains("statusFilter=completed", window.Uri.Query, StringComparison.Ordinal);
            Assert.Contains("queryOrder=finishTimeDescending", window.Uri.Query, StringComparison.Ordinal);
            Assert.Contains("%24top=4&", window.Uri.Query, StringComparison.Ordinal);
            // maxTime is the current build's finish time, formatted with the invariant culture.
            Assert.Contains("maxTime=2026-09-15T10%3A00%3A00.0000000%2B00%3A00", window.Uri.Query, StringComparison.Ordinal);
            Assert.Equal(scope == AdoTestHistoryScope.SameBranch,
                window.Uri.Query.Contains("branchName=refs%2Fheads%2Fmain", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task QueueTimeIsUsedWhenTheBuildHasNotFinished()
    {
        TestRunFixture fixture = History();
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(status: "inProgress", finished: false),
            new TestFailureQuery { HistoryCount = 2 }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        RequestSnapshot window = handler.Requests.First(static request =>
            request.Uri.AbsolutePath.EndsWith("/_apis/build/builds", StringComparison.Ordinal));
        Assert.Contains("maxTime=2026-09-15T09%3A00%3A00.0000000%2B00%3A00", window.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestBudgetStopsHistoryAndMarksOlderBuildsUnavailable()
    {
        TestRunFixture fixture = History();
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(),
            new TestFailureQuery { HistoryCount = 4, MaximumHistoryRequests = 5 }, CultureInfo.InvariantCulture,
            TestContext.Current.CancellationToken);
        // The newest earlier build is read; the budget then stops the older ones.
        Assert.Equal([false, false, true, true], set.History.Select(static summary => summary.IsAvailable));
        AdoDiagnostic diagnostic = Assert.Single(set.Diagnostics, item => item.Code == DiagnosticCodes.HistoryLimitExceeded);
        Assert.Equal(["5"], diagnostic.Arguments);
        AdoTestFailure cart = Assert.Single(set.Failures, failure => failure.ShortName == "AddsItem");
        Assert.Equal([AdoTestHistoryOutcome.Unavailable, AdoTestHistoryOutcome.Unavailable,
            AdoTestHistoryOutcome.Passed, AdoTestHistoryOutcome.Failed], cart.History.Select(static cell => cell.Outcome));
        Assert.Equal(AdoTestFailureStatus.Complete, set.Status);
        // Build 398 was never requested.
        Assert.DoesNotContain(handler.Requests, static request =>
            request.Uri.Query.Contains("Build%2F398", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HistoryCountOfOneAsksForNoWindow()
    {
        TestRunFixture fixture = History();
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(),
            new TestFailureQuery { HistoryCount = 1 }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Single(set.History);
        Assert.True(set.History[0].IsCurrent);
        Assert.All(set.Failures, static failure => Assert.Single(failure.History));
        Assert.DoesNotContain(handler.Requests, static request =>
            request.Uri.AbsolutePath.EndsWith("/_apis/build/builds", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PipedBuildsWithTheSameTimestampEachAppearOnlyOnceInTheirOwnWindow()
    {
        // Both builds use the same definition, branch and finish time. The first cached
        // window must not be reused after its current build was excluded from the listing.
        TestRunFixture fixture = new TestRunFixture()
            .RouteBody("""{"count":2,"value":[{"id":401,"buildNumber":"first"},{"id":400,"buildNumber":"second"}]}""",
                "/_apis/build/builds")
            .Route("runs-empty.json", "/test/runs");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        TestFailureRetrievalService service = TestRunFixture.Service(client);
        TestFailureQuery query = new() { HistoryCount = 2 };
        AdoBuildTestFailureSet first = await service.GetAsync(TestRunFixture.Build(id: 401), query,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        AdoBuildTestFailureSet second = await service.GetAsync(TestRunFixture.Build(id: 400), query,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);

        Assert.Equal([400, 401], first.History.Select(summary => summary.BuildId));
        Assert.Equal([401, 400], second.History.Select(summary => summary.BuildId));
        Assert.Single(second.History, summary => summary.IsCurrent);
    }

    [Fact]
    public async Task HistoryListingsAreCachedForTheInvocationAcrossPipedBuilds()
    {
        TestRunFixture fixture = History();
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        TestFailureRetrievalService service = TestRunFixture.Service(client);
        TestFailureQuery query = new() { HistoryCount = 4 };
        await service.GetAsync(TestRunFixture.Build(), query, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        int afterFirst = service.RequestCount;
        int historyRequests = handler.Requests.Count(static request =>
            request.Uri.Query.Contains("Build%2F400", StringComparison.Ordinal)
            || request.Uri.AbsolutePath.Contains("/Runs/261/", StringComparison.Ordinal));
        Assert.True(historyRequests > 0);
        await service.GetAsync(TestRunFixture.Build(), query, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        // The second build of the same definition reuses the cached window and per-build data.
        Assert.Equal(historyRequests, handler.Requests.Count(static request =>
            request.Uri.Query.Contains("Build%2F400", StringComparison.Ordinal)
            || request.Uri.AbsolutePath.Contains("/Runs/261/", StringComparison.Ordinal)));
        Assert.True(service.RequestCount - afterFirst < afterFirst);
    }

    private static void AssertBarsMatchCells(AdoBuildTestFailureSet set)
    {
        foreach (AdoBuildTestSummary summary in set.History)
        {
            if (!summary.IsAvailable)
            {
                Assert.Equal(0, summary.Passed + summary.Failed + summary.Flaky + summary.Other);
                continue;
            }
            AdoTestHistoryOutcome[] cells = set.Failures
                .Select(failure => failure.History.First(cell => cell.BuildId == summary.BuildId).Outcome)
                .ToArray();
            // Reported identities plus counted-only ones make up each bar; this fixture reports all.
            Assert.Equal(cells.Count(static cell => cell is AdoTestHistoryOutcome.Passed or AdoTestHistoryOutcome.Flaky),
                summary.Passed);
            Assert.Equal(cells.Count(static cell => cell == AdoTestHistoryOutcome.Failed), summary.Failed);
            Assert.Equal(cells.Count(static cell => cell == AdoTestHistoryOutcome.Flaky), summary.Flaky);
            Assert.Equal(cells.Count(static cell => cell == AdoTestHistoryOutcome.Other), summary.Other);
        }
    }

    internal static TestRunFixture History() => new TestRunFixture()
        .Route("builds-history.json", "/_apis/build/builds")
        .Route("runs-two.json", "/test/runs", "Build%2F401", "%24skip=0&")
        .RouteBody(CurrentResults, "/Runs/201/results", "%24skip=0&")
        .RouteBody(TestRunFixture.EmptyPage, "/Runs/202/results")
        .Route("result-detail-201-1.json", "/Runs/201/results/1?")
        .RouteBody(TotalsDetail, "/Runs/201/results/11?")
        .Route("attachments-empty.json", "/attachments")
        .RouteBugs()
        .Route("workitems-testcases.json", "workitemsbatch")
        .Route("runs-history-400.json", "/test/runs", "Build%2F400", "%24skip=0&")
        .Route("results-history-400.json", "/Runs/261/results", "%24skip=0&")
        .Route("runs-history-399.json", "/test/runs", "Build%2F399", "%24skip=0&")
        .Route("results-history-399.json", "/Runs/271/results", "%24skip=0&")
        .RouteStatus(500, "{\"message\":\"Synthetic failure.\"}", "Build%2F398");

    // Both reported identities live in run 201 so history routing stays to one run per build.
    private const string CurrentResults = "{\"count\":2,\"value\":["
        + "{\"id\":1,\"outcome\":\"Failed\",\"automatedTestName\":\"Contoso.Web.Tests.CartTests.AddsItem\","
        + "\"automatedTestStorage\":\"Contoso.Web.Tests.dll\",\"testCaseTitle\":\"Adds an item to the cart\","
        + "\"testCase\":{\"id\":\"1010\"}},"
        + "{\"id\":11,\"outcome\":\"Failed\",\"automatedTestName\":\"Contoso.Orders.Tests.OrderTests.Totals\","
        + "\"automatedTestStorage\":\"Contoso.Orders.Tests.dll\",\"testCaseTitle\":\"Computes order totals\"}]}";

    private const string TotalsDetail = "{\"id\":11,\"outcome\":\"Failed\","
        + "\"automatedTestName\":\"Contoso.Orders.Tests.OrderTests.Totals\","
        + "\"automatedTestStorage\":\"Contoso.Orders.Tests.dll\",\"errorMessage\":\"Expected 19,99.\"}";
}
