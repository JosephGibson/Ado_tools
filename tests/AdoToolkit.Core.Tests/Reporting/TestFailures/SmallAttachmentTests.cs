using System.Net.Http;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.Tests.TestRuns;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// By default the export downloads JSON and text of at most MaximumInlineJsonBytes from every run
// inside the attachment window. Larger files keep the rule of LatestRunAttachmentTests. The
// latest run comes first wherever a total can run out. The small-file limit is 100 bytes here.
public sealed class SmallAttachmentTests
{
    private const int SmallLimit = 100;
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");
    private static readonly DateTimeOffset Generated = TestFailureReportFixture.Clock;

    // Attempt 1 is in the older run 201 and attempt 2 in the latest run 202.
    [Theory]
    [InlineData(false, new[] { 52, 54, 51 })]
    [InlineData(true, new[] { 52, 54, 51, 53 })]
    public async Task SmallFilesOfOlderRunsAreDownloadedAndLargeOnesFollowTheLatestRunRule(bool allRuns, int[] requested)
    {
        using TestDirectory directory = new();
        AttachmentFixture fixture = new AttachmentFixture().Serve(51, Text(10)).Serve(52, Text(400)).Serve(53, Text(400)).Serve(54, "{\"a\":1}"u8.ToArray());
        using HttpClient client = new(fixture.Handler);
        AdoBuildTestFailureSet set = Set([Run(201), Run(202)],
            [File(201, 51, "small.txt", 10), File(201, 53, "large.log", 400)],
            [File(202, 52, "latest.log", 400), File(202, 54, "context.json", 7)]);
        TestFailureExporter exporter = new(new SilentLauncher());
        TestFailureExportPlan plan = exporter.Prepare(set, Options(directory.Root, allRuns));
        Assert.True(plan.DownloadsAttachments);
        TestFailureExportResult result = await exporter.ExportAsync(plan, fixture.Downloader(client, Limits()), null, TestContext.Current.CancellationToken);

        // The latest run first, then the older one, each in report order.
        Assert.Equal(requested, fixture.RequestedIds());
        Assert.Empty(result.Diagnostics);
        string html = System.IO.File.ReadAllText(result.Report.FullName);
        // The small file of the older run has a local copy and a preview.
        string small = Item(html, 1, 51);
        Assert.Contains("data-local-file", small, StringComparison.Ordinal);
        Assert.Contains("<details class=\"attachment-preview\">", small, StringComparison.Ordinal);
        // The large file of the older run stays a link, with no note, unless every run is downloaded.
        string large = Item(html, 1, 53);
        Assert.Equal(allRuns, large.Contains("data-local-file", StringComparison.Ordinal));
        if (!allRuns)
        {
            Assert.DoesNotContain("attachment-status", large, StringComparison.Ordinal);
            Assert.DoesNotContain("data-download-status", large, StringComparison.Ordinal);
        }
        // The large file of the latest run is downloaded, and too large to preview.
        string latest = Item(html, 2, 52);
        Assert.Contains("data-local-file", latest, StringComparison.Ordinal);
        Assert.Contains("Too large to show here", latest, StringComparison.Ordinal);
        Assert.Contains("<details class=\"attachment-preview\">", Item(html, 2, 54), StringComparison.Ordinal);
        Assert.Equal(requested.Length, Directory.GetFiles(result.AttachmentDirectory!.FullName).Length);
    }

    [Fact]
    public async Task RunOutsideTheWindowGivesNothingHoweverSmallItsFiles()
    {
        using TestDirectory directory = new();
        AttachmentFixture fixture = new AttachmentFixture().Serve(54, Text(7));
        using HttpClient client = new(fixture.Handler);
        AdoBuildTestFailureSet set = Set([Run(201, Generated.AddDays(-8)), Run(202)],
            [File(201, 51, "small.txt", 10)], [File(202, 54, "latest.txt", 7)]);
        TestFailureExporter exporter = new(new SilentLauncher());
        TestFailureExportPlan plan = exporter.Prepare(set, Options(directory.Root));
        Assert.Equal([202], plan.Attachments.SmallRunIds);
        await exporter.ExportAsync(plan, fixture.Downloader(client, Limits()), null, TestContext.Current.CancellationToken);
        Assert.Equal([54], fixture.RequestedIds());

        // With only the old run holding a file, nothing is downloaded and no downloader is needed.
        TestFailureExportPlan none = exporter.Prepare(Set([Run(201, Generated.AddDays(-8)), Run(202)], [File(201, 51, "small.txt", 10)]), Options(directory.Root));
        Assert.False(none.DownloadsAttachments);
        Assert.Null(none.AttachmentDirectory);
    }

