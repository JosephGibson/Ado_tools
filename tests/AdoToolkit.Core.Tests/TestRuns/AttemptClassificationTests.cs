using System.Net.Http;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

[Trait("Acceptance", "S5-2")]
public sealed class AttemptClassificationTests
{
    private static readonly TestFailureQuery NoHistory = new() { HistoryCount = 1 };

    // Test results fixture 3: in-task rerun groups.
    [Fact]
    public async Task InTaskRerunFailThenPassIsFlakyWithEveryAttemptKept()
    {
        AdoBuildTestFailureSet set = await RerunSetAsync();
        AdoTestFailure flaky = Assert.Single(set.Failures, failure => failure.ShortName == "FlakyOnce");
        Assert.Equal(AdoTestFailureClassification.Flaky, flaky.Classification);
        Assert.Equal([1, 2], flaky.Attempts.Select(static attempt => attempt.Number));
        Assert.Equal(["Failed", "Passed"], flaky.Attempts.Select(static attempt => attempt.Outcome));
        Assert.All(flaky.Attempts, static attempt =>
        {
            Assert.Equal(AdoTestAttemptSource.Rerun, attempt.Source);
            Assert.Equal(201, attempt.RunId);
            Assert.Equal(21, attempt.ResultId);
        });
        Assert.Equal([1, 2], flaky.Attempts.Select(static attempt => attempt.SubResultId));
        Assert.Equal("Assert.AreEqual failed. Expected:<1>. Actual:<2>.", flaky.Attempts[0].ErrorMessage);
        Assert.Contains("RetryTests.cs:line 42", flaky.Attempts[0].StackTrace!, StringComparison.Ordinal);
        Assert.Null(flaky.Attempts[1].ErrorMessage);
        Assert.Equal(TimeSpan.FromMilliseconds(1200.5), flaky.Attempts[0].Duration);
        // Parent-level metadata is inherited by rerun attempts.
        Assert.All(flaky.Attempts, static attempt => Assert.Equal("AGENT-01", attempt.ComputerName));
    }

    [Fact]
    public async Task RerunAttemptsAreOrderedBySequenceIdNotResponseOrder()
    {
        AdoBuildTestFailureSet set = await RerunSetAsync();
        AdoTestFailure flaky = Assert.Single(set.Failures, failure => failure.ShortName == "FlakyTwice");
        Assert.Equal(AdoTestFailureClassification.Flaky, flaky.Classification);
        // The fixture lists sequence 3 first; attempts still run 1, 2, 3.
        Assert.Equal([4, 5, 6], flaky.Attempts.Select(static attempt => attempt.SubResultId));
        Assert.Equal(["Failed", "Failed", "Passed"], flaky.Attempts.Select(static attempt => attempt.Outcome));
    }

    [Fact]
    public async Task RerunGroupThatNeverPassesIsFailed()
    {
        AdoBuildTestFailureSet set = await RerunSetAsync();
        AdoTestFailure failed = Assert.Single(set.Failures, failure => failure.ShortName == "AlwaysFails");
        Assert.Equal(AdoTestFailureClassification.Failed, failed.Classification);
        Assert.Equal(3, failed.Attempts.Count);
        Assert.All(failed.Attempts, static attempt =>
        {
            Assert.Equal(AdoTestOutcomeClass.Failure, attempt.OutcomeClass);
            Assert.Equal("Regression", attempt.FailureType);
            Assert.Equal("NeedsInvestigation", attempt.ResolutionState);
        });
        // Report order puts the Failed identity before both Flaky ones.
        Assert.Equal(1, failed.Ordinal);
        Assert.Equal(["AlwaysFails", "FlakyOnce", "FlakyTwice"], set.Failures.Select(static item => item.ShortName));
        Assert.Equal(1, set.FailedCount);
        Assert.Equal(2, set.FlakyCount);
        // Flaky identities count as passed in the bars, with a separate flaky count (Q-23).
        Assert.Equal(2, set.Summary.Passed);
        Assert.Equal(2, set.Summary.Flaky);
        Assert.Equal(1, set.Summary.Failed);
        Assert.Equal(0, set.Summary.Other);
    }

