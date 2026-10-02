using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// Server times arrive in UTC, while the export time carries the offset of the machine that ran it.
// Every time in the report is shown in the export's offset, so the build cannot appear to finish
// hours after the report was generated.
public sealed class ReportTimeTests
{
    [Theory]
    [InlineData("en-US", "9/16/2026 9:18 AM", "9/16/2026 9:30 AM", "9/16/2026 9:15 AM", "9/13/2026 9:30 AM", " PM")]
    [InlineData("fr-CA", "2026-09-16 09 h 18", "2026-09-16 09 h 30", "2026-09-16 09 h 15", "2026-09-13 09 h 30", " 13 h ")]
    public void EveryTimeIsShownInTheOffsetOfTheExport(string culture, string finished, string generated, string started, string history,
        string utcOnly)
    {
        TestFailureReportModel model = TestFailureReportModelBuilder.Build(TestFailureReportFixture.Set("failed"), new TestFailureReportOptions
        {
            Culture = culture, SessionCulture = CultureInfo.GetCultureInfo("en-US"), ToolkitVersion = "5.3.0-test", IncludeFlaky = true,
            GeneratedAt = TestFailureReportFixture.Clock.ToOffset(TimeSpan.FromHours(-4)),
        });
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        // The build finished twelve minutes before the export, the run started three minutes earlier.
        Assert.Contains(finished, html, StringComparison.Ordinal);
        Assert.Contains("<dd>" + generated + "</dd>", html, StringComparison.Ordinal);
        Assert.Contains("<dd>" + started + "</dd>", html, StringComparison.Ordinal);
        Assert.Contains(history, html, StringComparison.Ordinal);
        Assert.DoesNotContain(utcOnly, html, StringComparison.Ordinal);
    }

    // A date that was never set arrives as year 1. Shifted to an offset west of UTC it would leave
    // the calendar, so it keeps its own offset.
    [Fact]
    public void ATimeAtTheLimitOfTheCalendarKeepsItsOwnOffset()
    {
        AdoBuildTestFailureSet source = TestFailureReportFixture.Set("failed");
        AdoTestRun run = source.Runs[0];
        TestFailureReportModel model = TestFailureReportModelBuilder.Build(new AdoBuildTestFailureSet
        {
            Build = source.Build, Summary = source.Summary, History = source.History, Failures = source.Failures, FailedCount = source.FailedCount,
            Status = source.Status, RetrievedAt = source.RetrievedAt, CollectionUri = source.CollectionUri,
            Runs = [new AdoTestRun { Id = run.Id, Name = run.Name, BuildId = run.BuildId, State = run.State, StartedDate = DateTimeOffset.MinValue,
                TeamProject = run.TeamProject, CollectionUri = run.CollectionUri }],
        }, new TestFailureReportOptions
        {
            Culture = "en-US", SessionCulture = CultureInfo.GetCultureInfo("en-US"), ToolkitVersion = "5.3.0-test", IncludeFlaky = true,
            GeneratedAt = TestFailureReportFixture.Clock.ToOffset(TimeSpan.FromHours(-4)),
        });
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.Contains(DateTimeOffset.MinValue.ToString("g", CultureInfo.GetCultureInfo("en-US")), html, StringComparison.Ordinal);
    }
}