    [Fact]
    public async Task SkipAttachmentsDownloadsNothingAndNeedsNoDownloader()
    {
        using TestDirectory directory = new();
        AdoBuildTestFailureSet set = Set([Run(201), Run(202)], [File(201, 51, "small.txt", 10)], [File(202, 54, "latest.txt", 7)]);
        TestFailureExporter exporter = new(new SilentLauncher());
        foreach (bool allRuns in new[] { false, true })
        {
            TestFailureExportPlan plan = exporter.Prepare(set, Options(directory.Root, allRuns, skip: true));
            Assert.False(plan.DownloadsAttachments);
            Assert.Empty(plan.Attachments.SmallRunIds);
            Assert.Empty(plan.Attachments.FullRunIds);
            TestFailureExportResult result = await exporter.ExportAsync(plan, null, null, TestContext.Current.CancellationToken);
            Assert.Null(result.AttachmentDirectory);
            string html = System.IO.File.ReadAllText(result.Report.FullName);
            Assert.Contains("small.txt", html, StringComparison.Ordinal);
            Assert.DoesNotContain("data-local-file", html, StringComparison.Ordinal);
        }
        Assert.Empty(Directory.GetDirectories(directory.Root));
    }

    // A file of an older run is selected because it declares a small size, or none. When its body
    // turns out larger than the small-file limit it is dropped quietly and stays a link, like a
    // large file that was never selected. A body that grew but still fits is kept.
    [Theory]
    // Declared size, served size, whether the response announces its length, kept.
    [InlineData(10, 300, true, false)]
    [InlineData(10, 300, false, false)]
    [InlineData(null, 300, true, false)]
    [InlineData(null, 300, false, false)]
    [InlineData(10, 60, true, true)]
    [InlineData(null, 60, false, true)]
    [InlineData(null, 100, true, true)]
    public async Task OlderRunFileLargerThanTheSmallLimitIsDroppedWithoutADiagnostic(int? declared, int served, bool announced, bool kept)
    {
        using TestDirectory directory = new();
        byte[] body = Text(served);
        AttachmentFixture fixture = new AttachmentFixture().Serve(54, Text(7)).Serve(51, () => announced ? AttachmentFixture.Ok(body)
            : new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new UnannouncedContent(body) });
        using HttpClient client = new(fixture.Handler);
        List<string> warnings = [];
        AdoBuildTestFailureSet set = Set([Run(201), Run(202)], [File(201, 51, "older.txt", declared)], [File(202, 54, "latest.txt", 7)]);
        TestFailureExporter exporter = new(new SilentLauncher());
        TestFailureExportPlan plan = exporter.Prepare(set, Options(directory.Root));
        TestFailureExportResult result = await exporter.ExportAsync(plan, fixture.Downloader(client, Limits()), new WarningLog(warnings), TestContext.Current.CancellationToken);

        Assert.Equal([54, 51], fixture.RequestedIds());
        Assert.Empty(result.Diagnostics);
        Assert.Empty(warnings);
        string[] expected = kept ? ["r201-11-a51.txt", "r202-12-a54.txt"] : ["r202-12-a54.txt"];
        Assert.Equal(expected, Directory.GetFiles(result.AttachmentDirectory!.FullName).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        string older = Item(System.IO.File.ReadAllText(result.Report.FullName), 1, 51);
        Assert.Equal(kept, older.Contains("data-local-file", StringComparison.Ordinal));
        Assert.Contains("older.txt", older, StringComparison.Ordinal);
        Assert.DoesNotContain("attachment-status", older, StringComparison.Ordinal);
        // Dropped: the remote link only, with no download status at all.
        Assert.Equal(kept, older.Contains("data-download-status=\"downloaded\"", StringComparison.Ordinal));
        Assert.Equal(kept, older.Contains("data-download-status", StringComparison.Ordinal));
        Assert.Empty(Directory.GetFiles(result.AttachmentDirectory.FullName, "*.part"));
    }