    // Test results fixture 4: a job re-attempt in a second run of the same build.
    [Fact]
    public async Task JobReAttemptFailThenPassIsFlakyAcrossRuns()
    {
        AdoBuildTestFailureSet set = await ReAttemptSetAsync();
        AdoTestFailure flaky = Assert.Single(set.Failures, failure => failure.ShortName == "JobRetry");
        Assert.Equal(AdoTestFailureClassification.Flaky, flaky.Classification);
        // Runs order by pipeline attempt even though the listing returned attempt 2 first.
        Assert.Equal([301, 302], set.Runs.Select(static run => run.Id));
        Assert.Equal([1, 2], set.Runs.Select(static run => run.PipelineAttempt));
        // A re-attempt keeps its pipeline names, so both runs stay one group and the test is flaky.
        Assert.All(set.Runs, static run => Assert.Equal(("Tests", "Web", "__default"), (run.StageName, run.PhaseName, run.JobName)));
        Assert.Equal([301, 302], flaky.Attempts.Select(static attempt => attempt.RunId));
        Assert.Equal(["Failed", "Passed"], flaky.Attempts.Select(static attempt => attempt.Outcome));
        Assert.All(flaky.Attempts, static attempt =>
        {
            Assert.Equal(AdoTestAttemptSource.RunAttempt, attempt.Source);
            Assert.Null(attempt.SubResultId);
        });
        Assert.Equal(["AGENT-01", "AGENT-04"], flaky.Attempts.Select(static attempt => attempt.ComputerName));
    }

    // English and French run in separate stages: a French failure is never hidden by a later English
    // pass. Without distinct names the last attempt still decides (§15.10).
    [Fact]
    public void AFailureInOnePipelineGroupIsNotHiddenByALaterPassInAnother()
    {
        static TestIdentityGroup Group(params (string Key, string Outcome)[] records) => new()
        {
            Identity = TestIdentity.ForAutomated("Synthetic.Tests.dll", "Synthetic.Localized"),
            Records = [.. records.Select((record, index) => new TestResultRecord
            {
                RunId = 301 + index, ResultId = 1, RunOrder = index + 1, PipelineKey = record.Key, Outcome = record.Outcome, AutomatedTestName = "Synthetic.Localized",
            })],
        };
        TestIdentityGroup split = Group(("en", "Failed"), ("fr", "Failed"), ("en", "Passed"));
        Assert.Equal(AdoTestFailureClassification.Failed, split.ProvisionalClassification);
        Assert.Equal(AdoTestHistoryOutcome.Failed, split.Cell);
        Assert.Equal(AdoTestFailureClassification.Flaky, Group(("en", "Failed"), ("fr", "Passed"), ("en", "Passed")).ProvisionalClassification);
        Assert.Equal(AdoTestFailureClassification.Flaky, Group(("", "Failed"), ("", "Failed"), ("", "Passed")).ProvisionalClassification);
        Assert.Equal(AdoTestOutcomeClass.Failure, OutcomeClassifier.Deciding([("en", AdoTestOutcomeClass.Pass), ("fr", AdoTestOutcomeClass.Failure)]));
        Assert.Equal(AdoTestOutcomeClass.Other, OutcomeClassifier.Deciding([("en", AdoTestOutcomeClass.Pass), ("fr", AdoTestOutcomeClass.Other)]));
    }

    // Test results fixture 5: both sources combined for one identity, run first then sub-result.
    [Fact]
    public async Task CombinedRerunAndReAttemptOrderByRunThenSubResult()
    {
        AdoBuildTestFailureSet set = await ReAttemptSetAsync();
        AdoTestFailure combined = Assert.Single(set.Failures, failure => failure.ShortName == "Combined");
        Assert.Equal(AdoTestFailureClassification.Flaky, combined.Classification);
        Assert.Equal([1, 2, 3], combined.Attempts.Select(static attempt => attempt.Number));
        Assert.Equal([301, 301, 302], combined.Attempts.Select(static attempt => attempt.RunId));
        Assert.Equal([71, 72, null], combined.Attempts.Select(static attempt => attempt.SubResultId));
        Assert.Equal([AdoTestAttemptSource.Rerun, AdoTestAttemptSource.Rerun, AdoTestAttemptSource.RunAttempt],
            combined.Attempts.Select(static attempt => attempt.Source));
        Assert.Equal(["Failed", "Failed", "Passed"], combined.Attempts.Select(static attempt => attempt.Outcome));
    }

