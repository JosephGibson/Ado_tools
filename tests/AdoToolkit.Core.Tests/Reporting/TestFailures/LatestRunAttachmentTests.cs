using System.Net.Http;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.Tests.TestRuns;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

public sealed class LatestRunAttachmentTests
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");
    private static readonly DateTimeOffset Generated = TestFailureReportFixture.Clock;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DownloadScopePreservesEveryAttemptAndRemoteAttachment(bool allRuns, bool skip)
    {
        using TestDirectory directory = new();
        AttachmentFixture fixture = new AttachmentFixture().Serve(51, [65, 66, 67]).Serve(52, [68, 69, 70]);
        using var handler = fixture.Handler;
        using HttpClient client = new(handler);
        CapturingLog log = new();
        AdoBuildTestFailureSet set = Set([Run(201), Run(202)], latestAttachments: true);
        TestFailureExporter exporter = new(new RecordingLauncher());
        TestFailureExportPlan plan = exporter.Prepare(set, Options(directory.Root, allRuns, skip));
        Assert.Equal(!skip, plan.DownloadsAttachments);
        Assert.Empty(handler.Requests);
        Assert.Empty(Directory.GetDirectories(directory.Root));
        TestFailureExportResult result = await exporter.ExportAsync(plan, fixture.Downloader(client, new TestResultOptions
        {
            // Exactly enough for the selected files; older files must not consume the default budget.
            MaximumAttachmentBytes = 3, MaximumTotalAttachmentBytes = allRuns ? 6 : 3,
        }, log), log, TestContext.Current.CancellationToken);

        int[] expected = skip ? [] : allRuns ? [51, 52] : [52];
        Assert.Equal(expected, fixture.RequestedIds());
        Assert.Empty(result.Diagnostics);
        Assert.Equal(expected.Length, log.ProgressEvents.Count);
        if (!skip)
        {
            Assert.Equal(expected.Length, log.ProgressEvents[^1].Completed);
            Assert.All(log.ProgressEvents, p => Assert.Equal(expected.Length, p.Total));
            Assert.Equal(expected.Length, Directory.GetFiles(result.AttachmentDirectory!.FullName).Length);
        }
        else Assert.Null(result.AttachmentDirectory);

        string html = File.ReadAllText(result.Report.FullName);
        Assert.Contains("data-attempt-count=\"3\"", html, StringComparison.Ordinal);
        foreach (int run in new[] { 201, 202 })
        {
            Assert.Contains("run-" + run.ToString(Culture) + ".log <span role=\"img\"", html, StringComparison.Ordinal);
            Assert.Contains("runId=" + run.ToString(Culture) + "&amp;resultId=11", html, StringComparison.Ordinal);
        }
        Assert.Contains("<span class=\"attachment-size\">3 bytes</span>", html, StringComparison.Ordinal);
        Assert.Equal(!skip && allRuns, html.Contains("r201-11-a51.txt", StringComparison.Ordinal));
        Assert.Equal(!skip, html.Contains("r202-11-s301-a52.txt", StringComparison.Ordinal));
        // Repeated metadata downloads only once, and the caller's objects remain untouched.
        Assert.All(set.Failures.SelectMany(f => f.Attempts).SelectMany(a => a.Attachments), a =>
        {
            Assert.Equal(AdoTestAttachmentStatus.NotRequested, a.DownloadStatus);
            Assert.Null(a.LocalRelativePath);
        });
    }

    // By default only JSON and text from the latest run are downloaded; -AllRunAttachments adds
    // earlier runs. PNG, HTML and other kinds are never requested, whatever the switches.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyJsonAndTextAreDownloadedAndHtmlAndPngNeverAre(bool allRuns)
    {
        using TestDirectory directory = new();
        AttachmentFixture fixture = new AttachmentFixture().Serve(61, "{}"u8.ToArray()).Serve(73, "{}"u8.ToArray())
            .Serve(74, "text"u8.ToArray()).Serve(75, "log"u8.ToArray());
        using HttpClient client = new(fixture.Handler);
        AdoTestAttachment Remote(int run, int id, string name) => new()
        { Id = id, RunId = run, ResultId = 11, Size = 4, FileName = name, Kind = AttachmentKinds.FromFileName(name) };
        AdoBuildTestFailureSet set = Set([Run(201), Run(202)], [
            [Remote(201, 60, "early.png"), Remote(201, 61, "early.json")],
            [Remote(202, 71, "screen.PNG"), Remote(202, 72, "page.html"), Remote(202, 73, "context.json"), Remote(202, 74, "console.txt"),
                Remote(202, 75, "agent.LOG"), Remote(202, 76, "trace.dat"), Remote(202, 77, "report.htm")]]);
        TestFailureExporter exporter = new(new RecordingLauncher());
        TestFailureExportPlan plan = exporter.Prepare(set, Options(directory.Root, allRuns));
        TestFailureExportResult result = await exporter.ExportAsync(plan, fixture.Downloader(client, new TestResultOptions()), null,
            TestContext.Current.CancellationToken);

        Assert.Equal(allRuns ? [61, 73, 74, 75] : [73, 74, 75], fixture.RequestedIds());
        string[] expected = allRuns ? ["r201-11-a61.json", "r202-11-a73.json", "r202-11-a74.txt", "r202-11-a75.txt"]
            : ["r202-11-a73.json", "r202-11-a74.txt", "r202-11-a75.txt"];
        Assert.Equal(expected, Directory.GetFiles(result.AttachmentDirectory!.FullName).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        string html = File.ReadAllText(result.Report.FullName);
        // Every attachment stays listed as a link to its result.
        foreach (string name in new[] { "early.png", "screen.PNG", "page.html", "trace.dat", "report.htm" })
            Assert.Contains(name + " <span role=\"img\"", html, StringComparison.Ordinal);
    }

    // A run is inside the window when it started at or after GeneratedAt minus the window. Outside
    // it, attachments are left out of the report and never downloaded, but every attempt stays.
    [Theory]
    [InlineData(7, 7 * 24 * 60, true)]
    [InlineData(7, 7 * 24 * 60 + 1, false)]
    [InlineData(3, 4 * 24 * 60, false)]
    [InlineData(30, 20 * 24 * 60, true)]
    public async Task AttachmentsOfRunsOutsideTheWindowAreLeftOut(int windowDays, int ageMinutes, bool inside)
    {
        using TestDirectory directory = new();
        AttachmentFixture fixture = new AttachmentFixture().Serve(51, [65]).Serve(52, [66]);
        using HttpClient client = new(fixture.Handler);
        AdoTestRun old = Run(201, Generated.AddMinutes(-ageMinutes));
        AdoBuildTestFailureSet set = Set([old], latestAttachments: false);
        TestFailureExporter exporter = new(new RecordingLauncher());
        TestFailureExportPlan plan = exporter.Prepare(set, Options(directory.Root, allRuns: true, windowDays: windowDays));
        Assert.Equal(inside, plan.DownloadsAttachments);
        TestFailureExportResult result = await exporter.ExportAsync(plan, inside ? fixture.Downloader(client, new TestResultOptions()) : null, null,
            TestContext.Current.CancellationToken);

        Assert.Equal(inside ? [51] : [], fixture.RequestedIds());
        string html = File.ReadAllText(result.Report.FullName);
        Assert.Contains("data-attempt-count=\"1\"", html, StringComparison.Ordinal);
        Assert.Equal(inside, html.Contains("run-201.log", StringComparison.Ordinal));
        Assert.Contains(inside ? "are left out: 0." : "are left out: 1.", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoEligibleRunDoesNotFallBackToOlderAttachments(bool noRuns)
    {
        using TestDirectory directory = new();
        TestFailureExporter exporter = new(new RecordingLauncher());
        AdoBuildTestFailureSet set = Set(noRuns ? [] : [Run(201), Run(202)], latestAttachments: false);
        TestFailureExportPlan plan = exporter.Prepare(set, Options(directory.Root));
        Assert.False(plan.DownloadsAttachments);
        Assert.Null(plan.AttachmentDirectory);
        TestFailureExportResult result = await exporter.ExportAsync(plan, null, null, TestContext.Current.CancellationToken);
        Assert.Null(result.AttachmentDirectory);
        Assert.Empty(Directory.GetDirectories(directory.Root));
        string html = File.ReadAllText(result.Report.FullName);
        // Without runs no run is inside the window, so the attachment is left out entirely.
        Assert.Equal(!noRuns, html.Contains("run-201.log", StringComparison.Ordinal));
        Assert.DoesNotContain("data-local-file", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("stage")]
    [InlineData("phase")]
    [InlineData("job")]
    [InlineData("date")]
    [InlineData("id")]
    public void LatestRunUsesAttemptOrderThenDateAndIdRegardlessOfInputOrder(string precedence)
    {
        using TestDirectory directory = new();
        // Date and ID favor 201 except when they are the tie-breaker being tested.
        AdoTestRun latest = Run(202, precedence == "date" ? Generated.AddHours(-1) : Generated.AddHours(-2),
            stage: precedence == "stage" ? 2 : null, phase: precedence == "phase" ? 2 : null,
            job: precedence == "job" ? 2 : null);
        AdoTestRun earlier = Run(201, precedence == "id" ? Generated.AddHours(-2) : Generated.AddHours(-1.5),
            phase: precedence == "stage" ? 5 : null, job: precedence == "phase" ? 5 : null);
        TestFailureExportPlan plan = new TestFailureExporter(new RecordingLauncher())
            .Prepare(Set([latest, earlier], latestAttachments: true), Options(directory.Root));
        Assert.Equal([202], plan.AttachmentRunIds);
        Assert.True(plan.DownloadsAttachments);
    }

    private static TestFailureExportOptions Options(string path, bool allRuns = false, bool skip = false, int windowDays = 7) => new()
    {
        Path = path, SessionCulture = Culture, Culture = Culture.Name, GeneratedAt = Generated, ToolkitVersion = "5.4.0-test",
        AllRunAttachments = allRuns, SkipAttachments = skip, AttachmentWindowDays = windowDays,
    };

    private static AdoTestRun Run(int id, DateTimeOffset? started = null, int? stage = null, int? phase = null, int? job = null) => new()
    {
        Id = id, BuildId = 401, Name = "Synthetic run " + id.ToString(Culture), State = "Completed",
        StartedDate = started ?? Generated.AddMinutes(id - 300), StageAttempt = stage, PhaseAttempt = phase, PipelineAttempt = job,
        TeamProject = TestRunFixture.Project, CollectionUri = TestRunFixture.Connection.CollectionUri,
    };

    private static AdoBuildTestFailureSet Set(IReadOnlyList<AdoTestRun> runs, bool latestAttachments)
    {
        AdoTestAttachment old = Attachment(201, 51), latest = Attachment(202, 52, 301);
        return Set(runs, latestAttachments ? [[old], [latest], [latest]] : [[old]]);
    }

    // One failure with one attempt per attachment list; each attempt's run is its first attachment's run.
    private static AdoBuildTestFailureSet Set(IReadOnlyList<AdoTestRun> runs, IReadOnlyList<AdoTestAttachment>[] attempts) => new()
    {
        Build = TestRunFixture.Build(), Runs = runs, Summary = new AdoBuildTestSummary
        { BuildId = 401, BuildNumber = "20260915.1", WebUrl = TestRunFixture.Build().WebUrl },
        Failures = [new AdoTestFailure
        {
            Ordinal = 1, ShortName = "SubmitOrder", CollectionUri = TestRunFixture.Connection.CollectionUri,
            Attempts = [.. attempts.Select((attachments, index) => new AdoTestAttempt
            {
                Number = index + 1, RunId = attachments[0].RunId, ResultId = 11, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure,
                Attachments = attachments,
            })],
        }],
        FailedCount = 1, RetrievedAt = Generated, CollectionUri = TestRunFixture.Connection.CollectionUri,
    };

    private static AdoTestAttachment Attachment(int run, int id, int? sub = null) => new()
    {
        Id = id, RunId = run, ResultId = 11, SubResultId = sub, Size = 3, FileName = "run-" + run.ToString(Culture) + ".log",
        Kind = AdoTestAttachmentKind.Text,
    };

    private sealed class RecordingLauncher : IDocumentLauncher
    {
        public void Open(string path) => Assert.Fail("This export must not launch a browser.");
    }
}
