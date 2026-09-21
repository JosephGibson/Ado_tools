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
        AttachmentFixture fixture = new AttachmentFixture().Serve(51, [1, 2, 3]).Serve(52, [4, 5, 6]);
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
            Assert.Contains("run-" + run.ToString(Culture) + ".dat <span role=\"img\"", html, StringComparison.Ordinal);
            Assert.Contains("runId=" + run.ToString(Culture) + "&amp;resultId=11", html, StringComparison.Ordinal);
        }
        Assert.Contains("<span>3 bytes</span>", html, StringComparison.Ordinal);
        Assert.Equal(!skip && allRuns, html.Contains("r201-11-a51.bin", StringComparison.Ordinal));
        Assert.Equal(!skip, html.Contains("r202-11-s301-a52.bin", StringComparison.Ordinal));
        // Repeated metadata downloads only once, and the caller's objects remain untouched.
        Assert.All(set.Failures.SelectMany(f => f.Attempts).SelectMany(a => a.Attachments), a =>
        {
            Assert.Equal(AdoTestAttachmentStatus.NotRequested, a.DownloadStatus);
            Assert.Null(a.LocalRelativePath);
        });
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
        Assert.Contains("run-201.dat", html, StringComparison.Ordinal);
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
        AdoTestRun latest = Run(202, precedence == "date" ? Generated.AddDays(1) : Generated,
            stage: precedence == "stage" ? 2 : null, phase: precedence == "phase" ? 2 : null,
            job: precedence == "job" ? 2 : null);
        AdoTestRun earlier = Run(201, precedence == "id" ? Generated : Generated.AddHours(1),
            phase: precedence == "stage" ? 5 : null, job: precedence == "phase" ? 5 : null);
        TestFailureExportPlan plan = new TestFailureExporter(new RecordingLauncher())
            .Prepare(Set([latest, earlier], latestAttachments: true), Options(directory.Root));
        Assert.Equal(202, plan.AttachmentRunId);
        Assert.True(plan.DownloadsAttachments);
    }

    private static TestFailureExportOptions Options(string path, bool allRuns = false, bool skip = false) => new()
    {
        Path = path, SessionCulture = Culture, Culture = Culture.Name, GeneratedAt = Generated, ToolkitVersion = "5.4.0-test",
        AllRunAttachments = allRuns, SkipAttachments = skip,
    };

    private static AdoTestRun Run(int id, DateTimeOffset? started = null, int? stage = null, int? phase = null, int? job = null) => new()
    {
        Id = id, BuildId = 401, Name = "Synthetic run " + id.ToString(Culture), State = "Completed",
        StartedDate = started, StageAttempt = stage, PhaseAttempt = phase, PipelineAttempt = job,
        TeamProject = TestRunFixture.Project, CollectionUri = TestRunFixture.Connection.CollectionUri,
    };

    private static AdoBuildTestFailureSet Set(IReadOnlyList<AdoTestRun> runs, bool latestAttachments)
    {
        AdoTestAttachment old = Attachment(201, 51), latest = Attachment(202, 52, 301);
        AdoTestAttempt Attempt(int number, int run, params AdoTestAttachment[] attachments) => new()
        {
            Number = number, RunId = run, ResultId = 11, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure,
            Attachments = attachments,
        };
        return new AdoBuildTestFailureSet
        {
            Build = TestRunFixture.Build(), Runs = runs, Summary = new AdoBuildTestSummary
            { BuildId = 401, BuildNumber = "20260915.1", WebUrl = TestRunFixture.Build().WebUrl },
            Failures = [new AdoTestFailure
            {
                Ordinal = 1, ShortName = "SubmitOrder", CollectionUri = TestRunFixture.Connection.CollectionUri,
                Attempts = latestAttachments ? [Attempt(1, 201, old), Attempt(2, 202, latest), Attempt(3, 202, latest)] : [Attempt(1, 201, old)],
            }],
            FailedCount = 1, RetrievedAt = Generated, CollectionUri = TestRunFixture.Connection.CollectionUri,
        };
    }

    private static AdoTestAttachment Attachment(int run, int id, int? sub = null) => new()
    {
        Id = id, RunId = run, ResultId = 11, SubResultId = sub, Size = 3, FileName = "run-" + run.ToString(Culture) + ".dat",
        Kind = AdoTestAttachmentKind.Other,
    };

    private sealed class RecordingLauncher : IDocumentLauncher
    {
        public void Open(string path) => Assert.Fail("This export must not launch a browser.");
    }
}