    // The same oversized body in the latest run is not a small-only file: it keeps its warning.
    [Fact]
    public async Task LatestRunFileOverThePerFileLimitStillWarns()
    {
        using TestDirectory directory = new();
        AttachmentFixture fixture = new AttachmentFixture().Serve(54, Text(300));
        using HttpClient client = new(fixture.Handler);
        AdoBuildTestFailureSet set = Set([Run(201), Run(202)], [File(201, 51, "older.png", 10)], [File(202, 54, "latest.txt", 10)]);
        TestFailureExporter exporter = new(new SilentLauncher());
        TestFailureExportResult result = await exporter.ExportAsync(exporter.Prepare(set, Options(directory.Root)),
            fixture.Downloader(client, Limits(perFile: 200)), null, TestContext.Current.CancellationToken);
        Assert.Equal([DiagnosticCodes.AttachmentTooLarge], result.Diagnostics.Select(static diagnostic => diagnostic.Code));
        Assert.Null(result.AttachmentDirectory);
        Assert.Contains("data-download-status=\"toolarge\"", Item(System.IO.File.ReadAllText(result.Report.FullName), 2, 54), StringComparison.Ordinal);
    }

    // Three small files of 50 bytes and an inline total of 110: two previews fit. The latest run's
    // file is chosen first although the older run's files come first in the report. The first older
    // file takes what is left, the second does not fit, and the 5-byte file after it still does.
    [Fact]
    public async Task InlineTotalGoesToTheLatestRunFirstAndASmallerLaterFileCanStillFit()
    {
        using TestDirectory directory = new();
        AttachmentFixture fixture = new AttachmentFixture().Serve(51, Text(50)).Serve(53, Text(50)).Serve(55, Text(5)).Serve(54, Text(50));
        using HttpClient client = new(fixture.Handler);
        AdoBuildTestFailureSet set = Set([Run(201), Run(202)],
            [File(201, 51, "older-a.txt", 50), File(201, 53, "older-b.txt", 50), File(201, 55, "older-c.txt", 5)],
            [File(202, 54, "latest.txt", 50)]);
        TestFailureExporter exporter = new(new SilentLauncher());
        TestFailureExportResult result = await exporter.ExportAsync(exporter.Prepare(set, Options(directory.Root)),
            fixture.Downloader(client, Limits(inlineTotal: 110)), null, TestContext.Current.CancellationToken);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(4, Directory.GetFiles(result.AttachmentDirectory!.FullName).Length);
        string html = System.IO.File.ReadAllText(result.Report.FullName);
        Assert.Equal([true, false, true, true], new[] { Item(html, 1, 51), Item(html, 1, 53), Item(html, 1, 55), Item(html, 2, 54) }
            .Select(static item => item.Contains("<details class=\"attachment-preview\">", StringComparison.Ordinal)));
        // The file left out keeps its local copy and says why it is not shown.
        Assert.Contains("data-local-file", Item(html, 1, 53), StringComparison.Ordinal);
        Assert.Contains("reached its limit for inline attachments", Item(html, 1, 53), StringComparison.Ordinal);
    }

    // The download total is spent in the same order: the latest run first. The older run's file no
    // longer fits, which is reported once, as before; a file over the per-file limit is never requested.
    [Fact]
    public async Task DownloadTotalAndPerFileLimitsApplyAsBeforeWithTheLatestRunFirst()
    {
        using TestDirectory directory = new();
        AttachmentFixture fixture = new AttachmentFixture().Serve(54, Text(60)).Serve(51, Text(60)).Serve(53, Text(20));
        using HttpClient client = new(fixture.Handler);
        AdoBuildTestFailureSet set = Set([Run(201), Run(202)],
            [File(201, 51, "older-a.txt", 60), File(201, 53, "older-b.txt", 20)],
            [File(202, 54, "latest.txt", 60), File(202, 56, "huge.log", 5000)]);
        TestFailureExporter exporter = new(new SilentLauncher());
        TestFailureExportResult result = await exporter.ExportAsync(exporter.Prepare(set, Options(directory.Root)),
            fixture.Downloader(client, Limits(perFile: 1000, total: 100)), null, TestContext.Current.CancellationToken);
        Assert.Equal([54], fixture.RequestedIds());
        Assert.Equal(new (string, string)[] { (DiagnosticCodes.AttachmentTooLarge, "56|202|12|1000"), (DiagnosticCodes.AttachmentBudgetExceeded, "100|51") },
            result.Diagnostics.Select(static diagnostic => (diagnostic.Code, string.Join('|', diagnostic.Arguments))));
        string html = System.IO.File.ReadAllText(result.Report.FullName);
        Assert.Contains("data-local-file", Item(html, 2, 54), StringComparison.Ordinal);
        Assert.Contains("data-download-status=\"toolarge\"", Item(html, 2, 56), StringComparison.Ordinal);
        // Once the total is reached every later file is marked, without a second warning.
        Assert.Contains("data-download-status=\"budgetexceeded\"", Item(html, 1, 51), StringComparison.Ordinal);
        Assert.Contains("data-download-status=\"budgetexceeded\"", Item(html, 1, 53), StringComparison.Ordinal);
    }

