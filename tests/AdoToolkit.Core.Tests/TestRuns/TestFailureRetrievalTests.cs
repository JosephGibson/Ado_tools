using System.Net.Http;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

[Trait("Acceptance", "S5-1")]
public sealed class TestFailureRetrievalTests
{
    private static readonly TestFailureQuery NoHistory = new() { HistoryCount = 1 };

    // Test results fixture 1.
    [Fact]
    public async Task BuildWithoutTestRunsReportsNoTestRunsAndStaysComplete()
    {
        TestRunFixture fixture = new();
        fixture.Route("runs-empty.json", "/test/runs", "%24skip=0&");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), NoHistory,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Empty(set.Runs);
        Assert.Empty(set.Failures);
        Assert.Equal(AdoTestFailureStatus.Complete, set.Status);
        AdoDiagnostic diagnostic = Assert.Single(set.Diagnostics);
        Assert.Equal(DiagnosticCodes.NoTestRuns, diagnostic.Code);
        Assert.Equal(AdoDiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), set.RetrievedAt);
        Assert.Equal(401, set.Summary.BuildId);
        Assert.True(set.Summary.IsCurrent);
        Assert.Equal(0, set.Summary.Passed + set.Summary.Failed + set.Summary.Other);
        // The buildUri filter and includeRunDetails are always sent.
        Assert.Contains("buildUri=vstfs%3A%2F%2F%2FBuild%2FBuild%2F401", handler.Requests[0].Uri.Query, StringComparison.Ordinal);
        Assert.Contains("includeRunDetails=true", handler.Requests[0].Uri.Query, StringComparison.Ordinal);
        Assert.Contains("api-version=6.0", handler.Requests[0].Uri.Query, StringComparison.Ordinal);
        Assert.Equal("/Collection/%C3%89quipe%20Web/_apis/test/runs", handler.Requests[0].Uri.AbsolutePath);
    }

    [Fact]
    public async Task MissingBuildUriIsComposedFromTheNumericId()
    {
        TestRunFixture fixture = new();
        fixture.Route("runs-empty.json", "/test/runs", "%24skip=0&");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(id: 777, hasUri: false), NoHistory,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Contains("buildUri=vstfs%3A%2F%2F%2FBuild%2FBuild%2F777", handler.Requests[0].Uri.Query, StringComparison.Ordinal);
    }

    // Test results fixture 2: identities, report order, links, and the §15.9 request bound.
    [Fact]
    public async Task TwoRunsYieldFailedIdentitiesInReportOrderWithinTheRequestBound()
    {
        TestRunFixture fixture = TwoRuns();
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        TestFailureRetrievalService service = TestRunFixture.Service(client);
        AdoBuildTestFailureSet set = await service.GetAsync(TestRunFixture.Build(), NoHistory,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal([201, 202], set.Runs.Select(static run => run.Id));
        Assert.Equal(3, set.Runs[0].TotalTests);
        Assert.Equal(1, set.Runs[0].OutcomeCounts["Failed"]);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_testManagement/runs?_a=runCharts&runId=201",
            set.Runs[0].WebUrl!.AbsoluteUri);
        // Failed before Flaky, then storage, then automated name, then result ID.
        Assert.Equal(["Contoso.Orders.Tests.OrderTests.Totals", "Contoso.Web.Tests.CartTests.AddsItem"],
            set.Failures.Select(static failure => failure.TestName));
        Assert.Equal([1, 2], set.Failures.Select(static failure => failure.Ordinal));
        Assert.Equal(["Totals", "AddsItem"], set.Failures.Select(static failure => failure.ShortName));
        Assert.All(set.Failures, static failure => Assert.Equal(AdoTestFailureClassification.Failed, failure.Classification));
        Assert.Equal(2, set.FailedCount);
        Assert.Equal(0, set.FlakyCount);
        // Counted-only identities: one passing identity across both runs, one not executed.
        Assert.Equal(1, set.Summary.Passed);
        Assert.Equal(2, set.Summary.Failed);
        Assert.Equal(1, set.Summary.Other);
        Assert.Equal(0, set.Summary.Flaky);
        AdoTestFailure cart = set.Failures[1];
        AdoTestAttempt attempt = Assert.Single(cart.Attempts);
        Assert.Equal(AdoTestAttemptSource.Single, attempt.Source);
        Assert.Equal(1, attempt.Number);
        Assert.Equal(201, attempt.RunId);
        Assert.Equal(1, attempt.ResultId);
        Assert.Equal("Failed", attempt.Outcome);
        Assert.Equal(AdoTestOutcomeClass.Failure, attempt.OutcomeClass);
        Assert.Equal("Assert.AreEqual failed. Expected:<1>. Actual:<2>.", attempt.ErrorMessage);
        Assert.Contains("CartTests.cs:line 42", attempt.StackTrace!, StringComparison.Ordinal);
        Assert.Equal("AGENT-01", attempt.ComputerName);
        Assert.Equal(TimeSpan.FromMilliseconds(2150.5), attempt.Duration);
        Assert.Equal("Regression", attempt.FailureType);
        Assert.Equal("NeedsInvestigation", attempt.ResolutionState);
        Assert.Equal("Reviewed by the on-call.", attempt.Comment);
        Assert.Equal(399, attempt.FailingSinceBuildId);
        Assert.Equal([2001, 2002], attempt.AssociatedBugIds);
        Assert.Equal("Équipe Web", cart.Owner!.DisplayName);
        Assert.Equal("equipe.web@contoso.test", cart.Owner.UniqueName);
        Assert.Equal("Build Service", attempt.RunBy!.DisplayName);
        Assert.Equal(2, cart.Priority);
        Assert.Equal("Panier", attempt.CustomFields["Suite de tests"]);
        Assert.Equal(3L, attempt.CustomFields["Retries"]);
        // Unknown scalars survive; unknown objects, arrays and response URLs do not.
        Assert.Equal("kept verbatim", attempt.AdditionalFields["unknownScalar"]);
        Assert.Equal(42L, attempt.AdditionalFields["unknownCount"]);
        Assert.Equal(true, attempt.AdditionalFields["unknownFlag"]);
        Assert.DoesNotContain("unknownObject", attempt.AdditionalFields.Keys);
        Assert.DoesNotContain("unknownArray", attempt.AdditionalFields.Keys);
        Assert.DoesNotContain("url", attempt.AdditionalFields.Keys);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_build/results?buildId=401"
            + "&view=ms.vss-test-web.build-test-results-tab&runId=201&resultId=1", attempt.WebUrl!.AbsoluteUri);
        // Attachment metadata: kinds come from the remote extension and nothing is downloaded yet.
        Assert.Equal([AdoTestAttachmentKind.Png, AdoTestAttachmentKind.Json, AdoTestAttachmentKind.Html,
            AdoTestAttachmentKind.Other, AdoTestAttachmentKind.Other], attempt.Attachments.Select(static item => item.Kind));
        Assert.All(attempt.Attachments, static item =>
        {
            Assert.Equal(AdoTestAttachmentStatus.NotRequested, item.DownloadStatus);
            Assert.Null(item.LocalRelativePath);
            Assert.Equal(201, item.RunId);
            Assert.Equal(1, item.ResultId);
            Assert.Null(item.SubResultId);
        });
        Assert.Empty(set.Failures[0].Attempts[0].Attachments);
        // §15.9 bound: 2 run pages + 4 result pages + 2 details + 2 attachment lists + 1 batch.
        Assert.Equal(11, handler.Requests.Count);
        Assert.Equal(11, service.RequestCount);
        Assert.Equal("None", Parameter(handler.Requests[2].Uri, "detailsToInclude"));
        Assert.Equal("1000", Parameter(handler.Requests[2].Uri, "%24top"));
        Assert.DoesNotContain(handler.Requests, static request =>
            request.Uri.Query.Contains("outcomes=", StringComparison.OrdinalIgnoreCase));
        // Pass 2 follows report order, so the second run's failure is detailed first.
        Assert.Equal("/Collection/%C3%89quipe%20Web/_apis/test/Runs/202/results/11", handler.Requests[6].Uri.AbsolutePath);
        Assert.Equal("Iterations%2CWorkItems%2CSubResults", Parameter(handler.Requests[6].Uri, "detailsToInclude"));
        Assert.Equal("/Collection/%C3%89quipe%20Web/_apis/test/Runs/201/Results/1/attachments",
            handler.Requests[9].Uri.AbsolutePath);
    }

    // Test results fixture 9, resolution half of S5-8.
    [Fact]
    public async Task ValidTestCaseLinkResolvesAndAMissingOneKeepsItsLink()
    {
        TestRunFixture fixture = TwoRuns();
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), NoHistory,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        AdoTestCaseLink resolved = set.Failures[1].TestCase!;
        Assert.Equal(1010, resolved.Id);
        Assert.True(resolved.IsResolved);
        Assert.Equal("Vérifier le panier", resolved.Title);
        Assert.Equal("Design", resolved.State);
        Assert.Equal(7, resolved.Rev);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_workitems/edit/1010", resolved.WebUrl.AbsoluteUri);
        AdoTestCaseLink missing = set.Failures[0].TestCase!;
        Assert.Equal(1011, missing.Id);
        Assert.False(missing.IsResolved);
        Assert.Null(missing.Title);
        Assert.Equal("https://ado.example.test/Collection/%C3%89quipe%20Web/_workitems/edit/1011", missing.WebUrl.AbsoluteUri);
        AdoDiagnostic diagnostic = Assert.Single(set.Diagnostics);
        Assert.Equal(DiagnosticCodes.UnresolvedTestCase, diagnostic.Code);
        Assert.Equal(1011, diagnostic.WorkItemId);
        // Both IDs travel in one reconciled batch request.
        Assert.Equal("POST", handler.Requests[^1].Method);
        Assert.EndsWith("/_apis/wit/workitemsbatch", handler.Requests[^1].Uri.AbsolutePath, StringComparison.Ordinal);
    }

    // Test results fixture 12.
    [Fact]
    public async Task IncompleteRunReportsTestRunInProgress()
    {
        TestRunFixture fixture = new();
        fixture.Route("runs-in-progress.json", "/test/runs", "%24skip=0&")
            .Route("results-run-201.json", "/Runs/401/results", "%24skip=0&")
            .Route("result-detail-201-1.json", "/Runs/401/results/1?")
            .Route("attachments-empty.json", "/attachments")
            .Route("workitems-testcases.json", "workitemsbatch");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(
            TestRunFixture.Build(status: "inProgress"), NoHistory, CultureInfo.InvariantCulture,
            TestContext.Current.CancellationToken);
        AdoDiagnostic diagnostic = Assert.Single(set.Diagnostics, item => item.Code == DiagnosticCodes.TestRunInProgress);
        Assert.Equal(AdoDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(AdoTestFailureStatus.Complete, set.Status);
        Assert.Single(set.Failures);
    }

    // Test results fixture 7.
    [Fact]
    public async Task ResultWithoutAnAutomatedNameIsItsOwnIdentityWithoutHistoryCells()
    {
        TestRunFixture fixture = new();
        fixture.Route("runs-two.json", "/test/runs", "%24skip=0&")
            .Route("results-ungrouped.json", "/Runs/201/results", "%24skip=0&")
            .RouteBody(TestRunFixture.EmptyPage, "/Runs/202/results")
            .RouteBody(TestRunFixture.EmptyPage, "/build/builds")
            .Route("result-detail-ungrouped-81.json", "/Runs/201/results/81?")
            .Route("result-detail-ungrouped-82.json", "/Runs/201/results/82?")
            .Route("attachments-empty.json", "/attachments");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(),
            new TestFailureQuery { HistoryCount = 4 }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(2, set.Failures.Count);
        Assert.All(set.Failures, static failure =>
        {
            Assert.Single(failure.Attempts);
            Assert.Empty(failure.History);
            Assert.Null(failure.TestName);
        });
        Assert.Equal(["Manual smoke test", "Empty automated name"], set.Failures.Select(static failure => failure.Title));
        Assert.Equal(2, set.Diagnostics.Count(static item => item.Code == DiagnosticCodes.UngroupedTestResult));
        Assert.Equal(2, set.Summary.Failed);
        // Fewer builds than requested is normal: no history diagnostic and only the current entry.
        Assert.Single(set.History);
        Assert.DoesNotContain(set.Diagnostics, static item =>
            item.Code is DiagnosticCodes.HistoryUnavailable or DiagnosticCodes.HistoryLimitExceeded);
    }

    // Test results fixture 8.
    [Fact]
    public async Task OutcomeClassesFollowTheDefaultAndUnknownStringsAreKeptVerbatim()
    {
        TestRunFixture fixture = new();
        fixture.Route("runs-two.json", "/test/runs", "%24skip=0&")
            .Route("results-outcomes.json", "/Runs/201/results", "%24skip=0&")
            .RouteBody(TestRunFixture.EmptyPage, "/Runs/202/results")
            .Route("attachments-empty.json", "/attachments");
        for (int id = 91; id <= 95; id++)
            fixture.RouteBody(Detail(id), "/Runs/201/results/" + id.ToString(CultureInfo.InvariantCulture) + "?");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), NoHistory,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        // Error, Timeout and Aborted are failure-class; Inconclusive and the unknown string are not.
        Assert.Equal(["Aborted", "Error", "Timeout"], set.Failures.Select(static failure => failure.Attempts[0].Outcome).Order(StringComparer.Ordinal));
        Assert.Equal(3, set.Summary.Failed);
        Assert.Equal(2, set.Summary.Other);
        Assert.Equal(0, set.Summary.Passed);
        Assert.All(set.Failures, static failure =>
            Assert.Equal(AdoTestOutcomeClass.Failure, failure.Attempts[0].OutcomeClass));
        Assert.Equal(AdoTestFailureStatus.Complete, set.Status);
    }

    // Test results fixture 11.
    [Fact]
    public async Task MoreFailingIdentitiesThanTheMaximumGivesFailureLimitExceededAndPartial()
    {
        TestRunFixture fixture = new();
        fixture.Route("runs-two.json", "/test/runs", "%24skip=0&")
            .Route("results-outcomes.json", "/Runs/201/results", "%24skip=0&")
            .RouteBody(TestRunFixture.EmptyPage, "/Runs/202/results")
            .Route("attachments-empty.json", "/attachments");
        for (int id = 91; id <= 95; id++)
            fixture.RouteBody(Detail(id), "/Runs/201/results/" + id.ToString(CultureInfo.InvariantCulture) + "?");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(),
            new TestFailureQuery { HistoryCount = 1, MaximumReportedFailures = 2 }, CultureInfo.InvariantCulture,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, set.Failures.Count);
        Assert.Equal(AdoTestFailureStatus.Partial, set.Status);
        AdoDiagnostic diagnostic = Assert.Single(set.Diagnostics, item => item.Code == DiagnosticCodes.FailureLimitExceeded);
        Assert.Equal(AdoDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(["3", "2"], diagnostic.Arguments);
        // The identities beyond the limit are still counted in the bars.
        Assert.Equal(3, set.Summary.Failed);
        Assert.Equal(2, set.Summary.Other);
        // Only the detailed identities cost a detail request.
        Assert.Equal(2, handler.Requests.Count(static request =>
            request.Uri.Query.Contains("detailsToInclude=Iterations", StringComparison.Ordinal)));
    }

    // Test results fixture 10: a short nonterminal page does not end enumeration.
    [Fact]
    public async Task ResultListingAdvancesBySkipAndAShortPageDoesNotEndEnumeration()
    {
        TestRunFixture fixture = new();
        fixture.Route("runs-two.json", "/test/runs", "%24skip=0&")
            .Route("results-run-201.json", "/Runs/201/results", "%24skip=0&")
            .RouteBody(TestRunFixture.EmptyPage, "/Runs/201/results", "%24skip=3&")
            .Route("results-run-202.json", "/Runs/202/results", "%24skip=0&")
            .RouteBody(SecondPage, "/Runs/202/results", "%24skip=2&")
            .RouteBody(TestRunFixture.EmptyPage, "/Runs/202/results", "%24skip=3&")
            .Route("result-detail-201-1.json", "/Runs/201/results/1?")
            .Route("result-detail-202-11.json", "/Runs/202/results/11?")
            .RouteBody(RoundingDetail, "/Runs/202/results/13?")
            .Route("attachments-empty.json", "/attachments")
            .Route("workitems-testcases.json", "workitemsbatch");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), NoHistory,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        // The identity on the short second page is retained and ordered with the rest.
        Assert.Equal(["Contoso.Orders.Tests.OrderTests.Rounding", "Contoso.Orders.Tests.OrderTests.Totals",
            "Contoso.Web.Tests.CartTests.AddsItem"], set.Failures.Select(static failure => failure.TestName));
        Assert.Equal(["0", "3"], Skips(handler, "/Runs/201/results"));
        Assert.Equal(["0", "2", "3"], Skips(handler, "/Runs/202/results"));
    }

    // Result IDs are unique only within a run: pipeline run attempts commonly repeat them.
    [Fact]
    public async Task RunAttemptsWithTheSameResultIdKeepTheirOwnRunForAttachmentsAndDiagnostics()
    {
        TestRunFixture fixture = new();
        fixture.Route("runs-reattempt.json", "/test/runs", "%24skip=0&")
            .RouteBody(SameIdPage("Failed"), "/Runs/301/results", "%24skip=0&")
            .RouteBody(SameIdPage("Passed"), "/Runs/302/results", "%24skip=0&")
            .RouteBody(SameIdDetail("Failed", "1010"), "/Runs/301/results/100000?")
            .RouteBody(SameIdDetail("Passed", "not-a-number"), "/Runs/302/results/100000?")
            .RouteBody(AttachmentPage(7, "run-301.png"), "/Runs/301/Results/100000/attachments")
            .RouteBody(AttachmentPage(8, "run-302.json"), "/Runs/302/Results/100000/attachments")
            .Route("workitems-testcases.json", "workitemsbatch");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), NoHistory,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        AdoTestFailure failure = Assert.Single(set.Failures);
        Assert.Equal(AdoTestFailureClassification.Flaky, failure.Classification);
        Assert.Equal([301, 302], failure.Attempts.Select(static attempt => attempt.RunId));
        Assert.All(failure.Attempts, static attempt => Assert.Equal(100000, attempt.ResultId));
        AdoTestAttachment first = Assert.Single(failure.Attempts[0].Attachments);
        Assert.Equal((7, 301), (first.Id, first.RunId));
        AdoTestAttachment second = Assert.Single(failure.Attempts[1].Attachments);
        Assert.Equal((8, 302), (second.Id, second.RunId));
        Assert.Single(handler.Requests, static request =>
            request.Uri.AbsolutePath.EndsWith("/Runs/302/Results/100000/attachments", StringComparison.Ordinal));
        AdoDiagnostic invalid = Assert.Single(set.Diagnostics, static item => item.Code == DiagnosticCodes.InvalidTestCaseReference);
        Assert.Equal(["100000", "302"], invalid.Arguments);
    }

    private static string SameIdPage(string outcome) => "{\"count\":1,\"value\":[{\"id\":100000,\"outcome\":\"" + outcome
        + "\",\"automatedTestName\":\"Contoso.Web.Tests.RetryTests.SameId\",\"automatedTestStorage\":\"Contoso.Web.Tests.dll\"}]}";

    private static string SameIdDetail(string outcome, string testCase) => "{\"id\":100000,\"outcome\":\"" + outcome
        + "\",\"automatedTestName\":\"Contoso.Web.Tests.RetryTests.SameId\",\"automatedTestStorage\":\"Contoso.Web.Tests.dll\","
        + "\"testCase\":{\"id\":\"" + testCase + "\"}}";

    private static string AttachmentPage(int id, string fileName) => "{\"count\":1,\"value\":[{\"id\":"
        + id.ToString(CultureInfo.InvariantCulture) + ",\"fileName\":\"" + fileName + "\",\"size\":10}]}";

    private static TestRunFixture TwoRuns() => new TestRunFixture()
        .Route("runs-two.json", "/test/runs", "%24skip=0&")
        .Route("results-run-201.json", "/Runs/201/results", "%24skip=0&")
        .Route("results-run-202.json", "/Runs/202/results", "%24skip=0&")
        .Route("result-detail-201-1.json", "/Runs/201/results/1?")
        .Route("result-detail-202-11.json", "/Runs/202/results/11?")
        .Route("attachments-result.json", "/Runs/201/Results/1/attachments")
        .Route("attachments-empty.json", "/Runs/202/Results/11/attachments")
        .Route("workitems-testcases.json", "workitemsbatch");

    private static string Detail(int id) =>
        "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"outcome\":\""
        + id switch { 91 => "Error", 92 => "Timeout", 93 => "Aborted", 94 => "Inconclusive", _ => "Ecoulé" }
        + "\",\"automatedTestStorage\":\"Contoso.Web.Tests.dll\",\"automatedTestName\":\"Contoso.Web.Tests.OutcomeTests."
        + id switch { 91 => "Errored", 92 => "TimedOut", 93 => "Aborted", 94 => "Inconclusive", _ => "Unknown" }
        + "\"}";

    private const string SecondPage = "{\"count\":1,\"value\":[{\"id\":13,\"outcome\":\"Failed\","
        + "\"automatedTestName\":\"Contoso.Orders.Tests.OrderTests.Rounding\","
        + "\"automatedTestStorage\":\"Contoso.Orders.Tests.dll\",\"testCaseTitle\":\"Rounds totals\"}]}";

    private const string RoundingDetail = "{\"id\":13,\"outcome\":\"Failed\","
        + "\"automatedTestName\":\"Contoso.Orders.Tests.OrderTests.Rounding\","
        + "\"automatedTestStorage\":\"Contoso.Orders.Tests.dll\",\"errorMessage\":\"Rounded to 19,99.\"}";

    private static IEnumerable<string> Skips(FakeHttpMessageHandler handler, string path) => handler.Requests
        .Where(request => request.Uri.AbsolutePath.EndsWith(path, StringComparison.Ordinal))
        .Select(static request => Parameter(request.Uri, "%24skip"));

    private static string Parameter(Uri uri, string name) => uri.Query.TrimStart('?').Split('&')
        .First(part => part.StartsWith(name + "=", StringComparison.Ordinal))[(name.Length + 1)..];
}
