using System.Net.Http;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.TestManagement;

public sealed class TestSuiteService
{
    private readonly AdoConnection connection;
    private readonly AdoHttpPipeline pipeline;

    public TestSuiteService(HttpClient client, AdoConnection connection, IAdoLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(connection.RequestTimeoutSeconds);
        this.connection = connection;
        pipeline = new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log);
    }

    /// <summary>
    /// Returns the start suite (the given suite, or the plan's root suites) and, with
    /// <paramref name="recurse"/>, each subtree depth-first. Sibling order is the server
    /// response order, which is provisional pending V-04.
    /// </summary>
    public async Task<IReadOnlyList<AdoTestSuite>> GetSuitesAsync(string project, int planId, int? suiteId, bool recurse,
        CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(planId);
        if (suiteId.HasValue) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(suiteId.Value);
        ArgumentNullException.ThrowIfNull(culture);
        EndpointDefinition endpoint = EndpointRegistry.TestSuitesForPlan;
        IReadOnlyList<TestSuiteDto> values = await pipeline.GetPagesAsync(endpoint, AdoJsonContext.Default.TestSuitePageDto,
            static page => page.Value, static item => item.Id.ToString(CultureInfo.InvariantCulture), culture, cancellationToken,
            new Dictionary<string, string> { ["project"] = project, ["planId"] = planId.ToString(CultureInfo.InvariantCulture) }).ConfigureAwait(false);
        List<AdoTestSuite> ordered = BuildTree(values, project, planId, culture, cancellationToken);
        int start = 0;
        int end = ordered.Count;
        if (suiteId.HasValue)
        {
            start = -1;
            for (int i = 0; i < ordered.Count; i++)
                if (ordered[i].Id == suiteId.Value) { start = i; break; }
            if (start < 0)
                throw new AdoNotFoundException(Messages.Get(AdoMessage.TestSuiteNotFound, culture,
                    suiteId.Value.ToString(CultureInfo.InvariantCulture), planId.ToString(CultureInfo.InvariantCulture)))
                { Operation = endpoint.Name, Project = project };
            int depth = ordered[start].SuitePath.Count;
            end = start + 1;
            while (end < ordered.Count && ordered[end].SuitePath.Count > depth) end++;
        }
        List<AdoTestSuite> result = [];
        for (int i = start; i < end; i++)
            if (recurse || i == start || (!suiteId.HasValue && ordered[i].ParentSuiteId is null)) result.Add(ordered[i]);
        return result.AsReadOnly();
    }

    private List<AdoTestSuite> BuildTree(IReadOnlyList<TestSuiteDto> values, string project, int planId,
        CultureInfo culture, CancellationToken cancellationToken)
    {
        Dictionary<int, TestSuiteDto> byId = [];
        Dictionary<int, List<TestSuiteDto>> children = [];
        List<TestSuiteDto> roots = [];
        foreach (TestSuiteDto value in values)
        {
            if (value.Id < 1 || string.IsNullOrEmpty(value.Name) || (value.ParentSuite is not null && value.ParentSuite.Id < 1) || !byId.TryAdd(value.Id, value))
                throw FormatError(culture);
        }
        foreach (TestSuiteDto value in values)
        {
            if (value.ParentSuite is null) { roots.Add(value); continue; }
            if (!byId.ContainsKey(value.ParentSuite.Id)) throw FormatError(culture);
            if (!children.TryGetValue(value.ParentSuite.Id, out List<TestSuiteDto>? list)) children[value.ParentSuite.Id] = list = [];
            list.Add(value);
        }
        List<AdoTestSuite> ordered = [];
        Stack<(TestSuiteDto Suite, IReadOnlyList<string> ParentPath)> pending = new();
        for (int i = roots.Count - 1; i >= 0; i--) pending.Push((roots[i], Array.Empty<string>()));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (TestSuiteDto suite, IReadOnlyList<string> parentPath) = pending.Pop();
            string[] path = [.. parentPath, suite.Name!];
            ordered.Add(new AdoTestSuite
            {
                Id = suite.Id, PlanId = planId, ParentSuiteId = suite.ParentSuite?.Id, Name = suite.Name!,
                SuitePath = Array.AsReadOnly(path), TeamProject = project, CollectionUri = connection.CollectionUri,
                WebUrl = AdoWebLinks.TestPlan(connection.CollectionUri, project, planId, suite.Id),
            });
            if (children.TryGetValue(suite.Id, out List<TestSuiteDto>? list))
                for (int i = list.Count - 1; i >= 0; i--) pending.Push((list[i], path));
        }
        // Suites unreachable from a root form a parent cycle.
        if (ordered.Count != values.Count) throw FormatError(culture);
        return ordered;
    }

    private static AdoResponseFormatException FormatError(CultureInfo culture) =>
        new(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = "TestSuitesForPlan" };
}
