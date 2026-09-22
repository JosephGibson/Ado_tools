using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.Tests.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.TestRuns;

public sealed class NamedRunGroupingTests
{
    private static readonly string[] RunNames = ["Synthetic EN 1", "Synthetic FR 1"];
    [Theory]
    [InlineData("en-US", false)]
    [InlineData("fr-CA", false)]
    [InlineData("en-US", true)]
    public async Task NamedRunsSeparateSevenRerunsAndClassifyCurrentAndHistoricalBuilds(string culture, bool secondPasses)
    {
        string secondOutcome = secondPasses ? "Passed" : "Failed";
        TestRunFixture fixture = new TestRunFixture()
            .RouteBody(Runs(401, 301), "/test/runs", "Build%2F401", "%24skip=0&")
            .RouteBody(Runs(400, 201), "/test/runs", "Build%2F400", "%24skip=0&")
            .Route("builds-history.json", "/_apis/build/builds")
            .RouteBody(Results("Failed"), "/Runs/301/results", "%24skip=0&")
            .RouteBody(Results(secondOutcome), "/Runs/302/results", "%24skip=0&")
            .RouteBody(Results("Failed"), "/Runs/201/results", "%24skip=0&")
            .RouteBody(Results(secondOutcome), "/Runs/202/results", "%24skip=0&")
            .RouteBody(Detail("Failed"), "/Runs/301/results/1?")
            .RouteBody(Detail(secondOutcome), "/Runs/302/results/1?")
            .RouteBody(TestRunFixture.EmptyPage, "/attachments");
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(), new TestFailureQuery { HistoryCount = 2 },
            CultureInfo.GetCultureInfo(culture), TestContext.Current.CancellationToken);
        AdoTestFailure failure = Assert.Single(set.Failures);
        Assert.Equal(AdoTestFailureClassification.Failed, failure.Classification);
        Assert.Equal(14, failure.Attempts.Count);
        Assert.Equal(1, set.FailedCount);
        Assert.Equal(0, set.FlakyCount);
        Assert.Equal(2, failure.History.Count);
        Assert.All(failure.History, cell => Assert.Equal(AdoTestHistoryOutcome.Failed, cell.Outcome));
        Assert.All(set.History, summary => Assert.Equal(1, summary.Failed));

        TestFailureReportModel model = TestFailureReportModelBuilder.Build(set, TestFailureReportFixture.Options(culture));
        Assert.Equal(["Synthetic EN 1", "Synthetic FR 1"], model.Grouping.Labels);
        var groups = TestFailureGroups.Of(Assert.Single(model.Failures), model.Grouping);
        Assert.Equal(2, groups.Count);
        Assert.All(groups, group => Assert.Equal(7, group.Attempts.Count));
        Assert.Equal(AdoTestHistoryOutcome.Failed, TestFailureGroups.Status(groups[0].Attempts));
        Assert.Equal(secondPasses ? AdoTestHistoryOutcome.Flaky : AdoTestHistoryOutcome.Failed, TestFailureGroups.Status(groups[1].Attempts));
        string html = TestFailureReportFixture.Render(model);
        Assert.Equal(2, Regex.Count(html, "<details class=\"attempt-group\""));
        Assert.Equal(14, Regex.Count(html, "<details class=\"attempt\""));
        if (!secondPasses) Assert.Equal(2, Regex.Count(html, "<span class=\"group-label\">[^<]+</span> <span class=\"status-badge status-failed\""));
        TestFailureReportValidator.Validate(new StringReader(html), model);
    }

    [Theory]
    [InlineData("Suite", "Suite (attempt 2)", 2, "Stage", "Stage", false)]
    [InlineData("Suite", "Suite (attempt 2)", null, "Stage", "Stage", true)]
    [InlineData("Suite", "Suite (attempt 2)", 3, "Stage", "Stage", true)]
    [InlineData("Other", "Suite (attempt 2)", 2, "Stage", "Stage", true)]
    [InlineData("Suite", "Suite (attempt 2)", 2, "One", "Two", true)]
    [InlineData("Suite 1", "Suite 2", 2, "Stage", "Stage", true)]
    [InlineData(" Suite ", "Suite", null, "Stage", "Stage", false)]
    [InlineData("Suite", "suite", null, "Stage", "Stage", true)]
    [InlineData("Alpha", "Beta", null, null, null, true)]
    [InlineData("", "", null, null, " __default ", false)]
    public void OnlyConfirmedRetrySuffixesMerge(string first, string second, int? attempt, string? firstStage, string? secondStage, bool split)
    {
        AdoTestRun[] runs = [Run(1, first, firstStage, 1), Run(2, second, secondStage, attempt)];
        // Response order must not affect finding the unsuffixed sibling.
        PipelineGrouping grouping = PipelineGrouping.Create(runs.Reverse());
        Assert.Equal(split, grouping.IsGrouped);
        Assert.Equal(split, grouping.KeyOf(1) != grouping.KeyOf(2));
        Assert.Equal(split, grouping.GroupOf(1) != grouping.GroupOf(2));
    }

    [Fact]
    public void SameNamedRunsMergeAndMissingRunsJoinTheUnnamedGroup()
    {
        PipelineGrouping grouping = PipelineGrouping.Create([Run(1, "Same", null, null), Run(2, "Same", null, null), Run(3, "", null, null)], [4]);
        Assert.Equal(["Same", null], grouping.Labels);
        Assert.Equal(grouping.GroupOf(1), grouping.GroupOf(2));
        Assert.Equal(grouping.GroupOf(3), grouping.GroupOf(4));
        Assert.Equal(grouping.KeyOf(3), grouping.KeyOf(4));
    }

    private static AdoTestRun Run(int id, string name, string? stage, int? attempt) => new()
    { Id = id, Name = name, StageName = stage, PipelineAttempt = attempt, BuildId = 401, State = "Completed", TeamProject = TestRunFixture.Project, CollectionUri = TestRunFixture.Connection.CollectionUri };

    private static string Runs(int build, int firstRun) => JsonSerializer.Serialize(new
    {
        count = 2,
        value = RunNames.Select((name, index) => new
        {
            id = firstRun + index, name, state = "Completed", build = new { id = build }, startedDate = "2026-09-14T10:00:00Z",
            pipelineReference = new { stageReference = new { stageName = "SharedStage" }, phaseReference = new { phaseName = "__default" }, jobReference = new { jobName = "__default" } },
        }),
    });

    private static string Results(string outcome) => JsonSerializer.Serialize(new
    { count = 1, value = new[] { new { id = 1, outcome, automatedTestName = "Synthetic.Ui.Checkout", automatedTestStorage = "Synthetic.Tests.dll", resultGroupType = "Rerun" } } });

    private static string Detail(string outcome) => JsonSerializer.Serialize(new
    {
        id = 1, outcome, automatedTestName = "Synthetic.Ui.Checkout", automatedTestStorage = "Synthetic.Tests.dll", resultGroupType = "Rerun",
        subResults = Enumerable.Range(1, 7).Select(id => new { id, sequenceId = id, outcome = id == 7 ? outcome : "Failed" }),
    });
}
