using System.Net.Http;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.Tests.Http;

[Trait("Culture", "Invariant")]
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
        await Assert.ThrowsAsync<AdoTimeoutException>(() => Download(pipeline));
        Assert.True(stream.Disposed);
        Assert.Single(handler.Requests);
    }

    // Twelve reads that each take nine tenths of the inactivity window on a manual clock: together they
    // last more than ten windows, and none of them reaches one. No real time passes.
    [Fact]
    [Trait("Acceptance", "S0-3")]
    public async Task DownloadInactivityBudgetResetsAfterEachRead()
    {
        ManualTimeProvider time = new();
        using FakeHttpMessageHandler handler = new();
        using ClockedStream stream = new(12, time, TimeSpan.FromMilliseconds(900));
        handler.Enqueue(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StreamContent(stream) });
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5),
            downloadTimeout: TimeSpan.FromSeconds(5), inactivityTimeout: TimeSpan.FromSeconds(1), timeProvider: time);
        Assert.Equal(new string('x', 12), Encoding.UTF8.GetString(await Download(pipeline)));
        Assert.True(stream.Disposed);
    }

    // Each read moves the manual clock on by its duration, then honours a cancellation that the
    // move caused, as a stream still waiting for its bytes would.
    private sealed class ClockedStream(int chunks, ManualTimeProvider time, TimeSpan duration) : MemoryStream
    {
        private int remaining = chunks;
        internal bool Disposed { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (remaining-- <= 0) return ValueTask.FromResult(0);
            time.Advance(duration);
            cancellationToken.ThrowIfCancellationRequested();
            buffer.Span[0] = (byte)'x';
            return ValueTask.FromResult(1);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    // The whole body, read through the stream that the pipeline hands to a download.
    private static Task<byte[]> Download(AdoHttpPipeline pipeline) => pipeline.DownloadStreamAsync(
        EndpointRegistry.ProjectsList with { Timeout = TimeoutClass.Download }, new Dictionary<string, string>(), null, CultureInfo.InvariantCulture,
        static async (_, body, token) =>
        {
            using MemoryStream output = new();
            await body.CopyToAsync(output, token);
            return output.ToArray();
        }, TestContext.Current.CancellationToken);

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
