using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit.Core.TestRuns;

// Reads the bugs of the reported tests in batches: every work item associated with one of a test's
// results, and every work item linked to its Test Case whose type is in the Bug category of its
// project. A bug is open unless its state is in the Completed or Removed category of its project
// and type. Lookup problems become warnings; only authentication, authorization and cancellation
// fail the retrieval.
internal sealed class TestBugResolver
{
    private const string BugCategory = "Microsoft.BugCategory";
    private static readonly string[] Fields = ["System.Id", "System.Title", "System.State", "System.WorkItemType", "System.TeamProject"];
    // Used only when a project's metadata cannot be read: the Bug type name, and the Completed and
    // Removed states of the Bug types of the Agile, Scrum and CMMI processes.
    private static readonly string[] DefaultBugTypes = ["Bug"];
    private static readonly string[] DefaultClosedStates = ["Closed", "Done", "Removed"];
    private readonly AdoConnection connection;
    private readonly AdoHttpPipeline pipeline;
    // Null marks metadata that could not be read. The server compares project and type names without case.
    private readonly Dictionary<string, IReadOnlyList<string>?> bugTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, IReadOnlyDictionary<string, string>?>> states = new(StringComparer.OrdinalIgnoreCase);

    internal TestBugResolver(HttpClient client, AdoConnection connection, IAdoLog? log, RequestCounter? counter)
    {
        this.connection = connection;
        pipeline = new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds),
            log, counter: counter);
    }

    // Returns one list per test, in the order given, each ordered by ID.
    internal async Task<IReadOnlyList<IReadOnlyList<AdoTestBug>>> ResolveAsync(IReadOnlyList<TestBugReferences> tests, int buildId,
        string project, CultureInfo culture, List<AdoDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        int[] candidates = tests.SelectMany(static test => test.Associated.Concat(test.Linked)).Distinct().Order().ToArray();
        if (candidates.Length == 0) return Compose(tests, new Dictionary<int, BugData>(), new HashSet<int>());
        Dictionary<int, WorkItemDto> read;
        try
        {
            read = await ReadAsync(candidates, culture, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (RunHistoryService.IsRecoverable(error, cancellationToken))
        {
            // Associated IDs stay as links; linked work items cannot be told apart from bugs without their type.
            diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.BugLookupFailed, culture,
                arguments: [buildId.ToString(CultureInfo.InvariantCulture)]));
            return Compose(tests, tests.SelectMany(static test => test.Associated).Distinct()
                .ToDictionary(id => id, id => BugData.Unresolved(id, project)), new HashSet<int>());
        }
        HashSet<string> warned = new(StringComparer.OrdinalIgnoreCase);
        void Degraded(string name)
        {
            if (warned.Add(name))
                diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.BugMetadataUnavailable, culture, arguments: [name]));
        }
        HashSet<int> associated = [.. tests.SelectMany(static test => test.Associated)];
        HashSet<int> linked = [.. tests.SelectMany(static test => test.Linked)];
        HashSet<int> linkedBugs = [];
        foreach (int id in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!linked.Contains(id) || !read.TryGetValue(id, out WorkItemDto? item)) continue;
            string owner = Project(item) ?? project;
            IReadOnlyList<string>? types = await BugTypesAsync(owner, culture, cancellationToken).ConfigureAwait(false);
            if (types is null) Degraded(owner);
            if (Text(item, "System.WorkItemType") is { } type && (types ?? DefaultBugTypes).Contains(type, StringComparer.OrdinalIgnoreCase))
                linkedBugs.Add(id);
        }
        Dictionary<int, BugData> bugs = [];
        foreach (int id in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!associated.Contains(id) && !linkedBugs.Contains(id)) continue;
            if (!read.TryGetValue(id, out WorkItemDto? item))
            {
                diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.UnresolvedBug, culture, id,
                    [id.ToString(CultureInfo.InvariantCulture)]));
                bugs[id] = BugData.Unresolved(id, project);
                continue;
            }
            string? reported = Project(item);
            string owner = reported ?? project;
            string? state = Text(item, "System.State"), type = Text(item, "System.WorkItemType"), category = null;
            // A type name is a route segment too; one that cannot be requested keeps the default states.
            if (state is not null && type is not null)
            {
                IReadOnlyDictionary<string, string>? known = RequestBuilder.IsPathSegment(type)
                    ? await StatesAsync(owner, type, culture, cancellationToken).ConfigureAwait(false) : null;
                if (known is null || !known.TryGetValue(state, out category)) Degraded(owner);
            }
            bugs[id] = new BugData(id, Text(item, "System.Title"), state, type, owner, reported, category, IsOpen(state, category), true);
        }
        return Compose(tests, bugs, linkedBugs);
    }

    // Open unless the category is Completed or Removed; without a category, unless the state has a default closed name.
    private static bool? IsOpen(string? state, string? category) => state is null ? null
        : category is not null
            ? !string.Equals(category, "Completed", StringComparison.OrdinalIgnoreCase) && !string.Equals(category, "Removed", StringComparison.OrdinalIgnoreCase)
            : !DefaultClosedStates.Contains(state, StringComparer.OrdinalIgnoreCase);

    private ReadOnlyCollection<IReadOnlyList<AdoTestBug>> Compose(IReadOnlyList<TestBugReferences> tests, IReadOnlyDictionary<int, BugData> bugs,
        IReadOnlySet<int> linkedBugs)
    {
        List<IReadOnlyList<AdoTestBug>> result = [];
        foreach (TestBugReferences test in tests)
        {
            SortedSet<int> ids = [.. test.Associated, .. test.Linked.Where(linkedBugs.Contains)];
            result.Add(Array.AsReadOnly(ids.Where(bugs.ContainsKey).Select(id => bugs[id]).Select(bug => new AdoTestBug
            {
                Id = bug.Id, Title = bug.Title, State = bug.State, WorkItemType = bug.Type, TeamProject = bug.TeamProject,
                StateCategory = bug.Category, IsOpen = bug.IsOpen, IsResolved = bug.IsResolved,
                IsAssociatedWithResult = test.Associated.Contains(bug.Id), IsLinkedToTestCase = test.Linked.Contains(bug.Id),
                WebUrl = AdoWebLinks.WorkItem(connection.CollectionUri, bug.Project, bug.Id),
            }).ToArray()));
        }
        return result.AsReadOnly();
    }

    private async Task<Dictionary<int, WorkItemDto>> ReadAsync(int[] ids, CultureInfo culture, CancellationToken cancellationToken)
    {
        EndpointDefinition endpoint = EndpointRegistry.WorkItemsBatch;
        // IdChunks rejects duplicate and unrequested IDs, so the result is keyed safely.
        IReadOnlyList<WorkItemDto> returned = await IdChunks.FetchAsync(ids, endpoint.ChunkSize,
            async (chunk, token) =>
            {
                byte[] body = JsonSerializer.SerializeToUtf8Bytes(new WorkItemBatchRequestDto { Ids = chunk, Fields = Fields },
                    AdoJsonContext.Default.WorkItemBatchRequestDto);
                return await pipeline.ExecuteAsync(endpoint, null, null, body, culture, async (response, requestToken) =>
                {
                    try
                    {
                        string bytes = await ResponseJson.ReadAsync(response, requestToken).ConfigureAwait(false);
                        return (IReadOnlyList<WorkItemDto>)(JsonSerializer.Deserialize(bytes, AdoJsonContext.Default.WorkItemBatchDto)?.Value
                            ?? throw new JsonException());
                    }
                    catch (JsonException error)
                    {
                        throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture), error) { Operation = endpoint.Name };
                    }
                }, token).ConfigureAwait(false);
            }, static item => item.Id, culture, cancellationToken).ConfigureAwait(false);
        return returned.ToDictionary(static item => item.Id);
    }

    private async Task<IReadOnlyList<string>?> BugTypesAsync(string project, CultureInfo culture, CancellationToken cancellationToken)
    {
        if (bugTypes.TryGetValue(project, out IReadOnlyList<string>? cached)) return cached;
        IReadOnlyList<string>? types;
        try
        {
            types = await WorkItemTypeCategories.ReadAsync(pipeline, project, BugCategory, culture, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (RunHistoryService.IsRecoverable(error, cancellationToken))
        {
            types = null;
        }
        bugTypes[project] = types;
        return types;
    }

    // State name to state category for one work item type of one project.
    private async Task<IReadOnlyDictionary<string, string>?> StatesAsync(string project, string type, CultureInfo culture,
        CancellationToken cancellationToken)
    {
        if (!states.TryGetValue(project, out Dictionary<string, IReadOnlyDictionary<string, string>?>? byType))
            states[project] = byType = new(StringComparer.OrdinalIgnoreCase);
        if (byType.TryGetValue(type, out IReadOnlyDictionary<string, string>? cached)) return cached;
        IReadOnlyDictionary<string, string>? result;
        try
        {
            result = await pipeline.ExecuteAsync(EndpointRegistry.WorkItemTypeStates,
                new Dictionary<string, string> { ["project"] = project, ["type"] = type }, null, null, culture,
                async (response, token) =>
                {
                    try
                    {
                        string text = await ResponseJson.ReadAsync(response, token).ConfigureAwait(false);
                        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
                        foreach (WorkItemTypeStateDto value in JsonSerializer.Deserialize(text, AdoJsonContext.Default.WorkItemTypeStatePageDto)?.Value
                            ?? throw new JsonException())
                            if (value?.Name is { Length: > 0 } name && value.Category is { Length: > 0 } category) map.TryAdd(name, category);
                        return (IReadOnlyDictionary<string, string>)map;
                    }
                    catch (JsonException error)
                    {
                        throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture), error)
                        { Operation = EndpointRegistry.WorkItemTypeStates.Name };
                    }
                }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (RunHistoryService.IsRecoverable(error, cancellationToken))
        {
            result = null;
        }
        byType[type] = result;
        return result;
    }

    private static string? Text(WorkItemDto item, string name) => item.Fields is null ? null : TestCaseLinkResolver.Text(item.Fields, name);

    // The project a work item reports, when it can name a request route or link; otherwise the
    // caller falls back to the build's project.
    private static string? Project(WorkItemDto item) =>
        Text(item, "System.TeamProject") is { } value && RequestBuilder.IsPathSegment(value) ? value : null;

    // Project is used for requests and links; TeamProject is what a resolved bug reported, if usable.
    private sealed record BugData(int Id, string? Title, string? State, string? Type, string Project, string? TeamProject, string? Category,
        bool? IsOpen, bool IsResolved)
    {
        internal static BugData Unresolved(int id, string project) => new(id, null, null, null, project, null, null, null, false);
    }
}
