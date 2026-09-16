using System.Net.Http;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.TestManagement;

/// <summary>
/// Reads suite test case memberships for one invocation. Plan listings and suite trees are
/// resolved once per project and plan; suites are walked depth-first and cases follow the
/// <c>order</c> field within each suite.
/// </summary>
public sealed class SuiteMembershipService
{
    private readonly AdoConnection connection;
    private readonly AdoHttpPipeline pipeline;
    private readonly TestPlanService plans;
    private readonly TestSuiteService suites;
    private readonly IAdoLog log;
    private readonly Dictionary<string, IReadOnlyList<AdoTestPlan>> plansByProject = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Project, int PlanId), IReadOnlyList<AdoTestSuite>> trees = [];
    private int listedSuites;
    private int knownSuites;

    public SuiteMembershipService(HttpClient client, AdoConnection connection, IAdoLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(connection.RequestTimeoutSeconds);
        this.connection = connection;
        this.log = log ?? new NullAdoLog();
        pipeline = new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log);
        plans = new TestPlanService(client, connection, log);
        suites = new TestSuiteService(client, connection, log);
    }

    public async Task<IReadOnlyList<TestCaseMembership>> GetMembershipsAsync(TestSuiteSelection selection, CultureInfo culture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(selection.Project);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(selection.PlanId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(selection.SuiteId);
        if (selection.Suite is not null)
        {
            if (selection.Suite.CollectionUri != connection.CollectionUri)
                throw new AdoConnectionMismatchException(Messages.Get(AdoMessage.ConnectionMismatch, culture));
            if (selection.Suite.Id != selection.SuiteId || selection.Suite.PlanId != selection.PlanId
                || !string.Equals(selection.Suite.TeamProject, selection.Project, StringComparison.Ordinal))
                throw new ArgumentException(null, nameof(selection));
        }
        string project = selection.Project;
        AdoTestPlan plan = await PlanAsync(project, selection.PlanId, culture, cancellationToken).ConfigureAwait(false);
        AdoTestSuite[] selected = selection.Suite is not null && !selection.Recurse
            ? [selection.Suite]
            : Subtree(await TreeAsync(project, selection.PlanId, culture, cancellationToken).ConfigureAwait(false),
                selection, culture);
        knownSuites += selected.Length;
        List<TestCaseMembership> memberships = [];
        foreach (AdoTestSuite suite in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<SuiteTestCaseDto> values = await pipeline.GetPagesAsync(EndpointRegistry.SuiteTestCaseList,
                AdoJsonContext.Default.SuiteTestCasePageDto, static page => page.Value,
                static item => item.WorkItem?.Id.ToString(CultureInfo.InvariantCulture) ?? string.Empty, culture, cancellationToken,
                new Dictionary<string, string>
                {
                    ["project"] = project, ["planId"] = selection.PlanId.ToString(CultureInfo.InvariantCulture),
                    ["suiteId"] = suite.Id.ToString(CultureInfo.InvariantCulture),
                }).ConfigureAwait(false);
            AdoTestSuiteRef reference = new()
            {
                PlanId = plan.Id, SuiteId = suite.Id, PlanName = plan.Name, SuiteName = suite.Name, SuitePath = suite.SuitePath,
                TeamProject = project, CollectionUri = connection.CollectionUri,
                PlanWebUrl = AdoWebLinks.TestPlan(connection.CollectionUri, project, plan.Id),
                WebUrl = AdoWebLinks.TestPlan(connection.CollectionUri, project, plan.Id, suite.Id),
            };
            HashSet<int> seen = [];
            // OrderBy is stable: equal order values keep the response order.
            foreach (SuiteTestCaseDto value in values.OrderBy(static value => value.Order))
            {
                if (value.WorkItem is null || value.WorkItem.Id < 1)
                    throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = EndpointRegistry.SuiteTestCaseList.Name };
                if (seen.Add(value.WorkItem.Id)) memberships.Add(new TestCaseMembership { TestCaseId = value.WorkItem.Id, Suite = reference });
            }
            log.Progress(new AdoProgress { Phase = AdoProgressPhase.Enumeration, Completed = ++listedSuites, Total = knownSuites });
        }
        return memberships.AsReadOnly();
    }

    private async Task<AdoTestPlan> PlanAsync(string project, int planId, CultureInfo culture, CancellationToken cancellationToken)
    {
        if (!plansByProject.TryGetValue(project, out IReadOnlyList<AdoTestPlan>? listing))
            plansByProject[project] = listing = await plans.GetPlansAsync(project, culture, cancellationToken).ConfigureAwait(false);
        return listing.FirstOrDefault(plan => plan.Id == planId)
            ?? throw new AdoNotFoundException(Messages.Get(AdoMessage.TestPlanNotFound, culture, planId.ToString(CultureInfo.InvariantCulture)))
            { Operation = EndpointRegistry.TestPlansList.Name, Project = project };
    }

    private async Task<IReadOnlyList<AdoTestSuite>> TreeAsync(string project, int planId, CultureInfo culture, CancellationToken cancellationToken)
    {
        if (!trees.TryGetValue((project, planId), out IReadOnlyList<AdoTestSuite>? tree))
            trees[(project, planId)] = tree = await suites.GetSuitesAsync(project, planId, null, true, culture, cancellationToken).ConfigureAwait(false);
        return tree;
    }

    // The tree is depth-first, so a subtree is the start suite plus the following deeper entries.
    private static AdoTestSuite[] Subtree(IReadOnlyList<AdoTestSuite> tree, TestSuiteSelection selection, CultureInfo culture)
    {
        int start = -1;
        for (int i = 0; i < tree.Count; i++)
            if (tree[i].Id == selection.SuiteId) { start = i; break; }
        if (start < 0)
            throw new AdoNotFoundException(Messages.Get(AdoMessage.TestSuiteNotFound, culture,
                selection.SuiteId.ToString(CultureInfo.InvariantCulture), selection.PlanId.ToString(CultureInfo.InvariantCulture)))
            { Operation = EndpointRegistry.TestSuitesForPlan.Name, Project = selection.Project };
        if (!selection.Recurse) return [tree[start]];
        int depth = tree[start].SuitePath.Count;
        int end = start + 1;
        while (end < tree.Count && tree[end].SuitePath.Count > depth) end++;
        return tree.Skip(start).Take(end - start).ToArray();
    }
}
