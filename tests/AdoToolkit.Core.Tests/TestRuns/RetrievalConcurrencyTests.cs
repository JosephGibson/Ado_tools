using System.Net.Http;
using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.Tests.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

// Retrieval sends the requests of a stage together and reads the history beside the main path.
// What it returns must not depend on the bound or on the order in which the server answers.
//
// One synthetic build serves every test. Build 401 has three runs: 301 and its retry 302 in the
// English stage, 303 in the French stage. T1 fails everywhere; T2, T3 and T4 fail and then pass in
// English; T5 fails only in French; result 7 of run 301 has no automated name. T2 is a rerun group
// in run 301. Test Case 1503 and bug 2003 cannot be read, T4 names an invalid Test Case, and bug
// 2002 is closed. History has builds 400 and 399; the runs of build 398 cannot be listed.
[Trait("Culture", "Invariant")]
public sealed partial class RetrievalConcurrencyTests
{
    private static readonly string[] Stages = ["runs", "results", "detail", "attachments", "test cases", "bugs", "category", "states", "window", "history"];
    private static readonly int[] EarlierBuilds = [400, 399, 398, 397, 396];

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SetAndRenderedReportAreIdenticalAtABoundOfOneAndOfEightUnderRandomDelays(int seed)
    {
        // At a bound of one the requests, the set and the report do not depend on the delays, so one
        // seed proves the bound-one side under delays and the others read it without them.
        using Server sequential = new(seed == 1 ? seed : 0);
        using Server parallel = new(seed * 31);
        AdoBuildTestFailureSet first = await sequential.GetAsync(new TestFailureQuery { HistoryCount = 4, MaximumConcurrentRequests = 1 });
        AdoBuildTestFailureSet second = await parallel.GetAsync(new TestFailureQuery { HistoryCount = 4, MaximumConcurrentRequests = 8 });

        Assert.Equal(1, sequential.Handler.PeakInFlight);
        Assert.InRange(parallel.Handler.PeakInFlight, 2, 8);
        // The same requests, whatever their order.
        Assert.Equal(sequential.Sent().Order(StringComparer.Ordinal), parallel.Sent().Order(StringComparer.Ordinal));
        Assert.Equal(Describe(first), Describe(second));
        Assert.Equal(Render(first), Render(second));

        // The set is the expected one, not merely the same twice. The result without a name has no
        // storage, so it sorts first among the failed tests.
        Assert.Equal(["Manual check", "T1", "T5", "T2", "T3", "T4"], second.Failures.Select(static failure => failure.ShortName));
        Assert.Equal((3, 3), (second.FailedCount, second.FlakyCount));
        Assert.Equal([301, 303, 302], second.Runs.Select(static run => run.Id));
        Assert.Equal([301, 303, 302], Test(second, "T1").Attempts.Select(static attempt => attempt.RunId));
        // The rerun group of run 301 is two attempts; runs 303 and 302 add one each.
        Assert.Equal([(301, 1), (301, 2), (303, null), (302, null)], Test(second, "T2").Attempts.Select(static attempt => (attempt.RunId, attempt.SubResultId)));
        Assert.Equal([61, 62], Test(second, "T1").Attempts[0].Attachments.Select(static item => item.Id));
        Assert.Equal([63], Test(second, "T2").Attempts[0].Attachments.Select(static item => item.Id));
        Assert.Equal([64], Test(second, "T5").Attempts.Single(static attempt => attempt.RunId == 303).Attachments.Select(static item => item.Id));
        Assert.Equal([2001, 3001], Test(second, "T1").Bugs.Select(static bug => bug.Id));
        Assert.Equal([2003], Test(second, "T5").Bugs.Select(static bug => bug.Id));
        Assert.Equal([398, 399, 400, 401], second.History.Select(static summary => summary.BuildId));
        Assert.Equal([false, true, true, true], second.History.Select(static summary => summary.IsAvailable));
        // Run-level, grouping, links, bugs, then history: the order of the sequential retrieval.
        Assert.Equal([DiagnosticCodes.UngroupedTestResult, DiagnosticCodes.InvalidTestCaseReference, DiagnosticCodes.InvalidTestCaseReference,
            DiagnosticCodes.InvalidTestCaseReference, DiagnosticCodes.UnresolvedTestCase, DiagnosticCodes.UnresolvedBug, DiagnosticCodes.HistoryUnavailable],
            second.Diagnostics.Select(static diagnostic => diagnostic.Code));
    }