    private static TestFailureExportOptions Options(string path, bool allRuns = false, bool skip = false) => new()
    {
        Path = path, SessionCulture = Culture, Culture = Culture.Name, GeneratedAt = Generated, ToolkitVersion = "5.4.0-test",
        AllRunAttachments = allRuns, SkipAttachments = skip, MaximumInlineJsonBytes = SmallLimit,
    };

    // The downloader and the selection read the same small-file limit, as the cmdlet sets them.
    private static TestResultOptions Limits(long perFile = 52428800, long total = 524288000, long inlineTotal = 8388608) => new()
    { MaximumInlineJsonBytes = SmallLimit, MaximumAttachmentBytes = perFile, MaximumTotalAttachmentBytes = total, MaximumInlineTotalBytes = inlineTotal };

    private static byte[] Text(int length) => Encoding.ASCII.GetBytes(new string('x', length));

    private static string Item(string html, int attempt, int attachment)
    {
        string opening = "<li id=\"f-1-a" + attempt.ToString(CultureInfo.InvariantCulture) + "-att" + attachment.ToString(CultureInfo.InvariantCulture) + "\"";
        int start = html.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, opening);
        return html[start..html.IndexOf("</li>", start, StringComparison.Ordinal)];
    }

    private static AdoTestRun Run(int id, DateTimeOffset? started = null) => new()
    {
        Id = id, BuildId = 401, Name = "Synthetic run " + id.ToString(Culture), State = "Completed",
        StartedDate = started ?? Generated.AddMinutes(id - 300), TeamProject = TestRunFixture.Project, CollectionUri = TestRunFixture.Connection.CollectionUri,
    };

    private static AdoTestAttachment File(int run, int id, string name, int? size) => new()
    { Id = id, RunId = run, ResultId = run - 190, Size = size, FileName = name, Kind = AttachmentKinds.FromFileName(name) };

    // One failure with one attempt per attachment list; each attempt's run is its first attachment's run.
    private static AdoBuildTestFailureSet Set(IReadOnlyList<AdoTestRun> runs, params IReadOnlyList<AdoTestAttachment>[] attempts) => new()
    {
        Build = TestRunFixture.Build(), Runs = runs,
        Summary = new AdoBuildTestSummary { BuildId = 401, BuildNumber = "20260915.1", WebUrl = TestRunFixture.Build().WebUrl },
        Failures = [new AdoTestFailure
        {
            Ordinal = 1, ShortName = "SubmitOrder", CollectionUri = TestRunFixture.Connection.CollectionUri,
            Attempts = [.. attempts.Select((attachments, index) => new AdoTestAttempt
            {
                Number = index + 1, RunId = attachments[0].RunId, ResultId = attachments[0].ResultId, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure,
                Attachments = attachments,
            })],
        }],
        FailedCount = 1, RetrievedAt = Generated, CollectionUri = TestRunFixture.Connection.CollectionUri,
    };

    // A body whose length the response does not announce, so only reading it shows its size.
    private sealed class UnannouncedContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    // Only what the export would show the user as a warning.
    private sealed class WarningLog(List<string> warnings) : IAdoLog
    {
        public void Verbose(string message) { }
        public void Debug(string message) { }
        public void Warning(string message) => warnings.Add(message);
        public void Progress(AdoProgress progress) { }
    }

    private sealed class SilentLauncher : IDocumentLauncher
    {
        public void Open(string path) => Assert.Fail("This export must not launch a browser.");
    }
}
