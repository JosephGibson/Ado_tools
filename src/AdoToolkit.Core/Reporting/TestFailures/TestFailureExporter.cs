using System.Diagnostics;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

// One report per set (§15.8). Prepare covers §13.4 steps 1–2 on the caller's thread so the shell
// can ask ShouldProcess before anything is downloaded or written; ExportAsync covers steps 3–8.
//
// ExportAsync writes one Verbose line per step, in this order whatever is downloaded: the
// attachment downloads with their files and requests, the rendered report with its bytes, its
// check, and the move into place. A summary of the export ends them.
public sealed class TestFailureExporter
{
    private readonly IDocumentLauncher launcher;
    private readonly Func<string>? downloads;
    private readonly GenerationFolderCommit commit;

    public TestFailureExporter(IDocumentLauncher launcher, Func<string>? downloads = null)
        : this(launcher, downloads, new GenerationFolderCommit()) { }

    internal TestFailureExporter(IDocumentLauncher launcher, Func<string>? downloads, GenerationFolderCommit commit)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(commit);
        this.launcher = launcher;
        this.downloads = downloads;
        this.commit = commit;
    }

    public TestFailureExportPlan Prepare(AdoBuildTestFailureSet set, TestFailureExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(options);
        TestFailureReportModel model = TestFailureReportModelBuilder.Build(set, new TestFailureReportOptions
        {
            Culture = options.Culture, ConfiguredCulture = options.ConfiguredCulture, SessionCulture = options.SessionCulture,
            GeneratedAt = options.GeneratedAt, ToolkitVersion = options.ToolkitVersion,
            AttachmentWindowDays = options.AttachmentWindowDays, IncludeFlaky = options.IncludeFlaky,
        });
        string name = ReportFileNames.TestFailures(set.Build.Id);
        string path = options.CreateDirectory && options.Path is not null && !Directory.Exists(options.Path)
            ? Path.GetFullPath(Path.Combine(options.Path, name))
            : ReportFileNames.Resolve(options.Path, name, options.SessionCulture, downloads);
        if (!path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            throw new AdoFileOutputException(Messages.Get(AdoMessage.TestFailureReportPathInvalid, options.SessionCulture, path));
        GenerationFolderPlan plan = commit.Plan(path, options.GeneratedAt, options.NoClobber, options.SessionCulture);
        // Only runs inside the attachment window qualify, and the downloader itself allows only JSON
        // and text. Files of any size come from the latest run in the build's run order (never the
        // last run with a reported attachment or failure, and with no fallback to an older run), or
        // from every run with AllRunAttachments. Files of at most MaximumInlineJsonBytes come from
        // every run. The 0.2.0 and 0.3.0 default downloaded the latest run only.
        IReadOnlyList<AdoTestRun> runs = AttemptGrouper.OrderRuns(model.Runs);
        int? latest = runs.Count > 0 ? runs[^1].Id : null;
        HashSet<int> full = options.SkipAttachments ? []
            : options.AllRunAttachments ? [.. model.AttachmentRunIds]
            : latest is int id && model.AttachmentRunIds.Contains(id) ? [id] : [];
        HashSet<int> small = options.SkipAttachments ? [] : [.. model.AttachmentRunIds];
        AttachmentSelection selection = new(full, small, options.MaximumInlineJsonBytes, latest);
        bool download = model.Failures.SelectMany(f => f.Attempts).SelectMany(a => a.Attachments).Any(selection.Selects);
        return new TestFailureExportPlan(model, plan, options, download, selection);
    }

    public async Task<TestFailureExportResult> ExportAsync(TestFailureExportPlan plan, AttachmentDownloader? downloader,
        IAdoLog? log, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.DownloadsAttachments) ArgumentNullException.ThrowIfNull(downloader);
        IAdoLog output = log ?? new NullAdoLog();
        CultureInfo culture = plan.Options.SessionCulture;
        TestFailureReportModel model = plan.Model;
        IReadOnlyList<AdoDiagnostic> diagnostics = [];
        StageTimer stage = new(output, culture);
        int files = 0, requests = 0;
        if (plan.Options.CreateDirectory)
        {
            try { Directory.CreateDirectory(plan.Commit.Directory); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw new AdoFileOutputException(Messages.Get(AdoMessage.FileOutput, culture, plan.Commit.Directory), error);
            }
        }
        // Nothing to download: the line keeps its place, with nothing counted.
        if (!plan.DownloadsAttachments) stage.End(AdoMessage.ExportStageDownloads, files, requests);
        GenerationCommitResult result = await commit.CommitAsync(plan.Commit, plan.Options.NoClobber, plan.DownloadsAttachments,
            async (folder, token) =>
            {
                AttachmentDownloadResult downloaded = await downloader!.DownloadAsync(plan.Model.Failures, plan.Model.Build.TeamProject,
                    folder, plan.Commit.FolderName, culture, token, plan.Attachments).ConfigureAwait(false);
                diagnostics = downloaded.Diagnostics;
                foreach (AdoDiagnostic diagnostic in diagnostics) output.Warning(diagnostic.Message);
                TestFailureLocalAttachments? local = downloaded.Files.Count == 0 ? null : new()
                {
                    FolderName = plan.Commit.FolderName, SourceFolder = folder, Files = downloaded.Files,
                    MaximumInlineJsonBytes = downloader.MaximumInlineJsonBytes, MaximumInlineTotalBytes = downloader.MaximumInlineTotalBytes,
                };
                model = TestFailureReportModelBuilder.WithAttachments(plan.Model, downloaded.Failures, diagnostics, local);
                files = downloaded.Files.Count;
                requests = downloader.RequestCount;
                stage.End(AdoMessage.ExportStageDownloads, files, requests);
                return local is not null;
            },
            writer => HtmlTestFailureRenderer.Render(model, writer),
            (report, folder) =>
            {
                // The report has been rendered, written and flushed when its check begins.
                stage.End(AdoMessage.ExportStageRender, new FileInfo(report).Length);
                TestFailureReportValidator.Validate(report, model, folder);
                stage.End(AdoMessage.ExportStageValidation);
            },
            output.Warning, culture, cancellationToken).ConfigureAwait(false);
        stage.End(AdoMessage.ExportStageCommit);
        output.Verbose(Messages.Get(AdoMessage.ExportSummary, culture, plan.Model.Build.Id, files, requests, stage.Total));
        if (plan.Options.Open) DocumentOpener.Open(launcher, result.Report.FullName, culture, output.Warning);
        return new TestFailureExportResult { Report = result.Report, AttachmentDirectory = result.AttachmentDirectory, Diagnostics = diagnostics };
    }

    // Times steps that follow one another: each line gives the milliseconds since the previous one,
    // so the lines add up, within rounding, to Total.
    private sealed class StageTimer
    {
        private readonly IAdoLog log;
        private readonly CultureInfo culture;
        private readonly long started;
        private long last;

        internal StageTimer(IAdoLog log, CultureInfo culture)
        {
            this.log = log;
            this.culture = culture;
            started = last = Stopwatch.GetTimestamp();
        }

        internal long Total => Milliseconds(started, Stopwatch.GetTimestamp());

        internal void End(AdoMessage step, params object[] values)
        {
            long now = Stopwatch.GetTimestamp();
            log.Verbose(Messages.Get(step, culture, [.. values, Milliseconds(last, now)]));
            last = now;
        }

        private static long Milliseconds(long from, long to) => (long)Math.Round(Stopwatch.GetElapsedTime(from, to).TotalMilliseconds);
    }
}