    // With one request at a time the stages follow one another as before, and history comes last.
    [Fact]
    public async Task ABoundOfOneSendsEveryStageInTheSequentialOrderWithHistoryLast()
    {
        using Server server = new();
        CapturingLog log = new();
        await server.GetAsync(new TestFailureQuery { HistoryCount = 4, MaximumConcurrentRequests = 1 }, log: log);
        int[] stages = [.. server.Handler.Requests.Select(Stage)];
        Assert.Equal(stages.Order(), stages);
        Assert.Equal(Enumerable.Range(0, Stages.Length), stages.Distinct());
        // Pass 1 follows attempt order; run 303 reports seven tests and lists six, so it alone asks for the empty page.
        Assert.Equal(["/Runs/301/results?0", "/Runs/303/results?0", "/Runs/303/results?6", "/Runs/302/results?0"],
            server.Handler.Requests.Where(static request => Stage(request) == 1).Select(static request =>
                request.Uri.AbsolutePath[request.Uri.AbsolutePath.IndexOf("/Runs/", StringComparison.Ordinal)..] + "?" + Skip().Match(request.Uri.Query).Groups[1].Value));
        // Pass 2 follows report order, one request per result record: the failed tests, the one
        // without a storage name first, then the flaky ones.
        Assert.Equal(["301/7", "301/1", "303/1", "302/1", "301/5", "303/5", "302/5", "301/2", "303/2", "302/2", "301/3", "303/3", "302/3", "301/4", "303/4", "302/4"],
            server.Handler.Requests.Where(static request => Stage(request) == 2).Select(static request =>
                Detail().Match(request.Uri.AbsolutePath).Groups[1].Value + "/" + Detail().Match(request.Uri.AbsolutePath).Groups[2].Value));
        // Every phase reports its progress in order, and each one ends at its total.
        foreach (AdoProgressPhase phase in new[] { AdoProgressPhase.TestResults, AdoProgressPhase.TestDetail, AdoProgressPhase.Attachments, AdoProgressPhase.History })
        {
            AdoProgress[] events = [.. log.ProgressEvents.Where(progress => progress.Phase == phase)];
            Assert.Equal(Enumerable.Range(1, events.Length), events.Select(static progress => progress.Completed));
            Assert.All(events, progress => Assert.Equal(events.Length, progress.Total));
        }
    }

    // The same progress with eight requests at a time: counts only rise, each phase ends at its
    // total, and the history events held back while the main path ran come after it, in order.
    [Fact]
    public async Task ProgressCountsRiseInOrderAndHistoryProgressFollowsTheMainPath()
    {
        using Server server = new(seed: 7);
        CapturingLog log = new();
        await server.GetAsync(new TestFailureQuery { HistoryCount = 4, MaximumConcurrentRequests = 8 }, log: log);
        foreach ((AdoProgressPhase phase, int total) in new[] { (AdoProgressPhase.TestResults, 3), (AdoProgressPhase.TestDetail, 16),
            (AdoProgressPhase.Attachments, 16), (AdoProgressPhase.History, 3) })
        {
            AdoProgress[] events = [.. log.ProgressEvents.Where(progress => progress.Phase == phase)];
            Assert.Equal(Enumerable.Range(1, total), events.Select(static progress => progress.Completed));
            Assert.All(events, progress => Assert.Equal(total, progress.Total));
        }
        AdoProgressPhase[] phases = [.. log.ProgressEvents.Select(static progress => progress.Phase)];
        Assert.Equal(phases.Length - 3, Array.IndexOf(phases, AdoProgressPhase.History));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(16)]
    public async Task RequestsInFlightNeverExceedTheBoundAndDoOverlap(int bound)
    {
        using Server server = new(seed: 11);
        await server.GetAsync(new TestFailureQuery { HistoryCount = 4, MaximumConcurrentRequests = bound });
        Assert.InRange(server.Handler.PeakInFlight, 2, bound);
    }

