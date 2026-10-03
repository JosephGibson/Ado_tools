using System.Net.Http;
using System.Text.RegularExpressions;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

// What -Verbose shows of a retrieval: one line per stage with the requests that stage sent and its
// milliseconds, then a summary with the build, the failures, every request and the elapsed time.
public sealed class RetrievalTimingTests
{
    private static readonly AdoMessage[] Stages =
    [
        AdoMessage.RetrievalStageBuild, AdoMessage.RetrievalStageRuns, AdoMessage.RetrievalStageResults, AdoMessage.RetrievalStageDetails,
        AdoMessage.RetrievalStageAttachments, AdoMessage.RetrievalStageTestCases, AdoMessage.RetrievalStageBugs, AdoMessage.RetrievalStageHistory,
    ];

    // History is read beside the main path at a bound above one, yet the lines keep the stage order
    // and each counts the requests of its own stage only.
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public async Task EachStageLineCountsItsOwnRequestsInStageOrderAndTheSummaryComesLast(int bound)
    {
        using FakeHttpMessageHandler handler = RunHistoryTests.History().Handler();
        using HttpClient client = new(handler);
        CapturingLog log = new();
        AdoBuildTestFailureSet set = await new TestFailureRetrievalService(client, TestRunFixture.Connection, log, new FakeClock())
            .GetAsync(TestRunFixture.Build(), new TestFailureQuery { HistoryCount = 3, MaximumConcurrentRequests = bound },
                CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        List<(AdoMessage Stage, int Requests)> lines = StageLines(log);
        // A build supplied by the caller is not read, so its line is left out.
        Assert.Equal(Stages[1..].Select(stage => (stage, handler.Requests.Count(request => StageOf(request) == stage))), lines);
        // Every stage of this build sends requests, so no count matches by being zero.
        Assert.All(lines, static line => Assert.True(line.Requests > 0, line.Stage.ToString()));
        Assert.Equal([401L, set.Failures.Count, handler.Requests.Count], Summary(log.Messages[^1])[..3]);
    }

    // Without attachment lists their line keeps its place after the details, with no request.
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public async Task SkippedAttachmentListsKeepTheirLineInStageOrderWithNoRequest(int bound)
    {
        using FakeHttpMessageHandler handler = RunHistoryTests.History().Handler();
        using HttpClient client = new(handler);
        CapturingLog log = new();
        AdoBuildTestFailureSet set = await new TestFailureRetrievalService(client, TestRunFixture.Connection, log, new FakeClock())
            .GetAsync(TestRunFixture.Build(), new TestFailureQuery { HistoryCount = 3, MaximumConcurrentRequests = bound, SkipAttachments = true },
                CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        List<(AdoMessage Stage, int Requests)> lines = StageLines(log);
        Assert.Equal(Stages[1..].Select(stage => (stage, handler.Requests.Count(request => StageOf(request) == stage))), lines);
        Assert.Equal((AdoMessage.RetrievalStageAttachments, 0), lines[3]);
        Assert.All(lines.Where(static line => line.Stage != AdoMessage.RetrievalStageAttachments), static line => Assert.True(line.Requests > 0, line.Stage.ToString()));
        Assert.Equal([401L, set.Failures.Count, handler.Requests.Count], Summary(log.Messages[^1])[..3]);
    }

    [Fact]
    public async Task ABuildReadThroughTheRetrievalIsItsFirstStageAndCountsInTheSummary()
    {
        TestRunFixture fixture = new TestRunFixture()
            .Route("build-401.json", "/_apis/build/builds/401?")
            .Route("runs-two.json", "/test/runs", "%24skip=0&")
            .Route("results-run-201.json", "/Runs/201/results", "%24skip=0&")
            .Route("results-run-202.json", "/Runs/202/results", "%24skip=0&")
            .Route("result-detail-201-1.json", "/Runs/201/results/1?")
            .Route("result-detail-202-11.json", "/Runs/202/results/11?")
            .Route("attachments-empty.json", "/attachments")
            .RouteBugs()
            .Route("workitems-testcases.json", "workitemsbatch");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        CapturingLog log = new();
        AdoBuildTestFailureSet set = await new TestFailureRetrievalService(client, TestRunFixture.Connection, log, new FakeClock())
            .GetAsync((builds, token) => builds.GetAsync(TestRunFixture.Project, 401, CultureInfo.InvariantCulture, token),
                new TestFailureQuery { HistoryCount = 1 }, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(401, set.Build.Id);
        Assert.EndsWith("/_apis/build/builds/401", handler.Requests[0].Uri.AbsolutePath, StringComparison.Ordinal);
        List<(AdoMessage Stage, int Requests)> lines = StageLines(log);
        Assert.Equal(Stages, lines.Select(static line => line.Stage));
        Assert.Equal((AdoMessage.RetrievalStageBuild, 1), lines[0]);
        // One history build: nothing earlier to read.
        Assert.Equal((AdoMessage.RetrievalStageHistory, 0), lines[^1]);
        Assert.Equal(handler.Requests.Count, lines.Sum(static line => line.Requests));
        Assert.Equal(handler.Requests.Count, Summary(log.Messages[^1])[2]);
    }

    // The stage lines in the order written, each with its request count.
    private static List<(AdoMessage Stage, int Requests)> StageLines(CapturingLog log)
    {
        List<(AdoMessage, int)> lines = [];
        foreach (string message in log.Messages)
            foreach (AdoMessage stage in Stages)
                if (Pattern(stage, 2).Match(message) is { Success: true } match)
                    lines.Add((stage, int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)));
        return lines;
    }

    // The build, the failures, the requests and the milliseconds of a summary line.
    private static long[] Summary(string message)
    {
        Match match = Pattern(AdoMessage.RetrievalSummary, 4).Match(message);
        Assert.True(match.Success, message);
        return [.. match.Groups.Cast<Group>().Skip(1).Select(static group => long.Parse(group.Value, CultureInfo.InvariantCulture))];
    }

    // A catalog message whose placeholders are numbers, each captured.
    private static Regex Pattern(AdoMessage key, int placeholders) => new("^" + Regex.Escape(Messages.Get(key, CultureInfo.InvariantCulture,
        [.. Enumerable.Repeat<object?>("\u0001", placeholders)])).Replace("\u0001", "([0-9]+)", StringComparison.Ordinal) + "$", RegexOptions.CultureInvariant);

    // The stage that sends a request of RunHistoryTests.History(): build 401 has runs 201 and 202,
    // its earlier builds 400 and 399 have runs 261 and 271.
    private static AdoMessage StageOf(RequestSnapshot request)
    {
        string path = request.Uri.AbsolutePath;
        bool current = request.Uri.Query.Contains("Build%2F401", StringComparison.Ordinal) || path.Contains("/Runs/20", StringComparison.Ordinal);
        if (path.EndsWith("/_apis/build/builds/401", StringComparison.Ordinal)) return AdoMessage.RetrievalStageBuild;
        if (path.EndsWith("/attachments", StringComparison.Ordinal)) return AdoMessage.RetrievalStageAttachments;
        if (path.EndsWith("/workitemsbatch", StringComparison.Ordinal))
            return request.Body!.Contains(TestRunFixture.TestCaseBatch, StringComparison.Ordinal) ? AdoMessage.RetrievalStageTestCases : AdoMessage.RetrievalStageBugs;
        // The Bug category and the Bug states.
        if (path.Contains("/_apis/wit/", StringComparison.Ordinal)) return AdoMessage.RetrievalStageBugs;
        if (!current) return AdoMessage.RetrievalStageHistory;
        if (path.EndsWith("/_apis/test/runs", StringComparison.Ordinal)) return AdoMessage.RetrievalStageRuns;
        return path.EndsWith("/results", StringComparison.Ordinal) ? AdoMessage.RetrievalStageResults : AdoMessage.RetrievalStageDetails;
    }
}
