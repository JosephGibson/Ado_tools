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
// The request budget is the service's own counter: requests of the main path never spend it. Builds
// are planned newest first, one at a time. A build's run list is charged as it is sent. Its result
// pages are then counted from its runs' totalTests and set aside from what is left, all or none: a
// build whose pages do not fit is unavailable, with every older build, before any of its pages is
// sent. Its pages are read while the next build's run list is, and are never given back, so the
// builds kept do not depend on the bound or on the order of the answers. Two cases take from what is
// left when they are sent instead: the pages of a run that cannot be counted (no totalTests, or not
// completed), whose build is read to its end before an older one is planned, and a request no count
// foresaw (a retry, or a page beyond totalTests), which near the budget can still depend on the
// order of the answers. With a bound of one, each build is read to its end before the next.
internal sealed class RunHistoryService
{
    private readonly HttpClient client;
    private readonly AdoConnection connection;
    private readonly IAdoLog? log;
    private readonly RequestGate? gate;
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
        this.client = client;
        this.connection = connection;
        this.log = log;
        this.gate = gate;
        this.cache = cache;
        this.maximumRequests = maximumRequests;
        this.concurrency = concurrency;
        budget = counter.Child(maximumRequests);
        pipeline = new AdoHttpPipeline(client, connection.CollectionUri, TimeSpan.FromSeconds(connection.RequestTimeoutSeconds),
            log, counter: budget, gate: gate);
        runs = new TestRunService(client, connection, log, budget, gate);
    }

    // The history requests sent so far: what the budget counts.
    internal int RequestCount => budget.Count;

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
        Task<BuildRead>[] reads = new Task<BuildRead>[window.Count];
        try
        {
            await PlanAsync(window, current.TeamProject, reads, culture, report, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // No request of this read is left running when it ends, whatever ended it: the retrieval
            // disposes the shared gate afterwards.
            await Task.WhenAll(reads.OfType<Task>()).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
        // The reads are judged newest first, as reading one build after another met them: the first
        // build the budget refused ends the history, and so does the first error that fails the
        // whole retrieval, whatever was read after it.
        List<HistoryEntry> earlier = [];
        bool ended = false;
        for (int index = 0; index < window.Count; index++)
        {
            HistoryBuild build = window[index];
            BuildRead read = await reads[index].ConfigureAwait(false);
            HistoryBuildData data = HistoryBuildData.Unavailable;
            if (read.Kind == BuildReadKind.Cached)
            {
                if (read.Unreadable)
                    diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.HistoryUnavailable, culture,
                        arguments: [build.Id.ToString(CultureInfo.InvariantCulture)]));
                data = read.Data;
            }
            else if (!ended)
                switch (read.Kind)
                {
                    case BuildReadKind.Exhausted:
                        // Not cached: a later set of the invocation, with a budget of its own, may read it.
                        diagnostics.Add(LimitExceeded());
                        ended = true;
                        break;
                    case BuildReadKind.Fatal:
                        ExceptionDispatchInfo.Capture(read.Error!).Throw();
                        break;
                    case BuildReadKind.Failed:
                        diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.HistoryUnavailable, culture,
                            arguments: [build.Id.ToString(CultureInfo.InvariantCulture)]));
                        cache.AddBuild(build.Id, data, true);
                        break;
                    case BuildReadKind.Read:
                        data = read.Data;
                        cache.AddBuild(build.Id, data, false);
                        break;
                }
            earlier.Add(new HistoryEntry(build, false, data));
        }
        earlier.Reverse();
        return new EarlierHistory(earlier.AsReadOnly(), diagnostics.AsReadOnly());
    }

    // Authentication, authorization and cancellation still fail the whole retrieval (§15.12).
    internal static bool IsRecoverable(Exception error, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested
        && error is AdoException and not AdoAuthenticationException and not AdoAuthorizationException;

    // Fills reads, newest first. Planning stops at the first build that ends the history; the older
    // builds are then only looked up in the cache. Result pages run on after this method returns.
    private async Task PlanAsync(IReadOnlyList<HistoryBuild> window, string project, Task<BuildRead>[] reads, CultureInfo culture,
        Action<AdoProgress> report, CancellationToken cancellationToken)
    {
        for (int index = 0; index < window.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HistoryBuild build = window[index];
            // The window holds the earlier builds only; the current build is read by the main path.
            report(new AdoProgress
            {
                Phase = AdoProgressPhase.History,
                Completed = index + 1,
                Total = window.Count,
            });
            if (cache.TryGetBuild(build.Id, out HistoryBuildData cached, out bool unreadable))
                reads[index] = Task.FromResult(new BuildRead(BuildReadKind.Cached, cached, unreadable));
            // Once a newer build has ended the history, older builds send nothing.
            else if (reads.Take(index).Any(static read => read.IsCompletedSuccessfully && read.Result.Ends))
                reads[index] = Task.FromResult(BuildRead.Skipped);
            else
            {
                (reads[index], bool wait) = await PlanBuildAsync(build, project, culture, cancellationToken).ConfigureAwait(false);
                if (wait) await reads[index].ConfigureAwait(false);
            }
        }
    }

    // Reads the build's run list and starts its result pages. Wait is true when the next build must
    // not be planned before this one has been read to its end.
    private async Task<(Task<BuildRead> Read, bool Wait)> PlanBuildAsync(HistoryBuild build, string project, CultureInfo culture,
        CancellationToken cancellationToken)
    {
        if (budget.IsSpent) return (Task.FromResult(BuildRead.Exhausted), false);
        IReadOnlyList<AdoTestRun> buildRuns;
        try
        {
            Uri buildUri = new("vstfs:///Build/Build/" + build.Id.ToString(CultureInfo.InvariantCulture));
            buildRuns = await runs.GetRunsAsync(project, build.Id, buildUri, culture, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            return (Task.FromResult(BuildRead.Failure(error, cancellationToken)), false);
        }
        if (buildRuns.Count == 0) return (Task.FromResult(new BuildRead(BuildReadKind.Read, HistoryBuildData.Unavailable)), false);
        // A run that cannot be counted sets nothing aside: its pages take from what is left.
        int?[] pages = [.. buildRuns.Select(TestRunService.ResultPages)];
        IReadOnlyList<RequestCounter>? reserved = budget.Reserve([.. pages.Select(static count => count ?? 0)]);
        if (reserved is null) return (Task.FromResult(BuildRead.Exhausted), false);
        return (ReadPagesAsync(buildRuns, reserved, project, culture, cancellationToken),
            concurrency == 1 || pages.Any(static count => count is null));
    }

    // Every run's listing is read to its end, each through its own share of the budget, even when
    // another one fails, so the requests sent never depend on which failed first.
    private async Task<BuildRead> ReadPagesAsync(IReadOnlyList<AdoTestRun> buildRuns, IReadOnlyList<RequestCounter> reserved,
        string project, CultureInfo culture, CancellationToken cancellationToken)
    {
        OrderedParallelOutcome<IReadOnlyList<TestResultListingDto>> lists = await OrderedParallel.TryRunAllAsync(
            [.. Enumerable.Range(0, buildRuns.Count)], concurrency,
            (index, token) => new TestRunService(client, connection, log, reserved[index], gate)
                .GetResultsAsync(project, buildRuns[index], culture, token), cancellationToken).ConfigureAwait(false);
        if (lists.Failures.Count > 0) return BuildRead.Failure(Decisive(lists.Failures, cancellationToken), cancellationToken);
        PipelineGrouping grouping = PipelineGrouping.Create(buildRuns);
        List<TestResultRecord> records = [];
        for (int index = 0; index < buildRuns.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (TestResultListingDto result in lists.Results[index])
                records.Add(TestFailureRetrievalService.ToRecord(result, buildRuns[index], index + 1, grouping));
        }
        return new BuildRead(BuildReadKind.Read,
            HistoryBuildData.FromGroups(AttemptGrouper.Group(records, culture, null, cancellationToken), cancellationToken));
    }

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

    // Several result lists of one build can fail. The failure that decides the build is chosen by
    // kind, never by which arrived first: an error that fails the whole retrieval, then the spent
    // budget, then an error that only makes this build unavailable. Within a kind the earliest run
    // decides.
    private static Exception Decisive(IReadOnlyList<(int Index, Exception Error)> failures, CancellationToken cancellationToken) => failures
        .OrderBy(failure => failure.Error is RequestBudgetExceededException ? 1 : IsRecoverable(failure.Error, cancellationToken) ? 2 : 0)
        .ThenBy(static failure => failure.Index).First().Error;

    private enum BuildReadKind
    {
        // From the invocation cache; Unreadable repeats the warning of a read that failed.
        Cached,
        Read,
        // An error that makes only this build unavailable.
        Failed,
        // The budget refused a request or could not hold the build's pages.
        Exhausted,
        // An error that fails the whole retrieval.
        Fatal,
        // Not read: a newer build had already ended the history.
        Skipped,
    }

    // What reading one earlier build came to.
    private sealed record BuildRead(BuildReadKind Kind, HistoryBuildData Data, bool Unreadable = false, Exception? Error = null)
    {
        internal static BuildRead Exhausted { get; } = new(BuildReadKind.Exhausted, HistoryBuildData.Unavailable);
        internal static BuildRead Skipped { get; } = new(BuildReadKind.Skipped, HistoryBuildData.Unavailable);

        // No older build is planned after this one.
        internal bool Ends => Kind is BuildReadKind.Exhausted or BuildReadKind.Fatal;

        internal static BuildRead Failure(Exception error, CancellationToken cancellationToken) =>
            error is RequestBudgetExceededException ? Exhausted
            : IsRecoverable(error, cancellationToken) ? new(BuildReadKind.Failed, HistoryBuildData.Unavailable)
            : new(BuildReadKind.Fatal, HistoryBuildData.Unavailable, Error: error);
    }
}
