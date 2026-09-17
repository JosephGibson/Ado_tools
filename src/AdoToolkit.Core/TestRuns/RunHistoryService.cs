using System.Net.Http;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.TestRuns;

internal sealed record HistoryEntry(HistoryBuild Build, bool IsCurrent, HistoryBuildData Data);

// The current build plus up to HistoryCount - 1 earlier builds of the same definition (§15.12).
// History never changes Status and never fails the report.
internal sealed class RunHistoryService
{
    private readonly AdoHttpPipeline pipeline;
    private readonly TestRunService runs;
    private readonly TestFailureInvocationCache cache;
    private readonly RequestCounter counter;
    private readonly IAdoLog progress;

    internal RunHistoryService(HttpClient client, AdoConnection connection, TestFailureInvocationCache cache,
        IAdoLog? log, RequestCounter counter)
    {
        this.cache = cache;
        this.counter = counter;
        progress = log ?? new NullAdoLog();
        pipeline = new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds),
            log, counter: counter);
        runs = new TestRunService(client, connection, log, counter);
    }

    internal async Task<IReadOnlyList<HistoryEntry>> GetHistoryAsync(AdoBuild current, HistoryBuildData currentData,
        int historyCount, AdoTestHistoryScope scope, int maximumRequests, CultureInfo culture,
        List<AdoDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        HistoryEntry currentEntry = new(new HistoryBuild
        {
            Id = current.Id,
            BuildNumber = current.BuildNumber,
            SourceBranch = current.SourceBranch,
            FinishTime = current.FinishTime,
            Result = current.Result,
        }, true, currentData);
        if (historyCount <= 1) return Array.AsReadOnly(new[] { currentEntry });
        int budgetStart = counter.Count;
        IReadOnlyList<HistoryBuild> window;
        try
        {
            window = await GetWindowAsync(current, historyCount, scope, culture, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (IsRecoverable(error, cancellationToken))
        {
            // Without the window there are no earlier builds to report; the current build stands alone.
            diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.HistoryUnavailable, culture,
                arguments: [current.Id.ToString(CultureInfo.InvariantCulture)]));
            return Array.AsReadOnly(new[] { currentEntry });
        }
        // Newest first, so the request budget is spent on the most recent builds and the
        // remaining older ones become Unavailable.
        List<HistoryEntry> earlier = [];
        bool limitReported = false;
        int completed = 0;
        foreach (HistoryBuild build in window)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Progress(new AdoProgress
            {
                Phase = AdoProgressPhase.History,
                Completed = ++completed,
                Total = window.Count + 1,
            });
            if (cache.TryGetBuild(build.Id, out HistoryBuildData cached))
            {
                earlier.Add(new HistoryEntry(build, false, cached));
                continue;
            }
            if (counter.Count - budgetStart >= maximumRequests)
            {
                if (!limitReported)
                {
                    diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.HistoryLimitExceeded, culture,
                        arguments: [maximumRequests.ToString(CultureInfo.InvariantCulture)]));
                    limitReported = true;
                }
                earlier.Add(new HistoryEntry(build, false, HistoryBuildData.Unavailable));
                continue;
            }
            HistoryBuildData data;
            try
            {
                data = await ReadBuildAsync(build, current.TeamProject, culture, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (IsRecoverable(error, cancellationToken))
            {
                diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.HistoryUnavailable, culture,
                    arguments: [build.Id.ToString(CultureInfo.InvariantCulture)]));
                data = HistoryBuildData.Unavailable;
            }
            cache.AddBuild(build.Id, data);
            earlier.Add(new HistoryEntry(build, false, data));
        }
        earlier.Reverse();
        earlier.Add(currentEntry);
        return earlier.AsReadOnly();
    }

    // Authentication, authorization and cancellation still fail the whole retrieval (§15.12).
    private static bool IsRecoverable(Exception error, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested
        && error is AdoException and not AdoAuthenticationException and not AdoAuthorizationException;

    private async Task<IReadOnlyList<HistoryBuild>> GetWindowAsync(AdoBuild current, int historyCount,
        AdoTestHistoryScope scope, CultureInfo culture, CancellationToken cancellationToken)
    {
        Dictionary<string, string> parameters = new(StringComparer.Ordinal)
        {
            ["definitions"] = current.Definition.Id.ToString(CultureInfo.InvariantCulture),
            ["statusFilter"] = "completed",
            ["queryOrder"] = "finishTimeDescending",
        };
        DateTimeOffset? maxTime = current.FinishTime ?? current.QueueTime;
        if (maxTime.HasValue)
            parameters["maxTime"] = maxTime.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        if (scope == AdoTestHistoryScope.SameBranch && !string.IsNullOrEmpty(current.SourceBranch))
            parameters["branchName"] = current.SourceBranch;
        // Cached windows exclude the current build. Equal finish/queue times can occur across
        // piped builds, so the current ID is part of that derived window's identity too.
        string key = string.Join('|', [current.TeamProject, current.Id.ToString(CultureInfo.InvariantCulture),
            .. parameters.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => pair.Key + "=" + pair.Value), historyCount.ToString(CultureInfo.InvariantCulture)]);
        if (cache.TryGetWindow(key, out IReadOnlyList<HistoryBuild> cached)) return cached;
        IReadOnlyList<BuildDto> values = await pipeline.GetPagesAsync(EndpointRegistry.BuildsList,
            AdoJsonContext.Default.BuildPageDto, static page => page.Value,
            static item => item.Id.ToString(CultureInfo.InvariantCulture), culture, cancellationToken,
            new Dictionary<string, string> { ["project"] = current.TeamProject }, historyCount,
            parameters: parameters).ConfigureAwait(false);
        List<HistoryBuild> window = [];
        HashSet<int> seen = [];
        foreach (BuildDto value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.Id < 1 || string.IsNullOrEmpty(value.BuildNumber))
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = "BuildsList" };
            // The current build is added last by the caller, whatever its position in the window.
            if (value.Id == current.Id || !seen.Add(value.Id)) continue;
            if (window.Count == historyCount - 1) break;
            window.Add(new HistoryBuild
            {
                Id = value.Id,
                BuildNumber = value.BuildNumber,
                SourceBranch = value.SourceBranch,
                FinishTime = value.FinishTime?.ToUniversalTime(),
                Result = value.Result,
            });
        }
        IReadOnlyList<HistoryBuild> result = window.AsReadOnly();
        cache.AddWindow(key, result);
        return result;
    }

    private async Task<HistoryBuildData> ReadBuildAsync(HistoryBuild build, string project, CultureInfo culture,
        CancellationToken cancellationToken)
    {
        Uri buildUri = new("vstfs:///Build/Build/" + build.Id.ToString(CultureInfo.InvariantCulture));
        IReadOnlyList<AdoTestRun> buildRuns = await runs
            .GetRunsAsync(project, build.Id, buildUri, culture, cancellationToken).ConfigureAwait(false);
        if (buildRuns.Count == 0) return HistoryBuildData.Unavailable;
        List<TestResultRecord> records = [];
        int order = 0;
        foreach (AdoTestRun run in buildRuns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            order++;
            foreach (TestResultDto result in await runs.GetResultsAsync(project, run.Id, culture, cancellationToken).ConfigureAwait(false))
                records.Add(TestFailureRetrievalService.ToRecord(result, run.Id, order));
        }
        return HistoryBuildData.FromGroups(AttemptGrouper.Group(records, culture, null, cancellationToken), cancellationToken);
    }
}
