using AdoToolkit.Completion;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit;

// One AdoBuildTestFailureSet per input build. The invocation cache is shared across records, so
// piped builds of one definition reuse the history listings (§17). Session state is touched only
// on the pipeline thread; the worker gets the leased client and the record's log.
// ByDefinition is the default so that a call without arguments uses the connected profile's
// definition and branch; BuildId still binds by position and builds still bind from the pipeline.
[Cmdlet(VerbsCommon.Get, "AdoBuildTestFailure", DefaultParameterSetName = "ByDefinition")]
[OutputType(typeof(AdoBuildTestFailureSet))]
public sealed class GetAdoBuildTestFailureCommand : AdoCmdletBase, IDisposable
{
    private const int ProgressActivityId = 1;
    private readonly TestFailureInvocationCache cache = new();
    private ClientLease? lease;
    private AdoConnection? resolved;
    private AdoConfiguration? configuration;

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ByBuildId")]
    [ValidateRange(1, int.MaxValue)]
    public int BuildId { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "ByBuild")]
    [ValidateNotNull]
    public AdoBuild? InputObject { get; set; }

    [Parameter(ParameterSetName = "ByDefinition")]
    [ValidateNotNull]
    public object? Definition { get; set; }

    [Parameter(ParameterSetName = "ByDefinition")]
    [ValidateNotNullOrEmpty]
    public string? Branch { get; set; }

    [Parameter(ParameterSetName = "ByDefinition")]
    public BuildResult? Result { get; set; }

    [Parameter]
    [ValidateRange(1, 50)]
    public int? HistoryCount { get; set; }

    [Parameter]
    public AdoTestHistoryScope? HistoryScope { get; set; }

    [Parameter(ParameterSetName = "ByBuildId")]
    [Parameter(ParameterSetName = "ByDefinition")]
    [ArgumentCompleter(typeof(ProjectNameCompleter))]
    [ValidateNotNullOrEmpty]
    public string? Project { get; set; }

    [Parameter]
    public AdoConnection? Connection { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        AdoConnection connection = Resolve();
        if (InputObject is not null) EnsureSameCollection(InputObject.CollectionUri, connection);
        string project = InputObject?.TeamProject ?? ResolveProject(Project, connection);
        TestResultOptions options = configuration!.TestResults;
        TestFailureQuery query = new()
        {
            HistoryCount = HistoryCount ?? Math.Clamp(options.HistoryCount, 1, 50),
            HistoryScope = HistoryScope ?? Scope(options.HistoryScope),
            MaximumReportedFailures = options.MaximumReportedFailures,
            MaximumHistoryRequests = options.MaximumHistoryRequests,
        };
        CultureInfo culture = MessageCulture;
        // ByDefinition selects the latest completed build, as Get-AdoBuild -Latest does.
        BuildQuery? latest = null;
        string? definitionText = null;
        if (ParameterSetName == "ByDefinition")
        {
            (int? id, string? name) = ResolveDefinition(Definition, connection);
            latest = new BuildQuery
            {
                DefinitionId = id, DefinitionName = name, Branch = Branch ?? connection.DefaultBranch, Result = Result, Latest = true,
            };
            definitionText = name ?? id!.Value.ToString(CultureInfo.InvariantCulture);
        }
        lease ??= SessionStateRegistry.Current.Acquire(connection);
        ClientLease active = lease;
        AdoBuildTestFailureSet set = RunWorker(async (log, token) =>
        {
            BuildService builds = new(active.Client, connection, log);
            AdoBuild build = InputObject ?? (latest is null
                ? await builds.GetAsync(project, BuildId, culture, token).ConfigureAwait(false)
                : await LatestAsync(builds, project, latest, definitionText!, culture, token).ConfigureAwait(false));
            return await new TestFailureRetrievalService(active.Client, connection, log, cache)
                .GetAsync(build, query, culture, token).ConfigureAwait(false);
        });
        WriteProgress(new ProgressRecord(ProgressActivityId, Messages.Get(AdoMessage.ProgressTestFailureActivity, culture),
            Messages.Get(AdoMessage.ProgressTestFailureCompleted, culture, set.Failures.Count))
        { RecordType = ProgressRecordType.Completed });
        if (set.Status == AdoTestFailureStatus.Partial)
            WriteWarning(Messages.Get(AdoMessage.PartialTestFailureSet, culture,
                set.Build.Id.ToString(CultureInfo.InvariantCulture),
                set.Diagnostics.Count(diagnostic => diagnostic.Severity == AdoDiagnosticSeverity.Error),
                set.Diagnostics.Count(diagnostic => diagnostic.Severity == AdoDiagnosticSeverity.Warning),
                set.Diagnostics.Count(diagnostic => diagnostic.Severity == AdoDiagnosticSeverity.Info)));
        WriteObject(set);
    });

    protected override void EndProcessing() => Release();

    public void Dispose() => Release();

    // An unknown configured scope falls back to the documented default (Q-22). Only names match:
    // Enum.TryParse would also accept numeric text and produce undefined values.
    private static AdoTestHistoryScope Scope(string? configured) =>
        Enum.GetValues<AdoTestHistoryScope>().FirstOrDefault(
            scope => string.Equals(scope.ToString(), configured, StringComparison.OrdinalIgnoreCase),
            AdoTestHistoryScope.SameBranch);

    private static async Task<AdoBuild> LatestAsync(BuildService builds, string project, BuildQuery query, string definition,
        CultureInfo culture, CancellationToken token)
    {
        IReadOnlyList<AdoBuild> found = await builds.GetBuildsAsync(project, query, culture, token).ConfigureAwait(false);
        return found.Count > 0 ? found[0]
            : throw new AdoNotFoundException(Messages.Get(AdoMessage.NoLatestBuild, culture, definition))
            { Operation = "BuildsList", Project = project };
    }

    private AdoConnection Resolve()
    {
        if (resolved is not null) return resolved;
        resolved = ResolveConnection(Connection);
        configuration = new ConfigurationStore().Load(MessageCulture);
        foreach (string warning in configuration.Warnings) WriteWarning(warning);
        return resolved;
    }

    private void Release() => Interlocked.Exchange(ref lease, null)?.Dispose();

    private protected override void OnProgress(AdoProgress progress)
    {
        int completed = progress.Completed;
        int? total = progress.Total;
        string status = progress.Phase switch
        {
            AdoProgressPhase.TestRuns => Messages.Get(AdoMessage.ProgressTestRuns, MessageCulture, completed),
            AdoProgressPhase.TestResults => Messages.Get(AdoMessage.ProgressTestResults, MessageCulture, completed, total ?? completed),
            AdoProgressPhase.TestDetail => Messages.Get(AdoMessage.ProgressTestDetail, MessageCulture, completed, total ?? completed),
            AdoProgressPhase.Attachments => Messages.Get(AdoMessage.ProgressAttachments, MessageCulture, completed, total ?? completed),
            AdoProgressPhase.TestCaseLinks => Messages.Get(AdoMessage.ProgressTestCaseLinks, MessageCulture, completed),
            _ => Messages.Get(AdoMessage.ProgressHistory, MessageCulture, completed, total ?? completed),
        };
        WriteProgress(new ProgressRecord(ProgressActivityId,
            Messages.Get(AdoMessage.ProgressTestFailureActivity, MessageCulture), status)
        {
            PercentComplete = total is int value && value > 0 ? (int)Math.Min(100, completed * 100L / value) : -1,
        });
    }
}
