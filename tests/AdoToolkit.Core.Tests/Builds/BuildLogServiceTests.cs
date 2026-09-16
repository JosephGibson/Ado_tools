using System.Net;
using System.Net.Http;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Tests.Http;

namespace AdoToolkit.Core.Tests.Builds;

[Trait("Acceptance", "S4-3")]
public sealed class BuildLogServiceTests
{
    private static readonly byte[] Original = [255, 0, 128, 13, 10];
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("fr-CA");

    [Theory]
    [InlineData(1000, 200, 800, 999)]
    [InlineData(1, 200, 0, 0)]
    [InlineData(200, 200, 0, 199)]
    [InlineData(201, 200, 1, 200)]
    [InlineData(int.MaxValue, 1, int.MaxValue - 1, int.MaxValue - 1)]
    [InlineData(int.MaxValue, int.MaxValue, 0, int.MaxValue - 1)]
    public void TailUsesZeroBasedInclusiveBounds(int count, int tail, int start, int end) =>
        Assert.Equal((start, end), BuildLogService.TailRange(count, tail));

    [Fact]
    public void EmptyRangeAndInvalidArguments()
    {
        Assert.Null(BuildLogService.TailRange(0, 200));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuildLogService.TailRange(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuildLogService.TailRange(1, 0));
        Assert.Equal("Build-401-Log-11.txt", ReportFileNames.BuildLog(401, 11));
    }

