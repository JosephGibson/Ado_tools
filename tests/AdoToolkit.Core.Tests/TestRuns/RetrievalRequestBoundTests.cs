using System.Net.Http;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

// The §15.9 bound: run pages + result pages (current and history builds) + one detail request per
// reported result record + attachment lists, none with SkipAttachments + ceil(distinct Test Case
// IDs / 200). The bug lookup adds ceil(distinct bug candidates / 200), one Bug category read per
// project with linked work items and one state list per project and bug type
// (TestBugResolutionTests). A result listing costs one request less when its run is completed and
// its short last page completes the run's total: the empty page that would end it is not requested.
[Trait("Acceptance", "S5-1")]
public sealed class RetrievalRequestBoundTests
{
    [Theory]
    [InlineData(false, 11)]
    [InlineData(true, 0)]
    public async Task RequestCountMatchesTheDocumentedBoundForTheRerunFixture(bool skipAttachments, int expectedAttachmentLists)
    {
        TestRunFixture fixture = new();
        fixture.Route("runs-two.json", "/test/runs", "%24skip=0&")
            .Route("results-rerun.json", "/Runs/201/results", "%24skip=0&")
            .RouteBody(TestRunFixture.EmptyPage, "/Runs/202/results")
            .Route("result-detail-rerun-flaky.json", "/Runs/201/results/21?")
            .Route("result-detail-rerun-flaky3.json", "/Runs/201/results/22?")
            .Route("result-detail-rerun-failed.json", "/Runs/201/results/23?")
            .Route("attachments-empty.json", "/attachments");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        TestFailureRetrievalService service = TestRunFixture.Service(client);
        AdoBuildTestFailureSet set = await service.GetAsync(TestRunFixture.Build(),
            new TestFailureQuery { HistoryCount = 1, SkipAttachments = skipAttachments }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        int runPages = Count(handler, "/_apis/test/runs");
        int resultPages = handler.Requests.Count(static request =>
            request.Uri.AbsolutePath.EndsWith("/results", StringComparison.Ordinal));
        int details = handler.Requests.Count(static request =>
            request.Uri.Query.Contains("detailsToInclude=Iterations", StringComparison.Ordinal));
        int attachmentLists = handler.Requests.Count(static request =>
            request.Uri.AbsolutePath.EndsWith("/attachments", StringComparison.Ordinal));
        int batches = Count(handler, "/_apis/wit/workitemsbatch");
        // Two run pages, two result pages (run 201 lists its three tests on one page; run 202 lists
        // none of its two, so its first page is the empty one), one detail per rerun parent, one
        // attachment list per detailed result plus one per rerun attempt unless they are skipped,
        // and no Test Case batch.
        Assert.Equal(2, runPages);
        Assert.Equal(2, resultPages);
        Assert.Equal(3, details);
        Assert.Equal(expectedAttachmentLists, attachmentLists);
        Assert.Equal(0, batches);
        Assert.Equal(runPages + resultPages + details + attachmentLists + batches, handler.Requests.Count);
        Assert.Equal(handler.Requests.Count, service.RequestCount);
        Assert.Equal(7 + expectedAttachmentLists, handler.Requests.Count);
        // Eight attempts over three identities cost three detail requests: sub-results arrive
        // with their parent, so a rerun group is one request whatever its attempt count.
        Assert.Equal(8, set.Failures.Sum(static failure => failure.Attempts.Count));
        Assert.Equal(!skipAttachments, set.AttachmentsListed);
    }

    [Fact]
    public async Task HistoryBuildsOnlyCostRunAndResultPages()
    {
        TestRunFixture fixture = new TestRunFixture()
            .Route("builds-history.json", "/_apis/build/builds")
            .Route("runs-two.json", "/test/runs", "Build%2F401", "%24skip=0&")
            .RouteBody(Single, "/Runs/201/results", "%24skip=0&")
            .RouteBody(TestRunFixture.EmptyPage, "/Runs/202/results")
            .RouteBody(SingleDetail, "/Runs/201/results/1?")
            .Route("attachments-empty.json", "/attachments")
            .Route("runs-history-400.json", "/test/runs", "Build%2F400", "%24skip=0&")
            .Route("results-history-400.json", "/Runs/261/results", "%24skip=0&");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), new TestFailureQuery { HistoryCount = 2 },
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        // Current build: 2 run pages + 3 result pages + 1 detail + 1 attachment list. Run 201 lists one
        // of its three tests, so its listing still ends on an empty page.
        // History: 1 window page + 2 run pages + 1 result page. No detail or attachment requests.
        Assert.Equal(1, Count(handler, "/_apis/build/builds"));
        Assert.Equal(1, handler.Requests.Count(static request =>
            request.Uri.Query.Contains("detailsToInclude=Iterations", StringComparison.Ordinal)));
        Assert.Equal(1, handler.Requests.Count(static request =>
            request.Uri.AbsolutePath.EndsWith("/attachments", StringComparison.Ordinal)));
        Assert.Equal(11, handler.Requests.Count);
    }

    private static int Count(FakeHttpMessageHandler handler, string path) => handler.Requests
        .Count(request => request.Uri.AbsolutePath.EndsWith(path, StringComparison.Ordinal));

    private const string Single = "{\"count\":1,\"value\":[{\"id\":1,\"outcome\":\"Failed\","
        + "\"automatedTestName\":\"Contoso.Web.Tests.CartTests.AddsItem\","
        + "\"automatedTestStorage\":\"Contoso.Web.Tests.dll\"}]}";

    private const string SingleDetail = "{\"id\":1,\"outcome\":\"Failed\","
        + "\"automatedTestName\":\"Contoso.Web.Tests.CartTests.AddsItem\","
        + "\"automatedTestStorage\":\"Contoso.Web.Tests.dll\",\"errorMessage\":\"Boom.\"}";
}
