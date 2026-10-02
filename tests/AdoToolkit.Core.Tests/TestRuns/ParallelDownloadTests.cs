using System.Net.Http;
using AdoToolkit.Core.Tests.Builds;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

// Attachment bodies are read several at a time, and each file is settled in report order by the
// rules of one-at-a-time downloading. Whatever order the bodies arrive in, the statuses, the
// diagnostics and the files are those of the sequential download.
public sealed class ParallelDownloadTests
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");
    private static readonly byte[] Binary = AttachmentFixture.Bytes("pattern.png");

    // Twelve files that between them meet every outcome: downloaded, wrong content, declared too
    // large, larger than declared, failed after retries, empty, without a declared size, JSON, and
    // one of a sub-result.
    private static readonly AdoTestAttachment[] Mixed =
    [
        Remote(1, "first.txt", 100), Remote(2, "binary.log", 94), Remote(3, "oversize.log", 5000), Remote(4, "understated.txt", 10),
        Remote(5, "failing.txt", 20), Remote(6, "empty.txt", 30), Remote(7, "seventh.txt", 200), Remote(8, "unsized.txt", null),
        Remote(9, "details.json", 189), Remote(10, "tenth.txt", 50), Remote(11, "eleventh.log", 60), Remote(12, "sub-result.txt", 70, 301),
    ];

    private static AttachmentFixture MixedContent() => new AttachmentFixture()
        .Serve(1, Text(100)).Serve(2, Binary).Serve(4, Text(3000)).Serve(5, () => AttachmentFixture.Status(503)).Serve(6, Array.Empty<byte>())
        .Serve(7, Text(200)).Serve(8, Text(150)).Serve(9, AttachmentFixture.Bytes("valid.json")).Serve(10, Text(50)).Serve(11, Text(60)).Serve(12, Text(70));

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(16)]
    public async Task OutcomeEqualsTheSequentialOneWhenBodiesArriveInReverseOrder(int bound)
    {
        Outcome sequential = await DownloadAsync(MixedContent(), Mixed, Limits(1));
        AttachmentFixture reversed = MixedContent();
        // The later a file is in the report, the sooner its body arrives.
        reversed.Delay = static id => TimeSpan.FromMilliseconds((13 - id) * 8);
        Outcome parallel = await DownloadAsync(reversed, Mixed, Limits(bound));

        Assert.Equal(sequential.Statuses, parallel.Statuses);
        Assert.Equal(sequential.Diagnostics, parallel.Diagnostics);
        Assert.Equal(sequential.Files, parallel.Files);
        Assert.Equal(sequential.Folder, parallel.Folder);
        Assert.Equal(sequential.Progress, parallel.Progress);
        // Every size is declared truthfully except one that the per-file cap stops, so the requests are the same too.
        Assert.Equal(sequential.Requested.Order(), parallel.Requested.Order());
        Assert.Equal(1, sequential.Peak);
        Assert.InRange(parallel.Peak, 2, bound);

        // The sequential outcome itself, so that two equal wrong answers cannot pass.
        Assert.Equal(["1:Downloaded:r201-11-a1.txt", "2:ContentMismatch:r201-11-a2.bin", "3:TooLarge:", "4:TooLarge:", "5:Failed:", "6:Failed:",
            "7:Downloaded:r201-11-a7.txt", "8:Downloaded:r201-11-a8.txt", "9:Downloaded:r201-11-a9.json", "10:Downloaded:r201-11-a10.txt",
            "11:Downloaded:r201-11-a11.txt", "12:Downloaded:r201-11-s301-a12.txt"], sequential.Statuses);
        Assert.Equal([DiagnosticCodes.AttachmentContentMismatch + " 2", DiagnosticCodes.AttachmentTooLarge + " 3", DiagnosticCodes.AttachmentTooLarge + " 4",
            DiagnosticCodes.AttachmentDownloadFailed + " 5", DiagnosticCodes.AttachmentDownloadFailed + " 6"], sequential.Diagnostics.Select(static text => text.Split('|')[0]));
        Assert.Equal(Enumerable.Range(1, 12).Select(static step => step.ToString(CultureInfo.InvariantCulture) + "/12"), sequential.Progress);
        // File 3 declares more than the per-file limit and is never requested; file 5 is tried three times.
        Assert.Equal([1, 2, 4, 5, 5, 5, 6, 7, 8, 9, 10, 11, 12], sequential.Requested);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(6, 6)]
    public async Task BodiesBeingReadNeverExceedTheBoundAndReachIt(int bound, int peak)
    {
        AdoTestAttachment[] files = [.. Enumerable.Range(1, 20).Select(static id => Remote(id, "file.txt", 40))];
        AttachmentFixture fixture = new() { Delay = static _ => TimeSpan.FromMilliseconds(40) };
        foreach (AdoTestAttachment file in files) fixture.Serve(file.Id, Text(40));
        Outcome outcome = await DownloadAsync(fixture, files, Limits(bound));
        Assert.All(outcome.Statuses, static status => Assert.Contains(":Downloaded:", status, StringComparison.Ordinal));
        Assert.Equal(peak, outcome.Peak);
        Assert.Equal(20, outcome.Requested.Count);
    }

    // The total is spent in report order. With truthful sizes a file is read ahead only when it
    // certainly fits, so the file that does not fit, and every file after it, is never requested,
    // exactly as when files are read one at a time.
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task TotalBudgetStopsTheSameFilesAndSendsTheSameRequestsWhenSizesAreTruthful(int bound)
    {
        AdoTestAttachment[] files = [Remote(1, "a.txt", 100), Remote(2, "b.txt", 100), Remote(3, "c.txt", 100), Remote(4, "d.txt", 150), Remote(5, "e.txt", 50)];
        AttachmentFixture fixture = new() { Delay = static id => TimeSpan.FromMilliseconds((6 - id) * 10) };
        foreach (AdoTestAttachment file in files) fixture.Serve(file.Id, Text((int)file.Size!.Value));
        Outcome outcome = await DownloadAsync(fixture, files, Limits(bound, total: 400));
        Assert.Equal(["1:Downloaded:r201-11-a1.txt", "2:Downloaded:r201-11-a2.txt", "3:Downloaded:r201-11-a3.txt", "4:BudgetExceeded:", "5:BudgetExceeded:"], outcome.Statuses);
        // Reported once, for the first file that did not fit; the one after it would have fitted.
        Assert.Equal([DiagnosticCodes.AttachmentBudgetExceeded + " 400|4"], outcome.Diagnostics);
        Assert.Equal([1, 2, 3], outcome.Requested.Order());
    }

    // The first file is larger than it declares, so the files read ahead on the strength of that
    // size no longer fit. They are dropped, and the outcome is the sequential one although more
    // requests were sent.
    [Theory]
    [InlineData(1, new[] { 1 })]
    [InlineData(4, new[] { 1, 2, 3 })]
    public async Task FilesReadAheadAreDroppedWhenAnEarlierFileWasLargerThanDeclared(int bound, int[] requested)
    {
        AdoTestAttachment[] files = [Remote(1, "a.txt", 100), Remote(2, "b.txt", 100), Remote(3, "c.txt", 50)];
        AttachmentFixture fixture = new AttachmentFixture { Delay = static id => TimeSpan.FromMilliseconds(id == 1 ? 60 : 0) }
            .Serve(1, Text(250)).Serve(2, Text(100)).Serve(3, Text(50));
        Outcome outcome = await DownloadAsync(fixture, files, Limits(bound, total: 300));
        Assert.Equal(["1:Downloaded:r201-11-a1.txt", "2:BudgetExceeded:", "3:BudgetExceeded:"], outcome.Statuses);
        Assert.Equal([DiagnosticCodes.AttachmentBudgetExceeded + " 300|2"], outcome.Diagnostics);
        Assert.Equal(["r201-11-a1.txt=250"], outcome.Files);
        Assert.Equal(["r201-11-a1.txt"], outcome.Folder);
        Assert.Equal(requested, outcome.Requested.Order());
    }

    // A file without a declared size is read ahead with the per-file cap. The file before it is
    // larger than it declared, so when the unsized body is settled it no longer fits what the total
    // leaves, although it is under the per-file limit. It is dropped as over the total, which is
    // what the smaller cap of one-at-a-time downloading gives.
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task BodyReadAheadThatNoLongerFitsTheTotalIsDroppedAsOverTheTotal(int bound)
    {
        AdoTestAttachment[] files = [Remote(1, "a.txt", 100), Remote(2, "unsized.txt", null), Remote(3, "c.txt", 20)];
        AttachmentFixture fixture = new AttachmentFixture { Delay = static id => TimeSpan.FromMilliseconds(id == 1 ? 60 : 0) }
            .Serve(1, Text(250)).Serve(2, Text(120)).Serve(3, Text(20));
        // 250 for each file and 360 in all. The first file takes 250, which leaves 110 for a body of 120.
        Outcome outcome = await DownloadAsync(fixture, files, Limits(bound, perFile: 250, total: 360));
        Assert.Equal(["1:Downloaded:r201-11-a1.txt", "2:BudgetExceeded:", "3:BudgetExceeded:"], outcome.Statuses);
        Assert.Equal([DiagnosticCodes.AttachmentBudgetExceeded + " 360|2"], outcome.Diagnostics);
        Assert.Equal(["r201-11-a1.txt=250"], outcome.Files);
        Assert.Equal(["r201-11-a1.txt"], outcome.Folder);
        Assert.Equal([1, 2], outcome.Requested.Order());
    }

    // The same file when its body is over the per-file limit: too large, at every bound.
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task UnsizedFileOverThePerFileLimitIsTooLargeAndLaterFilesStillDownload(int bound)
    {
        AdoTestAttachment[] files = [Remote(1, "a.txt", 100), Remote(2, "unsized.txt", null), Remote(3, "c.txt", 20)];
        AttachmentFixture fixture = new AttachmentFixture { Delay = static id => TimeSpan.FromMilliseconds(id == 1 ? 60 : 0) }
            .Serve(1, Text(100)).Serve(2, Text(180)).Serve(3, Text(20));
        Outcome outcome = await DownloadAsync(fixture, files, Limits(bound, perFile: 150, total: 250));
        Assert.Equal(["1:Downloaded:r201-11-a1.txt", "2:TooLarge:", "3:Downloaded:r201-11-a3.txt"], outcome.Statuses);
        Assert.Equal([DiagnosticCodes.AttachmentTooLarge + " 2|201|11|150"], outcome.Diagnostics);
        Assert.Equal(["r201-11-a1.txt", "r201-11-a3.txt"], outcome.Folder);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task TerminatingErrorStopsEveryBodyAndLeavesNoPartialFile(int status)
    {
        using TestDirectory directory = new();
        AdoTestAttachment[] files = [.. Enumerable.Range(1, 8).Select(static id => Remote(id, "file.txt", 40))];
        AttachmentFixture fixture = new() { Delay = static id => TimeSpan.FromMilliseconds(id == 3 ? 5 : 40) };
        foreach (AdoTestAttachment file in files)
        {
            Func<HttpResponseMessage> respond = file.Id == 3 ? () => AttachmentFixture.Status(status) : () => AttachmentFixture.Ok(Text(40));
            fixture.Serve(file.Id, respond);
        }
        using HttpClient client = new(fixture.Handler);
        AdoException error = await Assert.ThrowsAnyAsync<AdoException>(() => fixture.Downloader(client, Limits(4)).DownloadAsync(
            [AttachmentFixture.Failure(files)], AttachmentFixture.Project, directory.Root, AttachmentFixture.FolderName, Culture, TestContext.Current.CancellationToken));
        Assert.IsType(status == 401 ? typeof(AdoAuthenticationException) : typeof(AdoAuthorizationException), error);
        // The files before the failing one were settled first; nothing half-written remains.
        Assert.Equal(["r201-11-a1.txt", "r201-11-a2.txt"], Names(directory.Root));
        int requests = fixture.Handler.Requests.Count;
        await Task.Delay(80, TestContext.Current.CancellationToken);
        Assert.Equal(requests, fixture.Handler.Requests.Count);
        Assert.Equal(["r201-11-a1.txt", "r201-11-a2.txt"], Names(directory.Root));
    }

    [Fact]
    public async Task CancellationWhileSeveralBodiesAreReadLeavesNoPartialFile()
    {
        using TestDirectory directory = new();
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        AdoTestAttachment[] files = [.. Enumerable.Range(1, 6).Select(static id => Remote(id, "file.txt", 84))];
        AttachmentFixture fixture = new() { Delay = static _ => TimeSpan.FromMilliseconds(20) };
        int reading = 0;
        foreach (AdoTestAttachment file in files)
        {
            // Bodies arrive a few bytes at a time; the third body to begin cancels the caller.
            int begun = 0;
            fixture.Serve(file.Id, () => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StreamContent(new ChunkedLogStream(AttachmentFixture.Bytes("other-bytes.txt"), onRead: () =>
                {
                    if (Interlocked.Exchange(ref begun, 1) == 0 && Interlocked.Increment(ref reading) == 3) caller.Cancel();
                })),
            });
        }
        using HttpClient client = new(fixture.Handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Downloader(client, Limits(3)).DownloadAsync(
            [AttachmentFixture.Failure(files)], AttachmentFixture.Project, directory.Root, AttachmentFixture.FolderName, Culture, caller.Token));
        Assert.DoesNotContain(Directory.GetFiles(directory.Root), static path => path.EndsWith(".part", StringComparison.Ordinal));
        Assert.InRange(fixture.Handler.Requests.Count, 3, 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void BoundOutsideOneToSixteenIsRejected(int bound)
    {
        using HttpClient client = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => new AttachmentFixture().Downloader(client, new TestResultOptions { MaximumConcurrentRequests = bound }));
    }

    private sealed record Outcome(IReadOnlyList<string> Statuses, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> Files, IReadOnlyList<string> Folder,
        IReadOnlyList<string> Progress, IReadOnlyList<int> Requested, int Peak);

    private static async Task<Outcome> DownloadAsync(AttachmentFixture fixture, AdoTestAttachment[] attachments, TestResultOptions limits)
    {
        using TestDirectory directory = new();
        string folder = Directory.CreateDirectory(Path.Combine(directory.Root, "work")).FullName;
        CapturingLog log = new();
        using HttpClient client = new(fixture.Handler);
        AttachmentDownloadResult result = await fixture.Downloader(client, limits, log).DownloadAsync([AttachmentFixture.Failure(attachments)],
            AttachmentFixture.Project, folder, AttachmentFixture.FolderName, Culture, TestContext.Current.CancellationToken);
        return new Outcome(
            [.. result.Failures.Single().Attempts.Single().Attachments.Select(static item => item.Id.ToString(CultureInfo.InvariantCulture) + ":" + item.DownloadStatus
                + ":" + (item.LocalRelativePath is { } path ? path[(path.IndexOf('/', StringComparison.Ordinal) + 1)..] : ""))],
            [.. result.Diagnostics.Select(static item => item.Code + " " + string.Join('|', item.Arguments))],
            [.. result.Files.OrderBy(static pair => pair.Key, StringComparer.Ordinal).Select(static pair => pair.Key + "=" + pair.Value.ToString(CultureInfo.InvariantCulture))],
            Names(folder),
            [.. log.ProgressEvents.Select(static item => item.Completed.ToString(CultureInfo.InvariantCulture) + "/" + item.Total!.Value.ToString(CultureInfo.InvariantCulture))],
            [.. fixture.RequestedIds()], fixture.Handler.PeakInFlight);
    }

    private static string[] Names(string folder) => [.. Directory.GetFiles(folder).Select(static path => Path.GetFileName(path)).Order(StringComparer.Ordinal)];

    private static TestResultOptions Limits(int bound, long perFile = 2048, long total = 1_000_000) => new()
    { MaximumAttachmentBytes = perFile, MaximumTotalAttachmentBytes = total, MaximumInlineJsonBytes = 256, MaximumConcurrentRequests = bound };

    private static byte[] Text(int length) => Encoding.ASCII.GetBytes(new string('x', length));

    private static AdoTestAttachment Remote(int id, string name, long? size, int? subResult = null) => new()
    { Id = id, RunId = 201, ResultId = 11, SubResultId = subResult, FileName = name, Size = size, Kind = AttachmentKinds.FromFileName(name) };
}
