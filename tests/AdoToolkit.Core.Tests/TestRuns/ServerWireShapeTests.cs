using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

// Server 2020 wire shapes observed at work: reference IDs arrive as numeric strings, runs carry
// aggregate counts instead of runStatistics, and failingSince nests its build reference.
public sealed class ServerWireShapeTests
{
    [Theory]
    [InlineData("""{"value":[{"id":201,"build":{"id":401,"name":"Sample reference"}}]}""")]
    [InlineData("""{"value":[{"id":"201","build":{"id":"401","name":"Sample reference"}}]}""")]
    public void ReferenceIdsReadFromNumbersAndNumericStrings(string body)
    {
        TestRunDto run = Assert.Single(JsonSerializer.Deserialize(body, AdoJsonContext.Default.TestRunPageDto)!.Value!);
        Assert.Equal(201, run.Id);
        Assert.Equal(401, run.Build!.Id);
        Assert.Equal("Sample reference", run.Build.Name);
    }

    [Theory]
    [InlineData("""{"value":[{"id":1,"testRun":{"id":"invalid"}}]}""")]
    [InlineData("""{"value":[{"id":1,"testRun":{"id":"12.5"}}]}""")]
    public void NonNumericReferenceIdsStillFail(string body) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(body, AdoJsonContext.Default.TestResultPageDto));

    [Fact]
    public async Task StringTestRunIdsInResultListsParse()
    {
        using FakeHttpMessageHandler handler = new TestRunFixture()
            .RouteBody("""{"count":1,"value":[{"id":1,"outcome":"Failed","testRun":{"id":"201","name":"Web tests"},"testCase":{"id":"1010"}}]}""",
                "/Runs/201/results", "%24skip=0&")
            .Handler();
        using HttpClient client = new(handler);
        IReadOnlyList<TestResultDto> results = await Service(client).GetResultsAsync(TestRunFixture.Project, 201,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(201, Assert.Single(results).TestRun!.Id);
    }

    [Fact]
    public async Task InvalidReferenceIdNamesTheOperationAndJsonPath()
    {
        using FakeHttpMessageHandler handler = new TestRunFixture()
            .RouteBody("""{"count":1,"value":[{"id":1,"outcome":"Failed","testRun":{"id":"invalid"}}]}""",
                "/Runs/201/results", "%24skip=0&")
            .Handler();
        using HttpClient client = new(handler);
        AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() => Service(client)
            .GetResultsAsync(TestRunFixture.Project, 201, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Equal("TestResultsList", error.Operation);
        Assert.Equal("$.value[0].testRun.id", Assert.IsType<JsonException>(error.InnerException).Path);
    }

    [Fact]
    public async Task Server2020RunAggregatesStayInServerTerms()
    {
        using FakeHttpMessageHandler handler = new TestRunFixture().Route("runs-server2020.json", "/test/runs", "%24skip=0&").Handler();
        using HttpClient client = new(handler);
        IReadOnlyList<AdoTestRun> runs = await Service(client).GetRunsAsync(TestRunFixture.Build(),
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal([201, 202], runs.Select(static run => run.Id));
        AdoTestRun run = runs[0];
        Assert.Equal(401, run.BuildId);
        Assert.Equal(5, run.TotalTests);
        Assert.Equal(2, run.PassedTests);
        Assert.Equal(1, run.NotApplicableTests);
        Assert.Equal(2, run.UnanalyzedTests);
        Assert.Equal(0, run.IncompleteTests);
        // Aggregates are not outcomes, so nothing is invented for OutcomeCounts.
        Assert.Empty(run.OutcomeCounts);
    }

    [Fact]
    public async Task RunStatisticsStillFillOutcomeCountsWithoutAggregates()
    {
        using FakeHttpMessageHandler handler = new TestRunFixture().Route("runs-two.json", "/test/runs", "%24skip=0&").Handler();
        using HttpClient client = new(handler);
        IReadOnlyList<AdoTestRun> runs = await Service(client).GetRunsAsync(TestRunFixture.Build(),
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(401, runs[0].BuildId);
        Assert.Equal(1, runs[0].OutcomeCounts["NotExecuted"]);
        Assert.Null(runs[0].PassedTests);
        Assert.Null(runs[0].UnanalyzedTests);
    }

    [Theory]
    [InlineData("""{"id":1,"outcome":"Failed","failingSince":{"date":"2026-09-13T08:00:00Z","build":{"id":"399","number":"20260913.2"}}}""", 399)]
    [InlineData("""{"id":1,"outcome":"Failed","failingSince":{"date":"2026-09-13T08:00:00Z","build":{"id":399}}}""", 399)]
    [InlineData("""{"id":1,"outcome":"Failed","failingSince":{"release":{"id":7}}}""", null)]
    [InlineData("""{"id":1,"outcome":"Failed","failingSince":{"id":399,"name":"20260913.2"}}""", null)]
    [InlineData("""{"id":1,"outcome":"Failed"}""", null)]
    public void FailingSinceReadsItsBuildReference(string body, int? expected)
    {
        TestResultDto result = JsonSerializer.Deserialize(body, AdoJsonContext.Default.TestResultDto)!;
        AdoTestAttempt attempt = TestAttemptMapper.FromResult(result, 201, 1, AdoTestAttemptSource.Single, null,
            TestContext.Current.CancellationToken);
        Assert.Equal(expected, attempt.FailingSinceBuildId);
    }

    [Fact]
    public void AssociatedBugIdsReadFromStrings()
    {
        TestResultDto result = JsonSerializer.Deserialize("""{"id":1,"outcome":"Failed","associatedBugs":[{"id":"2001"},{"id":2002}]}""",
            AdoJsonContext.Default.TestResultDto)!;
        AdoTestAttempt attempt = TestAttemptMapper.FromResult(result, 201, 1, AdoTestAttemptSource.Single, null,
            TestContext.Current.CancellationToken);
        Assert.Equal([2001, 2002], attempt.AssociatedBugIds);
    }

    private static TestRunService Service(HttpClient client) => new(client, TestRunFixture.Connection);
}