    // History has a budget of its own. The main path sends far more requests than that budget while
    // history is being read, and none of them spends it: the builds that fit are the ones that fit
    // when everything is sequential, and a refusal marks every older build unavailable.
    [Theory]
    // The window, both run pages of build 400 and its one result page: build 400 fits exactly.
    [InlineData(4, 1)]
    [InlineData(4, 8)]
    // One request is left, for the first run page of build 399.
    [InlineData(5, 1)]
    [InlineData(5, 8)]
    // Both run pages of build 399 fit; its two result listings are refused, together or one by one.
    [InlineData(6, 1)]
    [InlineData(6, 8)]
    public async Task HistoryBudgetCountsOnlyHistoryRequestsAndGivesTheSameBuildsAtEveryBound(int budget, int bound)
    {
        // At a bound of one the order is fixed, so delays would change nothing; seed 0 sends none.
        using Server server = new(seed: bound == 1 ? 0 : budget);
        AdoBuildTestFailureSet set = await server.GetAsync(new TestFailureQuery { HistoryCount = 4, MaximumHistoryRequests = budget, MaximumConcurrentRequests = bound });
        Assert.Equal([398, 399, 400, 401], set.History.Select(static summary => summary.BuildId));
        Assert.Equal([false, false, true, true], set.History.Select(static summary => summary.IsAvailable));
        Assert.Equal([budget.ToString(CultureInfo.InvariantCulture)], Assert.Single(set.Diagnostics, static item => item.Code == DiagnosticCodes.HistoryLimitExceeded).Arguments);
        // Build 398 was never reached, so its failure was never seen.
        Assert.DoesNotContain(set.Diagnostics, static item => item.Code == DiagnosticCodes.HistoryUnavailable);
        Assert.DoesNotContain(server.Handler.Requests, static request => request.Uri.Query.Contains("Build%2F398", StringComparison.Ordinal));
        Assert.Equal(budget, server.Handler.Requests.Count(static request => Stage(request) >= 8));
        Assert.True(server.Handler.Requests.Count(static request => Stage(request) < 8) > 5 * budget);
        Assert.Equal(server.Handler.Requests.Count, server.RequestCount);
    }

    // An error of the main path is the one reported, whether or not the history read, running
    // beside it, has already failed with another error.
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public async Task MainPathErrorIsReportedWhateverTheHistoryReadDoes(int bound)
    {
        using Server server = new(seed: 5) { DeniedHistoryBuild = 400, FailingDetail = "303/5" };
        AdoException error = await Assert.ThrowsAnyAsync<AdoException>(() => server.GetAsync(new TestFailureQuery { HistoryCount = 4, MaximumConcurrentRequests = bound }));
        Assert.IsNotType<AdoAuthorizationException>(error);
        Assert.Equal("TestResultGet", error.Operation);
        // Nothing is left running: no request arrives after the call has ended.
        int requests = server.Handler.Requests.Count;
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(requests, server.Handler.Requests.Count);
    }

    // Without a main path error, an authorization failure in history still fails the retrieval.
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public async Task AuthorizationFailureInHistorySurfacesAfterTheMainPath(int bound)
    {
        using Server server = new(seed: 5) { DeniedHistoryBuild = 399 };
        await Assert.ThrowsAsync<AdoAuthorizationException>(() => server.GetAsync(new TestFailureQuery { HistoryCount = 4, MaximumConcurrentRequests = bound }));
        // The main path ran to its end first.
        Assert.Contains(server.Handler.Requests, static request => Stage(request) == 7);
    }

    // Piped builds share one cache, as one cmdlet invocation does: a later build asks neither for
    // the Test Cases nor for the bug metadata again, and still warns about the Test Case that was
    // not found.
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public async Task PipedBuildsReuseTestCasesAndBugMetadataAndRepeatTheWarningForACachedMiss(int bound)
    {
        using Server server = new();
        TestFailureInvocationCache cache = new();
        TestFailureQuery query = new() { HistoryCount = 1, MaximumConcurrentRequests = bound };
        AdoBuildTestFailureSet first = await server.GetAsync(query, cache);
        int afterFirst = server.Handler.Requests.Count;
        AdoBuildTestFailureSet second = await server.GetAsync(query, cache);
        foreach (int stage in new[] { 4, 6, 7 })
            Assert.Single(server.Handler.Requests, request => Stage(request) == stage);
        // Bugs themselves are read for every build: their state can differ from one build to the next.
        Assert.Equal(2, server.Handler.Requests.Count(static request => Stage(request) == 5));
        Assert.Equal(afterFirst - 3, server.Handler.Requests.Count - afterFirst);
        foreach (AdoBuildTestFailureSet set in new[] { first, second })
        {
            Assert.Equal(["1503"], Assert.Single(set.Diagnostics, static item => item.Code == DiagnosticCodes.UnresolvedTestCase).Arguments);
            Assert.False(Test(set, "T3").TestCase!.IsResolved);
            Assert.Equal("Case 1501", Test(set, "T1").TestCase!.Title);
            Assert.Equal([2001, 3001], Test(set, "T1").Bugs.Select(static bug => bug.Id));
        }
        Assert.Equal(Describe(first), Describe(second));
    }

