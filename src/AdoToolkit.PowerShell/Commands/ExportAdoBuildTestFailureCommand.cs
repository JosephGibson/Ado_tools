using System.IO;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit;

// One report per input set (§15.8). Steps 1–2 of §13.4 and ShouldProcess run on the pipeline
// thread; one ShouldProcess call covers steps 3–8, so -WhatIf names the report and attachment
// folder and requests nothing. A connection is needed only when attachments are downloaded.
[Cmdlet(VerbsData.Export, "AdoBuildTestFailure", SupportsShouldProcess = true, DefaultParameterSetName = "Input")]
[OutputType(typeof(FileInfo))]
public sealed class ExportAdoBuildTestFailureCommand : AdoCmdletBase, IDisposable
{
    private const int ProgressActivityId = 1;
    private ClientLease? lease;
    private AdoConnection? resolved;
    private AdoConfiguration? configuration;
    private string? resolvedPath;
    private bool pathIsFile;
    private bool createDirectory;
    private int received;
    private int downloadTotal;

    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ParameterSetName = "Input")]
    [ValidateNotNull]
    public AdoBuildTestFailureSet? InputObject { get; set; }

    [Parameter] [ValidateNotNullOrEmpty] public string? Culture { get; set; }
    [Parameter] [ValidateNotNullOrEmpty] public string? Path { get; set; }
    [Parameter] public SwitchParameter SkipAttachments { get; set; }
    [Parameter] public SwitchParameter AllRunAttachments { get; set; }
    [Parameter] [ValidateRange(1, 365)] public int AttachmentWindowDays { get; set; } = TestFailureReportOptions.DefaultAttachmentWindowDays;
    [Parameter] public SwitchParameter IncludeFlaky { get; set; }
    [Parameter] public SwitchParameter NoClobber { get; set; }
    [Parameter] public SwitchParameter Open { get; set; }
    [Parameter] public AdoConnection? Connection { get; set; }

    protected override void BeginProcessing()
    {
        base.BeginProcessing();
        if (Path is null) return;
        resolvedPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path, out ProviderInfo provider, out _);
        if (!string.Equals(provider.Name, "FileSystem", StringComparison.OrdinalIgnoreCase))
            ThrowTerminatingError(new ErrorRecord(new ArgumentException(Messages.Get(AdoMessage.FileSystemPathRequired, MessageCulture)),
                "FileSystemPathRequired", ErrorCategory.InvalidArgument, Path));
        // An existing directory or an .html name keeps its meaning. Any other path without an
        // extension is a directory created at export time; a trailing separator forces that.
        bool isDirectory = Directory.Exists(resolvedPath);
        pathIsFile = !isDirectory && resolvedPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
        createDirectory = !isDirectory && !pathIsFile;
        if (createDirectory && (File.Exists(resolvedPath) || System.IO.Path.HasExtension(resolvedPath)))
            ThrowTerminatingError(new ErrorRecord(new ArgumentException(Messages.Get(AdoMessage.TestFailureReportPathInvalid, MessageCulture, resolvedPath)),
                "TestFailureReportPathInvalid", ErrorCategory.InvalidArgument, Path));
    }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        AdoBuildTestFailureSet set = InputObject!;
        // A file path names exactly one report; each later set gets a per-input error.
        if (pathIsFile && received++ > 0)
        {
            WriteError(new ErrorRecord(new AdoFileOutputException(Messages.Get(AdoMessage.TestFailureReportSingleFile, MessageCulture,
                set.Build.Id.ToString(CultureInfo.InvariantCulture))), "TestFailureReportSingleFile", ErrorCategory.InvalidArgument, set));
            return;
        }
        if (configuration is null)
        {
            configuration = new ConfigurationStore().Load(MessageCulture);
            foreach (string warning in configuration.Warnings) WriteWarning(warning);
        }
        TestFailureExporter exporter = new(new ShellDocumentLauncher());
        TestFailureExportPlan plan = exporter.Prepare(set, new TestFailureExportOptions
        {
            Culture = Culture, ConfiguredCulture = configuration.Reporting.Culture, SessionCulture = MessageCulture,
            Path = resolvedPath, CreateDirectory = createDirectory, NoClobber = NoClobber, SkipAttachments = SkipAttachments, Open = Open,
            AllRunAttachments = AllRunAttachments, AttachmentWindowDays = AttachmentWindowDays, IncludeFlaky = IncludeFlaky,
            GeneratedAt = DateTimeOffset.Now,
            ToolkitVersion = typeof(ExportAdoBuildTestFailureCommand).Assembly.GetName().Version!.ToString(),
        });
        foreach (string warning in plan.Warnings) WriteWarning(warning);
        AdoConnection? connection = null;
        if (plan.DownloadsAttachments)
        {
            connection = resolved ??= ResolveConnection(Connection);
            EnsureSameCollection(set.CollectionUri, connection);
        }
        string action = plan.AttachmentDirectory is { } folder
            ? Messages.Get(AdoMessage.ExportTestFailureReportWithAttachments, MessageCulture, folder)
            : Messages.Get(AdoMessage.ExportTestFailureReport, MessageCulture);
        if (!ShouldProcess(plan.ReportPath, action)) return;
        ClientLease? active = connection is null ? null : lease ??= SessionStateRegistry.Current.Acquire(connection);
        TestResultOptions limits = configuration.TestResults;
        TestFailureExportResult result = RunWorker((log, token) => exporter.ExportAsync(plan,
            active is null ? null : new AttachmentDownloader(active.Client, connection!, limits, log), log, token));
        if (plan.DownloadsAttachments)
            WriteProgress(new ProgressRecord(ProgressActivityId, Messages.Get(AdoMessage.ExportTestFailureReport, MessageCulture),
                Messages.Get(AdoMessage.ProgressAttachmentDownload, MessageCulture, downloadTotal, downloadTotal))
            { RecordType = ProgressRecordType.Completed });
        PSObject output = PSObject.AsPSObject(result.Report);
        if (result.AttachmentDirectory is not null)
            output.Properties.Add(new PSNoteProperty("AttachmentDirectory", result.AttachmentDirectory.FullName));
        WriteObject(output);
    });

    protected override void EndProcessing() => Release();

    public void Dispose() => Release();

    private void Release() => Interlocked.Exchange(ref lease, null)?.Dispose();

    private protected override void OnProgress(AdoProgress progress)
    {
        if (progress.Phase != AdoProgressPhase.AttachmentDownload) return;
        int total = downloadTotal = progress.Total ?? progress.Completed;
        WriteProgress(new ProgressRecord(ProgressActivityId, Messages.Get(AdoMessage.ExportTestFailureReport, MessageCulture),
            Messages.Get(AdoMessage.ProgressAttachmentDownload, MessageCulture, progress.Completed, total))
        {
            PercentComplete = total > 0 ? (int)Math.Min(100, progress.Completed * 100L / total) : -1,
        });
    }
}
