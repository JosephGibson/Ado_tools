using System.Net.Http;
using System.Text.Json;
using AdoToolkit.Core.Http;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

// Server 2020 wire shapes observed at work: reference IDs arrive as numeric strings, runs carry
// aggregate counts instead of runStatistics, and failingSince nests its build reference.
[Trait("Culture", "Invariant")]
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
    [InlineData("""{"id":1,"testRun":{"id":"invalid"}}""")]
    [InlineData("""{"id":1,"testRun":{"id":"12.5"}}""")]
    public void NonNumericReferenceIdsStillFail(string body) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(body, AdoJsonContext.Default.TestResultDto));

    [Fact]
    public async Task StringTestRunIdsInResultDetailsParse()
    {
        using FakeHttpMessageHandler handler = new TestRunFixture()
            .RouteBody("""{"id":1,"outcome":"Failed","testRun":{"id":"201","name":"Web tests"},"testCase":{"id":"1010"}}""", "/Runs/201/results/1?")
            .Handler();
        using HttpClient client = new(handler);
        TestResultDto result = await Service(client).GetResultAsync(TestRunFixture.Project, 201, 1,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Equal(201, result.TestRun!.Id);
    }

    [Fact]
    public async Task InvalidReferenceIdNamesTheOperationAndJsonPath()
    {
        using FakeHttpMessageHandler handler = new TestRunFixture()
            .RouteBody("""{"id":1,"outcome":"Failed","testRun":{"id":"invalid"}}""", "/Runs/201/results/1?")
            .Handler();
        using HttpClient client = new(handler);
        AdoResponseFormatException error = await Assert.ThrowsAsync<AdoResponseFormatException>(() => Service(client)
            .GetResultAsync(TestRunFixture.Project, 201, 1, CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Equal("TestResultGet", error.Operation);
        Assert.Equal("$.testRun.id", Assert.IsType<JsonException>(error.InnerException).Path);
    }

    // Pass 1 reads the fields of §15.9 step 3, and the error message that history compares (D-8),
    // and skips every other field of a listed result unread, so a field it does not use can neither
    // fail the listing nor cost its parsing.
    [Fact]
    public async Task ResultListingsReadOnlyThePass1FieldsAndTheErrorMessage()
    {
        using FakeHttpMessageHandler handler = new TestRunFixture()
            .RouteBody("""
                {"count":1,"value":[{"id":1,"outcome":"Failed","automatedTestName":"Contoso.Web.Tests.CartTests.AddsItem",
                "automatedTestStorage":"Contoso.Web.Tests.dll","testCaseTitle":"Adds an item","resultGroupType":"rerun",
                "startedDate":"2026-09-15T10:00:00Z","testCase":{"id":"1010"},"testRun":{"id":"invalid"},"durationInMs":"slow","owner":42,
                "errorMessage":"Expected:<3>. Actual:<2>.\r\nat line 2"}]}
                """, "/Runs/201/results", "%24skip=0&")
            .Handler();
        using HttpClient client = new(handler);
        TestResultListingDto result = Assert.Single(await Service(client).GetResultsAsync(TestRunFixture.Project, 201,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Equal((1, "Failed", "Contoso.Web.Tests.CartTests.AddsItem", "Contoso.Web.Tests.dll", "Adds an item", "rerun"),
            (result.Id, result.Outcome, result.AutomatedTestName, result.AutomatedTestStorage, result.TestCaseTitle, result.ResultGroupType));
        Assert.Equal(new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero), result.StartedDate);
        Assert.Equal("1010", result.TestCase!.Id);
        Assert.Equal("Expected:<3>. Actual:<2>.\r\nat line 2", result.ErrorMessage);
        Assert.Equal(9, typeof(TestResultListingDto).GetProperties().Length);
    }

    // An error message that is not a string reads as no message: one odd value must not fail the
    // listing, and with it the build or its history.
    [Theory]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("""{"text":"Expected 1"}""")]
    [InlineData("""["Expected 1"]""")]
    [InlineData("null")]
    // A string that cannot be read: half of a surrogate pair, escaped.
    [InlineData("\"Expected \\ud83d\"")]
    public async Task AnErrorMessageThatIsNotAStringReadsAsNone(string value)
    {
        using FakeHttpMessageHandler handler = new TestRunFixture()
            .RouteBody("""{"count":1,"value":[{"id":1,"outcome":"Failed","errorMessage":""" + value + ""","automatedTestName":"Contoso.A"}]}""",
                "/Runs/201/results", "%24skip=0&")
            .Handler();
        using HttpClient client = new(handler);
        TestResultListingDto result = Assert.Single(await Service(client).GetResultsAsync(TestRunFixture.Project, 201,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken));
        Assert.Null(result.ErrorMessage);
        Assert.Equal("Contoso.A", result.AutomatedTestName);
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
