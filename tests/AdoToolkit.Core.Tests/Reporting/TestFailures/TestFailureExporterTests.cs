using System.Net.Http;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.Tests.TestRuns;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

public sealed class TestFailureExporterTests
{
    private static readonly CultureInfo Session = CultureInfo.GetCultureInfo("en-US");
    private static readonly DateTimeOffset Generated = new(2026, 9, 16, 13, 30, 0, TimeSpan.Zero);

    // S5-3: an unreadable history build (500) is a warning, and the report still commits.
    [Fact]
    [Trait("Acceptance", "S5-3")]
    public async Task HistoryBuildReturning500StillCommitsTheReport()
    {
        using TestDirectory directory = new();
        using FakeHttpMessageHandler handler = RunHistoryTests.History().Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(),
            new TestFailureQuery { HistoryCount = 4 }, Session, TestContext.Current.CancellationToken);
        Assert.Contains(set.Diagnostics, d => d.Code == DiagnosticCodes.HistoryUnavailable);
        TestFailureExporter exporter = new(new RecordingLauncher());
        TestFailureExportPlan plan = exporter.Prepare(set, Options(directory.Root, culture: "fr-CA"));
        Assert.False(plan.DownloadsAttachments);
        TestFailureExportResult result = await exporter.ExportAsync(plan, null, null, TestContext.Current.CancellationToken);
        Assert.Equal(Path.Combine(directory.Root, "Build-401-TestFailures.html"), result.Report.FullName);
        Assert.Null(result.AttachmentDirectory);
        string html = File.ReadAllText(result.Report.FullName);
        Assert.Contains("data-diagnostic=\"HistoryUnavailable\"", html, StringComparison.Ordinal);
        Assert.Contains("data-history-count=\"4\"", html, StringComparison.Ordinal);
        Assert.Contains("<html lang=\"fr-CA\"", html, StringComparison.Ordinal);
        Assert.Equal([result.Report.FullName], Directory.GetFileSystemEntries(directory.Root).Where(p => !p.EndsWith("config.json", StringComparison.Ordinal)));
    }

    // S5-6: -SkipAttachments downloads nothing, creates no folder, and still lists names and sizes.
    [Fact]
    [Trait("Acceptance", "S5-6")]
    public async Task SkipAttachmentsDownloadsNothingAndCreatesNoFolder()
    {
        using TestDirectory directory = new();
        (AdoBuildTestFailureSet set, FakeHttpMessageHandler handler, HttpClient client) = await Retrieve();
        using (handler)
        using (client)
        {
            int requests = handler.Requests.Count;
            TestFailureExporter exporter = new(new RecordingLauncher());
            TestFailureExportPlan plan = exporter.Prepare(set, Options(directory.Root, skip: true));
            Assert.False(plan.DownloadsAttachments);
            Assert.Null(plan.AttachmentDirectory);
            TestFailureExportResult result = await exporter.ExportAsync(plan, null, null, TestContext.Current.CancellationToken);
            Assert.Equal(requests, handler.Requests.Count);
            Assert.Null(result.AttachmentDirectory);
            Assert.Empty(Directory.GetDirectories(directory.Root));
            string html = File.ReadAllText(result.Report.FullName);
            Assert.DoesNotContain("data-local-file", html, StringComparison.Ordinal);
            Assert.DoesNotContain("data-download-status", html, StringComparison.Ordinal);
            foreach (string name in new[] { "screenshot.PNG", "payload.json", "report.htm", "trace.dat", "noextension" })
                Assert.Contains(">" + name + " <span role=\"img\"", html, StringComparison.Ordinal);
            Assert.Contains("<span>2,048 bytes</span>", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Acceptance", "S5-6")]
    public async Task ExportDownloadsIntoTheGenerationFolderAndLinksLocalFiles()
    {
        using TestDirectory directory = new();
        string destination = Directory.CreateDirectory(Path.Combine(directory.Root, "Rapport été #1")).FullName;
        (AdoBuildTestFailureSet set, FakeHttpMessageHandler handler, HttpClient client) = await Retrieve();
        using (handler)
        using (client)
        {
            RecordingLauncher launcher = new();
            TestFailureExporter exporter = new(launcher);
            CapturingLog log = new();
            AttachmentDownloader downloader = new(new AdoHttpPipeline(client, TestRunFixture.Connection.CollectionUri,
                TimeSpan.FromSeconds(100), log, new FakeClock()), new TestResultOptions(), log);
            TestFailureExportPlan plan = exporter.Prepare(set, Options(destination, open: true));
            string folderName = "Build-401-TestFailures.files-20260916T133000000Z";
            Assert.Equal(Path.Combine(destination, folderName), plan.AttachmentDirectory);
            Assert.False(Directory.Exists(plan.AttachmentDirectory));
            TestFailureExportResult result = await exporter.ExportAsync(plan, downloader, log, TestContext.Current.CancellationToken);

            Assert.Equal(Path.Combine(destination, folderName), result.AttachmentDirectory!.FullName);
            Assert.Equal(["r201-1-a5001.png", "r201-1-a5002.json", "r201-1-a5003.html", "r201-1-a5004.bin", "r201-1-a5005.bin"],
                Directory.GetFiles(result.AttachmentDirectory.FullName).Select(p => Path.GetFileName(p)).Order(StringComparer.Ordinal));
            Assert.Equal([result.Report.FullName], launcher.Opened);
            Assert.Empty(result.Diagnostics);
            Assert.Equal(5, handler.Requests.Count(r => r.Uri.AbsolutePath.Contains("/attachments/", StringComparison.Ordinal)));
            string html = File.ReadAllText(result.Report.FullName);
            string prefix = Uri.EscapeDataString(folderName) + "/";
            Assert.Contains("<a class=\"thumbnail\" data-local-file href=\"" + prefix + "r201-1-a5001.png\"><img data-local-file src=\""
                + prefix + "r201-1-a5001.png\" loading=\"lazy\" alt=\"Attachment image: screenshot.PNG\"></a>", html, StringComparison.Ordinal);
            Assert.Contains("<code class=\"lang-json\">", html, StringComparison.Ordinal);
            Assert.Contains("href=\"" + prefix + "r201-1-a5003.html\">Test output HTML (local file)</a>", html, StringComparison.Ordinal);
            Assert.Contains("href=\"" + prefix + "r201-1-a5004.bin\">Local copy</a> <code>r201-1-a5004.bin</code>", html, StringComparison.Ordinal);
            Assert.Equal(5, html.Split("data-download-status=\"downloaded\"").Length - 1);
            // The committed report validates against the renamed folder too.
            TestFailureReportModel model = TestFailureReportModelBuilder.WithAttachments(Model(set),
                [.. set.Failures.OrderBy(f => f.ShortName == "Totals" ? 0 : 1).Select((f, i) => Renumber(f, i + 1, folderName))], [],
                new TestFailureLocalAttachments
                {
                    FolderName = folderName, SourceFolder = result.AttachmentDirectory.FullName, MaximumInlineJsonBytes = 262144,
                    Files = Directory.GetFiles(result.AttachmentDirectory.FullName).ToDictionary(p => Path.GetFileName(p), p => new FileInfo(p).Length),
                });
            TestFailureReportValidator.Validate(result.Report.FullName, model, result.AttachmentDirectory.FullName);

            // Re-export later: a new generation replaces the report and removes the previous folder.
            TestFailureExportPlan again = exporter.Prepare(set, Options(destination, generated: Generated.AddMinutes(5)));
            TestFailureExportResult second = await exporter.ExportAsync(again, downloader, log, TestContext.Current.CancellationToken);
            Assert.False(Directory.Exists(result.AttachmentDirectory.FullName));
            Assert.Equal([second.AttachmentDirectory!.FullName, second.Report.FullName],
                Directory.GetFileSystemEntries(destination).Order(StringComparer.Ordinal));
            Assert.Equal(AdoProgressPhase.AttachmentDownload, log.ProgressEvents[^1].Phase);
        }
    }

    [Fact]
    [Trait("Acceptance", "S5-7")]
    public async Task NoClobberRefusesBeforeAnyDownloadAndPlansNameBothTargets()
    {
        using TestDirectory directory = new();
        (AdoBuildTestFailureSet set, FakeHttpMessageHandler handler, HttpClient client) = await Retrieve();
        using (handler)
        using (client)
        {
            int requests = handler.Requests.Count;
            TestFailureExporter exporter = new(new RecordingLauncher(), () => directory.Root);
            TestFailureExportPlan plan = exporter.Prepare(set, Options(null));
            Assert.Equal(Path.Combine(directory.Root, "Build-401-TestFailures.html"), plan.ReportPath);
            Assert.Equal(Path.Combine(directory.Root, "Build-401-TestFailures.files-20260916T133000000Z"), plan.AttachmentDirectory);
            Assert.Empty(Directory.GetDirectories(directory.Root));
            File.WriteAllText(plan.ReportPath, "existing");
            Assert.Throws<AdoFileOutputException>(() => exporter.Prepare(set, Options(null, noClobber: true)));
            Assert.Throws<AdoFileOutputException>(() => exporter.Prepare(set, Options(Path.Combine(directory.Root, "report.txt"))));
            Assert.Equal("existing", File.ReadAllText(plan.ReportPath));
            Assert.Equal(requests, handler.Requests.Count);
            Assert.Equal("custom.html", Path.GetFileName(exporter.Prepare(set, Options(Path.Combine(directory.Root, "custom.html"))).ReportPath));
            Assert.Equal("Build-401-TestFailures.html", Path.GetFileName(exporter.Prepare(set, Options(directory.Root)).ReportPath));
            await Assert.ThrowsAsync<ArgumentNullException>(() => exporter.ExportAsync(plan, null, null, TestContext.Current.CancellationToken));
        }
    }

    private static TestFailureExportOptions Options(string? path, bool skip = false, bool noClobber = false, bool open = false,
        string culture = "en-US", DateTimeOffset? generated = null) => new()
        {
            Culture = culture, SessionCulture = Session, Path = path, SkipAttachments = skip, NoClobber = noClobber, Open = open,
            GeneratedAt = generated ?? Generated, ToolkitVersion = "5.4.0-test",
        };

    private static TestFailureReportModel Model(AdoBuildTestFailureSet set) => TestFailureReportModelBuilder.Build(set,
        new TestFailureReportOptions { Culture = "en-US", SessionCulture = Session, GeneratedAt = Generated, ToolkitVersion = "5.4.0-test" });

    // Mirrors the export's own copies so the committed file can be validated independently.
    private static AdoTestFailure Renumber(AdoTestFailure failure, int ordinal, string folderName) => new()
    {
        Ordinal = ordinal, Classification = failure.Classification, TestName = failure.TestName, ShortName = failure.ShortName,
        Storage = failure.Storage, Title = failure.Title, TestCase = failure.TestCase, History = failure.History, Owner = failure.Owner,
        Priority = failure.Priority, CollectionUri = failure.CollectionUri,
        Attempts = [.. failure.Attempts.Select(a => a.WithAttachments([.. a.Attachments.Select(x => x.WithDownload(AdoTestAttachmentStatus.Downloaded,
            folderName + "/r201-1-a" + x.Id.ToString(CultureInfo.InvariantCulture) + (x.Kind switch
            {
                AdoTestAttachmentKind.Png => ".png", AdoTestAttachmentKind.Json => ".json", AdoTestAttachmentKind.Html => ".html", _ => ".bin",
            })))]))],
    };

    // Test results 2 with attachment content for fixture 201-1 (served before the list route matches).
    private static async Task<(AdoBuildTestFailureSet, FakeHttpMessageHandler, HttpClient)> Retrieve()
    {
        TestRunFixture fixture = new TestRunFixture()
            .RouteBytes(AttachmentFixture.Bytes("pattern.png"), "/Runs/201/Results/1/attachments/5001")
            .RouteBytes(AttachmentFixture.Bytes("valid.json"), "/Runs/201/Results/1/attachments/5002")
            .RouteBytes(AttachmentFixture.Bytes("test-output.html"), "/Runs/201/Results/1/attachments/5003")
            .RouteBytes(AttachmentFixture.Bytes("other-bytes.txt"), "/Runs/201/Results/1/attachments/5004")
            .RouteBytes(AttachmentFixture.Bytes("other-bytes.txt"), "/Runs/201/Results/1/attachments/5005")
            .Route("runs-two.json", "/test/runs", "%24skip=0&")
            .Route("results-run-201.json", "/Runs/201/results", "%24skip=0&")
            .Route("results-run-202.json", "/Runs/202/results", "%24skip=0&")
            .Route("result-detail-201-1.json", "/Runs/201/results/1?")
            .Route("result-detail-202-11.json", "/Runs/202/results/11?")
            .Route("attachments-result.json", "/Runs/201/Results/1/attachments")
            .Route("attachments-empty.json", "/Runs/202/Results/11/attachments")
            .Route("workitems-testcases.json", "workitemsbatch");
        FakeHttpMessageHandler handler = fixture.Handler();
        HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), new TestFailureQuery { HistoryCount = 1 },
            Session, TestContext.Current.CancellationToken);
        Assert.Equal(5, set.Failures.Sum(f => f.Attempts.Sum(a => a.Attachments.Count)));
        return (set, handler, client);
    }

    private sealed class RecordingLauncher : IDocumentLauncher
    {
        internal List<string> Opened { get; } = [];
        public void Open(string path) => Opened.Add(path);
    }
}
