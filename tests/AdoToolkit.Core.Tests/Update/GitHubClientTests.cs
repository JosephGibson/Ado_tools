using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.Update;

namespace AdoToolkit.Core.Tests.Update;

// The updater's HTTP behavior over a fake handler: no request reaches GitHub.
public sealed class GitHubClientTests
{
    private const string Latest = "https://api.github.com/repos/JosephGibson/Ado_tools/releases/latest";
    private const string Download = "https://github.com/JosephGibson/Ado_tools/releases/download/v1.2.3/AdoToolkit-1.2.3.zip";
    private const string AssetHost = "https://release-assets.githubusercontent.com/github-production-release-asset/1/x";
    private static readonly Version Version = new(1, 2, 3);
    private static readonly CultureInfo Culture = UpdateFixture.Culture;
    private static readonly ReleaseReader.Asset Asset = new("AdoToolkit-1.2.3.zip", 5, new string('0', 64));

    [Fact]
    public async Task TheApiRequestNamesTheToolkitAndSendsNoCredentials()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response("{}"));

        Assert.Equal("{}", Encoding.UTF8.GetString(await Client(handler).GetLatestReleaseAsync(Culture, TestContext.Current.CancellationToken)));

        HttpRequestMessage request = Assert.Single(handler.Requests).Request;
        Assert.Equal(new Uri(Latest), request.RequestUri);
        Assert.Equal("AdoToolkit/0.11.0", request.Headers.UserAgent.ToString());
        Assert.Equal("application/vnd.github+json", request.Headers.Accept.ToString());
        Assert.Equal("2022-11-28", Assert.Single(request.Headers.GetValues("X-GitHub-Api-Version")));
        Assert.Null(request.Headers.Authorization);
        Assert.False(request.Headers.Contains("Cookie"));
    }

    [Fact]
    public async Task ADownloadFollowsGitHubsRedirectAndNeverLogsTheSignedQuery()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(UpdateFixture.Redirect(AssetHost + "?sp=r&sig=signed-secret"));
        handler.Enqueue(UpdateFixture.Bytes([1, 2, 3, 4, 5]));
        CapturingLog log = new();
        using MemoryStream body = new();
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        await Client(handler, log: log).DownloadAsync(Version, Asset, body, hash, TimeSpan.FromMinutes(5), null, Culture,
            TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3, 4, 5], body.ToArray());
        Assert.Equal(UpdateFixture.Sha256([1, 2, 3, 4, 5]), Convert.ToHexStringLower(hash.GetHashAndReset()));
        Assert.Equal([new Uri(Download), new Uri(AssetHost + "?sp=r&sig=signed-secret")], handler.Requests.Select(request => request.Uri));
        Assert.Equal("application/octet-stream", handler.Requests[1].Request.Headers.Accept.ToString());
        Assert.DoesNotContain(log.Messages, message => message.Contains("sig=", StringComparison.Ordinal) || message.Contains("sp=", StringComparison.Ordinal));
        Assert.Contains(log.Messages, message => message.Contains("release-assets.githubusercontent.com/github-production-release-asset/1/x", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://evil.example.test/asset")]
    [InlineData("http://release-assets.githubusercontent.com/asset")]
    [InlineData("https://user:secret@release-assets.githubusercontent.com/asset")]
    [InlineData("https://release-assets.githubusercontent.com:8443/asset")]
    [InlineData("https://release-assets.githubusercontent.com.evil.test/asset")]
    [InlineData("https://objects.githubusercontent.com/asset")]
    [InlineData("ftp://github.com/asset")]
    public async Task ARedirectOutsideTheAllowedHostsOrHttpsIsBlocked(string location)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(UpdateFixture.Redirect(location + "?sig=signed-secret"));

        AdoRedirectException error = await Assert.ThrowsAsync<AdoRedirectException>(() => DownloadAsync(handler));

        Assert.Single(handler.Requests);
        Assert.DoesNotContain("sig=", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SixHopsOrNoLocationEndTheRedirects()
    {
        using FakeHttpMessageHandler handler = new();
        for (int hop = 0; hop < 6; hop++) handler.Enqueue(UpdateFixture.Redirect(AssetHost + hop.ToString(CultureInfo.InvariantCulture)));
        await Assert.ThrowsAsync<AdoRedirectException>(() => DownloadAsync(handler));
        Assert.Equal(6, handler.Requests.Count);

        using FakeHttpMessageHandler empty = new();
        empty.Enqueue(new HttpResponseMessage(HttpStatusCode.Found) { Content = new ByteArrayContent([]) });
        AdoRedirectException error = await Assert.ThrowsAsync<AdoRedirectException>(() => DownloadAsync(empty));
        Assert.Equal(Messages.Get(AdoMessage.UpdateRedirectLimit, Culture, "github.com/JosephGibson/Ado_tools/releases/download/v1.2.3/AdoToolkit-1.2.3.zip"), error.Message);
    }

    [Fact]
    public async Task AnExhaustedRateLimitShowsTheResetTimeInTheLocalZone()
    {
        ManualTimeProvider time = new() { Zone = TimeZoneInfo.Utc };
        using FakeHttpMessageHandler handler = new();
        HttpResponseMessage response = FakeHttpMessageHandler.Response("{\"message\":\"API rate limit exceeded\"}", 403);
        response.Headers.Add("x-ratelimit-remaining", "0");
        response.Headers.Add("x-ratelimit-reset", time.GetUtcNow().AddMinutes(42).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        handler.Enqueue(response);

        AdoThrottledException error = await Assert.ThrowsAsync<AdoThrottledException>(() => Client(handler, time).GetLatestReleaseAsync(Culture,
            TestContext.Current.CancellationToken));

        Assert.Equal(Messages.Get(AdoMessage.UpdateRateLimited, Culture, new DateTime(2026, 1, 1, 0, 42, 0)), error.Message);
        Assert.Equal(403, error.StatusCode);
        Assert.DoesNotContain("API rate limit", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASecondaryLimitUsesRetryAfter()
    {
        ManualTimeProvider time = new() { Zone = TimeZoneInfo.Utc };
        using FakeHttpMessageHandler handler = new();
        HttpResponseMessage response = FakeHttpMessageHandler.Response("{}", 403);
        response.Headers.Add("Retry-After", "120");
        handler.Enqueue(response);

        AdoThrottledException error = await Assert.ThrowsAsync<AdoThrottledException>(() => Client(handler, time).GetLatestReleaseAsync(Culture,
            TestContext.Current.CancellationToken));

        Assert.Equal(Messages.Get(AdoMessage.UpdateRateLimited, Culture, new DateTime(2026, 1, 1, 0, 2, 0)), error.Message);
    }

    [Fact]
    public async Task A429WithoutAResetSaysLater()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response("{}", 429));

        AdoThrottledException error = await Assert.ThrowsAsync<AdoThrottledException>(() => Client(handler).GetLatestReleaseAsync(Culture,
            TestContext.Current.CancellationToken));

        Assert.Equal(Messages.Get(AdoMessage.UpdateRateLimitedLater, Culture), error.Message);
    }

    public static TheoryData<int, Type> Statuses => new()
    {
        { 403, typeof(AdoAuthorizationException) },
        { 404, typeof(AdoNotFoundException) },
        { 407, typeof(AdoAuthenticationException) },
        { 418, typeof(AdoRequestException) },
        { 500, typeof(AdoServerException) },
        { 503, typeof(AdoServerException) },
        { 204, typeof(AdoRequestException) },
    };

    [Theory]
    [MemberData(nameof(Statuses))]
    public async Task EachStatusHasItsErrorType(int status, Type expected)
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response("{\"message\":\"<b>remote</b>\"}", status));

        AdoException error = await Assert.ThrowsAnyAsync<AdoException>(() => Client(handler).GetLatestReleaseAsync(Culture,
            TestContext.Current.CancellationToken));

        Assert.IsType(expected, error);
        Assert.DoesNotContain("remote", error.Message, StringComparison.Ordinal);
        Assert.Equal(UpdateHttp.LatestOperation, error.Operation);
    }

    [Fact]
    public async Task NotFoundNamesTheReleaseOrTheAsset()
    {
        using FakeHttpMessageHandler api = new();
        api.Enqueue(FakeHttpMessageHandler.Response("{}", 404));
        AdoNotFoundException none = await Assert.ThrowsAsync<AdoNotFoundException>(() => Client(api).GetLatestReleaseAsync(Culture,
            TestContext.Current.CancellationToken));
        Assert.Equal(Messages.Get(AdoMessage.UpdateNoRelease, Culture), none.Message);

        using FakeHttpMessageHandler asset = new();
        asset.Enqueue(FakeHttpMessageHandler.Response("Not Found", 404, "text/plain"));
        AdoNotFoundException missing = await Assert.ThrowsAsync<AdoNotFoundException>(() => DownloadAsync(asset));
        Assert.Equal(Messages.Get(AdoMessage.UpdateAssetMissing, Culture, "1.2.3", "AdoToolkit-1.2.3.zip"), missing.Message);
    }

    // A proxy answers the HTTPS CONNECT tunnel; .NET reports its status as an exception, as the
    // planning spike observed with a loopback proxy.
    [Fact]
    public async Task AProxyThatRefusesTheTunnelIsNamedAsTheCause()
    {
        using FakeHttpMessageHandler authentication = new();
        authentication.Enqueue((_, _) => throw new HttpRequestException(HttpRequestError.ProxyTunnelError, "407", null, HttpStatusCode.ProxyAuthenticationRequired));
        AdoAuthenticationException signIn = await Assert.ThrowsAsync<AdoAuthenticationException>(() => Client(authentication).GetLatestReleaseAsync(Culture,
            TestContext.Current.CancellationToken));
        Assert.Equal(Messages.Get(AdoMessage.UpdateProxyAuthentication, Culture, "api.github.com"), signIn.Message);

        using FakeHttpMessageHandler blocked = new();
        blocked.Enqueue((_, _) => throw new HttpRequestException(HttpRequestError.ProxyTunnelError, "403", null, HttpStatusCode.Forbidden));
        AdoRequestException refused = await Assert.ThrowsAsync<AdoRequestException>(() => Client(blocked).GetLatestReleaseAsync(Culture,
            TestContext.Current.CancellationToken));
        Assert.Equal(Messages.Get(AdoMessage.UpdateProxyRefused, Culture, "api.github.com", "403"), refused.Message);
    }

    [Fact]
    public async Task AConnectionThatFailsOrIsLostMidBodyIsATransportError()
    {
        foreach (HttpRequestError cause in new[] { HttpRequestError.NameResolutionError, HttpRequestError.ConnectionError })
        {
            using FakeHttpMessageHandler unreachable = new();
            unreachable.Enqueue((_, _) => throw new HttpRequestException(cause, "failed"));
            AdoRequestException error = await Assert.ThrowsAsync<AdoRequestException>(() => Client(unreachable).GetLatestReleaseAsync(Culture,
                TestContext.Current.CancellationToken));
            Assert.Equal(Messages.Get(AdoMessage.UpdateUnreachable, Culture, "api.github.com", cause.ToString()), error.Message);
        }

        using FakeHttpMessageHandler dropped = new();
        dropped.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new ChunkStream([1, 2], new HttpIOException(HttpRequestError.ResponseEnded, "ended"))),
        });
        AdoRequestException lost = await Assert.ThrowsAsync<AdoRequestException>(() => DownloadAsync(dropped));
        Assert.Equal(Messages.Get(AdoMessage.UpdateConnectionLost, Culture, "github.com/JosephGibson/Ado_tools/releases/download/v1.2.3/AdoToolkit-1.2.3.zip"),
            lost.Message);
    }

    // SocketsHttpHandler.ConnectTimeout ends the request as a cancellation that holds a TimeoutException,
    // with neither the caller's token nor the total timer cancelled: a host that a firewall drops.
    [Fact]
    public async Task AConnectTimeoutIsUnreachableNotTheTotalTimeLimit()
    {
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue((_, _) => throw new TaskCanceledException("connect", new TimeoutException("connect timed out")));

        AdoRequestException error = await Assert.ThrowsAsync<AdoRequestException>(() => DownloadAsync(handler));

        Assert.Equal(Messages.Get(AdoMessage.UpdateUnreachable, Culture, "github.com", "ConnectTimeout"), error.Message);
        Assert.Equal(UpdateHttp.DownloadOperation, error.Operation);
    }

    [Fact]
    public async Task TheTotalTimeLimitIsATimeout()
    {
        ManualTimeProvider time = new();
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue((_, token) =>
        {
            time.Advance(TimeSpan.FromSeconds(61));
            token.ThrowIfCancellationRequested();
            return Task.FromResult(FakeHttpMessageHandler.Response("{}"));
        });

        AdoTimeoutException error = await Assert.ThrowsAsync<AdoTimeoutException>(() => Client(handler, time).GetLatestReleaseAsync(Culture,
            TestContext.Current.CancellationToken));

        Assert.Equal(Messages.Get(AdoMessage.UpdateTimeoutSeconds, Culture, "api.github.com/repos/JosephGibson/Ado_tools/releases/latest", "60"), error.Message);
    }

    [Fact]
    public async Task AStalledReadIsATimeout()
    {
        ManualTimeProvider time = new();
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StallingStream(time, TimeSpan.FromSeconds(61))) });

        AdoTimeoutException error = await Assert.ThrowsAsync<AdoTimeoutException>(() => DownloadAsync(handler, time));

        Assert.Equal(Messages.Get(AdoMessage.UpdateStalled, Culture, "github.com", "60"), error.Message);
    }

    // The cmdlet's log throws once the caller has cancelled; the response must not wait for the collector.
    [Fact]
    public async Task AResponseIsDisposedWhenTheVerboseLogThrows()
    {
        DisposalStream body = new();
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });

        await Assert.ThrowsAsync<OperationCanceledException>(() => new GitHubClient(new HttpClient(handler, disposeHandler: false), new UpdateHttp.Limits(),
            new ManualTimeProvider(), Version, new CancelledLog()).GetLatestReleaseAsync(Culture, TestContext.Current.CancellationToken));

        Assert.True(body.Disposed);
    }

    [Fact]
    public async Task TheCallersCancellationIsNotATimeout()
    {
        using CancellationTokenSource cancellation = new();
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue((_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(FakeHttpMessageHandler.Response("{}"));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).GetLatestReleaseAsync(Culture, cancellation.Token));
    }

    [Fact]
    public async Task ABodyOfAnotherLengthThanTheReleaseSaysFails()
    {
        // Announced by the header.
        using FakeHttpMessageHandler announced = new();
        announced.Enqueue(UpdateFixture.Bytes([1, 2, 3, 4, 5, 6, 7]));
        AdoResponseFormatException header = await Assert.ThrowsAsync<AdoResponseFormatException>(() => DownloadAsync(announced));
        Assert.Equal(Messages.Get(AdoMessage.UpdateSizeMismatch, Culture, "AdoToolkit-1.2.3.zip", 7L, 5L), header.Message);

        // Truncated, and longer, without a length header: no byte past the size is written.
        using FakeHttpMessageHandler streamed = new();
        streamed.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new ChunkStream([1, 2, 3])) });
        streamed.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new ChunkStream([1, 2, 3, 4, 5, 6, 7])) });
        AdoResponseFormatException truncated = await Assert.ThrowsAsync<AdoResponseFormatException>(() => DownloadAsync(streamed));
        Assert.Equal(Messages.Get(AdoMessage.UpdateSizeMismatch, Culture, "AdoToolkit-1.2.3.zip", 3L, 5L), truncated.Message);
        using MemoryStream body = new();
        await Assert.ThrowsAsync<AdoResponseFormatException>(() => Client(streamed).DownloadAsync(Version, Asset, body, null, TimeSpan.FromMinutes(5),
            null, Culture, TestContext.Current.CancellationToken));
        Assert.InRange(body.Length, 0, Asset.Size);
    }

    [Fact]
    public async Task AReleaseDescriptionOverItsCapIsAFormatError()
    {
        UpdateHttp.Limits limits = new() { ReleaseBytes = 4 };
        using FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Response("{\"a\":1}"));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new ChunkStream(Encoding.UTF8.GetBytes("{\"a\":1}"))) });

        await Assert.ThrowsAsync<AdoResponseFormatException>(() => Client(handler, limits: limits).GetLatestReleaseAsync(Culture, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AdoResponseFormatException>(() => Client(handler, limits: limits).GetLatestReleaseAsync(Culture, TestContext.Current.CancellationToken));
    }

    private static GitHubClient Client(FakeHttpMessageHandler handler, TimeProvider? time = null, CapturingLog? log = null, UpdateHttp.Limits? limits = null) =>
        new(new HttpClient(handler, disposeHandler: false), limits ?? new UpdateHttp.Limits(), time ?? new ManualTimeProvider(), new Version(0, 11, 0),
            log ?? new CapturingLog());

    private static async Task DownloadAsync(FakeHttpMessageHandler handler, TimeProvider? time = null)
    {
        using MemoryStream body = new();
        await Client(handler, time).DownloadAsync(Version, Asset, body, null, TimeSpan.FromMinutes(5), null, Culture, TestContext.Current.CancellationToken);
    }

    // A body without a length: the bytes in two reads, then the end or a failure.
    private sealed class ChunkStream(byte[] bytes, Exception? failure = null) : Stream
    {
        private int position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (position >= bytes.Length)
            {
                if (failure is not null) throw failure;
                return 0;
            }
            int length = Math.Min(Math.Min(count, bytes.Length - position), Math.Max(1, bytes.Length / 2));
            Array.Copy(bytes, position, buffer, offset, length);
            position += length;
            return length;
        }
    }

    private sealed class CancelledLog : IAdoLog
    {
        public void Verbose(string message) => throw new OperationCanceledException();
        public void Debug(string message) { }
        public void Warning(string message) { }
        public void Progress(AdoProgress progress) { }
    }

    private sealed class DisposalStream() : MemoryStream([])
    {
        internal bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    // A read that moves the manual clock past the stall limit, then honours the cancellation it caused.
    private sealed class StallingStream(ManualTimeProvider time, TimeSpan wait) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            time.Advance(wait);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(0);
        }
    }
}
