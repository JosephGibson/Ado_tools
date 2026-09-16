using AdoToolkit.Completion;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit;

// BySuite is the default because none of its mandatory parameters come from the pipeline: PowerShell
// drops it for piped input, so no default-set preference lets an Id property bind by name first.
[Cmdlet(VerbsCommon.Get, "AdoTestCase", DefaultParameterSetName = BySuite)]
[OutputType(typeof(AdoTestCase))]
public sealed class GetAdoTestCaseCommand : AdoCmdletBase
{
    private const string ById = "ById";
    private const string BySuite = "BySuite";
    private const string BySuiteObject = "BySuiteObject";
    private const string ByWiql = "ByWiql";
    private const string ByWorkItem = "ByWorkItem";
    private const int ProgressActivityId = 1;
    // Inputs in arrival order: a test case ID, or a suite whose memberships are read in EndProcessing.
    private readonly List<(int Id, TestSuiteSelection? Selection)> inputs = [];
    private AdoConnection? resolved;

    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = ById)]
    [ValidateRange(1, int.MaxValue)]
    [ValidateNotNullOrEmpty]
    public int[] Id { get; set; } = [];

    [Parameter(Mandatory = true, ParameterSetName = BySuite)]
    [ValidateRange(1, int.MaxValue)]
    public int PlanId { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = BySuite)]
    [ValidateRange(1, int.MaxValue)]
    public int SuiteId { get; set; }

    // Typed objects bind by value only. Scalars match exactly in the first binding stage, before
    // any Id property could bind by name, so a suite ID never becomes a test case ID.
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = BySuiteObject)]
    [ValidateNotNull]
    public AdoTestSuite? Suite { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ByWiql)]
    [ValidateNotNull]
    public AdoWiqlResult? WiqlResult { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ByWorkItem)]
    [ValidateNotNull]
    public AdoWorkItem? WorkItem { get; set; }

    [Parameter(ParameterSetName = BySuite)]
    [Parameter(ParameterSetName = BySuiteObject)]
    public SwitchParameter Recurse { get; set; }

    [Parameter(ParameterSetName = BySuite)]
    [ArgumentCompleter(typeof(ProjectNameCompleter))]
    [ValidateNotNullOrEmpty]
    public string? Project { get; set; }

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? MaximumSharedStepDepth { get; set; }

    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? MaximumExpandedSteps { get; set; }

    [Parameter]
    public SwitchParameter Strict { get; set; }

    [Parameter]
    public AdoConnection? Connection { get; set; }

    // Provenance of objects bound by property name; typed inputs carry their own CollectionUri.
    [Parameter(ValueFromPipelineByPropertyName = true, DontShow = true, ParameterSetName = ById)]
    public Uri? CollectionUri { get; set; }

    // Guards the by-name Id binding: untyped suite or plan shapes (for example deserialized
    // or selected objects) carry these properties, and their Id is not a test case ID.
    [Parameter(ValueFromPipelineByPropertyName = true, DontShow = true, ParameterSetName = ById)]
    public object? SuitePath { get; set; }

    [Parameter(ValueFromPipelineByPropertyName = true, DontShow = true, ParameterSetName = ById)]
    public object? RootSuiteId { get; set; }

    protected override void ProcessRecord() => RunLocal(() =>
    {
        resolved ??= ResolveConnection(Connection);
        if (CollectionUri is not null) EnsureSameCollection(CollectionUri, resolved);
        switch (ParameterSetName)
        {
            case ById:
                if (SuitePath is not null || RootSuiteId is not null)
                {
                    string target = string.Join(",", Id.Select(id => id.ToString(CultureInfo.InvariantCulture)));
                    WriteError(new ErrorRecord(new ArgumentException(Messages.Get(AdoMessage.InputNotTestCase, MessageCulture, target)),
                        "InputNotTestCase", ErrorCategory.InvalidType, Id));
                    return;
                }
                foreach (int id in Id) inputs.Add((id, null));
                break;
            case ByWorkItem:
                EnsureSameCollection(WorkItem!.CollectionUri, resolved);
                inputs.Add((WorkItem.Id, null));
                break;
            case ByWiql:
                EnsureSameCollection(WiqlResult!.CollectionUri, resolved);
                foreach (int id in WiqlResult.Ids) inputs.Add((id, null));
                break;
            case BySuite:
                inputs.Add((0, new TestSuiteSelection { Project = ResolveProject(Project, resolved), PlanId = PlanId, SuiteId = SuiteId, Recurse = Recurse }));
                break;
            case BySuiteObject:
                EnsureSameCollection(Suite!.CollectionUri, resolved);
                inputs.Add((0, new TestSuiteSelection { Project = Suite.TeamProject, PlanId = Suite.PlanId, SuiteId = Suite.Id, Recurse = Recurse, Suite = Suite }));
                break;
        }
    });

    protected override void EndProcessing() => RunLocal(() =>
    {
        if (inputs.Count == 0) return;
        AdoConfiguration configuration = new ConfigurationStore().Load(MessageCulture);
        foreach (string warning in configuration.Warnings) WriteWarning(warning);
        TestCaseOptions options = new()
        {
            MaximumSharedStepDepth = MaximumSharedStepDepth ?? configuration.TestCases.MaximumSharedStepDepth,
            MaximumExpandedSteps = MaximumExpandedSteps ?? configuration.TestCases.MaximumExpandedSteps,
            MaximumResolvedWorkItems = configuration.TestCases.MaximumResolvedWorkItems,
        };
        SessionStateHolder session = SessionStateRegistry.Current;
        using ClientLease lease = session.Acquire(resolved!);
        CultureInfo culture = MessageCulture;
        (List<TestCaseMembership> memberships, List<AdoException> failures, TestCaseResult result) = RunWorker(async (log, token) =>
        {
            SuiteMembershipService suites = new(lease.Client, resolved!, log);
            List<TestCaseMembership> ordered = [];
            List<AdoException> notFound = [];
            // One occurrence per case ID, and per case within each plan suite.
            HashSet<(int Id, int PlanId, int SuiteId)> seen = [];
            foreach ((int id, TestSuiteSelection? selection) in inputs)
            {
                if (selection is null)
                {
                    if (seen.Add((id, 0, 0))) ordered.Add(new TestCaseMembership { TestCaseId = id });
                    continue;
                }
                try
                {
                    foreach (TestCaseMembership membership in await suites.GetMembershipsAsync(selection, culture, token).ConfigureAwait(false))
                        if (seen.Add((membership.TestCaseId, membership.Suite!.PlanId, membership.Suite.SuiteId))) ordered.Add(membership);
                }
                catch (AdoNotFoundException error) { notFound.Add(error); }
            }
            TestCaseResult retrieved = ordered.Count == 0 ? new TestCaseResult()
                : await new TestCaseService(lease.Client, resolved!, session.TestCategories, log)
                    .GetTestCasesForMembershipsAsync(ordered, culture, token, options).ConfigureAwait(false);
            return (ordered, notFound, retrieved);
        });
        WriteProgress(new ProgressRecord(ProgressActivityId, Messages.Get(AdoMessage.ProgressActivity, culture),
            Messages.Get(AdoMessage.ProgressCompleted, culture, result.TestCases.Count)) { RecordType = ProgressRecordType.Completed });
        foreach (AdoException failure in failures) Report(failure);
        HashSet<int> found = [.. result.TestCases.Select(item => item.Id)];
        Dictionary<int, AdoDiagnostic> rejected = result.InputDiagnostics.ToDictionary(diagnostic => diagnostic.WorkItemId!.Value);
        HashSet<int> reported = [];
        int next = 0;
        foreach (TestCaseMembership membership in memberships)
        {
            int id = membership.TestCaseId;
            if (found.Contains(id))
            {
                // Results follow membership order, so the next result is this membership's copy.
                AdoTestCase item = result.TestCases[next++];
                if (item.Status == AdoTestCaseStatus.Partial)
                {
                    string message = Messages.Get(AdoMessage.PartialTestCase, culture, id.ToString(CultureInfo.InvariantCulture),
                        item.Diagnostics.Count(d => d.Severity == AdoDiagnosticSeverity.Error),
                        item.Diagnostics.Count(d => d.Severity == AdoDiagnosticSeverity.Warning),
                        item.Diagnostics.Count(d => d.Severity == AdoDiagnosticSeverity.Info));
                    if (Strict)
                    {
                        InvalidOperationException error = new(message);
                        error.Data["Diagnostics"] = item.Diagnostics;
                        WriteError(new ErrorRecord(error, "PartialTestCase", ErrorCategory.InvalidData, item));
                        continue;
                    }
                    if (reported.Add(id)) WriteWarning(message);
                }
                WriteObject(item);
            }
            else if (!reported.Add(id)) continue;
            else if (rejected.TryGetValue(id, out AdoDiagnostic? diagnostic))
                WriteError(new ErrorRecord(new InvalidOperationException(diagnostic.Message), diagnostic.Code, ErrorCategory.InvalidType, id));
            else
            {
                AdoNotFoundException error = new(Messages.Get(AdoMessage.WorkItemNotFound, culture, id.ToString(CultureInfo.InvariantCulture))) { Operation = "WorkItemsBatch" };
                WriteError(new ErrorRecord(error, "AdoNotFound", ErrorCategory.ObjectNotFound, id));
            }
        }
    });

    private protected override void OnProgress(AdoProgress progress)
    {
        int completed = progress.Completed;
        string status = progress.Phase switch
        {
            AdoProgressPhase.Enumeration => Messages.Get(AdoMessage.ProgressEnumeration, MessageCulture, completed, progress.Total ?? completed),
            AdoProgressPhase.Fetch => Messages.Get(AdoMessage.ProgressFetch, MessageCulture, completed),
            _ => Messages.Get(AdoMessage.ProgressExpansion, MessageCulture, completed, progress.Total ?? completed),
        };
        WriteProgress(new ProgressRecord(ProgressActivityId, Messages.Get(AdoMessage.ProgressActivity, MessageCulture), status)
        {
            PercentComplete = progress.Total is int total && total > 0 ? (int)Math.Min(100, completed * 100L / total) : -1,
        });
    }
}
