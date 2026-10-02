using System.Net.Http;
using System.Runtime.ExceptionServices;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.TestRuns;

internal sealed record HistoryEntry(HistoryBuild Build, bool IsCurrent, HistoryBuildData Data);

// The earlier builds of one history read, oldest first, with the warnings the read produced.
internal sealed record EarlierHistory(IReadOnlyList<HistoryEntry> Entries, IReadOnlyList<AdoDiagnostic> Diagnostics)
{
    internal static EarlierHistory None { get; } = new([], []);
}

// The current build plus up to HistoryCount - 1 earlier builds of the same definition (§15.12).
// History never changes Status and never fails the report. Reading the earlier builds needs only
// the current build, so the retrieval may run it beside its main path; the current entry is added
// afterwards, from data the main path computes.
//
// Builds are read one after another, newest first, and the result lists of one build together.
// The request budget is the service's own counter: requests of the main path never spend it.
internal sealed class RunHistoryService
{
    private readonly AdoHttpPipeline pipeline;
    private readonly TestRunService runs;
    private readonly TestFailureInvocationCache cache;
    private readonly RequestCounter budget;
    private readonly int maximumRequests;
    private readonly int concurrency;

    // counter is the retrieval's counter; the history budget is a child of it, so every history
    // request still counts in the retrieval's total.
    internal RunHistoryService(HttpClient client, AdoConnection connection, TestFailureInvocationCache cache,
        IAdoLog? log, RequestCounter counter, int maximumRequests, RequestGate? gate = null, int concurrency = 1)
    {
        this.cache = cache;
        this.maximumRequests = maximumRequests;
        this.concurrency = concurrency;
        budget = counter.Child(maximumRequests);
        pipeline = new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds),
            log, counter: budget, gate: gate);
        runs = new TestRunService(client, connection, log, budget, gate);
    }

    internal static HistoryEntry Current(AdoBuild current, HistoryBuildData data) => new(new HistoryBuild
    {
        Id = current.Id,
        BuildNumber = current.BuildNumber,
        SourceBranch = current.SourceBranch,
        FinishTime = current.FinishTime,
        Result = current.Result,
    }, true, data);

    // report receives one progress event per earlier build, in order. The caller decides whether
    // it is shown at once or held back until its own progress has ended.
    internal async Task<EarlierHistory> ReadEarlierAsync(AdoBuild current, int historyCount, AdoTestHistoryScope scope,
        CultureInfo culture, Action<AdoProgress> report, CancellationToken cancellationToken)
    {
        if (historyCount <= 1) return EarlierHistory.None;
        List<AdoDiagnostic> diagnostics = [];
        AdoDiagnostic LimitExceeded() => DiagnosticMessageRenderer.Create(DiagnosticCodes.HistoryLimitExceeded, culture,
            arguments: [maximumRequests.ToString(CultureInfo.InvariantCulture)]);
        IReadOnlyList<HistoryBuild> window;
        try
        {
            window = await GetWindowAsync(current, historyCount, scope, culture, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestBudgetExceededException)
        {
            diagnostics.Add(LimitExceeded());
            return new EarlierHistory([], diagnostics.AsReadOnly());
        }
        catch (Exception error) when (IsRecoverable(error, cancellationToken))
        {
            // Without the window there are no earlier builds to report; the current build stands alone.
            diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.HistoryUnavailable, culture,
                arguments: [current.Id.ToString(CultureInfo.InvariantCulture)]));
            return new EarlierHistory([], diagnostics.AsReadOnly());
        }
        // Newest first, so the request budget is spent on the most recent builds and the
        // remaining older ones become Unavailable.
        List<HistoryEntry> earlier = [];
        bool limitReported = false;
        int completed = 0;
        foreach (HistoryBuild build in window)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // The window holds the earlier builds only; the current build is read by the main path.
            report(new AdoProgress
            {
                Phase = AdoProgressPhase.History,
                Completed = ++completed,
                Total = window.Count,
            });
            if (cache.TryGetBuild(build.Id, out HistoryBuildData cached, out bool unreadable))
            {
                if (unreadable)
                    diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.HistoryUnavailable, culture,
                        arguments: [build.Id.ToString(CultureInfo.InvariantCulture)]));
                earlier.Add(new HistoryEntry(build, false, cached));
                continue;
            }
            if (budget.IsSpent)
            {
                if (!limitReported)
                {
                    diagnostics.Add(LimitExceeded());
                    limitReported = true;
                }
                earlier.Add(new HistoryEntry(build, false, HistoryBuildData.Unavailable));
                continue;
            }
            HistoryBuildData data;
            bool exhausted = false, failed = false;
            try
            {
                data = await ReadBuildAsync(build, current.TeamProject, culture, cancellationToken).ConfigureAwait(false);
            }
            catch (RequestBudgetExceededException)
            {
                // A request is refused only when the budget is fully spent, so every older build is
                // unavailable too, whichever of this build's requests was the one refused.
                if (!limitReported)
                {
                    diagnostics.Add(LimitExceeded());
                    limitReported = true;
                }
                exhausted = true;
                data = HistoryBuildData.Unavailable;
            }
            catch (Exception error) when (IsRecoverable(error, cancellationToken))
            {
                diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.HistoryUnavailable, culture,
                    arguments: [build.Id.ToString(CultureInfo.InvariantCulture)]));
                data = HistoryBuildData.Unavailable;
                failed = true;
            }
            if (!exhausted) cache.AddBuild(build.Id, data, failed);
            earlier.Add(new HistoryEntry(build, false, data));
        }
        earlier.Reverse();
        return new EarlierHistory(earlier.AsReadOnly(), diagnostics.AsReadOnly());
    }

    // Authentication, authorization and cancellation still fail the whole retrieval (§15.12).
    internal static bool IsRecoverable(Exception error, CancellationToken cancellationToken) =>
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
            ["$top"] = historyCount.ToString(CultureInfo.InvariantCulture),
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
        PipelineGrouping grouping = PipelineGrouping.Create(buildRuns);
        OrderedParallelOutcome<IReadOnlyList<TestResultDto>> lists = await OrderedParallel.TryRunAsync(buildRuns, concurrency,
            (run, token) => runs.GetResultsAsync(project, run, culture, token), cancellationToken).ConfigureAwait(false);
        if (lists.Failures.Count > 0) ExceptionDispatchInfo.Capture(Decisive(lists.Failures, cancellationToken)).Throw();
        List<TestResultRecord> records = [];
        for (int index = 0; index < buildRuns.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (TestResultDto result in lists.Results[index])
                records.Add(TestFailureRetrievalService.ToRecord(result, buildRuns[index], index + 1, grouping));
        }
        return HistoryBuildData.FromGroups(AttemptGrouper.Group(records, culture, null, cancellationToken), cancellationToken);
    }

    // Several result lists of one build can fail at once. The failure that decides the build is
    // chosen by kind, never by which arrived first: an error that fails the whole retrieval, then
    // the spent budget, then an error that only makes this build unavailable. Within a kind the
    // earliest run decides.
    private static Exception Decisive(IReadOnlyList<(int Index, Exception Error)> failures, CancellationToken cancellationToken) => failures
        .OrderBy(failure => failure.Error is RequestBudgetExceededException ? 1 : IsRecoverable(failure.Error, cancellationToken) ? 2 : 0)
        .ThenBy(static failure => failure.Index).First().Error;
}
