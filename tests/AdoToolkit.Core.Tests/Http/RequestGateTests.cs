using System.Net.Http;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.Tests.Http;

// The gate bounds the requests one retrieval has in flight. A pipeline without one is unchanged.
[Trait("Culture", "Invariant")]
public sealed class RequestGateTests
{
    private static readonly Uri Collection = new("https://ado.example.test/Collection");

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task RequestsInFlightNeverExceedTheGateAndReachItWhenThereIsEnoughWork(int maximum)
    {
        using FakeHttpMessageHandler handler = new()
        {
            Fallback = async (_, token) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(15), token);
                return FakeHttpMessageHandler.Response("{}");
            },
        };
        using HttpClient client = new(handler);
        using RequestGate gate = new(maximum);
        AdoHttpPipeline pipeline = new(client, Collection, TimeSpan.FromSeconds(30), gate: gate);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => SendAsync(pipeline, TestContext.Current.CancellationToken)));
        Assert.Equal(12, handler.Requests.Count);
        Assert.Equal(maximum, handler.PeakInFlight);
        Assert.Equal(maximum, gate.Maximum);
    }

    [Fact]
    public async Task PipelineWithoutAGateSendsEveryRequestAtOnce()
    {
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using FakeHttpMessageHandler handler = new()
        {
            Fallback = async (_, _) =>
            {
                await release.Task;
                return FakeHttpMessageHandler.Response("{}");
            },
        };
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, Collection, TimeSpan.FromSeconds(30));
        Task[] calls = [.. Enumerable.Range(0, 5).Select(_ => SendAsync(pipeline, TestContext.Current.CancellationToken))];
        // Every request reaches the handler while none has been answered.
        while (handler.PeakInFlight < 5) await Task.Delay(5, TestContext.Current.CancellationToken);
        release.SetResult();
        await Task.WhenAll(calls);
        Assert.Equal(5, handler.PeakInFlight);
    }

    // The first request holds the only slot until it times out. The second waits for the slot for
    // that whole time, which is the request timeout, and then still has its own full timeout: it
    // succeeds although more than the timeout has passed since it was asked for. The slot of the
    // failed request was released, or the second request would never have started.
    [Fact]
    public async Task TimeSpentWaitingForASlotIsNotRequestTimeAndAFailedRequestReleasesItsSlot()
    {
        TimeSpan timeout = TimeSpan.FromMilliseconds(500);
        int received = 0;
        using FakeHttpMessageHandler handler = new()
        {
            Fallback = async (_, token) =>
            {
                if (Interlocked.Increment(ref received) == 1) await Task.Delay(Timeout.InfiniteTimeSpan, token);
                await Task.Delay(TimeSpan.FromMilliseconds(50), token);
                return FakeHttpMessageHandler.Response("{}");
            },
        };
        using HttpClient client = new(handler);
        using RequestGate gate = new(1);
        AdoHttpPipeline pipeline = new(client, Collection, timeout, gate: gate);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        Task<bool> first = SendAsync(pipeline, TestContext.Current.CancellationToken);
        while (handler.Requests.Count < 1) await Task.Delay(5, TestContext.Current.CancellationToken);
        Task<bool> second = SendAsync(pipeline, TestContext.Current.CancellationToken);
        Task<bool> third = SendAsync(pipeline, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<AdoTimeoutException>(() => first);
        Assert.True(await second);
        Assert.True(await third);
        Assert.True(System.Diagnostics.Stopwatch.GetElapsedTime(started) > timeout);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(1, handler.PeakInFlight);
    }

    [Fact]
    public async Task CancellationReleasesTheSlotWhetherTheRequestHeldItOrWasWaitingForIt()
    {
        int received = 0;
        using FakeHttpMessageHandler handler = new()
        {
            Fallback = async (_, token) =>
            {
                if (Interlocked.Increment(ref received) == 1) await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return FakeHttpMessageHandler.Response("{}");
            },
        };
        using HttpClient client = new(handler);
        using RequestGate gate = new(1);
        AdoHttpPipeline pipeline = new(client, Collection, TimeSpan.FromSeconds(30), gate: gate);
        using CancellationTokenSource holder = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using CancellationTokenSource waiter = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<bool> holding = SendAsync(pipeline, holder.Token);
        while (handler.Requests.Count < 1) await Task.Delay(5, TestContext.Current.CancellationToken);
        Task<bool> waiting = SendAsync(pipeline, waiter.Token);
        // The waiting request never reaches the handler: it is cancelled before it gets a slot.
        await waiter.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Single(handler.Requests);
        await holder.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => holding);
        // The slot is free again.
        Assert.True(await SendAsync(pipeline, TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void GateNeedsAtLeastOneSlot()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RequestGate(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RequestGate(-1));
    }

    private static Task<bool> SendAsync(AdoHttpPipeline pipeline, CancellationToken cancellationToken) =>
        pipeline.ExecuteAsync(EndpointRegistry.ProjectsList, null, null, null, CultureInfo.InvariantCulture,
            static (_, _) => Task.FromResult(true), cancellationToken);
}
