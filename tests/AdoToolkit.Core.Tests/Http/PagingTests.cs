using System.Net.Http;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.Tests.Http;

[Trait("Culture", "Invariant")]
public sealed class PagingTests
{
    [Fact]
    [Trait("Acceptance", "S0-3")]
    public async Task TopSkipContinuesShortPagesAndAdvancesByActualCount()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("success.json"));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("projects-second.json"));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("projects-empty.json"));
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5));
        IReadOnlyList<ProjectDto> result = await RetryPolicyTests.Fetch(pipeline);
        Assert.Equal(2, result.Count);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("%24skip=0", handler.Requests[0].Uri.Query, StringComparison.Ordinal);
        Assert.Contains("%24skip=1", handler.Requests[1].Uri.Query, StringComparison.Ordinal);
        Assert.Contains("%24skip=2", handler.Requests[2].Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Acceptance", "S0-3")]
    public async Task ContinuationTokensAreOpaqueAndEmptyPagesContinue()
    {
        using FakeHttpMessageHandler handler = new();
        HttpResponseMessage first = FakeHttpMessageHandler.Fixture("projects-empty.json");
        first.Headers.TryAddWithoutValidation("x-ms-continuationtoken", "opaque +/%?&");
        handler.Enqueue(first);
        handler.Enqueue(FakeHttpMessageHandler.Fixture("success.json"));
        using HttpClient client = new(handler);
        CapturingLog log = new();
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5), log);
        IReadOnlyList<ProjectDto> result = await RetryPolicyTests.Fetch(pipeline, EndpointRegistry.ProjectsList with { Paging = PagingStrategy.ContinuationHeader });
        Assert.Single(result);
        Assert.Contains("continuationToken=opaque%20%2B%2F%25%3F%26", handler.Requests[1].Uri.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("$skip", Uri.UnescapeDataString(handler.Requests[1].Uri.Query), StringComparison.Ordinal);
        Assert.DoesNotContain(log.Messages, message => message.Contains("opaque", StringComparison.Ordinal) || message.Contains("Équipe", StringComparison.Ordinal) || message.Contains("api-version", StringComparison.Ordinal));
    }

    [Theory]
    [Trait("Acceptance", "S0-3")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedPagesOrTokensFail(bool continuation)
    {
        using FakeHttpMessageHandler handler = new();
        for (int i = 0; i < 2; i++)
        {
            HttpResponseMessage response = FakeHttpMessageHandler.Fixture("success.json");
            if (continuation) response.Headers.TryAddWithoutValidation("x-ms-continuationtoken", "same");
            handler.Enqueue(response);
        }
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<AdoResponseFormatException>(() => RetryPolicyTests.Fetch(pipeline,
            EndpointRegistry.ProjectsList with { Paging = continuation ? PagingStrategy.ContinuationHeader : PagingStrategy.TopSkip }));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    [Trait("Acceptance", "S0-3")]
    public async Task ResultLimitStopsFetchingIndependentlyOfPageSize()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("success.json"));
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5));
        IReadOnlyList<ProjectDto> result = await pipeline.GetPagesAsync(EndpointRegistry.ProjectsList, AdoJsonContext.Default.ProjectPageDto,
            static page => page.Value, static item => item.Id ?? "", CultureInfo.InvariantCulture, TestContext.Current.CancellationToken, top: 1, pageSize: 100);
        Assert.Single(result);
        Assert.Single(handler.Requests);
        Assert.Contains("%24top=100", handler.Requests[0].Uri.Query, StringComparison.Ordinal);
    }

    // A listing that never ends fails at the ceiling instead of returning what it read. The ceiling
    // is lowered to three pages here: the 10,000 of the product cost 10,000 round trips.
    [Fact]
    [Trait("Acceptance", "S0-3")]
    public async Task PageCeilingFailsInsteadOfReportingPartialSuccess()
    {
        using FakeHttpMessageHandler handler = new();
        int pages = 0;
        handler.Fallback = (_, _) =>
        {
            HttpResponseMessage response = FakeHttpMessageHandler.Response("{\"value\":[{\"id\":\"" + Guid.NewGuid().ToString("D") + "\",\"name\":\"sample\"}]}");
            response.Headers.TryAddWithoutValidation("x-ms-continuationtoken", (++pages).ToString(CultureInfo.InvariantCulture));
            return Task.FromResult(response);
        };
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5));
        AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() => pipeline.GetPagesAsync(
            EndpointRegistry.ProjectsList with { Paging = PagingStrategy.ContinuationHeader }, AdoJsonContext.Default.ProjectPageDto,
            static page => page.Value, static item => item.Id ?? "", CultureInfo.InvariantCulture, TestContext.Current.CancellationToken,
            maximumPages: 3));
        Assert.Equal(EndpointRegistry.ProjectsList.Name, error.Operation);
        Assert.Equal(3, pages);
        Assert.Equal(10000, AdoHttpPipeline.MaximumPages); // §6.4
    }
}
