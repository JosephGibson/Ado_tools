using System.Net;
using System.Net.Http;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

[Trait("Acceptance", "S5-1")]
public sealed class RetrievalCancellationTests
{
    [Fact]
    public async Task CancellationDuringPassTwoStopsBeforeTheNextRequest()
    {
        using CancellationTokenSource cancellation = new();
        int details = 0;
        FakeHttpMessageHandler handler = new()
        {
            Fallback = (request, _) =>
            {
                string target = request.RequestUri!.PathAndQuery;
                if (target.Contains("detailsToInclude=Iterations", StringComparison.Ordinal))
                {
                    // Cancel while the first detail response is being produced.
                    if (++details == 1) cancellation.Cancel();
                    string resultId = request.RequestUri.AbsolutePath[(request.RequestUri.AbsolutePath.LastIndexOf('/') + 1)..];
                    return Task.FromResult(FakeHttpMessageHandler.Response(Detail.Replace("\"id\":1,", "\"id\":" + resultId + ",", StringComparison.Ordinal)));
                }
                if (target.Contains("/_apis/test/runs", StringComparison.Ordinal))
                    return Task.FromResult(FakeHttpMessageHandler.Response(
                        target.Contains("%24skip=0&", StringComparison.Ordinal)
                            ? TestRunFixture.Read("runs-two.json") : TestRunFixture.EmptyPage));
                if (target.Contains("/Runs/201/results?", StringComparison.Ordinal))
                    return Task.FromResult(FakeHttpMessageHandler.Response(
                        target.Contains("%24skip=0&", StringComparison.Ordinal)
                            ? TestRunFixture.Read("results-rerun.json") : TestRunFixture.EmptyPage));
                return Task.FromResult(FakeHttpMessageHandler.Response(TestRunFixture.EmptyPage));
            },
        };
        using HttpClient client = new(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TestRunFixture.Service(client)
            .GetAsync(TestRunFixture.Build(), new TestFailureQuery { HistoryCount = 1 }, CultureInfo.InvariantCulture,
                cancellation.Token));
        // Only the cancelled detail request was issued; no further detail or attachment call followed.
        Assert.Equal(1, details);
        Assert.DoesNotContain(handler.Requests, static request =>
            request.Uri.AbsolutePath.EndsWith("/attachments", StringComparison.Ordinal));
        handler.Dispose();
    }

    [Fact]
    public async Task AuthorizationFailureInHistoryStillFailsTheWholeRetrieval()
    {
        TestRunFixture fixture = new TestRunFixture()
            .Route("builds-history.json", "/_apis/build/builds")
            .Route("runs-two.json", "/test/runs", "Build%2F401", "%24skip=0&")
            .RouteBody(List, "/Runs/201/results", "%24skip=0&")
            .RouteBody(TestRunFixture.EmptyPage, "/Runs/202/results")
            .RouteBody(Detail, "/Runs/201/results/1?")
            .Route("attachments-empty.json", "/attachments")
            .RouteStatus((int)HttpStatusCode.Forbidden, "{\"message\":\"Synthetic denial.\"}", "Build%2F400");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        await Assert.ThrowsAsync<AdoAuthorizationException>(() => TestRunFixture.Service(client)
            .GetAsync(TestRunFixture.Build(), new TestFailureQuery { HistoryCount = 4 }, CultureInfo.InvariantCulture,
                TestContext.Current.CancellationToken));
    }

    private const string List = "{\"count\":1,\"value\":[{\"id\":1,\"outcome\":\"Failed\","
        + "\"automatedTestName\":\"Contoso.Web.Tests.CartTests.AddsItem\","
        + "\"automatedTestStorage\":\"Contoso.Web.Tests.dll\"}]}";

    private const string Detail = "{\"id\":1,\"outcome\":\"Failed\","
        + "\"automatedTestName\":\"Contoso.Web.Tests.CartTests.AddsItem\","
        + "\"automatedTestStorage\":\"Contoso.Web.Tests.dll\",\"errorMessage\":\"Boom.\"}";
}