    [Fact]
    public async Task TestThatOnlyPassedIsCountedButNotReported()
    {
        TestRunFixture fixture = new();
        fixture.Route("runs-two.json", "/test/runs", "%24skip=0&")
            .RouteBody(PassedOnly, "/Runs/201/results", "%24skip=0&")
            .RouteBody(TestRunFixture.EmptyPage, "/Runs/202/results");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), NoHistory,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        Assert.Empty(set.Failures);
        Assert.Equal(1, set.Summary.Passed);
        Assert.Equal(0, set.Summary.Flaky);
        Assert.Equal(AdoTestFailureStatus.Complete, set.Status);
        // No detail request is made when nothing is a candidate.
        Assert.DoesNotContain(handler.Requests, static request =>
            request.Uri.Query.Contains("detailsToInclude=Iterations", StringComparison.Ordinal));
    }

    // Test results fixture 6: data-driven sub-results are never attempts.
    [Fact]
    public async Task DataDrivenSubResultsNestInsideTheAttemptAndIterationsAreMapped()
    {
        TestRunFixture fixture = new();
        fixture.Route("runs-two.json", "/test/runs", "%24skip=0&")
            .RouteBody(DataDrivenList, "/Runs/201/results", "%24skip=0&")
            .RouteBody(TestRunFixture.EmptyPage, "/Runs/202/results")
            .Route("result-detail-datadriven.json", "/Runs/201/results/31?")
            .Route("attachments-empty.json", "/attachments");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), NoHistory,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
        AdoTestFailure failure = Assert.Single(set.Failures);
        AdoTestAttempt attempt = Assert.Single(failure.Attempts);
        Assert.Equal(AdoTestAttemptSource.Single, attempt.Source);
        Assert.Equal([41, 42], attempt.SubResults.Select(static sub => sub.Id));
        Assert.Equal(["Passed", "Failed"], attempt.SubResults.Select(static sub => sub.Outcome));
        Assert.Equal("Row 2 failed.", attempt.SubResults[1].ErrorMessage);
        // Nested groups keep nesting, up to three levels.
        AdoTestSubResult nested = Assert.Single(attempt.SubResults[1].SubResults);
        Assert.Equal(43, nested.Id);
        Assert.Empty(attempt.SubResults[0].SubResults);
        Assert.Equal([1, 2], attempt.Iterations.Select(static iteration => iteration.Id));
        Assert.Equal("value", Assert.Single(attempt.Iterations[1].Parameters).Name);
        Assert.Equal("2", attempt.Iterations[1].Parameters[0].Value);
        Assert.Equal("Step failed.", Assert.Single(attempt.Iterations[1].ActionResults).ErrorMessage);
        Assert.Equal("00000002", attempt.Iterations[1].ActionResults[0].ActionPath);
    }

    // Dots inside a data-driven test's arguments are not name separators.
    [Theory]
    [InlineData("Contoso.Web.Tests.LoginTests.SignIn", "SignIn", "LoginTests")]
    [InlineData("Contoso.Web.Tests.LoginTests.SignIn(\"user@example.test\",3.5)", "SignIn(\"user@example.test\",3.5)", "LoginTests")]
    [InlineData("Contoso.Web.Tests.Outer+Inner.Check", "Check", "Inner")]
    [InlineData("SignIn(\"a.b\")", "SignIn(\"a.b\")", null)]
    [InlineData("Contoso.Web.", "Contoso.Web.", "Web")]
    public void ShortAndClassNamesIgnoreSeparatorsInsideArguments(string name, string shortName, string? className)
    {
        Assert.Equal(shortName, AttemptGrouper.ShortName(name, "Title"));
        Assert.Equal(className, AttemptGrouper.ClassName(name));
    }

    private static async Task<AdoBuildTestFailureSet> RerunSetAsync()
    {
        TestRunFixture fixture = new();
        fixture.Route("runs-two.json", "/test/runs", "%24skip=0&")
            .Route("results-rerun.json", "/Runs/201/results", "%24skip=0&")
            .RouteBody(TestRunFixture.EmptyPage, "/Runs/202/results")
            .Route("result-detail-rerun-flaky.json", "/Runs/201/results/21?")
            .Route("result-detail-rerun-flaky3.json", "/Runs/201/results/22?")
            .Route("result-detail-rerun-failed.json", "/Runs/201/results/23?")
            .Route("attachments-empty.json", "/attachments");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        return await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), NoHistory,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
    }

    private static async Task<AdoBuildTestFailureSet> ReAttemptSetAsync()
    {
        TestRunFixture fixture = new();
        fixture.Route("runs-reattempt.json", "/test/runs", "%24skip=0&")
            .Route("results-run-301.json", "/Runs/301/results", "%24skip=0&")
            .Route("results-run-302.json", "/Runs/302/results", "%24skip=0&")
            .Route("result-detail-301-51.json", "/Runs/301/results/51?")
            .Route("result-detail-301-52.json", "/Runs/301/results/52?")
            .Route("result-detail-302-61.json", "/Runs/302/results/61?")
            .Route("result-detail-302-62.json", "/Runs/302/results/62?")
            .Route("attachments-empty.json", "/attachments");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        return await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), NoHistory,
            CultureInfo.InvariantCulture, TestContext.Current.CancellationToken);
    }

    private const string PassedOnly = "{\"count\":1,\"value\":[{\"id\":201,\"outcome\":\"Passed\","
        + "\"automatedTestName\":\"Contoso.Web.Tests.CartTests.Passes\","
        + "\"automatedTestStorage\":\"Contoso.Web.Tests.dll\"}]}";

    private const string DataDrivenList = "{\"count\":1,\"value\":[{\"id\":31,\"outcome\":\"Failed\","
        + "\"resultGroupType\":\"DataDriven\",\"automatedTestName\":\"Contoso.Web.Tests.DataTests.Rows\","
        + "\"automatedTestStorage\":\"Contoso.Web.Tests.dll\",\"testCaseTitle\":\"Data rows\"}]}";
}
