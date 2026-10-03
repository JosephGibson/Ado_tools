using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.RichText;
using AdoToolkit.Core.TestRuns;
using AdoToolkit.Core.WorkItems;

namespace AdoToolkit.Core.TestManagement;

// Reads what a report shows about Test Cases beyond their steps, in three kinds of request:
// 1. the Test Cases with their relations, in batches (every field comes back, V-10);
// 2. the title, state and type of the work items they link to, in batches;
// 3. their test points, per project, for the plans, suites, configurations and latest outcomes.
// The field names and relation attributes are assumptions until V-31 passes, the points query
// until V-32. A lookup that fails becomes a warning and leaves its part out; only authentication,
// authorization and cancellation stop the export.
public sealed class TestCaseDetailService
{
    internal const int PointPageSize = 1000;
    private static readonly string[] LinkedFields = ["System.Id", "System.Title", "System.State", "System.WorkItemType", "System.TeamProject"];
    // The outcome of a point that has no result yet.
    private static readonly string[] NotRunOutcomes = ["Unspecified", "None"];
    private readonly AdoConnection connection;
    private readonly AdoHttpPipeline pipeline;

    public TestCaseDetailService(HttpClient client, AdoConnection connection, IAdoLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(connection.RequestTimeoutSeconds);
        this.connection = connection;
        pipeline = new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds), log);
    }

    public async Task<TestCaseDetailResult> GetAsync(IReadOnlyList<AdoTestCase> cases, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(culture);
        // A case in several suites is read once.
        Dictionary<int, Draft> drafts = [];
        Dictionary<int, string> planNames = [], suiteNames = [];
        foreach (AdoTestCase item in cases)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.CollectionUri != connection.CollectionUri)
                throw new AdoConnectionMismatchException(Messages.Get(AdoMessage.ConnectionMismatch, culture));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(item.Id);
            drafts.TryAdd(item.Id, new Draft(item.Id, item.TeamProject));
            if (item.Suite is { } suite)
            {
                planNames.TryAdd(suite.PlanId, suite.PlanName);
                suiteNames.TryAdd(suite.SuiteId, suite.SuiteName);
            }
        }
        List<AdoDiagnostic> problems = [];
        int[] ids = [.. drafts.Keys];
        await ReadTestCasesAsync(ids, drafts, problems, culture, cancellationToken).ConfigureAwait(false);
        await ReadLinkedWorkItemsAsync(drafts, problems, culture, cancellationToken).ConfigureAwait(false);
        foreach (IGrouping<string, Draft> project in drafts.Values.GroupBy(static draft => draft.Project, StringComparer.OrdinalIgnoreCase))
            await ReadPointsAsync(project.Key, [.. project], planNames, suiteNames, problems, culture, cancellationToken).ConfigureAwait(false);
        return new TestCaseDetailResult
        {
            Details = new ReadOnlyDictionary<int, AdoTestCaseDetail>(drafts.ToDictionary(static pair => pair.Key, static pair => pair.Value.Build())),
            Diagnostics = problems.AsReadOnly(),
        };
    }

    private async Task ReadTestCasesAsync(int[] ids, Dictionary<int, Draft> drafts, List<AdoDiagnostic> problems,
        CultureInfo culture, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkItemDto> items;
        try
        {
            items = await WorkItemBatchReader.ReadAsync(pipeline, ids, null, true, culture, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (RunHistoryService.IsRecoverable(error, cancellationToken))
        {
            AdoDiagnostic unavailable = DiagnosticMessageRenderer.Create(DiagnosticCodes.TestCaseDetailUnavailable, culture);
            problems.Add(unavailable);
            foreach (Draft draft in drafts.Values) draft.Diagnostics.Add(unavailable);
            return;
        }
        // The batch reader rejects duplicate and unrequested IDs, so the result is keyed safely.
        Dictionary<int, WorkItemDto> returned = items.ToDictionary(static item => item.Id);
        foreach (Draft draft in drafts.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!returned.TryGetValue(draft.Id, out WorkItemDto? item) || item.Fields is not { } fields)
            {
                AdoDiagnostic missing = DiagnosticMessageRenderer.Create(DiagnosticCodes.UnresolvedTestCaseDetail, culture, draft.Id,
                    [draft.Id.ToString(CultureInfo.InvariantCulture)]);
                problems.Add(missing);
                draft.Diagnostics.Add(missing);
                continue;
            }
            draft.IsResolved = true;
            // Only these fields are read, so no other field of the expanded response can fail the lookup.
            if (TestCaseLinkResolver.Text(fields, "System.Description") is { } description
                && PlainTextConverter.Convert(description, culture).Text is { Length: > 0 } plain)
            {
                draft.Description = plain;
                draft.DescriptionSource = description;
            }
            draft.Tags = [.. (TestCaseLinkResolver.Text(fields, "System.Tags") ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            draft.CreatedBy = Identity(Field(fields, "System.CreatedBy"));
            draft.CreatedDate = Date(Field(fields, "System.CreatedDate"));
            draft.AutomatedTestName = NonEmpty(TestCaseLinkResolver.Text(fields, "Microsoft.VSTS.TCM.AutomatedTestName"));
            draft.AutomatedTestStorage = NonEmpty(TestCaseLinkResolver.Text(fields, "Microsoft.VSTS.TCM.AutomatedTestStorage"));
            draft.AutomatedTestType = NonEmpty(TestCaseLinkResolver.Text(fields, "Microsoft.VSTS.TCM.AutomatedTestType"));
            HashSet<(string, int)> seen = [];
            foreach (WorkItemRelationDto relation in item.Relations ?? [])
            {
                // Relations are untrusted data: an unexpected shape is skipped.
                if (relation?.Rel is not { Length: > 0 } rel) continue;
                string? name = Attribute(relation, "name"), comment = Attribute(relation, "comment");
                if (string.Equals(rel, "AttachedFile", StringComparison.OrdinalIgnoreCase))
                {
                    if (name is not null) draft.Attachments.Add(new AdoTestCaseAttachment { Name = name, Comment = comment, Size = Size(relation) });
                }
                else if (string.Equals(rel, "Hyperlink", StringComparison.OrdinalIgnoreCase))
                {
                    if (NonEmpty(relation.Url) is { } url) draft.Hyperlinks.Add(new AdoTestCaseHyperlink { Url = url, Comment = comment });
                }
                else if (TestCaseLinkResolver.TryLinkedId(draft.Id, relation, out int linked) && seen.Add((rel, linked)))
                    draft.Links.Add((rel, name, comment, linked));
            }
        }
    }

    private async Task ReadLinkedWorkItemsAsync(Dictionary<int, Draft> drafts, List<AdoDiagnostic> problems,
        CultureInfo culture, CancellationToken cancellationToken)
    {
        int[] ids = [.. drafts.Values.SelectMany(static draft => draft.Links).Select(static link => link.Id).Distinct().Order()];
        if (ids.Length == 0) return;
        Dictionary<int, WorkItemDto>? read = null;
        AdoDiagnostic? unavailable = null;
        try
        {
            read = (await WorkItemBatchReader.ReadAsync(pipeline, ids, LinkedFields, false, culture, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static item => item.Id);
        }
        catch (Exception error) when (RunHistoryService.IsRecoverable(error, cancellationToken))
        {
            unavailable = DiagnosticMessageRenderer.Create(DiagnosticCodes.LinkedWorkItemsUnavailable, culture);
            problems.Add(unavailable);
        }
        foreach (Draft draft in drafts.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (draft.Links.Count == 0) continue;
            if (unavailable is not null) draft.Diagnostics.Add(unavailable);
            HashSet<int> reported = [];
            foreach ((string rel, string? name, string? comment, int id) in draft.Links
                .OrderBy(static link => link.Name ?? link.Rel, StringComparer.Ordinal).ThenBy(static link => link.Id))
            {
                WorkItemDto? item = read?.GetValueOrDefault(id);
                Dictionary<string, JsonElement>? fields = item?.Fields;
                string? project = fields is null ? null : TestCaseLinkResolver.Text(fields, "System.TeamProject");
                if (!RequestBuilder.IsPathSegment(project)) project = null;
                if (read is not null && fields is null && reported.Add(id))
                {
                    AdoDiagnostic missing = DiagnosticMessageRenderer.Create(DiagnosticCodes.UnresolvedLinkedWorkItem, culture, draft.Id,
                        [id.ToString(CultureInfo.InvariantCulture)]);
                    problems.Add(missing);
                    draft.Diagnostics.Add(missing);
                }
                draft.Resolved.Add(new AdoLinkedWorkItem
                {
                    Id = id, LinkType = rel, LinkName = name, Comment = comment, IsResolved = fields is not null,
                    Title = fields is null ? null : TestCaseLinkResolver.Text(fields, "System.Title"),
                    State = fields is null ? null : TestCaseLinkResolver.Text(fields, "System.State"),
                    WorkItemType = fields is null ? null : TestCaseLinkResolver.Text(fields, "System.WorkItemType"),
                    TeamProject = project,
                    // A work item that could not be read is linked through the project of its Test Case.
                    WebUrl = AdoWebLinks.WorkItem(connection.CollectionUri, project ?? draft.Project, id),
                });
            }
        }
    }

    private async Task ReadPointsAsync(string project, Draft[] drafts, Dictionary<int, string> planNames, Dictionary<int, string> suiteNames,
        List<AdoDiagnostic> problems, CultureInfo culture, CancellationToken cancellationToken)
    {
        Dictionary<int, List<AdoTestPoint>> points = drafts.ToDictionary(static draft => draft.Id, static _ => new List<AdoTestPoint>());
        try
        {
            // A name that cannot be a route segment is refused before any request.
            if (!RequestBuilder.IsPathSegment(project)) throw new AdoRequestException(Messages.Get(AdoMessage.InvalidProjectName, culture, project));
            foreach (int[] chunk in points.Keys.Chunk(EndpointRegistry.TestPointsQuery.ChunkSize))
                foreach (TestPointDto point in await QueryPointsAsync(project, chunk, culture, cancellationToken).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    (int? urlPlan, int? urlSuite) = PlanAndSuite(point.Url);
                    // A point that names no requested Test Case, plan or suite cannot be placed: the shape is not the expected one.
                    if (point.Id < 1 || !TestCaseLinkResolver.TryParseReference(point.TestCase?.Id, out int testCase)
                        || !points.TryGetValue(testCase, out List<AdoTestPoint>? list)
                        || (Reference(point.TestPlan) ?? urlPlan) is not int plan || (Reference(point.Suite) ?? urlSuite) is not int suite)
                        throw FormatError(culture);
                    string? outcome = NonEmpty(point.Outcome);
                    bool hasRun = outcome is not null && !NotRunOutcomes.Contains(outcome, StringComparer.OrdinalIgnoreCase);
                    int? run = Reference(point.LastTestRun);
                    list.Add(new AdoTestPoint
                    {
                        Id = point.Id, PlanId = plan, SuiteId = suite, TeamProject = project,
                        PlanName = NonEmpty(point.TestPlan?.Name) ?? planNames.GetValueOrDefault(plan),
                        SuiteName = NonEmpty(point.Suite?.Name) ?? suiteNames.GetValueOrDefault(suite),
                        ConfigurationName = NonEmpty(point.Configuration?.Name),
                        Outcome = hasRun ? outcome : null, HasRun = hasRun,
                        OutcomeClass = hasRun ? OutcomeClassifier.Classify(outcome) : AdoTestOutcomeClass.Other,
                        State = NonEmpty(point.State), Tester = Identity(point.AssignedTo), LastUpdated = Date(point.LastUpdatedDate),
                        LastRunId = run, LastResultId = Reference(point.LastResult),
                        PlanWebUrl = AdoWebLinks.TestPlan(connection.CollectionUri, project, plan),
                        SuiteWebUrl = AdoWebLinks.TestPlan(connection.CollectionUri, project, plan, suite),
                        LastRunWebUrl = run is int runId ? AdoWebLinks.TestRun(connection.CollectionUri, project, runId) : null,
                    });
                }
        }
        catch (Exception error) when (RunHistoryService.IsRecoverable(error, cancellationToken))
        {
            AdoDiagnostic unavailable = DiagnosticMessageRenderer.Create(DiagnosticCodes.TestPointsUnavailable, culture, arguments: [project]);
            problems.Add(unavailable);
            foreach (Draft draft in drafts) draft.Diagnostics.Add(unavailable);
            return;
        }
        foreach (Draft draft in drafts)
            draft.Points = [.. points[draft.Id].OrderBy(static point => point.PlanId).ThenBy(static point => point.SuiteId)
                .ThenBy(static point => point.ConfigurationName, StringComparer.Ordinal).ThenBy(static point => point.Id)];
    }

    // One query per chunk of Test Cases, paged with $top and $skip. A short page is the last one,
    // and so is a page that brings no new point, which is what a server that ignores $skip returns.
    private async Task<List<TestPointDto>> QueryPointsAsync(string project, int[] testCases, CultureInfo culture, CancellationToken cancellationToken)
    {
        EndpointDefinition endpoint = EndpointRegistry.TestPointsQuery;
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new TestPointsQueryDto { PointsFilter = new TestPointsFilterDto { TestcaseIds = testCases } },
            AdoJsonContext.Default.TestPointsQueryDto);
        List<TestPointDto> result = [];
        HashSet<(string?, string?, string?, int)> seen = [];
        for (int page = 1, skip = 0; page <= AdoHttpPipeline.MaximumPages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<TestPointDto> points = await pipeline.ExecuteAsync(endpoint, new Dictionary<string, string> { ["project"] = project },
                new Dictionary<string, string>
                {
                    ["$top"] = PointPageSize.ToString(CultureInfo.InvariantCulture), ["$skip"] = skip.ToString(CultureInfo.InvariantCulture),
                }, body, culture, async (response, token) =>
                {
                    try
                    {
                        return (await ResponseJson.DeserializeAsync(response, AdoJsonContext.Default.TestPointsPageDto, endpoint, culture, token)
                            .ConfigureAwait(false))?.Points ?? throw new JsonException();
                    }
                    catch (JsonException error) { throw FormatError(culture, error); }
                }, cancellationToken).ConfigureAwait(false);
            int added = 0;
            foreach (TestPointDto point in points)
            {
                if (point is null) throw FormatError(culture);
                if (!seen.Add((point.TestPlan?.Id, point.Suite?.Id, point.Url, point.Id))) continue;
                result.Add(point);
                added++;
            }
            if (points.Count < PointPageSize || added == 0) return result;
            skip = checked(skip + points.Count);
        }
        throw new AdoResponseFormatException(Messages.Get(AdoMessage.PagingGuard, culture)) { Operation = endpoint.Name };
    }

    private static AdoResponseFormatException FormatError(CultureInfo culture, Exception? error = null) =>
        new(Messages.Get(AdoMessage.ResponseFormat, culture), error) { Operation = EndpointRegistry.TestPointsQuery.Name };

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static int? Reference(TestPointReferenceDto? reference) =>
        TestCaseLinkResolver.TryParseReference(reference?.Id, out int id) ? id : null;

    // The IDs in .../Plans/<plan>/Suites/<suite>/Points/<point>, read from the path and never requested.
    private static (int? Plan, int? Suite) PlanAndSuite(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)) return (null, null);
        string[] segments = parsed.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        int? Following(string name)
        {
            int index = Array.FindLastIndex(segments, segment => string.Equals(segment, name, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < segments.Length && TestCaseLinkResolver.TryParseReference(segments[index + 1], out int id) ? id : null;
        }
        return (Following("Plans"), Following("Suites"));
    }

    private static JsonElement Field(Dictionary<string, JsonElement> fields, string name)
    {
        foreach ((string key, JsonElement value) in fields)
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) return value;
        return default;
    }

    private static string? Attribute(WorkItemRelationDto relation, string name) =>
        relation.Attributes is { } attributes && attributes.TryGetValue(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? NonEmpty(value.GetString()) : null;

    private static long? Size(WorkItemRelationDto relation) =>
        relation.Attributes is { } attributes && attributes.TryGetValue("resourceSize", out JsonElement value)
            && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long size) && size >= 0 ? size : null;

    // An identity object, or a display name sent as text.
    private static AdoIdentityRef? Identity(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return NonEmpty(value.GetString()) is { } name ? new AdoIdentityRef { DisplayName = name } : null;
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("displayName", out JsonElement display)
            || display.ValueKind != JsonValueKind.String || NonEmpty(display.GetString()) is not { } displayName) return null;
        string? Text(string property) => value.TryGetProperty(property, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? NonEmpty(element.GetString()) : null;
        return new AdoIdentityRef { DisplayName = displayName, Id = Text("id"), UniqueName = Text("uniqueName") };
    }

    private static DateTimeOffset? Date(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out DateTimeOffset date) ? date.ToUniversalTime() : null;

    private sealed class Draft(int id, string project)
    {
        internal int Id { get; } = id;
        internal string Project { get; } = project;
        internal bool IsResolved { get; set; }
        internal string? Description { get; set; }
        internal string? DescriptionSource { get; set; }
        internal string[] Tags { get; set; } = [];
        internal AdoIdentityRef? CreatedBy { get; set; }
        internal DateTimeOffset? CreatedDate { get; set; }
        internal string? AutomatedTestName { get; set; }
        internal string? AutomatedTestStorage { get; set; }
        internal string? AutomatedTestType { get; set; }
        internal List<(string Rel, string? Name, string? Comment, int Id)> Links { get; } = [];
        internal List<AdoLinkedWorkItem> Resolved { get; } = [];
        internal List<AdoTestCaseHyperlink> Hyperlinks { get; } = [];
        internal List<AdoTestCaseAttachment> Attachments { get; } = [];
        internal AdoTestPoint[]? Points { get; set; }
        internal List<AdoDiagnostic> Diagnostics { get; } = [];

        internal AdoTestCaseDetail Build() => new()
        {
            Id = Id, IsResolved = IsResolved, Description = Description, DescriptionSource = DescriptionSource, Tags = Array.AsReadOnly(Tags),
            CreatedBy = CreatedBy, CreatedDate = CreatedDate, AutomatedTestName = AutomatedTestName,
            AutomatedTestStorage = AutomatedTestStorage, AutomatedTestType = AutomatedTestType,
            Links = Resolved.AsReadOnly(), Hyperlinks = Hyperlinks.AsReadOnly(), Attachments = Attachments.AsReadOnly(),
            Points = Points is null ? null : Array.AsReadOnly(Points), Diagnostics = Diagnostics.AsReadOnly(),
        };
    }
}
