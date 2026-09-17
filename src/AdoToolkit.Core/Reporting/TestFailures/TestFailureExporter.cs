using AdoToolkit.Core.IO;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Reporting.TestFailures;

// One report per set (§15.8). Prepare covers §13.4 steps 1–2 on the caller's thread so the shell
// can ask ShouldProcess before anything is downloaded or written; ExportAsync covers steps 3–8.
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
        });
        string name = ReportFileNames.TestFailures(set.Build.Id);
        string path = options.CreateDirectory && options.Path is not null && !Directory.Exists(options.Path)
            ? Path.GetFullPath(Path.Combine(options.Path, name))
            : ReportFileNames.Resolve(options.Path, name, options.SessionCulture, downloads);
        if (!path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            throw new AdoFileOutputException(Messages.Get(AdoMessage.TestFailureReportPathInvalid, options.SessionCulture, path));
        GenerationFolderPlan plan = commit.Plan(path, options.GeneratedAt, options.NoClobber, options.SessionCulture);
        bool download = !options.SkipAttachments && model.Failures.Any(f => f.Attempts.Any(a => a.Attachments.Count > 0));
        return new TestFailureExportPlan(model, plan, options, download);
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
        if (plan.Options.CreateDirectory)
        {
            try { Directory.CreateDirectory(plan.Commit.Directory); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                throw new AdoFileOutputException(Messages.Get(AdoMessage.FileOutput, culture, plan.Commit.Directory), error);
            }
        }
        GenerationCommitResult result = await commit.CommitAsync(plan.Commit, plan.Options.NoClobber, plan.DownloadsAttachments,
            async (folder, token) =>
            {
                AttachmentDownloadResult downloaded = await downloader!.DownloadAsync(plan.Model.Failures, plan.Model.Build.TeamProject,
                    folder, plan.Commit.FolderName, culture, token).ConfigureAwait(false);
                diagnostics = downloaded.Diagnostics;
                foreach (AdoDiagnostic diagnostic in diagnostics) output.Warning(diagnostic.Message);
                TestFailureLocalAttachments? local = downloaded.Files.Count == 0 ? null : new()
                {
                    FolderName = plan.Commit.FolderName, SourceFolder = folder, Files = downloaded.Files,
                    MaximumInlineJsonBytes = downloader.MaximumInlineJsonBytes,
                };
                model = TestFailureReportModelBuilder.WithAttachments(plan.Model, downloaded.Failures, diagnostics, local);
                return local is not null;
            },
            writer => HtmlTestFailureRenderer.Render(model, writer),
            (report, folder) => TestFailureReportValidator.Validate(report, model, folder),
            output.Warning, culture, cancellationToken).ConfigureAwait(false);
        if (plan.Options.Open) launcher.Open(result.Report.FullName);
        return new TestFailureExportResult { Report = result.Report, AttachmentDirectory = result.AttachmentDirectory, Diagnostics = diagnostics };
    }
}
