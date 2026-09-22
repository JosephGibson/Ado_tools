using System.Collections.ObjectModel;
using System.Net.Http;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.Connections;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.TestRuns;

// Two-pass retrieval for one build (§15.9). Every stage is sequential and observes cancellation.
public sealed class TestFailureRetrievalService
{
    private const string CompletedState = "Completed";
    private readonly HttpClient client;
    private readonly AdoConnection connection;
    private readonly IAdoLog? log;
    private readonly IAdoLog progress;
    private readonly ISystemClock clock;
    private readonly TestFailureInvocationCache cache;
    private readonly TestRunService runs;
    private readonly TestCaseLinkResolver links;
    private readonly TestBugResolver bugs;
    private readonly RequestCounter counter = new();

    // The cache is shared across the pipeline records of one invocation (§17).
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
        runs = new TestRunService(client, connection, log, counter);
        links = new TestCaseLinkResolver(client, connection, log, counter);
        bugs = new TestBugResolver(client, connection, log, counter);
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
        StartedDate = result.StartedDate,
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
        List<AdoDiagnostic> diagnostics = [];
        string project = build.TeamProject;
        IReadOnlyList<AdoTestRun> runList = await runs.GetRunsAsync(build, culture, cancellationToken).ConfigureAwait(false);
        progress.Progress(new AdoProgress { Phase = AdoProgressPhase.TestRuns, Completed = runList.Count });
        if (runList.Count == 0)
            diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.NoTestRuns, culture,
                arguments: [build.Id.ToString(CultureInfo.InvariantCulture)]));
        bool incomplete = !string.Equals(build.Status, "completed", StringComparison.OrdinalIgnoreCase)
            || runList.Any(static run => !string.Equals(run.State, CompletedState, StringComparison.OrdinalIgnoreCase));
        // An incomplete build warns even before it has runs: its results can still change.
        if (incomplete)
            diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.TestRunInProgress, culture,
                arguments: [build.Id.ToString(CultureInfo.InvariantCulture)]));
        // GetRunsAsync already returns runs in attempt order.
        IReadOnlyList<AdoTestRun> ordered = runList;
        PipelineGrouping grouping = PipelineGrouping.Create(ordered);
        List<TestResultRecord> records = [];
        int runOrder = 0;
        foreach (AdoTestRun run in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            runOrder++;
            foreach (TestResultDto result in await runs.GetResultsAsync(project, run.Id, culture, cancellationToken).ConfigureAwait(false))
                records.Add(ToRecord(result, run, runOrder, grouping));
            progress.Progress(new AdoProgress { Phase = AdoProgressPhase.TestResults, Completed = runOrder, Total = ordered.Count });
        }
        IReadOnlyList<TestIdentityGroup> groups = AttemptGrouper.Group(records, culture, diagnostics, cancellationToken);
        IReadOnlyList<TestIdentityGroup> candidates = AttemptGrouper.ReportOrder(groups.Where(static group => group.IsCandidate));
        int limit = Math.Min(candidates.Count, query.MaximumReportedFailures);
        if (candidates.Count > limit)
            diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.FailureLimitExceeded, culture, arguments:
            [
                candidates.Count.ToString(CultureInfo.InvariantCulture),
                query.MaximumReportedFailures.ToString(CultureInfo.InvariantCulture),
            ]));
        List<DetailedIdentity> detailed = [];
        int detailedResults = 0;
        int totalResults = candidates.Take(limit).Sum(static group => group.Records.Count);
        for (int index = 0; index < limit; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TestIdentityGroup group = candidates[index];
            List<AdoTestAttempt> attempts = [];
            List<TestResultDto> details = [];
            foreach (TestResultRecord record in group.Records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TestResultDto detail = await runs.GetResultAsync(project, record.RunId, record.ResultId, culture, cancellationToken)
                    .ConfigureAwait(false);
                details.Add(detail);
                progress.Progress(new AdoProgress
                {
                    Phase = AdoProgressPhase.TestDetail,
                    Completed = ++detailedResults,
                    Total = totalResults,
                });
            }
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
        await AddAttachmentsAsync(detailed, project, culture, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, AdoTestCaseLink> resolved = await ResolveLinksAsync(detailed, project, culture, diagnostics, cancellationToken)
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
        IReadOnlyList<IReadOnlyList<AdoTestBug>> reportedBugs = await bugs.ResolveAsync([.. reported.Select(item => new TestBugReferences(
                item.Attempts.SelectMany(static attempt => attempt.AssociatedBugIds).ToHashSet(),
                (item.TestCaseId is int testCaseId ? links.LinkedWorkItems(testCaseId) : []).ToHashSet()))],
            build.Id, project, culture, diagnostics, cancellationToken).ConfigureAwait(false);
        HistoryBuildData currentData = CurrentBuildData(groups, detailed, cancellationToken);
        RunHistoryService history = new(client, connection, cache, log, counter);
        IReadOnlyList<HistoryEntry> entries = await history.GetHistoryAsync(build, currentData, query.HistoryCount,
            query.HistoryScope, query.MaximumHistoryRequests, culture, diagnostics, cancellationToken).ConfigureAwait(false);
        List<AdoTestFailure> failures = [];
        for (int index = 0; index < reported.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DetailedIdentity item = reported[index];
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
                TestCase = item.TestCaseId is int id && resolved.TryGetValue(id, out AdoTestCaseLink? link) ? link : null,
                Bugs = reportedBugs[index],
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

    // Attachment metadata for each detailed result and for its attempt and iteration sub-results
    // (§15.9 step 5). Result-level attachments belong to the first attempt of their record.
    private async Task AddAttachmentsAsync(List<DetailedIdentity> detailed, string project, CultureInfo culture,
        CancellationToken cancellationToken)
    {
        int total = detailed.Sum(static item => item.Details.Count);
        int completed = 0;
        foreach (DetailedIdentity item in detailed)
        {
            List<AdoTestAttachment> collected = [];
            for (int recordIndex = 0; recordIndex < item.Details.Count; recordIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Result IDs are unique only within a run, so every match also compares the run.
                int runId = item.Group.Records[recordIndex].RunId;
                int resultId = item.Details[recordIndex].Id;
                AdoTestAttempt[] owners = item.Attempts
                    .Where(attempt => attempt.RunId == runId && attempt.ResultId == resultId).ToArray();
                collected.AddRange(await runs.GetAttachmentsAsync(project, runId, resultId, null, culture, cancellationToken)
                    .ConfigureAwait(false));
                foreach (int subId in owners.SelectMany(AttachmentSubResultIds).Distinct())
                    collected.AddRange(await runs.GetAttachmentsAsync(project, runId, resultId, subId, culture, cancellationToken)
                        .ConfigureAwait(false));
                progress.Progress(new AdoProgress { Phase = AdoProgressPhase.Attachments, Completed = ++completed, Total = total });
            }
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

    private async Task<IReadOnlyDictionary<int, AdoTestCaseLink>> ResolveLinksAsync(List<DetailedIdentity> detailed,
        string project, CultureInfo culture, List<AdoDiagnostic> diagnostics, CancellationToken cancellationToken)
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
                    diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.InvalidTestCaseReference, culture, arguments:
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
            : await links.ResolveAsync(ids, project, culture, diagnostics, progress, cancellationToken).ConfigureAwait(false);
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
