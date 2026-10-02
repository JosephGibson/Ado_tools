using System.Collections.ObjectModel;
using System.Net.Http;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.TestRuns;

// Two-pass retrieval for one build (§15.9). The stages follow one another as §15.9 lists them, and
// every stage observes cancellation. Unlike §1.4 and §15.9, which made every request sequential,
// the requests of one stage now run together, at most MaximumConcurrentRequests at a time, and
// the history read runs beside the main path. The set, its diagnostics and their order do not
// depend on which request finished first; with a bound of one the requests are sent one after
// another in the order of §15.9, history last.
public sealed class TestFailureRetrievalService
{
    private readonly HttpClient client;
    private readonly AdoConnection connection;
    private readonly IAdoLog? log;
    private readonly IAdoLog progress;
    private readonly ISystemClock clock;
    private readonly TestFailureInvocationCache cache;
    private readonly RequestCounter counter = new();

    // The cache is shared across the pipeline records of one invocation (§17). With more than one
    // request at a time, the log is called from several threads.
    public TestFailureRetrievalService(HttpClient client, AdoConnection connection, IAdoLog? log = null,
        TestFailureInvocationCache? cache = null)
        : this(client, connection, log, null, cache) { }

    internal TestFailureRetrievalService(HttpClient client, AdoConnection connection, IAdoLog? log,
        ISystemClock? clock, TestFailureInvocationCache? cache = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(connection.RequestTimeoutSeconds);
        this.client = client;
        this.connection = connection;
        this.log = log;
        progress = log ?? new NullAdoLog();
        this.clock = clock ?? new SystemClock();
        this.cache = cache ?? new TestFailureInvocationCache();
    }

    internal int RequestCount => counter.Count;

    internal static TestResultRecord ToRecord(TestResultDto result, AdoTestRun run, int runOrder, PipelineGrouping grouping) => new()
    {
        RunId = run.Id,
        ResultId = result.Id,
        RunOrder = runOrder,
        PipelineKey = grouping.KeyOf(run.Id),
        Outcome = result.Outcome,
        AutomatedTestName = result.AutomatedTestName,
        AutomatedTestStorage = result.AutomatedTestStorage,
        TestCaseTitle = result.TestCaseTitle,
        ResultGroupType = result.ResultGroupType,
    };

