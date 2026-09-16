using System.Collections.ObjectModel;
using System.Net.Http;
using AdoToolkit.Core.Configuration;
using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.TestManagement;

public sealed class TestCaseService
{
    private static readonly string[] RootFields = ["System.Id", "System.Rev", "System.Title", "System.WorkItemType", "System.TeamProject",
        "System.State", "System.AreaPath", "System.IterationPath", "System.AssignedTo", "System.ChangedBy", "System.ChangedDate",
        "Microsoft.VSTS.Common.Priority", "Microsoft.VSTS.TCM.AutomationStatus", "Microsoft.VSTS.TCM.Steps",
        "Microsoft.VSTS.TCM.Parameters", "Microsoft.VSTS.TCM.LocalDataSource"];
    private readonly AdoConnection connection;
    private readonly TestWorkItemReader reader;
    private readonly TestCapabilityDetector detector;
    private readonly IAdoLog log;

    public TestCaseService(HttpClient client, AdoConnection connection, SessionCache<IReadOnlyList<string>> categoryCache, IAdoLog? log = null)
    {
        this.connection = connection;
        this.log = log ?? new NullAdoLog();
        reader = new(client, connection, log);
        detector = new(client, connection, categoryCache, log);
    }

    public Task<TestCaseResult> GetTestCasesAsync(IReadOnlyList<int> ids, CultureInfo culture,
        CancellationToken cancellationToken, TestCaseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(culture);
        options ??= new();
        ValidateOptions(options);
        foreach (int id in ids) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        return RetrieveAsync(ids.Distinct().ToArray(), culture, options, cancellationToken);
    }

    /// <summary>
    /// Bulk path (§14.2): every distinct case is fetched and expanded once with one Shared Steps
    /// cache across all roots, then emitted once per membership in membership order. Copies for
    /// different suites share the expanded step list.
    /// </summary>
    public async Task<TestCaseResult> GetTestCasesForMembershipsAsync(IReadOnlyList<TestCaseMembership> memberships, CultureInfo culture,
        CancellationToken cancellationToken, TestCaseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(memberships);
        ArgumentNullException.ThrowIfNull(culture);
        options ??= new();
        ValidateOptions(options);
        foreach (TestCaseMembership membership in memberships)
        {
            ArgumentNullException.ThrowIfNull(membership);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(membership.TestCaseId);
            if (membership.Suite is not null && membership.Suite.CollectionUri != connection.CollectionUri)
                throw new AdoConnectionMismatchException(Messages.Get(AdoMessage.ConnectionMismatch, culture));
        }
        TestCaseResult distinct = await RetrieveAsync(memberships.Select(membership => membership.TestCaseId).Distinct().ToArray(),
            culture, options, cancellationToken).ConfigureAwait(false);
        Dictionary<int, AdoTestCase> byId = distinct.TestCases.ToDictionary(item => item.Id);
        List<AdoTestCase> emitted = [];
        foreach (TestCaseMembership membership in memberships)
            if (byId.TryGetValue(membership.TestCaseId, out AdoTestCase? item))
                emitted.Add(membership.Suite is null ? item : WithSuite(item, membership.Suite));
        return new() { TestCases = emitted.AsReadOnly(), InputDiagnostics = distinct.InputDiagnostics, MissingIds = distinct.MissingIds };
    }