    [Fact]
    public async Task ChunkedTailPreservesEveryByteAndCommitsOnlyAfterValidation()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "saved.txt");
        File.WriteAllBytes(path, Original);
        byte[] bytes = [0xef, 0xbb, 0xbf, 0xff, 0, 13, 10, .. File.ReadAllBytes(Path.Combine(TestDirectory.RepositoryRoot, "tests/Fixtures/Rest/build-log-body.txt"))];
        List<string> temporaries = [];
        AtomicFileWriter writer = new((stage, temporary) =>
        {
            Assert.Equal(directory.Root, Path.GetDirectoryName(temporary));
            Assert.Equal(Original, File.ReadAllBytes(path));
            if (stage == AtomicWriteStage.TemporaryCreated) temporaries.Add(temporary);
            if (stage == AtomicWriteStage.Validated) Assert.Equal(bytes, File.ReadAllBytes(temporary));
        });
        using FakeHttpMessageHandler handler = Listed();
        using ChunkedLogStream source = new(bytes, onRead: () =>
        {
            // A buffered HTTP response would read before the destination temporary exists.
            Assert.True(File.Exists(Assert.Single(temporaries)));
            Assert.Equal(Original, File.ReadAllBytes(path));
        });
        handler.Enqueue(Body(source));
        using HttpClient client = new(handler);
        FileInfo result = await Service(client, writer).SaveAsync("Équipe Web", 401, 11, path, 200, Culture, TestContext.Current.CancellationToken);
        Assert.Equal(path, result.FullName);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Single(temporaries);
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
        Assert.True(source.Disposed);
        Assert.Equal(TimeoutClass.Download, EndpointRegistry.BuildLog.Timeout);
        Assert.True(EndpointRegistry.BuildLog.IsSafeToRetry);
        Assert.Equal("GET", handler.Requests[1].Method);
        Assert.Equal("/Collection/%C3%89quipe%20Web/_apis/build/builds/401/logs/11", handler.Requests[1].Uri.AbsolutePath);
        Assert.Equal("?startLine=800&endLine=999&api-version=6.0", handler.Requests[1].Uri.Query);
        Assert.Equal("text/plain", handler.Requests[1].Request.Headers.Accept.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialBodyRetriesUseFreshTemporariesAndNeverRepeatPrefix(bool exhausted)
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "saved.txt");
        File.WriteAllBytes(path, Original);
        byte[] bytes = Encoding.UTF8.GetBytes("first chunk; second chunk; final chunk");
        List<string> temporaries = [];
        AtomicFileWriter writer = new((stage, temporary) =>
        {
            if (stage != AtomicWriteStage.TemporaryCreated) return;
            Assert.All(temporaries, previous => Assert.False(File.Exists(previous)));
            Assert.Equal(Original, File.ReadAllBytes(path));
            temporaries.Add(temporary);
        });
        using FakeHttpMessageHandler handler = Listed();
        List<ChunkedLogStream> streams = [];
        for (int i = 0; i < (exhausted ? 3 : 2); i++)
        {
            ChunkedLogStream stream = new(bytes, exhausted || i == 0);
            streams.Add(stream);
            handler.Enqueue(Body(stream));
        }
        using HttpClient client = new(handler);
        Task<FileInfo> pending = Service(client, writer).SaveAsync("Web", 401, 11, path, null, Culture, TestContext.Current.CancellationToken);
        if (exhausted) await Assert.ThrowsAsync<AdoRequestException>(() => pending);
        else await pending;
        Assert.Equal(exhausted ? Original : bytes, File.ReadAllBytes(path));
        Assert.Equal(exhausted ? 3 : 2, temporaries.Distinct(StringComparer.Ordinal).Count());
        Assert.All(streams, stream => Assert.True(stream.Disposed));
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task FileFailuresAtEveryStageNeverRetryOrReplace(int failedStage)
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "saved.txt");
        File.WriteAllBytes(path, Original);
        AtomicFileWriter writer = new((stage, _) => { if ((int)stage == failedStage) throw new IOException("Injected file failure"); });
        using FakeHttpMessageHandler handler = Listed();
        handler.Enqueue(Body(new ChunkedLogStream(Original)));
        using HttpClient client = new(handler);
        AdoFileOutputException error = await Assert.ThrowsAsync<AdoFileOutputException>(() => Service(client, writer)
            .SaveAsync("Web", 401, 11, path, null, Culture, TestContext.Current.CancellationToken));
        Assert.Contains(path, error.Message, StringComparison.Ordinal);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(Original, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    [Theory]
    [InlineData(12, true)] [InlineData(11, false)] [InlineData(13, false)]
    public async Task EmptyBodyIsValidOnlyWhenListReportsZeroLines(int logId, bool valid)
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "saved.txt");
        File.WriteAllBytes(path, Original);
        using FakeHttpMessageHandler handler = Listed();
        handler.Enqueue(Body(new ChunkedLogStream([])));
        using HttpClient client = new(handler);
        Task<FileInfo> pending = Service(client).SaveAsync("Web", 401, logId, path, 200, Culture, TestContext.Current.CancellationToken);
        if (valid) { await pending; Assert.Empty(File.ReadAllBytes(path)); }
        else { await Assert.ThrowsAsync<AdoResponseFormatException>(() => pending); Assert.Equal(Original, File.ReadAllBytes(path)); }
        Assert.Equal(2, handler.Requests.Count);
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    [Fact]
    public async Task UnknownLineCountWarnsAndDownloadsFullBody()
    {
        using TestDirectory directory = new();
        using FakeHttpMessageHandler handler = Listed();
        handler.Enqueue(Body(new ChunkedLogStream(Original)));
        using HttpClient client = new(handler);
        CapturingLog log = new();
        await Service(client, log: log).SaveAsync("Web", 401, 13, Path.Combine(directory.Root, "saved.txt"), 200, Culture, TestContext.Current.CancellationToken);
        Assert.Equal("?api-version=6.0", handler.Requests[1].Uri.Query);
        Assert.Contains(log.Messages, message => message.Contains("nombre de lignes est inconnu", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InactivityAndTotalBudgetsApplyToBodyAndPreserveDestination(bool total)
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "saved.txt");
        File.WriteAllBytes(path, Original);
        using FakeHttpMessageHandler handler = Listed();
        using DripStream source = new(100, total ? TimeSpan.FromMilliseconds(10) : TimeSpan.FromSeconds(10));
        handler.Enqueue(Body(source));
        using HttpClient client = new(handler);
        AdoHttpPipeline pipeline = new(client, BuildQueryTests.Connection.CollectionUri, TimeSpan.FromSeconds(5),
            downloadTimeout: TimeSpan.FromMilliseconds(150), inactivityTimeout: total ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(40));
        AdoTimeoutException error = await Assert.ThrowsAsync<AdoTimeoutException>(() => new BuildLogService(pipeline, new AtomicFileWriter())
            .SaveAsync("Web", 401, 11, path, null, Culture, TestContext.Current.CancellationToken));
        Assert.Equal("BuildLog", error.Operation);
        Assert.Equal(Original, File.ReadAllBytes(path));
        Assert.Equal(2, handler.Requests.Count);
        Assert.True(source.Disposed);
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    [Fact]
    public async Task CallerCancellationDuringReadCleansTemporaryWithoutRetry()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Root, "saved.txt");
        File.WriteAllBytes(path, Original);
        using CancellationTokenSource caller = new();
        using FakeHttpMessageHandler handler = Listed();
        handler.Enqueue(Body(new ChunkedLogStream(Original, onRead: caller.Cancel)));
        using HttpClient client = new(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(client).SaveAsync("Web", 401, 11, path, null, Culture, caller.Token));
        Assert.Equal(Original, File.ReadAllBytes(path));
        Assert.Equal(2, handler.Requests.Count);
        Assert.Empty(Directory.GetFiles(directory.Root, ".*.tmp"));
    }

    private static FakeHttpMessageHandler Listed()
    {
        FakeHttpMessageHandler handler = new();
        handler.Enqueue(FakeHttpMessageHandler.Fixture("build-logs.json"));
        return handler;
    }
    private static HttpResponseMessage Body(Stream source) => new(HttpStatusCode.OK) { Content = new StreamContent(source) };
    private static BuildLogService Service(HttpClient client, AtomicFileWriter? writer = null, IAdoLog? log = null) =>
        new(new AdoHttpPipeline(client, BuildQueryTests.Connection.CollectionUri, TimeSpan.FromSeconds(5), clock: new FakeClock()), writer ?? new AtomicFileWriter(), log);
}