    public async Task<AdoBuildTestFailureSet> GetAsync(AdoBuild build, TestFailureQuery query, CultureInfo culture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.HistoryCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.MaximumReportedFailures);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.MaximumHistoryRequests);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.MaximumConcurrentRequests);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(query.MaximumConcurrentRequests, Configuration.TestResultOptions.MaximumConcurrentRequestsLimit);
        // The gate lives for this retrieval only; every pipeline below shares it. It is disposed after
        // the main path and the history read have both ended.
        using RequestGate gate = new(query.MaximumConcurrentRequests);
        Stages stages = new(new TestRunService(client, connection, log, counter, gate),
            new TestCaseLinkResolver(client, connection, log, counter, gate, cache),
            new TestBugResolver(client, connection, log, counter, gate, cache, query.MaximumConcurrentRequests),
            query.MaximumConcurrentRequests, build.TeamProject, culture);
        List<AdoDiagnostic> diagnostics = [];
        IReadOnlyList<AdoTestRun> runList = await stages.Runs.GetRunsAsync(build, culture, cancellationToken).ConfigureAwait(false);
        progress.Progress(new AdoProgress { Phase = AdoProgressPhase.TestRuns, Completed = runList.Count });
        if (runList.Count == 0)
            diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.NoTestRuns, culture,
                arguments: [build.Id.ToString(CultureInfo.InvariantCulture)]));
        bool incomplete = !string.Equals(build.Status, "completed", StringComparison.OrdinalIgnoreCase)
            || runList.Any(static run => !string.Equals(run.State, TestRunService.CompletedState, StringComparison.OrdinalIgnoreCase));
        // An incomplete build warns even before it has runs: its results can still change.
        if (incomplete)
            diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.TestRunInProgress, culture,
                arguments: [build.Id.ToString(CultureInfo.InvariantCulture)]));
        // The earlier builds need only the current build, so their read starts here and runs beside
        // the main path. Its progress is held back until the main path has reported its own, and its
        // diagnostics are appended last, where the sequential order put them. With a bound of one
        // the read waits for the main path instead, which reproduces the sequential request order.
        RunHistoryService history = new(client, connection, cache, log, counter, query.MaximumHistoryRequests, gate, query.MaximumConcurrentRequests);
        using CancellationTokenSource historyStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        List<AdoProgress> heldProgress = [];
        Task<EarlierHistory>? beside = query.HistoryCount > 1 && query.MaximumConcurrentRequests > 1
            ? history.ReadEarlierAsync(build, query.HistoryCount, query.HistoryScope, culture, heldProgress.Add, historyStop.Token)
            : null;
        CurrentBuild current;
        try
        {
            current = await ReadCurrentAsync(stages, build, runList, query, diagnostics, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The main path's error is the one reported, whatever the history read was doing: stop
            // that read, wait for it, and drop its outcome.
            if (beside is not null)
            {
                await historyStop.CancelAsync().ConfigureAwait(false);
                await ((Task)beside).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
            throw;
        }
        EarlierHistory earlier = beside is not null ? await beside.ConfigureAwait(false)
            : await history.ReadEarlierAsync(build, query.HistoryCount, query.HistoryScope, culture, progress.Progress, cancellationToken)
                .ConfigureAwait(false);
        foreach (AdoProgress held in heldProgress) progress.Progress(held);
        diagnostics.AddRange(earlier.Diagnostics);
        IReadOnlyList<HistoryEntry> entries = [.. earlier.Entries, RunHistoryService.Current(build, current.Data)];
        string project = build.TeamProject;
        List<AdoTestFailure> failures = [];
        for (int index = 0; index < current.Reported.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DetailedIdentity item = current.Reported[index];
            TestResultDto last = item.Details[^1];
            failures.Add(new AdoTestFailure
            {
                Ordinal = index + 1,
                Classification = item.Classification,
                TestName = item.Group.Name,
                ShortName = AttemptGrouper.ShortName(item.Group.Name, item.Group.Title),
                Storage = item.Group.Storage,
                Title = item.Group.Title,
                Attempts = item.Attempts.AsReadOnly(),
                TestCase = item.TestCaseId is int id && current.Links.TryGetValue(id, out AdoTestCaseLink? link) ? link : null,
                Bugs = current.Bugs[index],
                History = Cells(item.Group.Identity, entries, project),
                Owner = last.Owner?.ToDomain(),
                Priority = last.Priority,
                CollectionUri = connection.CollectionUri,
            });
        }
        IReadOnlyList<AdoBuildTestSummary> summaries = Summaries(entries, project);
        return new AdoBuildTestFailureSet
        {
            Build = build,
            Runs = runList,
            Summary = summaries[^1],
            History = summaries,
            Failures = failures.AsReadOnly(),
            FailedCount = failures.Count(static failure => failure.Classification == AdoTestFailureClassification.Failed),
            FlakyCount = failures.Count(static failure => failure.Classification == AdoTestFailureClassification.Flaky),
            Status = diagnostics.Any(static diagnostic => diagnostic.Severity == AdoDiagnosticSeverity.Error)
                ? AdoTestFailureStatus.Partial : AdoTestFailureStatus.Complete,
            Diagnostics = diagnostics.AsReadOnly(),
            RetrievedAt = clock.UtcNow,
            CollectionUri = connection.CollectionUri,
        };
    }

    // The main path: both passes, the attachment lists, the Test Case links and the bugs of the build.
    // Each fan-out returns its values in request order, and only this method writes to the lists.
    private async Task<CurrentBuild> ReadCurrentAsync(Stages stages, AdoBuild build, IReadOnlyList<AdoTestRun> runList,
        TestFailureQuery query, List<AdoDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        CultureInfo culture = stages.Culture;
        string project = stages.Project;
        // GetRunsAsync already returns runs in attempt order.
        IReadOnlyList<AdoTestRun> ordered = runList;
        PipelineGrouping grouping = PipelineGrouping.Create(ordered);
        // Pass 1: one listing per run, joined in run order.
        ProgressCount listed = new(progress, AdoProgressPhase.TestResults, ordered.Count);
        IReadOnlyList<IReadOnlyList<TestResultDto>> listings = await OrderedParallel.RunAsync(ordered, stages.Concurrency, async (run, token) =>
        {
            IReadOnlyList<TestResultDto> results = await stages.Runs.GetResultsAsync(project, run, culture, token).ConfigureAwait(false);
            listed.Advance();
            return results;
        }, cancellationToken).ConfigureAwait(false);
        List<TestResultRecord> records = [];
        for (int index = 0; index < ordered.Count; index++)
            foreach (TestResultDto result in listings[index])
                records.Add(ToRecord(result, ordered[index], index + 1, grouping));
        IReadOnlyList<TestIdentityGroup> groups = AttemptGrouper.Group(records, culture, diagnostics, cancellationToken);
        IReadOnlyList<TestIdentityGroup> candidates = AttemptGrouper.ReportOrder(groups.Where(static group => group.IsCandidate));
        // The failure limit is applied before any detail request, so it bounds every fan-out below.
        int limit = Math.Min(candidates.Count, query.MaximumReportedFailures);
        if (candidates.Count > limit)
            diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.FailureLimitExceeded, culture, arguments:
            [
                candidates.Count.ToString(CultureInfo.InvariantCulture),
                query.MaximumReportedFailures.ToString(CultureInfo.InvariantCulture),
            ]));
        // Pass 2: one request per result record of the reported tests, in report order.
        List<TestResultRecord> detailRecords = [.. candidates.Take(limit).SelectMany(static group => group.Records)];
        ProgressCount read = new(progress, AdoProgressPhase.TestDetail, detailRecords.Count);
        IReadOnlyList<TestResultDto> fetched = await OrderedParallel.RunAsync(detailRecords, stages.Concurrency, async (record, token) =>
        {
            TestResultDto detail = await stages.Runs.GetResultAsync(project, record.RunId, record.ResultId, culture, token).ConfigureAwait(false);
            read.Advance();
            return detail;
        }, cancellationToken).ConfigureAwait(false);
        List<DetailedIdentity> detailed = [];
        int next = 0;
        for (int index = 0; index < limit; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TestIdentityGroup group = candidates[index];
            List<AdoTestAttempt> attempts = [];
            List<TestResultDto> details = [];
            for (int recordIndex = 0; recordIndex < group.Records.Count; recordIndex++) details.Add(fetched[next++]);
            bool multipleRecords = group.Records.Count > 1;
            for (int recordIndex = 0; recordIndex < group.Records.Count; recordIndex++)
            {
                TestResultRecord record = group.Records[recordIndex];
                TestResultDto detail = details[recordIndex];
                Uri resultUrl = AdoWebLinks.BuildTestResult(connection.CollectionUri, project, build.Id, record.RunId, record.ResultId);
                IReadOnlyList<TestSubResultDto> rerun = record.IsRerunGroup ? TestAttemptMapper.RerunAttempts(detail) : [];
                if (rerun.Count > 0)
                    foreach (TestSubResultDto sub in rerun)
                        attempts.Add(TestAttemptMapper.FromSubResult(sub, detail, record.RunId, attempts.Count + 1, resultUrl, cancellationToken));
                else
                    attempts.Add(TestAttemptMapper.FromResult(detail, record.RunId, attempts.Count + 1,
                        multipleRecords ? AdoTestAttemptSource.RunAttempt : AdoTestAttemptSource.Single, resultUrl, cancellationToken));
            }
            detailed.Add(new DetailedIdentity(group, attempts, details));
        }
        await AddAttachmentsAsync(stages, detailed, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, AdoTestCaseLink> resolved = await ResolveLinksAsync(stages, detailed, diagnostics, cancellationToken)
            .ConfigureAwait(false);
        // Only identities that really hold a failure-class attempt are reported (§15.6).
        List<DetailedIdentity> reported = detailed
            .Where(static item => item.Attempts.Any(static attempt => attempt.OutcomeClass == AdoTestOutcomeClass.Failure))
            .OrderBy(static item => item.Classification == AdoTestFailureClassification.Failed ? 0 : 1)
            .ThenBy(static item => item.Group.Storage ?? "", StringComparer.Ordinal)
            .ThenBy(static item => item.Group.Name ?? "", StringComparer.Ordinal)
            .ThenBy(static item => item.Group.FirstResultId)
            .ToList();
        // One lookup for every reported test, after the Test Case read that supplies the linked work items.
        IReadOnlyList<IReadOnlyList<AdoTestBug>> reportedBugs = await stages.Bugs.ResolveAsync([.. reported.Select(item => new TestBugReferences(
                item.Attempts.SelectMany(static attempt => attempt.AssociatedBugIds).ToHashSet(),
                (item.TestCaseId is int testCaseId ? stages.Links.LinkedWorkItems(project, testCaseId) : []).ToHashSet()))],
            build.Id, project, culture, diagnostics, cancellationToken).ConfigureAwait(false);
        return new CurrentBuild(reported, resolved, reportedBugs, CurrentBuildData(groups, detailed, cancellationToken));
    }

    // Attachment metadata for each detailed result and for its attempt and iteration sub-results
    // (§15.9 step 5). Result-level attachments belong to the first attempt of their record. Every
    // list is one request; they run together and are assigned in the order they were asked for.
    private async Task AddAttachmentsAsync(Stages stages, List<DetailedIdentity> detailed, CancellationToken cancellationToken)
    {
        List<AttachmentListing> work = [];
        List<int> pending = [];
        for (int itemIndex = 0; itemIndex < detailed.Count; itemIndex++)
        {
            DetailedIdentity item = detailed[itemIndex];
            for (int recordIndex = 0; recordIndex < item.Details.Count; recordIndex++)
            {
                // Result IDs are unique only within a run, so every match also compares the run.
                int runId = item.Group.Records[recordIndex].RunId;
                int resultId = item.Details[recordIndex].Id;
                int record = pending.Count, before = work.Count;
                work.Add(new AttachmentListing(itemIndex, record, runId, resultId, null));
                foreach (int subId in item.Attempts.Where(attempt => attempt.RunId == runId && attempt.ResultId == resultId)
                    .SelectMany(AttachmentSubResultIds).Distinct())
                    work.Add(new AttachmentListing(itemIndex, record, runId, resultId, subId));
                pending.Add(work.Count - before);
            }
        }
        // Progress counts result records, as before: a record is done when its last list is read.
        int[] remaining = [.. pending];
        ProgressCount listed = new(progress, AdoProgressPhase.Attachments, remaining.Length);
        IReadOnlyList<IReadOnlyList<AdoTestAttachment>> lists = await OrderedParallel.RunAsync(work, stages.Concurrency, async (listing, token) =>
        {
            IReadOnlyList<AdoTestAttachment> list = await stages.Runs
                .GetAttachmentsAsync(stages.Project, listing.RunId, listing.ResultId, listing.SubResultId, stages.Culture, token).ConfigureAwait(false);
            if (Interlocked.Decrement(ref remaining[listing.Record]) == 0) listed.Advance();
            return list;
        }, cancellationToken).ConfigureAwait(false);
        List<AdoTestAttachment>[] collectedByItem = [.. detailed.Select(static _ => new List<AdoTestAttachment>())];
        for (int index = 0; index < work.Count; index++) collectedByItem[work[index].Item].AddRange(lists[index]);
        for (int itemIndex = 0; itemIndex < detailed.Count; itemIndex++)
        {
            DetailedIdentity item = detailed[itemIndex];
            List<AdoTestAttachment> collected = collectedByItem[itemIndex];
            if (collected.Count == 0) continue;
            for (int index = 0; index < item.Attempts.Count; index++)
            {
                AdoTestAttempt attempt = item.Attempts[index];
                HashSet<int> owned = [.. AttachmentSubResultIds(attempt)];
                bool first = item.Attempts.FindIndex(candidate =>
                    candidate.RunId == attempt.RunId && candidate.ResultId == attempt.ResultId) == index;
                List<AdoTestAttachment> mine = collected
                    .Where(attachment => attachment.RunId == attempt.RunId && attachment.ResultId == attempt.ResultId
                        && (attachment.SubResultId.HasValue ? owned.Contains(attachment.SubResultId.Value) : first))
                    .ToList();
                if (mine.Count == 0) continue;
                item.Attempts[index] = attempt.WithAttachments(mine.AsReadOnly());
            }
        }
    }

    private static IEnumerable<int> AttachmentSubResultIds(AdoTestAttempt attempt)
    {
        if (attempt.SubResultId is int id) yield return id;
        foreach (int subId in DescendantIds(attempt.SubResults)) yield return subId;
    }

    // The mapper has already bounded this tree to three levels. Use the same traversal for
    // listing and assigning attachments so nested data rows retain their owning attempt.
    private static IEnumerable<int> DescendantIds(IReadOnlyList<AdoTestSubResult> values)
    {
        foreach (AdoTestSubResult value in values)
        {
            yield return value.Id;
            foreach (int id in DescendantIds(value.SubResults)) yield return id;
        }
    }

    private async Task<IReadOnlyDictionary<int, AdoTestCaseLink>> ResolveLinksAsync(Stages stages, List<DetailedIdentity> detailed,
        List<AdoDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        List<int> ids = [];
        foreach (DetailedIdentity item in detailed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int recordIndex = 0; recordIndex < item.Details.Count; recordIndex++)
            {
                TestResultDto detail = item.Details[recordIndex];
                string? reference = detail.TestCase?.Id;
                if (string.IsNullOrWhiteSpace(reference)) continue;
                if (!TestCaseLinkResolver.TryParseReference(reference, out int id))
                {
                    diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.InvalidTestCaseReference, stages.Culture, arguments:
                    [
                        detail.Id.ToString(CultureInfo.InvariantCulture),
                        item.Group.Records[recordIndex].RunId.ToString(CultureInfo.InvariantCulture),
                    ]));
                    continue;
                }
                item.TestCaseId ??= id;
                ids.Add(id);
            }
        }
        return ids.Count == 0
            ? new Dictionary<int, AdoTestCaseLink>()
            : await stages.Links.ResolveAsync(ids, stages.Project, stages.Culture, diagnostics, progress, cancellationToken).ConfigureAwait(false);
    }

    // The current build's bars and cells use the final classification for detailed identities and
    // the pass-1 classification for the rest, so the two always agree.
    private static HistoryBuildData CurrentBuildData(IReadOnlyList<TestIdentityGroup> groups,
        List<DetailedIdentity> detailed, CancellationToken cancellationToken)
    {
        Dictionary<TestIdentity, AdoTestHistoryOutcome> overrides = [];
        foreach (DetailedIdentity item in detailed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool reported = item.Attempts.Any(static attempt => attempt.OutcomeClass == AdoTestOutcomeClass.Failure);
            overrides[item.Group.Identity] = OutcomeClassifier.Cell(item.Deciding, reported);
        }
        HistoryBuildData pass1 = HistoryBuildData.FromGroups(groups, cancellationToken);
        if (overrides.Count == 0) return pass1;
        Dictionary<TestIdentity, AdoTestHistoryOutcome> cells = new(pass1.Cells);
        foreach ((TestIdentity identity, AdoTestHistoryOutcome cell) in overrides) cells[identity] = cell;
        int passed = 0, failed = 0, flaky = 0, other = 0;
        foreach (AdoTestHistoryOutcome cell in cells.Values)
            switch (cell)
            {
                case AdoTestHistoryOutcome.Flaky: passed++; flaky++; break;
                case AdoTestHistoryOutcome.Passed: passed++; break;
                case AdoTestHistoryOutcome.Failed: failed++; break;
                default: other++; break;
            }
        return new HistoryBuildData
        {
            IsAvailable = true,
            Passed = passed,
            Failed = failed,
            Flaky = flaky,
            Other = other,
            Cells = cells,
        };
    }

    // An ungrouped result has no history cells (§15.10).
    private IReadOnlyList<AdoTestHistoryEntry> Cells(TestIdentity identity, IReadOnlyList<HistoryEntry> entries, string project)
    {
        if (!identity.IsGrouped) return Array.Empty<AdoTestHistoryEntry>();
        List<AdoTestHistoryEntry> cells = [];
        foreach (HistoryEntry entry in entries)
            cells.Add(new AdoTestHistoryEntry
            {
                BuildId = entry.Build.Id,
                BuildNumber = entry.Build.BuildNumber,
                Outcome = !entry.Data.IsAvailable ? AdoTestHistoryOutcome.Unavailable
                    : entry.Data.Cells.TryGetValue(identity, out AdoTestHistoryOutcome cell) ? cell
                    : AdoTestHistoryOutcome.NotRun,
                IsCurrent = entry.IsCurrent,
                WebUrl = AdoWebLinks.Build(connection.CollectionUri, project, entry.Build.Id),
            });
        return cells.AsReadOnly();
    }

    private ReadOnlyCollection<AdoBuildTestSummary> Summaries(IReadOnlyList<HistoryEntry> entries, string project)
    {
        List<AdoBuildTestSummary> summaries = [];
        foreach (HistoryEntry entry in entries)
            summaries.Add(new AdoBuildTestSummary
            {
                BuildId = entry.Build.Id,
                BuildNumber = entry.Build.BuildNumber,
                SourceBranch = entry.Build.SourceBranch,
                FinishTime = entry.Build.FinishTime,
                Result = entry.Build.Result,
                Passed = entry.Data.Passed,
                Failed = entry.Data.Failed,
                Flaky = entry.Data.Flaky,
                Other = entry.Data.Other,
                IsAvailable = entry.Data.IsAvailable,
                IsCurrent = entry.IsCurrent,
                WebUrl = AdoWebLinks.Build(connection.CollectionUri, project, entry.Build.Id),
            });
        return summaries.AsReadOnly();
    }

    // The services of one retrieval, all built on its gate, with what every stage needs to call them.
    private sealed record Stages(TestRunService Runs, TestCaseLinkResolver Links, TestBugResolver Bugs, int Concurrency, string Project,
        CultureInfo Culture);

    // What the main path hands back: the reported tests in report order, with their links, bugs and
    // the current build's history data.
    private sealed record CurrentBuild(IReadOnlyList<DetailedIdentity> Reported, IReadOnlyDictionary<int, AdoTestCaseLink> Links,
        IReadOnlyList<IReadOnlyList<AdoTestBug>> Bugs, HistoryBuildData Data);

    // One attachment list to request: for a result, or for one of its sub-results. Record numbers
    // the result records across all tests, for progress.
    private readonly record struct AttachmentListing(int Item, int Record, int RunId, int ResultId, int? SubResultId);

    // Progress from requests that end in any order: the count only ever rises, one step at a time.
    private sealed class ProgressCount(IAdoLog log, AdoProgressPhase phase, int total)
    {
        private readonly Lock gate = new();
        private int completed;

        internal void Advance()
        {
            lock (gate) log.Progress(new AdoProgress { Phase = phase, Completed = ++completed, Total = total });
        }
    }

    private sealed class DetailedIdentity(TestIdentityGroup group, List<AdoTestAttempt> attempts, List<TestResultDto> details)
    {
        internal TestIdentityGroup Group { get; } = group;
        internal List<AdoTestAttempt> Attempts { get; } = attempts;
        internal List<TestResultDto> Details { get; } = details;
        internal int? TestCaseId { get; set; }
        // Each attempt belongs to its run's pipeline group, so a failure in one language is never
        // hidden by a later pass in another.
        internal AdoTestOutcomeClass Deciding => OutcomeClassifier.Deciding(Attempts.Select(attempt => (
            Group.Records.FirstOrDefault(record => record.RunId == attempt.RunId)?.PipelineKey ?? "", attempt.OutcomeClass)));
        internal AdoTestFailureClassification Classification => OutcomeClassifier.Classify(Deciding);
    }
}
