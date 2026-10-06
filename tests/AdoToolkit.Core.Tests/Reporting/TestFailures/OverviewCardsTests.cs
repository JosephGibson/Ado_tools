using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// The Overview's cards under its table describe the whole build: new and recurring tests, the most
// common errors, the tests without an open bug and, in a grouped build, each group. A card that would
// be empty is left out, and the filters act on the table only.
public sealed class OverviewCardsTests
{
    [Theory]
    [InlineData("en-US", "New and recurring", "Most common errors", "Without an open bug", "By group")]
    [InlineData("fr-CA", "Nouveaux et récurrents", "Erreurs les plus fréquentes", "Sans bogue ouvert", "Par groupe")]
    public void TheLargeBuildHasEveryCardAfterTheTable(string culture, params string[] headings)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("large", culture);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        string overview = Overview(html);
        Assert.Contains("</tbody></table></div>\n<div class=\"glance\">\n", overview, StringComparison.Ordinal);
        Assert.Equal(headings, Regex.Matches(Glance(overview), "<div class=\"glance-panel\"><h3>([^<]+)</h3>").Select(static m => m.Groups[1].Value));
        // The cards read the build, not the filters: nothing in them is a row the script can hide.
        Assert.DoesNotContain("data-index-for", Glance(overview), StringComparison.Ordinal);
    }

    // Four tests are new, ten recur and two have no comparison: one count per line, then the shares.
    [Fact]
    public void NewAndRecurringCountsEachTrendAndDrawsTheShares()
    {
        string card = Panel(Overview(TestFailureReportFixture.Render("large")), "New and recurring");
        Assert.Contains("<ul class=\"glance-counts\"><li><span class=\"trend trend-new\"><span aria-hidden=\"true\">✦</span> New</span> <strong>4</strong></li>"
            + "<li><span class=\"trend trend-since\">Recurring</span> <strong>10</strong></li><li><span class=\"trend trend-none\">No comparison</span> <strong>2</strong></li></ul>",
            card, StringComparison.Ordinal);
        Assert.Contains("<div class=\"glance-bar\" aria-hidden=\"true\"><span class=\"bar-new\" style=\"--share: 25%\"></span><span class=\"bar-recurring\" style=\"--share: 62.5%\">"
            + "</span><span class=\"bar-none\" style=\"--share: 12.5%\"></span></div>", card, StringComparison.Ordinal);
        // A share is a CSS value: the invariant culture, whatever the report's.
        Assert.Contains("--share: 62.5%", Overview(TestFailureReportFixture.Render("large", "fr-CA")), StringComparison.Ordinal);
    }

    // The three largest clusters of two tests or more, each with its type and line, opening it in By error.
    [Fact]
    public void MostCommonErrorsLinkTheThreeLargestClusters()
    {
        string card = Panel(Overview(TestFailureReportFixture.Render("large")), "Most common errors");
        Assert.Equal(["4 #e-1 TimeoutException", "3 #e-2 FileNotFoundException", "2 #e-3 ApiException"],
            Regex.Matches(card, "<li><span class=\"glance-count\">([0-9]+)</span> <a class=\"glance-error\" href=\"(#e-[0-9]+)\"><code class=\"exception-type\">([^<]+)</code> ")
                .Select(static m => m.Groups[1].Value + " " + m.Groups[2].Value + " " + m.Groups[3].Value));
        Assert.Contains("<span class=\"glance-line\">Timed out after 3000 ms waiting for #submit</span></a></li>", card, StringComparison.Ordinal);
    }

    // The errors that the most tests had in any failed attempt: AddToCart had CheckTitle's error once,
    // so that error counts two tests although it is the primary error of one. Generic errors stay out.
    [Fact]
    public void MostCommonErrorsCountEveryTestThatHadTheError()
    {
        string overview = Overview(TestFailureReportFixture.Render("bilingual"));
        Assert.Equal(["2 #e-1"], Regex.Matches(Panel(overview, "Most common errors"), "<li><span class=\"glance-count\">([0-9]+)</span> <a class=\"glance-error\" href=\"(#e-[0-9]+)\">")
            .Select(static m => m.Groups[1].Value + " " + m.Groups[2].Value));
        Assert.Equal(["New and recurring", "Most common errors", "Generic errors", "Without an open bug", "By group"],
            Regex.Matches(Glance(overview), "<div class=\"glance-panel\"><h3>([^<]+)</h3>").Select(static m => m.Groups[1].Value));
    }

    // The tests with a generic error, and among them those with no other error; the card opens the
    // generic errors in By error, and is left out without them.
    [Theory]
    [InlineData("en-US", "Generic errors", "With a generic error", "Only generic errors")]
    [InlineData("fr-CA", "Erreurs génériques", "Avec erreur générique", "Erreurs génériques seulement")]
    public void GenericErrorsCountTheTestsTheyReached(string culture, string heading, string with, string only)
    {
        string card = Panel(Overview(TestFailureReportFixture.Render("bilingual", culture)), heading);
        Assert.Contains("<ul class=\"glance-counts\"><li><a href=\"#generic-errors\">" + with + "</a> <strong>3</strong></li><li><span>" + only
            + "</span> <strong>2</strong></li></ul></div>", card, StringComparison.Ordinal);
        Assert.DoesNotContain("<h3>Generic errors</h3>", Overview(TestFailureReportFixture.Render("large")), StringComparison.Ordinal);
    }

    // The tests that most need a bug: the longest failing first, then the new ones. An unread bug is
    // not an open bug, so GetOrder, whose only bug could not be read, leads.
    [Fact]
    public void WithoutAnOpenBugListsTheTestsThatMostNeedOne()
    {
        string card = Panel(Overview(TestFailureReportFixture.Render("large")), "Without an open bug");
        Assert.Contains("<p class=\"glance-note\">10 of 16 tests</p>", card, StringComparison.Ordinal);
        Assert.Equal(["GetOrder", "ApplyCoupon", "CancelOrder"], Regex.Matches(card, "<a class=\"glance-name\" href=\"#f-[0-9]+\" title=\"([^\"]+)\">").Select(static m => m.Groups[1].Value));
        Assert.Equal(3, Regex.Count(card, "</a> <a class=\"trend trend-since\""));
        Assert.Contains("<p class=\"glance-more\"><a href=\"#no-bug\">All tests without an open bug</a></p>", card, StringComparison.Ordinal);
        Assert.Contains("<tbody class=\"error-cluster\" id=\"no-bug\">", TestFailureReportFixture.Render("large"), StringComparison.Ordinal);
    }

    // Per group, the tests that ended failed or flaky in it and those that failed there alone.
    [Fact]
    public void ByGroupCountsEachGroupOfAGroupedBuild()
    {
        string card = Panel(Overview(TestFailureReportFixture.Render("large")), "By group");
        Assert.Contains("<tr><th scope=\"row\">Tests_EN</th><td class=\"num\">12</td><td class=\"num\">1</td><td class=\"num\">5</td></tr>", card, StringComparison.Ordinal);
        Assert.Contains("<tr><th scope=\"row\">Tests_FR</th><td class=\"num\">9</td><td class=\"num\">1</td><td class=\"num\">2</td></tr>", card, StringComparison.Ordinal);
        // The grouped fixture: SubmitOrder is flaky in English and fails in French alone; ShowBanner fails
        // in English alone. A zero is muted.
        string grouped = Panel(Overview(TestFailureReportFixture.Render("grouped")), "By group");
        Assert.Contains("<tr><th scope=\"row\">Tests_EN</th><td class=\"num\">1</td><td class=\"num\">1</td><td class=\"num\">1</td></tr>", grouped, StringComparison.Ordinal);
        Assert.Contains("<tr><th scope=\"row\">Tests_FR</th><td class=\"num\">1</td><td class=\"num text-muted\">0</td><td class=\"num\">1</td></tr>", grouped, StringComparison.Ordinal);
    }

    // Each card is left out when it would be empty, and with every card the row of cards.
    [Fact]
    public void ACardThatWouldBeEmptyIsLeftOut()
    {
        // The failed fixture: a recurring test with an open bug and its own error.
        string failed = Overview(TestFailureReportFixture.Render("failed"));
        Assert.Equal(["New and recurring", "Without an open bug"], Regex.Matches(Glance(failed), "<h3>([^<]+)</h3>").Select(static m => m.Groups[1].Value));
        // Every test has an open bug: the card says so and lists none.
        Assert.Contains("<p class=\"glance-note\">0 of 1 tests</p></div>", Panel(failed, "Without an open bug"), StringComparison.Ordinal);
        Assert.DoesNotContain("<li><span class=\"trend trend-none\">", failed, StringComparison.Ordinal);

        // No history, no bug, distinct errors and no groups: no card at all.
        TestFailureReportModel bare = Model(Failure(1, "A", "Failed A"), Failure(2, "B", "Failed B"));
        string html = TestFailureReportFixture.Render(bare);
        TestFailureReportValidator.Validate(new StringReader(html), bare);
        Assert.DoesNotContain("class=\"glance", html, StringComparison.Ordinal);
        // The same two tests with one error: Most common errors alone.
        TestFailureReportModel shared = Model(Failure(1, "A", "Timed out after 10 ms"), Failure(2, "B", "Timed out after 20 ms"));
        Assert.Equal(["Most common errors"], Regex.Matches(Glance(Overview(TestFailureReportFixture.Render(shared))), "<h3>([^<]+)</h3>").Select(static m => m.Groups[1].Value));
        // No test: no table and no card.
        Assert.DoesNotContain("class=\"glance", Overview(TestFailureReportFixture.Render(Model())), StringComparison.Ordinal);
    }

    private static AdoTestFailure Failure(int ordinal, string name, string message) => new()
    {
        Ordinal = ordinal, Classification = AdoTestFailureClassification.Failed, ShortName = name, TestName = "Synthetic.GlanceTests." + name, Storage = "Synthetic.Tests.dll",
        CollectionUri = TestFailureReportFixture.Collection,
        Attempts = [new AdoTestAttempt { Number = 1, RunId = 201, ResultId = 10 + ordinal, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure, ErrorMessage = message }],
    };

    private static TestFailureReportModel Model(params AdoTestFailure[] failures)
    {
        AdoBuildTestFailureSet source = TestFailureReportFixture.Set("failed");
        return TestFailureReportModelBuilder.Build(new AdoBuildTestFailureSet
        {
            Build = source.Build, Runs = source.Runs, Summary = source.Summary, History = source.History, Failures = failures, FailedCount = failures.Length,
            Status = source.Status, RetrievedAt = source.RetrievedAt, CollectionUri = source.CollectionUri,
        }, TestFailureReportFixture.Options("en-US"));
    }

    private static string Overview(string html) => Section(html, "<section class=\"view\" id=\"overview\" data-view>", "</section>");

    private static string Glance(string overview) => Section(overview, "<div class=\"glance\">", "\n</div>\n");

    // A card is its heading's line and the line after it.
    private static string Panel(string overview, string heading)
    {
        string start = "<div class=\"glance-panel\"><h3>" + heading + "</h3>\n";
        int position = overview.IndexOf(start, StringComparison.Ordinal);
        Assert.True(position >= 0, start);
        return overview[position..overview.IndexOf('\n', position + start.Length)];
    }

    private static string Section(string html, string start, string end)
    {
        int position = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(position >= 0, start);
        int stop = html.IndexOf(end, position + start.Length, StringComparison.Ordinal);
        Assert.True(stop > position, end);
        return html[position..stop];
    }
}
