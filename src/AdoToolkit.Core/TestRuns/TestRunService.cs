using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.TestRuns;

public sealed class TestRunService
{
    // Largest documented page for detailsToInclude=None [Verify V-20]. A short page never ends
    // enumeration: TopSkip advances by the number actually returned and stops on an empty page.
    internal const int ResultPageSize = 1000;
    internal const int RunPageSize = 100;
    private readonly AdoConnection connection;
    private readonly AdoHttpPipeline pipeline;

    public TestRunService(HttpClient client, AdoConnection connection, IAdoLog? log = null)
        : this(client, connection, log, null) { }

    internal TestRunService(HttpClient client, AdoConnection connection, IAdoLog? log, RequestCounter? counter)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(connection.RequestTimeoutSeconds);
        this.connection = connection;
        pipeline = new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds),
            log, counter: counter);
    }

    // vstfs:///Build/Build/<id> is composed when the build carries no uri of its own [Verify V-25].
    public static Uri BuildUri(AdoBuild build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return build.Uri ?? new Uri("vstfs:///Build/Build/" + build.Id.ToString(CultureInfo.InvariantCulture));
    }

    public Task<IReadOnlyList<AdoTestRun>> GetRunsAsync(AdoBuild build, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        return GetRunsAsync(build.TeamProject, build.Id, BuildUri(build), culture, cancellationToken);
    }

    internal async Task<IReadOnlyList<AdoTestRun>> GetRunsAsync(string project, int buildId, Uri buildUri,
        CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(buildId);
        ArgumentNullException.ThrowIfNull(buildUri);
        ArgumentNullException.ThrowIfNull(culture);
        IReadOnlyList<TestRunDto> values = await pipeline.GetPagesAsync(EndpointRegistry.TestRunsList,
            AdoJsonContext.Default.TestRunPageDto, static page => page.Value,
            static item => item.Id.ToString(CultureInfo.InvariantCulture), culture, cancellationToken,
            new Dictionary<string, string> { ["project"] = project }, null, RunPageSize,
            new Dictionary<string, string>
            {
                ["buildUri"] = buildUri.AbsoluteUri,
                ["includeRunDetails"] = "true",
            }).ConfigureAwait(false);
        List<AdoTestRun> result = [];
        HashSet<int> seen = [];
        foreach (TestRunDto value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.Id < 1 || !seen.Add(value.Id)) throw FormatError(culture, "TestRunsList");
            Dictionary<string, int> counts = new(StringComparer.Ordinal);
            foreach (RunStatisticDto statistic in value.RunStatistics ?? [])
            {
                if (statistic is null || string.IsNullOrEmpty(statistic.Outcome) || statistic.Count < 0)
                    throw FormatError(culture, "TestRunsList");
                counts[statistic.Outcome] = counts.TryGetValue(statistic.Outcome, out int existing)
                    ? checked(existing + statistic.Count) : statistic.Count;
            }
            result.Add(new AdoTestRun
            {
                Id = value.Id,
                Name = value.Name ?? "",
                BuildId = value.Build is not null && value.Build.Id > 0 ? value.Build.Id : buildId,
                State = value.State ?? "",
                IsAutomated = value.IsAutomated,
                StartedDate = value.StartedDate?.ToUniversalTime(),
                CompletedDate = value.CompletedDate?.ToUniversalTime(),
                PipelineAttempt = value.PipelineReference?.PipelineAttempt,
                TotalTests = value.TotalTests,
                OutcomeCounts = new ReadOnlyDictionary<string, int>(counts),
                WebUrl = AdoWebLinks.TestRun(connection.CollectionUri, project, value.Id),
                TeamProject = project,
                CollectionUri = connection.CollectionUri,
            });
        }
        // Runs are emitted in attempt order (§15.10), not response order.
        return AttemptGrouper.OrderRuns(result);
    }

    // Pass 1: lightweight fields only. The server outcomes filter is never used (§15.9 step 3).
    internal async Task<IReadOnlyList<TestResultDto>> GetResultsAsync(string project, int runId,
        CultureInfo culture, CancellationToken cancellationToken)
    {
        IReadOnlyList<TestResultDto> values = await pipeline.GetPagesAsync(EndpointRegistry.TestResultsList,
            AdoJsonContext.Default.TestResultPageDto, static page => page.Value,
            static item => item.Id.ToString(CultureInfo.InvariantCulture), culture, cancellationToken,
            new Dictionary<string, string>
            {
                ["project"] = project,
                ["runId"] = runId.ToString(CultureInfo.InvariantCulture),
            }, null, ResultPageSize,
            new Dictionary<string, string> { ["detailsToInclude"] = "None" }).ConfigureAwait(false);
        foreach (TestResultDto value in values)
            if (value.Id < 1) throw FormatError(culture, "TestResultsList");
        return values;
    }

    // Pass 2: one request per result record; rerun sub-results arrive with their parent.
    internal Task<TestResultDto> GetResultAsync(string project, int runId, int resultId,
        CultureInfo culture, CancellationToken cancellationToken) =>
        pipeline.ExecuteAsync(EndpointRegistry.TestResultGet, new Dictionary<string, string>
        {
            ["project"] = project,
            ["runId"] = runId.ToString(CultureInfo.InvariantCulture),
            ["resultId"] = resultId.ToString(CultureInfo.InvariantCulture),
        }, new Dictionary<string, string> { ["detailsToInclude"] = "Iterations,WorkItems,SubResults" }, null, culture,
            async (response, token) =>
            {
                try
                {
                    byte[] body = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
                    TestResultDto result = JsonSerializer.Deserialize(body, AdoJsonContext.Default.TestResultDto)
                        ?? throw new JsonException();
                    return result.Id == resultId ? result : throw new JsonException();
                }
                catch (JsonException error)
                {
                    throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture), error)
                    { Operation = "TestResultGet" };
                }
            }, cancellationToken);

    internal async Task<IReadOnlyList<AdoTestAttachment>> GetAttachmentsAsync(string project, int runId, int resultId,
        int? subResultId, CultureInfo culture, CancellationToken cancellationToken)
    {
        EndpointDefinition endpoint = subResultId.HasValue
            ? EndpointRegistry.TestSubResultAttachmentsList : EndpointRegistry.TestResultAttachmentsList;
        Dictionary<string, string> parameters = [];
        if (subResultId.HasValue) parameters["testSubResultId"] = subResultId.Value.ToString(CultureInfo.InvariantCulture);
        IReadOnlyList<TestAttachmentDto> values = await pipeline.GetPagesAsync(endpoint,
            AdoJsonContext.Default.TestAttachmentPageDto, static page => page.Value,
            static item => item.Id.ToString(CultureInfo.InvariantCulture), culture, cancellationToken,
            new Dictionary<string, string>
            {
                ["project"] = project,
                ["runId"] = runId.ToString(CultureInfo.InvariantCulture),
                ["resultId"] = resultId.ToString(CultureInfo.InvariantCulture),
            }, null, RunPageSize, parameters).ConfigureAwait(false);
        List<AdoTestAttachment> result = [];
        foreach (TestAttachmentDto value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.Id < 1 || value.Size < 0) throw FormatError(culture, endpoint.Name);
            result.Add(new AdoTestAttachment
            {
                Id = value.Id,
                RunId = runId,
                ResultId = resultId,
                SubResultId = subResultId,
                FileName = value.FileName ?? "",
                Comment = value.Comment,
                Size = value.Size,
                AttachmentType = value.AttachmentType,
                Kind = AttachmentKinds.FromFileName(value.FileName),
                DownloadStatus = AdoTestAttachmentStatus.NotRequested,
            });
        }
        return result.AsReadOnly();
    }

    private static AdoResponseFormatException FormatError(CultureInfo culture, string operation) =>
        new(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = operation };
}