    // Near the history budget, the builds kept and the requests sent depend neither on the bound nor
    // on the order of the answers. Build 400 fits whole. One result listing of build 399 is refused
    // by the server; its other listings are still read to their end, so the build spends its whole
    // reservation (0.7.10 cancelled them when the failure came first, so what was left for older
    // builds depended on timing). Build 398 has a run without totalTests and build 397 a run still
    // in progress: each is read to its end before an older build is planned, its uncosted pages
    // taking from what is left. A build whose result pages do not fit sends none of them.
    [Theory]
    // Budget; kept earlier builds, oldest first (396 to 400); result requests per earlier build,
    // newest first (400 to 396); history requests.
    [InlineData(5, "00000", "0 0 0 0 0", 3)]
    [InlineData(6, "00001", "3 0 0 0 0", 6)]
    [InlineData(11, "00001", "3 0 0 0 0", 8)]
    [InlineData(12, "00001", "3 4 0 0 0", 12)]
    [InlineData(14, "00001", "3 4 0 0 0", 14)]
    [InlineData(15, "00001", "3 4 1 0 0", 15)]
    [InlineData(16, "00001", "3 4 2 0 0", 16)]
    [InlineData(17, "00101", "3 4 3 0 0", 17)]
    [InlineData(20, "00101", "3 4 3 1 0", 20)]
    [InlineData(24, "01101", "3 4 3 2 0", 23)]
    [InlineData(25, "11101", "3 4 3 2 2", 25)]
    public async Task NearTheHistoryBudgetTheBuildsKeptAndTheRequestsSentDependOnNeitherTheBoundNorTheResponseOrder(int budget, string kept, string pages, int requests)
    {
        string? firstRequests = null, firstSet = null;
        foreach ((int bound, int seed) in new[] { (1, 0), (8, 1), (8, 2), (8, 3) })
        {
            using HistoryServer server = new(seed);
            AdoBuildTestFailureSet set = await server.GetAsync(new TestFailureQuery
            {
                HistoryCount = 6, MaximumHistoryRequests = budget, MaximumConcurrentRequests = bound, SkipAttachments = true,
            });
            Assert.Equal([396, 397, 398, 399, 400, 401], set.History.Select(static summary => summary.BuildId));
            Assert.Equal(kept + "1", string.Concat(set.History.Select(static summary => summary.IsAvailable ? '1' : '0')));
            Assert.Equal(budget < 25 ? 1 : 0, set.Diagnostics.Count(static item => item.Code == DiagnosticCodes.HistoryLimitExceeded));
            Assert.Equal(budget >= 12 ? ["399"] : [], set.Diagnostics.Where(static item => item.Code == DiagnosticCodes.HistoryUnavailable)
                .Select(static item => item.Arguments[0]));
            Assert.Equal(pages, string.Join(' ', EarlierBuilds.Select(build => server.ResultRequests(build).ToString(CultureInfo.InvariantCulture))));
            Assert.Equal(requests, server.HistoryRequests);
            string sent = string.Join('\n', server.Sent().Order(StringComparer.Ordinal)), described = Describe(set);
            firstRequests ??= sent;
            firstSet ??= described;
            Assert.Equal(firstRequests, sent);
            Assert.Equal(firstSet, described);
        }
    }

    private static AdoTestFailure Test(AdoBuildTestFailureSet set, string name) => Assert.Single(set.Failures, failure => failure.ShortName == name);

    private static string Render(AdoBuildTestFailureSet set) =>
        TestFailureReportFixture.Render(TestFailureReportModelBuilder.Build(set, TestFailureReportFixture.Options("en-US")));

    // Everything the set holds that the report does not show.
    private static string Describe(AdoBuildTestFailureSet set) => string.Join('\n',
    [
        "status " + set.Status + " failed " + set.FailedCount.ToString(CultureInfo.InvariantCulture) + " flaky " + set.FlakyCount.ToString(CultureInfo.InvariantCulture),
        .. set.Diagnostics.Select(static item => "diagnostic " + item.Code + " " + item.Severity + " " + string.Join(',', item.Arguments)),
        .. set.History.Select(static item => "history " + item.BuildId.ToString(CultureInfo.InvariantCulture) + " " + item.IsAvailable + " "
            + item.Passed.ToString(CultureInfo.InvariantCulture) + "/" + item.Failed.ToString(CultureInfo.InvariantCulture) + "/"
            + item.Flaky.ToString(CultureInfo.InvariantCulture) + "/" + item.Other.ToString(CultureInfo.InvariantCulture)),
        .. set.Failures.Select(static failure => "failure " + failure.Ordinal.ToString(CultureInfo.InvariantCulture) + " " + failure.ShortName + " " + failure.Classification
            + " case " + (failure.TestCase?.Id.ToString(CultureInfo.InvariantCulture) ?? "-") + " bugs " + string.Join(',', failure.Bugs.Select(static bug => bug.Id))
            + " history " + string.Join(',', failure.History.Select(static cell => cell.Outcome))
            + " attempts " + string.Join(';', failure.Attempts.Select(static attempt => attempt.Number.ToString(CultureInfo.InvariantCulture) + ":"
                + attempt.RunId.ToString(CultureInfo.InvariantCulture) + ":" + attempt.ResultId.ToString(CultureInfo.InvariantCulture) + ":" + attempt.SubResultId + ":" + attempt.Outcome
                + ":" + string.Join('+', attempt.Attachments.Select(static item => item.Id))))),
    ]);

