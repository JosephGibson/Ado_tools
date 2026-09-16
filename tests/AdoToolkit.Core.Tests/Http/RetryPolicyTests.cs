using System.Net;
using System.Net.Http;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.Tests.Http;

public sealed class RetryPolicyTests
{
    [Theory]
    [Trait("Acceptance", "S0-3")]
    [InlineData(429)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task SafeTransientStatusRetriesAtMostThreeTimes(int status)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("unavailable.json", status));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("unavailable.json", status));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("unavailable.json", status));
        using HttpClient client = new(handler);
        FakeClock clock = new();
        CapturingLog log = new();
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5), log, clock);
        Exception? error = await Record.ExceptionAsync(() => Fetch(pipeline));
        Assert.IsAssignableFrom<AdoException>(error);
        Assert.Equal(status == 429 ? typeof(AdoThrottledException) : typeof(AdoServerException), error!.GetType());
        Assert.True(((AdoException)error).IsRetryable);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, clock.Delays);
        Assert.Equal(2, log.Messages.Count(message => message.Contains("Retrying", StringComparison.Ordinal)));
        Assert.NotSame(handler.Requests[0].Request, handler.Requests[1].Request);
    }

    [Fact]
    [Trait("Acceptance", "S0-3")]
    public async Task RetryAfterIsHonoredAndPostContentIsRecreated()
    {
        using FakeHttpMessageHandler handler = new();
        HttpResponseMessage throttled = FakeHttpMessageHandler.Fixture("throttled.json", 429);
        throttled.Headers.TryAddWithoutValidation("Retry-After", "7");
        handler.Enqueue(throttled);
        handler.Enqueue(FakeHttpMessageHandler.Response("{}"));
        using HttpClient client = new(handler);
        FakeClock clock = new();
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5), clock: clock);
        EndpointDefinition safePost = EndpointRegistry.ProjectsList with { Method = HttpMethod.Post };
        string value = await pipeline.ExecuteAsync(safePost, null, null, Encoding.UTF8.GetBytes("{\"sample\":true}"), CultureInfo.InvariantCulture,
            (response, token) => response.Content.ReadAsStringAsync(token), TestContext.Current.CancellationToken);
        Assert.Equal("{}", value);
        Assert.Equal(TimeSpan.FromSeconds(7), Assert.Single(clock.Delays));
        Assert.Equal(handler.Requests[0].Body, handler.Requests[1].Body);
        Assert.NotSame(handler.Requests[0].Request.Content, handler.Requests[1].Request.Content);
    }

    [Theory]
    [Trait("Acceptance", "S0-3")]
    [Trait("Acceptance", "S0-8")]
    [InlineData("120", 60)]
    [InlineData("-1", 0)]
    [InlineData("Thu, 01 Jan 2026 00:00:12 GMT", 12)]
    [InlineData("Wed, 31 Dec 2025 23:59:59 GMT", 0)]
    [InlineData("invalid", 1)]
    public void RetryAfterParsingIsInvariantAndBounded(string header, double expected)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "en-US", "fr-CA" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                using HttpResponseMessage response = new(HttpStatusCode.TooManyRequests);
                response.Headers.TryAddWithoutValidation("Retry-After", header);
                Assert.Equal(TimeSpan.FromSeconds(expected), new RetryPolicy(new FakeClock()).Delay(response, 1));
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [Trait("Acceptance", "S0-3")]
    [InlineData(0, 0.8)]
    [InlineData(1, 1.2)]
    public void JitterStaysWithinTwentyPercent(double jitter, double seconds)
    {
        RetryPolicy policy = new(new FakeClock { Jitter = jitter });
        Assert.Equal(TimeSpan.FromSeconds(seconds), policy.Delay(null, 1));
        Assert.Equal(TimeSpan.FromSeconds(seconds * 2), policy.Delay(null, 2));
    }

    [Theory]
    [Trait("Acceptance", "S0-3")]
    [InlineData(500, true)]
    [InlineData(503, false)]
    public async Task DoesNotRetryInternalErrorsOrUnsafeOperations(int status, bool safe)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("server-error.json", status));
        using HttpClient client = new(handler);
        FakeClock clock = new();
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5), clock: clock);
        await Assert.ThrowsAsync<AdoServerException>(() => Fetch(pipeline, EndpointRegistry.ProjectsList with { IsSafeToRetry = safe }));
        Assert.Single(handler.Requests);
        Assert.Empty(clock.Delays);
    }

    [Theory]
    [Trait("Acceptance", "S0-3")]
    [InlineData(HttpRequestError.ConnectionError, 2)]
    [InlineData(HttpRequestError.NameResolutionError, 2)]
    [InlineData(HttpRequestError.SecureConnectionError, 1)]
    [InlineData(HttpRequestError.UserAuthenticationError, 1)]
    [InlineData(HttpRequestError.InvalidResponse, 1)]
    public async Task OnlyConnectionLevelTransportErrorsAreRetried(HttpRequestError kind, int attempts)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue((_, _) => throw new HttpRequestException(kind, "synthetic failure"));
        handler.Enqueue(FakeHttpMessageHandler.Fixture("projects-empty.json"));
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, new Uri("https://ado.example.test/Collection"), TimeSpan.FromSeconds(5), clock: new FakeClock());
        if (attempts == 2) Assert.Empty(await Fetch(pipeline));
        else await Assert.ThrowsAsync<AdoRequestException>(() => Fetch(pipeline));
        Assert.Equal(attempts, handler.Requests.Count);
    }

    internal static Task<IReadOnlyList<ProjectDto>> Fetch(AdoHttpPipeline pipeline, EndpointDefinition? endpoint = null, CancellationToken? token = null) =>
        pipeline.GetPagesAsync(endpoint ?? EndpointRegistry.ProjectsList, AdoJsonContext.Default.ProjectPageDto,
            static page => page.Value, static item => item.Id ?? "", CultureInfo.InvariantCulture, token ?? TestContext.Current.CancellationToken);
}
