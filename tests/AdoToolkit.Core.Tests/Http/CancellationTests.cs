using System.Net.Http;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.Tests.Http;

public sealed class CancellationTests
{
    [Fact]
    [Trait("Acceptance", "S0-4")]
    public async Task CallerCancellationReachesAnInFlightRequestWithoutRetry()
    {
        using FakeHttpMessageHandler handler = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Enqueue(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("unreachable");
        });
        using HttpClient client = new(handler);
        using CancellationTokenSource caller = new();
        FakeClock clock = new();
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(10), clock: clock);
        Task pending = RetryPolicyTests.Fetch(pipeline, token: caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        caller.Cancel();
        OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(caller.Token, error.CancellationToken);
        Assert.Single(handler.Requests);
        Assert.Empty(clock.Delays);
    }

    [Theory]
    [Trait("Acceptance", "S0-3")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MetadataAndQueryUseTheirLinkedBudget(bool query)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("unreachable");
        });
        using HttpClient client = new(handler);
        FakeClock clock = new();
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromMilliseconds(40), clock: clock);
        await Assert.ThrowsAsync<AdoTimeoutException>(() => RetryPolicyTests.Fetch(pipeline,
            EndpointRegistry.ProjectsList with { Timeout = query ? TimeoutClass.Query : TimeoutClass.Metadata }));
        Assert.Single(handler.Requests);
        Assert.Empty(clock.Delays);
    }

    [Fact]
    [Trait("Acceptance", "S0-3")]
    public async Task RetryDelayIsIncludedInTheOperationBudget()
    {
        using FakeHttpMessageHandler handler = new();
        HttpResponseMessage response = FakeHttpMessageHandler.Fixture("unavailable.json", 503);
        response.Headers.TryAddWithoutValidation("Retry-After", "60");
        handler.Enqueue(response);
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromMilliseconds(40));
        await Assert.ThrowsAsync<AdoTimeoutException>(() => RetryPolicyTests.Fetch(pipeline));
        Assert.Single(handler.Requests);
    }

    [Theory]
    [Trait("Acceptance", "S0-3")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DownloadsEnforceInactivityAndTotalBudgets(bool total)
    {
        using FakeHttpMessageHandler handler = new();
        using DripStream stream = new(100, total ? TimeSpan.FromMilliseconds(15) : TimeSpan.FromSeconds(10));
        handler.Enqueue(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StreamContent(stream) });
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5),
            downloadTimeout: TimeSpan.FromMilliseconds(100), inactivityTimeout: TimeSpan.FromMilliseconds(40));
        await Assert.ThrowsAsync<AdoTimeoutException>(() => pipeline.DownloadAsync(
            EndpointRegistry.ProjectsList with { Timeout = TimeoutClass.Download }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.True(stream.Disposed);
        Assert.Single(handler.Requests);
    }

    [Fact]
    [Trait("Acceptance", "S0-3")]
    public async Task DownloadInactivityBudgetResetsAfterEachRead()
    {
        using FakeHttpMessageHandler handler = new();
        using DripStream stream = new(4, TimeSpan.FromMilliseconds(20));
        handler.Enqueue(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StreamContent(stream) });
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5),
            downloadTimeout: TimeSpan.FromSeconds(2), inactivityTimeout: TimeSpan.FromMilliseconds(60));
        byte[] result = await pipeline.DownloadAsync(EndpointRegistry.ProjectsList with { Timeout = TimeoutClass.Download },
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal("xxxx", Encoding.UTF8.GetString(result));
        Assert.True(stream.Disposed);
    }

    [Fact]
    [Trait("Acceptance", "S0-3")]
    public async Task RetryAndSuccessResponsesAreDisposed()
    {
        using FakeHttpMessageHandler handler = new();
        using TrackingStream first = new(Encoding.UTF8.GetBytes("{}"));
        using TrackingStream second = new(Encoding.UTF8.GetBytes("{\"value\":[]}"));
        handler.Enqueue(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable) { Content = new StreamContent(first) });
        handler.Enqueue(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StreamContent(second) });
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5), clock: new FakeClock());
        Assert.Empty(await RetryPolicyTests.Fetch(pipeline));
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
    }
}
