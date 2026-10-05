using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.Tests.Reporting.TestFailures;

namespace AdoToolkit.Core.Tests.Reporting;

// The features that the Test Case report takes from the failed-test report: the top bar with its
// counts, section links and filters, cards, collapsing, copying, and the closing line of the page.
public sealed class TestCaseReportStructureTests
{
    [Fact]
    public void OneCaseHasATopBarWithItsCountsLinksAndFiltersAndOneCard()
    {
        string html = Body(ReportFixture.Model("review"));
        string bar = html[html.IndexOf("<header class=\"top-bar\">", StringComparison.Ordinal)..html.IndexOf("<main id=\"report-content\">", StringComparison.Ordinal)];
        Assert.Contains("<span class=\"report-brand\">AdoToolkit</span><h1>Azure DevOps Test Case</h1><span class=\"report-scope\">Équipe / Web?#</span>", bar, StringComparison.Ordinal);
        Assert.Contains("<span class=\"count-label\">Steps</span><strong class=\"count-value\">2</strong>", bar, StringComparison.Ordinal);
        Assert.Contains("<span class=\"count-label\">Shared Steps</span><strong class=\"count-value\">2</strong>", bar, StringComparison.Ordinal);
        Assert.Contains("<span class=\"count-label\">Iterations</span><strong class=\"count-value\">1</strong>", bar, StringComparison.Ordinal);
        Assert.Contains("<span class=\"results-link\"><a rel=\"noreferrer\" href=\"https://ado.example.test/tfs/Collection%20A/%C3%89quipe%20%2F%20Web%3F%23/_workitems/edit/10\">Open in Azure DevOps</a></span>", bar, StringComparison.Ordinal);
        Assert.Equal(["report-1-overview", "report-1-parameters", "report-1-steps"],
            Regex.Matches(bar[bar.IndexOf("<nav class=\"section-links\"", StringComparison.Ordinal)..bar.IndexOf("</nav>", StringComparison.Ordinal)], "href=\"#([^\"]+)\"").Select(static match => match.Groups[1].Value));
        // The filters of one case search its steps and collapse its Shared Steps groups.
        Assert.Contains("<label class=\"search\">Search <input type=\"search\" data-filter></label>", bar, StringComparison.Ordinal);
        Assert.Contains("data-action=\"expand\">Expand all</button>", bar, StringComparison.Ordinal);
        Assert.Contains("data-action=\"collapse\">Collapse all</button>", bar, StringComparison.Ordinal);
        Assert.Contains("data-filter-count data-label-count=\"{0} of {1} steps\"", bar, StringComparison.Ordinal);
        Assert.Contains("<p class=\"text-muted keyboard-hint\">/ Search · j/k Next/previous step · Esc Clear</p>", bar, StringComparison.Ordinal);
        Assert.DoesNotContain("data-toggle", bar, StringComparison.Ordinal);
        Assert.StartsWith("<body>\n<a class=\"skip-link\" href=\"#report-1-steps\">Steps</a>\n<header class=\"top-bar\">", html, StringComparison.Ordinal);

        Assert.Single(Regex.Matches(html, "<article class=\"test-case\">"));
        Assert.Contains("<header id=\"report-1-overview\" data-case=\"10\" data-status=\"complete\"><span class=\"case-number\">10</span>"
            + "<span class=\"status-badge status-complete\"><span aria-hidden=\"true\">✓</span> Complete</span><h2><a rel=\"noreferrer\" href=\"", html, StringComparison.Ordinal);
        Assert.Contains("<span data-copy-value>Synthetic case &lt;title&gt;</span> <span role=\"img\" aria-label=\"Open in Azure DevOps\">↗</span></a></h2>", html, StringComparison.Ordinal);
        Assert.Contains("data-action=\"copy\" aria-label=\"Copy — Title\">Copy</button></span></header>", html, StringComparison.Ordinal);
        // One case is never collapsed as a whole, and has no links of its own beside those of the top bar.
        Assert.DoesNotContain("class=\"case-links\"", html, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(html, "data-action=\"toggle\""));
        Assert.Contains("<p data-no-matches hidden>No step matches the search.</p>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"shared-list\"><span class=\"strip-label\">Shared Steps</span><ul><li><a rel=\"noreferrer\" href=\"https://ado.example.test/tfs/Collection%20A/Autre%20%2F%20%C3%A9quipe/_workitems/edit/20\">Shared &lt;title&gt; (#20)",
            html, StringComparison.Ordinal);
        // The server, the collection and the times close the page.
        Assert.EndsWith("<dl class=\"secondary-line\"><div><dt>Generated on</dt><dd>Tuesday, September 15, 2026 10:30 -03:00</dd></div><div><dt>Retrieved on</dt><dd>9/15/2026 10:29 AM</dd></div>"
            + "<div><dt>Toolkit version</dt><dd>2.1.0-test</dd></div><div><dt>Server</dt><dd>https://ado.example.test/</dd></div><div><dt>Collection</dt><dd>https://ado.example.test/tfs/Collection%20A/</dd></div></dl>\n"
            + "</main>\n<p class=\"copy-feedback\" role=\"status\" data-copy-status hidden data-label-done=\"Copied\" data-label-selected=\"Text selected; use your copy shortcut.\"></p>\n\n</body>\n</html>\n",
            html, StringComparison.Ordinal);
    }

    [Fact]
    public void TimesAreShownInTheOffsetOfTheExportAndExtremeTimesDoNotFailIt()
    {
        AdoTestCase testCase = ReportFixture.Case("parameterized");
        // Changed at 23:30 UTC; the export runs at UTC-3, so the report shows the same evening.
        AdoTestCase late = Copy(testCase, new DateTimeOffset(2026, 9, 14, 23, 30, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 15, 13, 29, 0, TimeSpan.Zero));
        string html = Body(Model(late));
        Assert.Contains("<div><dt>Last changed</dt><dd>9/14/2026 8:30 PM</dd></div>", html, StringComparison.Ordinal);
        Assert.Contains("<div><dt>Retrieved on</dt><dd>9/15/2026 10:29 AM</dd></div>", html, StringComparison.Ordinal);
        Assert.Contains("<div><dt>Dernière modification</dt><dd>2026-09-14 20 h 30</dd></div>", Body(Model(late, "fr-CA")), StringComparison.Ordinal);
        // A hand-made object can carry no date at all.
        string unset = Body(Model(Copy(testCase, default, DateTimeOffset.MaxValue)));
        Assert.Contains("<dt>Last changed</dt>", unset, StringComparison.Ordinal);
        Assert.Contains("<dt>Retrieved on</dt>", unset, StringComparison.Ordinal);
    }

    // The page from <body>, without the script.
    internal static string Body(ReportDocumentModel model)
    {
        string html = TestFailureMarkup.WithoutScripts(GoldenReportTests.Render(model, ReportFormat.Html));
        return html[html.IndexOf("<body>", StringComparison.Ordinal)..];
    }

    private static ReportDocumentModel Model(AdoTestCase testCase, string culture = "en-US") =>
        ReportModelBuilder.Build(testCase, new AdoConnection { CollectionUri = ReportFixture.Collection }, ReportFixture.Options(culture));

    private static AdoTestCase Copy(AdoTestCase source, DateTimeOffset changed, DateTimeOffset retrieved) => new()
    {
        Id = source.Id, Rev = source.Rev, Title = source.Title, WorkItemType = source.WorkItemType, TeamProject = source.TeamProject, State = source.State,
        WebUrl = source.WebUrl, CollectionUri = source.CollectionUri, Steps = source.Steps, ChangedDate = changed, RetrievedAt = retrieved,
    };
}
