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
    public void ExpandedSharedStepsGroupsCanBeCollapsedAndUnexpandedOnesCannot()
    {
        string nested = Body(ReportFixture.Model("nested"));
        Assert.Contains("<div class=\"step-card shared-banner\" id=\"report-1-step-2\"><h4><span class=\"outline-number\">2</span> ▸ <a rel=\"noreferrer\" href=\"", nested, StringComparison.Ordinal);
        Assert.Contains("</h4><button type=\"button\" class=\"interactive collapse-toggle\" data-enhance hidden data-action=\"toggle\" aria-expanded=\"true\" aria-label=\"Show or hide — Shared &lt;title&gt; (#20)\">"
            + "<span class=\"when-open\" aria-hidden=\"true\">▾</span><span class=\"when-closed\" aria-hidden=\"true\">▸</span></button></div>\n<div class=\"shared-group\">\n", nested, StringComparison.Ordinal);
        Assert.Contains("<div class=\"step-card\" id=\"report-1-step-4\">\n<h4 class=\"step-number\"><span class=\"outline-number\">2.1.1</span> Step</h4>", nested, StringComparison.Ordinal);
        // The source of this step is the markup of its text, so it is rendered from it.
        Assert.Contains("<div class=\"action\"><h5>Action</h5><div class=\"rich-text\"><p>Confirm</p></div></div>\n"
            + "<div class=\"expected\"><h5>Expected Result</h5><div class=\"rich-text\"><strong>Visible</strong></div></div>\n</div>\n</div>\n</div>\n</div>\n</section>", nested, StringComparison.Ordinal);

        string partial = Body(ReportFixture.Model("partial"));
        Assert.DoesNotContain("data-action=\"toggle\"", partial, StringComparison.Ordinal);
        Assert.DoesNotContain("data-action=\"expand\"", partial, StringComparison.Ordinal);
        Assert.Contains("<div class=\"step-card shared-banner warning\" id=\"report-1-step-2\"><h4><span class=\"outline-number\">2</span> ▸ Shared Steps (#99)</h4>"
            + "<p>The referenced shared steps are missing or inaccessible.</p></div>\n", partial, StringComparison.Ordinal);
    }

    [Fact]
    public void APartialCaseLinksItsDiagnosticsFromTheTopBarAndEachDiagnosticToItsStep()
    {
        string html = Body(ReportFixture.Model("partial"));
        Assert.Contains("<a class=\"partial-link\" href=\"#report-1-diagnostics\"><span aria-hidden=\"true\">!</span> Partial</a>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"#report-1-diagnostics\">Diagnostics 3</a>", html, StringComparison.Ordinal);
        Assert.Contains("<header id=\"report-1-overview\" data-case=\"10\" data-status=\"partial\"><span class=\"case-number\">10</span><span class=\"status-badge status-partial\"><span aria-hidden=\"true\">!</span> Partial</span>",
            html, StringComparison.Ordinal);
        Assert.Contains("<div><dt>Diagnostics</dt><dd>Test case 10 is partial: 3 error(s), 0 warning(s), 0 information diagnostic(s).</dd></div>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"diagnostic\" data-severity=\"error\" data-diagnostic=\"UnresolvedSharedStep\"><strong>Error · UnresolvedSharedStep</strong> <a href=\"#report-1-step-2\">Step 2</a><p>",
            html, StringComparison.Ordinal);
        Assert.Contains("data-diagnostic=\"ExpansionLimitExceeded\"><strong>Error · ExpansionLimitExceeded</strong> <a href=\"#report-1-step-3\">Step 3</a><p>", html, StringComparison.Ordinal);
        // A diagnostic of the document as a whole has no step to point to.
        Assert.Contains("data-diagnostic=\"MalformedStepsXml\"><strong>Error · MalformedStepsXml</strong><p>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SeveralCasesGetDocumentFiltersAContentsTableAndCollapsibleCards()
    {
        ReportDocumentModel model = MultiCaseFixture.Model();
        string html = Body(model);
        string bar = html[html.IndexOf("<header class=\"top-bar\">", StringComparison.Ordinal)..html.IndexOf("<main id=\"report-content\">", StringComparison.Ordinal)];
        Assert.Contains("<span class=\"count-chip status-complete\"><span class=\"count-label\">Complete</span><strong class=\"count-value\">3</strong></span>"
            + "<span class=\"count-chip status-partial\"><span class=\"count-label\">Partial</span><strong class=\"count-value\">1</strong></span>", bar, StringComparison.Ordinal);
        Assert.Equal(["overview", "contents"], Regex.Matches(bar, "<a href=\"#([^\"]+)\"").Select(static match => match.Groups[1].Value));
        Assert.Contains("<label><input type=\"checkbox\" data-toggle=\"partial\">Partial only</label>", bar, StringComparison.Ordinal);
        // Offered only when test points were read and one of them failed.
        Assert.DoesNotContain("data-toggle=\"failed\"", bar, StringComparison.Ordinal);
        Assert.Contains("data-filter-count data-label-count=\"{0} of {1} test cases\"", bar, StringComparison.Ordinal);
        Assert.Contains("j/k Next/previous test case", bar, StringComparison.Ordinal);
        Assert.StartsWith("<body>\n<a class=\"skip-link\" href=\"#contents\">Contents</a>\n", html, StringComparison.Ordinal);

        Assert.Contains("<thead><tr><th scope=\"col\">ID</th><th scope=\"col\">Title</th><th scope=\"col\">State</th><th scope=\"col\">Steps</th><th scope=\"col\">Status</th></tr></thead>", html, StringComparison.Ordinal);
        Assert.Contains("<tr data-index-for=\"tc-10-2\"><td class=\"col-number\">10</td><td class=\"col-title\"><a href=\"#tc-10-2\">Synthetic case 10 &lt;title&gt;</a></td><td>Ready</td>"
            + "<td class=\"col-number\">2</td><td><span class=\"status-badge status-complete\"><span aria-hidden=\"true\">✓</span> Complete</span></td></tr>", html, StringComparison.Ordinal);
        Assert.Contains("<p data-no-matches hidden>No test case matches these filters.</p>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("No step matches the search.", html, StringComparison.Ordinal);

        // Each card can be collapsed, carries its own section links, and leads back to the contents.
        Assert.Equal(4, Regex.Count(html, "<header id=\"tc-[0-9-]+-overview\" data-case=\"[0-9]+\" data-status=\"(?:complete|partial)\">"));
        Assert.Equal(4, Regex.Count(html, "data-action=\"copy\" aria-label=\"Copy — Title\">Copy</button><button type=\"button\" class=\"interactive collapse-toggle\" data-enhance hidden data-action=\"toggle\""));
        string[] navigation = Regex.Matches(html, "<nav class=\"case-links\" aria-label=\"([^\"]+)\">").Select(static match => match.Groups[1].Value).ToArray();
        Assert.Equal(["Report sections — 10", "Report sections — 11", "Report sections — 10", "Report sections — 12"], navigation);
        Assert.Equal(4, Regex.Count(html, "<nav class=\"case-links\"[^>]*>\n(?:<a href=\"#tc-[^\"]+\">[^<]+</a>\n)+<a href=\"#contents\">Contents</a>\n</nav>"));
        Assert.Contains("<a href=\"#tc-12-steps\">Steps</a>\n<a href=\"#tc-12-diagnostics\">Diagnostics 3</a>\n<a href=\"#contents\">Contents</a>", html, StringComparison.Ordinal);
        // The times and the server are stated once for the document, not in every case.
        Assert.Single(Regex.Matches(html, "<dt>Generated on</dt>"));
        Assert.Single(Regex.Matches(html, "<dt>Server</dt>"));
        Assert.DoesNotContain("<dt>Retrieved on</dt>", html, StringComparison.Ordinal);
        Assert.Equal(4, Regex.Count(html, "<div><dt>Step count</dt><dd>[0-9]+</dd></div>"));

        string[] identifiers = Regex.Matches(html, "\\sid=\"([^\"]+)\"").Select(static match => match.Groups[1].Value).ToArray();
        Assert.Equal(identifiers.Length, identifiers.Distinct(StringComparer.Ordinal).Count());
        Assert.All(Regex.Matches(html, "href=\"#([^\"]+)\"").Select(static match => match.Groups[1].Value), target => Assert.Contains(target, identifiers));
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

    [Fact]
    public void TheTitleElementNamesTheCaseOrTheDocument()
    {
        Assert.Contains("<title>Azure DevOps Test Case — 10 · Synthetic case &lt;title&gt;</title>", GoldenReportTests.Render(ReportFixture.Model(), ReportFormat.Html), StringComparison.Ordinal);
        // A title of several lines is one line in the title element, and several in the heading of the card.
        string french = GoldenReportTests.Render(ReportFixture.Model("french", "fr-CA"), ReportFormat.Html);
        Assert.Contains("<title>Cas de test Azure DevOps — 10 · " + SinkEncoding.Attribute(ReportFixture.FrenchText.Replace('\n', ' ')) + "</title>", french, StringComparison.Ordinal);
        Assert.Contains("<span data-copy-value>" + SinkEncoding.Html(ReportFixture.FrenchText) + "</span>", french, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt; &amp; &quot;quoted&quot;<br>next</span>", french, StringComparison.Ordinal);
        Assert.Contains("<title>Azure DevOps Test Case Report</title>", GoldenReportTests.Render(MultiCaseFixture.Model(), ReportFormat.Html), StringComparison.Ordinal);
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