    private static int Stage(RequestSnapshot request)
    {
        string path = request.Uri.AbsolutePath, query = request.Uri.Query;
        bool current = query.Contains("Build%2F401", StringComparison.Ordinal) || CurrentRun().IsMatch(path);
        if (path.EndsWith("/_apis/build/builds", StringComparison.Ordinal)) return 8;
        if (path.EndsWith("/_apis/test/runs", StringComparison.Ordinal)) return current ? 0 : 9;
        if (path.EndsWith("/results", StringComparison.Ordinal)) return current ? 1 : 9;
        if (path.EndsWith("/attachments", StringComparison.Ordinal)) return 3;
        if (Detail().IsMatch(path)) return 2;
        if (path.EndsWith("/workitemsbatch", StringComparison.Ordinal)) return request.Body!.Contains("\"$expand\"", StringComparison.Ordinal) ? 4 : 5;
        if (path.Contains("/workitemtypecategories/", StringComparison.Ordinal)) return 6;
        return path.EndsWith("/states", StringComparison.Ordinal) ? 7 : throw new InvalidOperationException(path);
    }

    [GeneratedRegex("/Runs/30[0-9]/", RegexOptions.CultureInvariant)]
    private static partial Regex CurrentRun();
    [GeneratedRegex("/Runs/([0-9]+)/results/([0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex Detail();
    [GeneratedRegex("%24skip=([0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex Skip();
    [GeneratedRegex("Build%2F([0-9]+)&", RegexOptions.CultureInvariant)]
    private static partial Regex BuildFilter();
    [GeneratedRegex("/Runs/([0-9]+)/results$", RegexOptions.CultureInvariant)]
    private static partial Regex Listing();
    [GeneratedRegex("/Runs/([0-9]+)/Results/([0-9]+)/attachments$", RegexOptions.CultureInvariant)]
    private static partial Regex AttachmentList();
    [GeneratedRegex("testSubResultId=([0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SubResult();
    [GeneratedRegex("\"ids\":\\[([0-9,]*)\\]", RegexOptions.CultureInvariant)]
    private static partial Regex BatchIds();

    // Answers every request of the synthetic build from its path, query and body, after a delay that
    // depends only on the request and the seed, so a run is reproducible whatever thread sends it.
    private sealed class Server : IDisposable
    {
        // Newest first, the reported build included, as the server lists them.
        private static readonly int[] WindowBuilds = [401, 400, 399, 398];
        private readonly HttpClient client;
        private readonly int seed;
        private TestFailureRetrievalService? last;

        internal Server(int seed = 0)
        {
            this.seed = seed;
            Handler = new FakeHttpMessageHandler
            {
                Fallback = async (request, token) =>
                {
                    string target = request.RequestUri!.PathAndQuery;
                    string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(token);
                    if (this.seed != 0) await Task.Delay(TimeSpan.FromMilliseconds(Hash(target + body) % 9), token);
                    return Respond(request.RequestUri, body);
                },
            };
            client = new HttpClient(Handler);
        }

        internal FakeHttpMessageHandler Handler { get; }
        // A history build whose run listing is refused, and a detail request ("run/result") that fails.
        internal int DeniedHistoryBuild { get; init; }
        internal string? FailingDetail { get; init; }
        internal int RequestCount => last!.RequestCount;

        internal Task<AdoBuildTestFailureSet> GetAsync(TestFailureQuery query, TestFailureInvocationCache? cache = null, IAdoLog? log = null)
        {
            last = new TestFailureRetrievalService(client, TestRunFixture.Connection, log, new FakeClock(), cache);
            return last.GetAsync(TestRunFixture.Build(), query, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        }

        internal IEnumerable<string> Sent() => Handler.Requests.Select(static request => request.Method + " " + request.Uri.PathAndQuery + " " + request.Body);

        public void Dispose()
        {
            client.Dispose();
            Handler.Dispose();
        }

        private uint Hash(string text)
        {
            uint hash = 2166136261 ^ (uint)seed;
            foreach (char character in text) hash = (hash ^ character) * 16777619;
            return hash;
        }

        private HttpResponseMessage Respond(Uri uri, string body)
        {
            string path = uri.AbsolutePath, query = uri.Query;
            int skip = Skip().Match(query) is { Success: true } paging ? Number(paging.Groups[1].Value) : 0;
            if (path.EndsWith("/_apis/build/builds", StringComparison.Ordinal))
                return Page(WindowBuilds.Select(static id => "{\"id\":" + N(id) + ",\"buildNumber\":\"2026." + N(id) + "\",\"sourceBranch\":\"refs/heads/main\","
                    + "\"result\":\"failed\",\"finishTime\":\"2026-09-" + N(id - 386) + "T10:00:00Z\"}"));
            if (path.EndsWith("/_apis/test/runs", StringComparison.Ordinal))
            {
                int build = Number(BuildFilter().Match(query).Groups[1].Value);
                if (build == DeniedHistoryBuild) return FakeHttpMessageHandler.Response("{\"message\":\"Synthetic denial.\"}", 403);
                if (build == 398) return FakeHttpMessageHandler.Response("{\"message\":\"Synthetic failure.\"}", 500);
                return Page(skip > 0 ? [] : build switch
                {
                    // Not in attempt order: the service orders them.
                    401 => [Run(302, "Speed EN (attempt 2)", "EN", 2, "10:10", 6), Run(303, "Speed FR", "FR", 1, "10:05", 7), Run(301, "Speed EN", "EN", 1, "10:00", 7)],
                    400 => [Run(311, "Speed", "EN", 1, "10:00", 6)],
                    _ => [Run(321, "Speed EN", "EN", 1, "10:00", 6), Run(322, "Speed FR", "FR", 1, "10:05", 6)],
                });
            }
            if (Listing().Match(path) is { Success: true } listing)
            {
                int run = Number(listing.Groups[1].Value);
                return Page(skip > 0 ? [] : Enumerable.Range(1, run == 301 ? 7 : 6).Select(id => Listed(run, id)));
            }
            if (Detail().Match(path) is { Success: true } detail)
            {
                int run = Number(detail.Groups[1].Value), id = Number(detail.Groups[2].Value);
                return FailingDetail == N(run) + "/" + N(id) ? FakeHttpMessageHandler.Response("{\"message\":\"Synthetic failure.\"}", 500)
                    : FakeHttpMessageHandler.Response(Detailed(run, id));
            }
            if (AttachmentList().Match(path) is { Success: true } list)
            {
                string key = list.Groups[1].Value + "/" + list.Groups[2].Value + (SubResult().Match(query) is { Success: true } sub ? "/" + sub.Groups[1].Value : "");
                return Page(key switch
                {
                    "301/1" => [Attachment(61, "console.txt", 10), Attachment(62, "shot.png", 20)],
                    "301/2/1" => [Attachment(63, "attempt-1.json", 30)],
                    "303/5" => [Attachment(64, "fr.log", 40)],
                    _ => [],
                });
            }
            if (path.EndsWith("/workitemsbatch", StringComparison.Ordinal))
            {
                int[] ids = [.. BatchIds().Match(body).Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Number)];
                // Only the requested work items that exist: 1503 and 2003 are never returned.
                return Page(body.Contains("\"$expand\"", StringComparison.Ordinal)
                    ? ids.Where(static id => id is 1501 or 1502).Select(static id => WorkItem(id, "Case " + N(id), "Ready", "Test Case", id == 1501 ? [3001, 3002] : []))
                    : ids.Where(static id => id is 2001 or 2002 or 3001 or 3002).Select(static id =>
                        WorkItem(id, "Work item " + N(id), id == 2002 ? "Closed" : "Active", id == 3002 ? "Task" : "Bug", [])));
            }
            if (path.Contains("/workitemtypecategories/", StringComparison.Ordinal))
                return FakeHttpMessageHandler.Response("{\"referenceName\":\"Microsoft.BugCategory\",\"workItemTypes\":[{\"name\":\"Bug\"}]}");
            if (path.EndsWith("/states", StringComparison.Ordinal))
                return Page(["{\"name\":\"Active\",\"category\":\"InProgress\"}", "{\"name\":\"Closed\",\"category\":\"Completed\"}"]);
            throw new InvalidOperationException("No synthetic response for " + uri.PathAndQuery);
        }

        private static string Outcome(int run, int id) => (run, id) switch
        {
            (301, <= 4) or (301, 7) or (302, 1) or (303, 1) or (303, 5) or (311, 5) or (321, 1) => "Failed",
            _ => "Passed",
        };

        private static string Identity(int run, int id) => run == 301 && id == 7 ? "\"testCaseTitle\":\"Manual check\""
            : "\"automatedTestName\":\"Synthetic.Speed.T" + N(id) + "\",\"automatedTestStorage\":\"Synthetic.Speed.dll\"";

        private static string Listed(int run, int id) => "{\"id\":" + N(id) + ",\"outcome\":\"" + Outcome(run, id) + "\"," + Identity(run, id)
            + (run == 301 && id == 2 ? ",\"resultGroupType\":\"Rerun\"" : "") + "}";

        private static string Detailed(int run, int id)
        {
            bool failed = Outcome(run, id) == "Failed";
            string text = "{\"id\":" + N(id) + ",\"outcome\":\"" + Outcome(run, id) + "\"," + Identity(run, id) + ",\"computerName\":\"AGENT-" + N(run) + "\",\"durationInMs\":1500.5";
            if (failed) text += ",\"errorMessage\":\"Failure of result " + N(id) + " in run " + N(run) + "\",\"stackTrace\":\"   at Synthetic.Speed.T" + N(id) + "()\"";
            text += id switch { 1 => ",\"testCase\":{\"id\":\"1501\"}", 2 => ",\"testCase\":{\"id\":\"1502\"}", 3 => ",\"testCase\":{\"id\":\"1503\"}", 4 => ",\"testCase\":{\"id\":\"abc\"}", _ => "" };
            text += id switch { 1 => ",\"associatedBugs\":[{\"id\":\"2001\"},{\"id\":\"2002\"}]", 5 => ",\"associatedBugs\":[{\"id\":\"2003\"}]", _ => "" };
            if (run == 301 && id == 2)
                text += ",\"resultGroupType\":\"Rerun\",\"subResults\":[{\"id\":1,\"sequenceId\":1,\"outcome\":\"Failed\",\"errorMessage\":\"First try\"},"
                    + "{\"id\":2,\"sequenceId\":2,\"outcome\":\"Failed\",\"errorMessage\":\"Second try\"}]";
            return text + "}";
        }

        private static string Run(int id, string name, string stage, int attempt, string time, int total) =>
            "{\"id\":" + N(id) + ",\"name\":\"" + name + "\",\"state\":\"Completed\",\"isAutomated\":true,\"startedDate\":\"2026-09-15T" + time + ":00Z\",\"totalTests\":" + N(total)
            + ",\"pipelineReference\":{\"stageReference\":{\"attempt\":1,\"stageName\":\"" + stage + "\"},\"phaseReference\":{\"attempt\":1,\"phaseName\":\"Tests\"},"
            + "\"jobReference\":{\"attempt\":" + N(attempt) + ",\"jobName\":\"__default\"}}}";

        private static string Attachment(int id, string name, int size) => "{\"id\":" + N(id) + ",\"fileName\":\"" + name + "\",\"size\":" + N(size) + "}";

        private static string WorkItem(int id, string title, string state, string type, int[] linked) =>
            "{\"id\":" + N(id) + ",\"rev\":2,\"fields\":{\"System.Id\":" + N(id) + ",\"System.Title\":\"" + title + "\",\"System.State\":\"" + state
            + "\",\"System.WorkItemType\":\"" + type + "\",\"System.TeamProject\":\"" + TestRunFixture.Project + "\"},\"relations\":["
            + string.Join(',', linked.Select(static target => "{\"rel\":\"Microsoft.VSTS.Common.TestedBy-Reverse\",\"url\":\"https://ado.example.test/Collection/_apis/wit/workItems/" + N(target) + "\"}"))
            + "]}";

        private static HttpResponseMessage Page(IEnumerable<string> items)
        {
            string[] values = [.. items];
            return FakeHttpMessageHandler.Response("{\"count\":" + N(values.Length) + ",\"value\":[" + string.Join(',', values) + "]}");
        }

        private static int Number(string text) => int.Parse(text, CultureInfo.InvariantCulture);
        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    }

    // The history of NearTheHistoryBudget…: build 401 has one run, 501, with one failed test, and
    // five earlier builds. Each earlier run is (ID, totalTests, state, results it lists); the
    // listings of run 420 are refused. Delays depend only on the request and the seed, as above.
    private sealed class HistoryServer : IDisposable
    {
        private const int FailingRun = 420;
        private static readonly int[] WindowBuilds = [401, 400, 399, 398, 397, 396];
        private static readonly Dictionary<int, (int Id, int? Total, string State, int Listed)[]> EarlierRuns = new()
        {
            [400] = [(410, 2, "Completed", 2), (411, 1000, "Completed", 1000)],
            [399] = [(FailingRun, 3, "Completed", 3), (421, 1000, "Completed", 1000), (422, 4, "Completed", 4)],
            [398] = [(430, null, "Completed", 2), (431, 1, "Completed", 1)],
            [397] = [(440, 5, "InProgress", 5)],
            [396] = [(450, 1, "Completed", 1), (451, 1, "Completed", 1)],
        };
        private readonly HttpClient client;
        private readonly int seed;

        internal HistoryServer(int seed)
        {
            this.seed = seed;
            Handler = new FakeHttpMessageHandler
            {
                Fallback = async (request, token) =>
                {
                    if (this.seed != 0) await Task.Delay(TimeSpan.FromMilliseconds(Hash(request.RequestUri!.PathAndQuery) % 9), token);
                    return Respond(request.RequestUri!);
                },
            };
            client = new HttpClient(Handler);
        }

        internal FakeHttpMessageHandler Handler { get; }

        // The window, the run lists and the result listings of the earlier builds.
        internal int HistoryRequests => Handler.Requests.Count(static request =>
            request.Uri.AbsolutePath.EndsWith("/_apis/build/builds", StringComparison.Ordinal)
            || (request.Uri.AbsolutePath.EndsWith("/_apis/test/runs", StringComparison.Ordinal) && !request.Uri.Query.Contains("Build%2F401", StringComparison.Ordinal))
            || (Listing().Match(request.Uri.AbsolutePath) is { Success: true } listing && listing.Groups[1].Value != "501"));

        internal int ResultRequests(int build) => Handler.Requests.Count(request =>
            Listing().Match(request.Uri.AbsolutePath) is { Success: true } listing
            && EarlierRuns[build].Any(run => N(run.Id) == listing.Groups[1].Value));

        internal Task<AdoBuildTestFailureSet> GetAsync(TestFailureQuery query) =>
            new TestFailureRetrievalService(client, TestRunFixture.Connection, null, new FakeClock())
                .GetAsync(TestRunFixture.Build(), query, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);

        internal IEnumerable<string> Sent() => Handler.Requests.Select(static request => request.Method + " " + request.Uri.PathAndQuery);

        public void Dispose()
        {
            client.Dispose();
            Handler.Dispose();
        }

        private uint Hash(string text)
        {
            uint hash = 2166136261 ^ (uint)seed;
            foreach (char character in text) hash = (hash ^ character) * 16777619;
            return hash;
        }

        private static HttpResponseMessage Respond(Uri uri)
        {
            string path = uri.AbsolutePath, query = uri.Query;
            int skip = Skip().Match(query) is { Success: true } paging ? Number(paging.Groups[1].Value) : 0;
            if (path.EndsWith("/_apis/build/builds", StringComparison.Ordinal))
                return Page(WindowBuilds.Select(static id => "{\"id\":" + N(id) + ",\"buildNumber\":\"2026." + N(id) + "\",\"sourceBranch\":\"refs/heads/main\","
                    + "\"result\":\"failed\",\"finishTime\":\"2026-09-" + N(id - 386) + "T10:00:00Z\"}"));
            if (path.EndsWith("/_apis/test/runs", StringComparison.Ordinal))
            {
                int build = Number(BuildFilter().Match(query).Groups[1].Value);
                return Page(skip > 0 ? [] : build == 401 ? [Run(501, 1, "Completed")]
                    : EarlierRuns[build].Select(static run => Run(run.Id, run.Total, run.State)));
            }
            if (Listing().Match(path) is { Success: true } listing)
            {
                int run = Number(listing.Groups[1].Value);
                if (run == FailingRun) return FakeHttpMessageHandler.Response("{\"message\":\"Synthetic failure.\"}", 500);
                int listed = run == 501 ? 1 : EarlierRuns.Values.SelectMany(static runs => runs).Single(item => item.Id == run).Listed;
                return Page(Enumerable.Range(skip + 1, Math.Clamp(listed - skip, 0, 1000)).Select(id => Listed(run, id)));
            }
            if (Detail().Match(path) is { Success: true } detail && detail.Groups[1].Value == "501" && detail.Groups[2].Value == "1")
                return FakeHttpMessageHandler.Response("{\"id\":1,\"outcome\":\"Failed\"," + Identity(1) + ",\"errorMessage\":\"Synthetic failure.\"}");
            throw new InvalidOperationException("No synthetic response for " + uri.PathAndQuery);
        }

        private static string Identity(int id) => "\"automatedTestName\":\"Synthetic.History.T" + N(id) + "\",\"automatedTestStorage\":\"Synthetic.History.dll\"";

        private static string Listed(int run, int id) =>
            "{\"id\":" + N(id) + ",\"outcome\":\"" + (id == 1 && run % 10 is 0 or 1 ? "Failed" : "Passed") + "\"," + Identity(id) + "}";

        private static string Run(int id, int? total, string state) => "{\"id\":" + N(id) + ",\"name\":\"History " + N(id) + "\",\"state\":\"" + state
            + "\",\"isAutomated\":true" + (total is int value ? ",\"totalTests\":" + N(value) : "") + "}";

        private static HttpResponseMessage Page(IEnumerable<string> items)
        {
            string[] values = [.. items];
            return FakeHttpMessageHandler.Response("{\"count\":" + N(values.Length) + ",\"value\":[" + string.Join(',', values) + "]}");
        }

        private static int Number(string text) => int.Parse(text, CultureInfo.InvariantCulture);
        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