    private async Task<TestCaseResult> RetrieveAsync(int[] unique, CultureInfo culture, TestCaseOptions options, CancellationToken cancellationToken)
    {
        int batches = 0;
        int rootBatches = (unique.Length + 199) / 200;
        IReadOnlyList<TestWorkItem> items = await reader.ReadAsync(unique, RootFields, culture, cancellationToken,
            () => log.Progress(new AdoProgress { Phase = AdoProgressPhase.Fetch, Completed = ++batches, Total = rootBatches })).ConfigureAwait(false);
        List<ResolvedStepDocument> roots = [];
        Dictionary<int, ParameterDocument> parameters = [];
        List<AdoDiagnostic> inputDiagnostics = [];
        foreach (TestWorkItem item in items)
        {
            string project = Required(item, "System.TeamProject", culture);
            string type = Required(item, "System.WorkItemType", culture);
            if (!await detector.IsStepContainerAsync(item.Fields.ContainsKey("Microsoft.VSTS.TCM.Steps"), project, type, culture, cancellationToken).ConfigureAwait(false))
            {
                inputDiagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.NotAStepContainer, culture, item.Id));
                continue;
            }
            roots.Add(new() { Item = item, Document = StepsXmlParser.Parse(item.Text("Microsoft.VSTS.TCM.Steps"), item.Id, item.Rev, culture) });
            parameters.Add(item.Id, ParameterDataParser.Parse(item.Text("Microsoft.VSTS.TCM.Parameters"),
                item.Text("Microsoft.VSTS.TCM.LocalDataSource"), culture, item.Id));
        }
        IReadOnlyDictionary<int, ResolvedStepDocument> cache = await new SharedStepResolver(reader).ResolveAsync(roots, unique, items,
            parameters.Values.SelectMany(parameter => parameter.SharedMapping.Values), options.MaximumSharedStepDepth,
            options.MaximumResolvedWorkItems, culture, cancellationToken,
            () => log.Progress(new AdoProgress { Phase = AdoProgressPhase.Fetch, Completed = ++batches })).ConfigureAwait(false);
        List<AdoTestCase> cases = [];
        foreach (ResolvedStepDocument root in roots)
        {
            TestWorkItem item = root.Item!;
            StepExpander expander = new(cache, connection.CollectionUri, options.MaximumSharedStepDepth, options.MaximumExpandedSteps, culture, cancellationToken);
            ExpansionContext expanded = expander.Expand(root.Document!);
            ParameterDocument parameter = parameters[item.Id];
            expanded.Diagnostics.AddRange(parameter.Diagnostics);
            AdoTestParameters joined = Join(parameter, cache, expanded.Diagnostics, culture, item.Id);
            if (item.Value("System.ChangedDate") is not DateTimeOffset changed)
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = "WorkItemsBatch" };
            object? priority = item.Value("Microsoft.VSTS.Common.Priority");
            if (priority is not null && (priority is not long integer || integer < int.MinValue || integer > int.MaxValue))
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = "WorkItemsBatch" };
            cases.Add(new()
            {
                Id = item.Id, Rev = item.Rev, Title = Required(item, "System.Title", culture), WorkItemType = Required(item, "System.WorkItemType", culture),
                TeamProject = Required(item, "System.TeamProject", culture), State = Required(item, "System.State", culture),
                Priority = priority is long value ? (int)value : null, AutomationStatus = item.Text("Microsoft.VSTS.TCM.AutomationStatus"),
                AreaPath = item.Text("System.AreaPath"), IterationPath = item.Text("System.IterationPath"),
                AssignedTo = item.Value("System.AssignedTo") as AdoIdentityRef, ChangedBy = item.Value("System.ChangedBy") as AdoIdentityRef,
                ChangedDate = changed, CollectionUri = connection.CollectionUri, WebUrl = AdoWebLinks.WorkItem(connection.CollectionUri, item.Text("System.TeamProject")!, item.Id),
                RetrievedAt = DateTimeOffset.UtcNow, Steps = expanded.Rows.AsReadOnly(), Diagnostics = expanded.Diagnostics.AsReadOnly(), Parameters = joined,
                SharedSteps = Array.AsReadOnly(expanded.Rows.Where(row => row.SharedStep is not null).GroupBy(row => row.SharedStep!.Id)
                    .Select(group => expander.Info(group.Key, group.Count())).ToArray()),
            });
            log.Progress(new AdoProgress { Phase = AdoProgressPhase.Expansion, Completed = cases.Count, Total = roots.Count });
        }
        return new() { TestCases = cases.AsReadOnly(), InputDiagnostics = inputDiagnostics.AsReadOnly(),
            MissingIds = Array.AsReadOnly(unique.Except(items.Select(item => item.Id)).ToArray()) };
    }

    private static void ValidateOptions(TestCaseOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumSharedStepDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumExpandedSteps);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumResolvedWorkItems);
    }

    private static AdoTestCase WithSuite(AdoTestCase item, AdoTestSuiteRef suite) => new()
    {
        Id = item.Id, Rev = item.Rev, Title = item.Title, WorkItemType = item.WorkItemType, TeamProject = item.TeamProject,
        State = item.State, Priority = item.Priority, AutomationStatus = item.AutomationStatus, AreaPath = item.AreaPath,
        IterationPath = item.IterationPath, AssignedTo = item.AssignedTo, ChangedBy = item.ChangedBy, ChangedDate = item.ChangedDate,
        WebUrl = item.WebUrl, Steps = item.Steps, Parameters = item.Parameters, SharedSteps = item.SharedSteps,
        Diagnostics = item.Diagnostics, Suite = suite, RetrievedAt = item.RetrievedAt, CollectionUri = item.CollectionUri,
    };

    private AdoTestParameters Join(ParameterDocument source, IReadOnlyDictionary<int, ResolvedStepDocument> cache,
        List<AdoDiagnostic> diagnostics, CultureInfo culture, int rootId)
    {
        if (source.Parameters.Source != AdoParameterSource.Shared) return source.Parameters;
        Dictionary<int, ParameterDocument> sets = [];
        List<AdoSharedParameterInfo> metadata = [];
        foreach (int id in source.SharedMapping.Values.Distinct())
        {
            cache.TryGetValue(id, out ResolvedStepDocument? resolved);
            TestWorkItem? item = resolved?.Item;
            AdoSharedStepInfo info = item?.Info(connection.CollectionUri) ?? new() { Id = id };
            metadata.Add(new() { Id = id, Title = info.Title, Rev = info.Rev, TeamProject = info.TeamProject, WebUrl = info.WebUrl });
            if (item is null)
            {
                diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.UnresolvedSharedParameter, culture, rootId));
                if (resolved?.ResolutionLimited == true)
                    diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.ResolutionLimitExceeded, culture, rootId));
                continue;
            }
            // [V-03] Shared parameter row XML is assumed to be in the Parameters field.
            ParameterDocument parsed = ParameterDataParser.ParseSharedSet(item.Text("Microsoft.VSTS.TCM.Parameters"), culture, id);
            sets.Add(id, parsed);
            diagnostics.AddRange(parsed.Diagnostics);
        }
        List<IReadOnlyDictionary<string, string>> rows = [];
        int count = sets.Values.Select(set => set.Parameters.Rows.Count).DefaultIfEmpty().Max();
        for (int index = 0; index < count; index++)
        {
            Dictionary<string, string> row = new(StringComparer.Ordinal);
            foreach ((string name, int id) in source.SharedMapping)
                if (sets.TryGetValue(id, out ParameterDocument? set) && index < set.Parameters.Rows.Count
                    && set.Parameters.Rows[index].TryGetValue(name, out string? value)) row.Add(name, value);
            rows.Add(new ReadOnlyDictionary<string, string>(row));
        }
        return new() { Source = AdoParameterSource.Shared, Names = source.Parameters.Names, Rows = rows.AsReadOnly(), SharedParameterSets = metadata.AsReadOnly() };
    }

    private static string Required(TestWorkItem item, string field, CultureInfo culture) => item.Text(field) is { Length: > 0 } text ? text
        : throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = "WorkItemsBatch" };
}
