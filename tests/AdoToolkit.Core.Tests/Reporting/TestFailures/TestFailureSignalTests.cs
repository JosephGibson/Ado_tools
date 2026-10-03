using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// New and recurring failures, read from a test's history cells alone: no request is needed. Cells
// are written oldest first, the last one being this build: P passed, F failed, Y flaky, N not run,
// O another outcome, U unavailable.
public sealed class TestFailureSignalTests
{
    [Theory]
    // The build before this one ran the test, and it did not fail: new.
    [InlineData("P F", true, 1, null, AdoTestHistoryOutcome.Failed)]
    [InlineData("F P Y", true, 1, null, AdoTestHistoryOutcome.Flaky)]
    // Failed or flaky in the builds just before: a streak, since the first of them, counted as the
    // History by test table counts it.
    [InlineData("P F F", false, 2, "b1", AdoTestHistoryOutcome.Failed)]
    [InlineData("F Y F", false, 3, "b0", AdoTestHistoryOutcome.Failed)]
    [InlineData("U F Y", false, 2, "b1", AdoTestHistoryOutcome.Flaky)]
    public void ATestIsNewOrFailsAgain(string cells, bool isNew, int streak, string? since, AdoTestHistoryOutcome current)
    {
        TestFailureSignal signal = Assert.IsType<TestFailureSignal>(TestFailureSignal.Of(History(cells)));
        Assert.Equal(new TestFailureSignal(isNew, streak, since, current), signal);
        Assert.Equal(streak, TestFailureSignal.StreakOf(History(cells)));
    }

    [Theory]
    // No earlier build.
    [InlineData("F")]
    // The previous build is unavailable, even after an older failure.
    [InlineData("U F")]
    [InlineData("F U F")]
    // The previous build did not run the test, or ended it another way.
    [InlineData("N F")]
    [InlineData("O F")]
    public void NothingIsSaidWithoutAComparablePreviousBuild(string cells) => Assert.Null(TestFailureSignal.Of(History(cells)));

    // The failed fixture's test failed in build 20260916.2 and again in this one; the build before
    // those did not run it.
    [Theory]
    [InlineData("en-US", "2 in a row", "2 in a row since 20260916.2", "Failing since")]
    [InlineData("fr-CA", "2 fois de suite", "2 fois de suite depuis 20260916.2", "En échec depuis")]
    public void RowsAndTheCardSayHowLongATestHasFailed(string culture, string row, string card, string failingSince)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("failed", culture);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        foreach (string view in new[] { "overview", "by-error", "bugs" })
            Assert.Contains("<span class=\"test-name\" title=\"SubmitOrder\">SubmitOrder</span></a> <span class=\"signal status-failed\"><span aria-hidden=\"true\">✕</span> "
                + row + "</span>", View(html, view), StringComparison.Ordinal);
        string cardHtml = Section(html, "<article class=\"card failure-card\"", "</article>");
        Assert.Contains("</ol><span class=\"history-signal status-failed\"><span aria-hidden=\"true\">✕</span> " + card + "</span></div>", cardHtml, StringComparison.Ordinal);
        // Failing since names a build of the history window by its number, not by its ID.
        Assert.Matches("<dt>" + failingSince + "</dt><dd><a rel=\"noreferrer\" href=\"[^\"]+buildId=400\">20260916\\.2 <span role=\"img\"", cardHtml);
    }

    [Fact]
    public void ANewFailureSaysNewAndAnUnreadPreviousBuildSaysNothing()
    {
        string html = Render(History("P F"), failingSince: 999);
        Assert.Contains("</a> <span class=\"signal status-failed\"><span aria-hidden=\"true\">✕</span> New</span>", View(html, "overview"), StringComparison.Ordinal);
        Assert.Contains("<span class=\"history-signal status-failed\"><span aria-hidden=\"true\">✕</span> New</span>", html, StringComparison.Ordinal);
        // A build outside the history window keeps its ID.
        Assert.Matches("<dt>Failing since</dt><dd><a rel=\"noreferrer\" href=\"[^\"]+buildId=999\">999 <span role=\"img\"", html);
        string unread = Render(History("U F"), failingSince: 400);
        Assert.DoesNotContain("class=\"signal", unread, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"history-signal", unread, StringComparison.Ordinal);
    }

    // The failed fixture with its one test given these history cells and this Failing since build.
    private static string Render(IReadOnlyList<AdoTestHistoryEntry> history, int failingSince)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("failed");
        AdoTestFailure original = model.Failures[0];
        AdoTestFailure failure = new()
        {
            Ordinal = original.Ordinal, Classification = original.Classification, TestName = original.TestName, ShortName = original.ShortName,
            Storage = original.Storage, CollectionUri = original.CollectionUri, Bugs = original.Bugs, History = history,
            Attempts = [.. original.Attempts.Select(attempt => new AdoTestAttempt
            {
                Number = attempt.Number, RunId = attempt.RunId, ResultId = attempt.ResultId, Outcome = attempt.Outcome, OutcomeClass = attempt.OutcomeClass,
                ErrorMessage = attempt.ErrorMessage, FailingSinceBuildId = failingSince,
            })],
        };
        model = TestFailureReportModelBuilder.WithAttachments(model, [failure], [], null);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        return html;
    }

    private static AdoTestHistoryEntry[] History(string cells)
    {
        string[] codes = cells.Split(' ');
        return [.. codes.Select((code, index) => new AdoTestHistoryEntry
        {
            BuildId = 400 + index - codes.Length + 1, BuildNumber = "b" + index.ToString(CultureInfo.InvariantCulture), IsCurrent = index == codes.Length - 1,
            WebUrl = TestFailureReportFixture.Untrusted,
            Outcome = code switch
            {
                "P" => AdoTestHistoryOutcome.Passed, "F" => AdoTestHistoryOutcome.Failed, "Y" => AdoTestHistoryOutcome.Flaky,
                "N" => AdoTestHistoryOutcome.NotRun, "O" => AdoTestHistoryOutcome.Other, _ => AdoTestHistoryOutcome.Unavailable,
            },
        })];
    }

    private static string View(string html, string id) => Section(html, "<section class=\"view\" id=\"" + id + "\" data-view>", "</section>");

    private static string Section(string html, string start, string end)
    {
        int position = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(position >= 0, start);
        return html[position..html.IndexOf(end, position + start.Length, StringComparison.Ordinal)];
    }
}
