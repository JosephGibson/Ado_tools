using System.Text.Json;
using System.Text.RegularExpressions;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.Reporting;

// What -IncludeDetail adds to the HTML report, and to no other format.
public sealed class TestCaseDetailRenderingTests
{
    [Fact]
    public void DetailsAddSummaryFieldsDescriptionLinksAndTestPointsToTheCase()
    {
        string html = TestCaseReportStructureTests.Body(ReportFixture.Model("detailed"));
        Assert.Equal(["report-1-overview", "report-1-description", "report-1-parameters", "report-1-steps", "report-1-links", "report-1-points", "report-1-diagnostics"],
            Regex.Matches(html[html.IndexOf("<nav class=\"section-links\"", StringComparison.Ordinal)..html.IndexOf("</nav>", StringComparison.Ordinal)], "href=\"#([^\"]+)\"").Select(static match => match.Groups[1].Value));
        Assert.Contains("<div><dt>Test assembly</dt><dd>Synthetic.Orders.Tests.dll</dd></div><div><dt>Automated test type</dt><dd>Unit Test</dd></div>", html, StringComparison.Ordinal);
        Assert.Contains("<div><dt>Created on</dt><dd>8/16/2026 10:30 AM</dd></div><div><dt>Created by</dt><dd>Fictional Author</dd></div>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"tag-list\"><span class=\"strip-label\">Tags</span><ul><li>Smoke</li><li>Été &lt;b&gt;</li></ul></div>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"name-section\"><span class=\"strip-label\">Automated test</span><code data-copy-value>Synthetic.Orders.OrderTests.Creates&lt;T&gt;</code> "
            + "<button type=\"button\" class=\"interactive\" data-enhance hidden data-action=\"copy\" aria-label=\"Copy — Automated test\">Copy</button></div>", html, StringComparison.Ordinal);
        // The description keeps its structure, like a formatted step.
        Assert.Contains("<h3 class=\"section-heading\" id=\"report-1-description\">Description</h3>\n<div class=\"description-text\"><div class=\"rich-text\"><p>Checks the <strong>order</strong> form.</p>"
            + "<ul><li>Uses the sandbox</li><li>See <a rel=\"noreferrer\" href=\"https://docs.example.test/orders\">https://docs.example.test/orders</a></li></ul></div></div>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkedWorkItemsLinkIntoAzureDevOpsAndOtherLinksAreTextUnlessTheirSchemeIsSafe()
    {
        string html = TestCaseReportStructureTests.Body(ReportFixture.Model("detailed"));
        string links = html[html.IndexOf("<section class=\"links\">", StringComparison.Ordinal)..html.IndexOf("<section class=\"points\">", StringComparison.Ordinal)];
        Assert.Contains("<h3 class=\"section-heading\" id=\"report-1-links\">Links <span class=\"count\">7</span></h3>", links, StringComparison.Ordinal);
        Assert.Contains("<thead><tr><th scope=\"col\">Link type</th><th scope=\"col\">Work item</th><th scope=\"col\">Title</th><th scope=\"col\">State</th></tr></thead>", links, StringComparison.Ordinal);
        Assert.Contains("<tr data-link=\"3050\"><td>Tests</td><td class=\"col-item\"><a rel=\"noreferrer\" href=\"https://ado.example.test/tfs/Collection%20A/%C3%89quipe%20%2F%20Web%3F%23/_workitems/edit/3050\">User Story #3050 ",
            links, StringComparison.Ordinal);
        Assert.Contains("</a></td><td>Order entry &lt;story&gt;</td><td>Active</td></tr>", links, StringComparison.Ordinal);
        Assert.Contains("<tr data-link=\"3001\"><td>Related <span class=\"text-muted\">See &lt;also&gt;</span></td><td class=\"col-item\"><a rel=\"noreferrer\" href=\"https://ado.example.test/tfs/Collection%20A/Autre%20%2F%20%C3%A9quipe/_workitems/edit/3001\">Bug #3001 ",
            links, StringComparison.Ordinal);
        // A work item that could not be read keeps its link type and its ID link.
        Assert.Contains("<tr data-link=\"3099\"><td>Contoso.LinkTypes.Blocks-Forward</td><td class=\"col-item\"><a rel=\"noreferrer\" href=\"https://ado.example.test/tfs/Collection%20A/%C3%89quipe%20%2F%20Web%3F%23/_workitems/edit/3099\">Work item #3099 ",
            links, StringComparison.Ordinal);
        Assert.Contains("</a></td><td></td><td></td></tr>", links, StringComparison.Ordinal);
        Assert.Contains("<span class=\"strip-label\">Hyperlinks</span><ul><li><a rel=\"noreferrer\" href=\"https://wiki.example.test/orders\">https://wiki.example.test/orders</a> <span class=\"text-muted\">Specification</span></li>"
            + "<li>javascript:alert(1)</li></ul>", links, StringComparison.Ordinal);
        // Attachments are named, not linked: no address of the response is ever used.
        Assert.Contains("<span class=\"strip-label\">Attachments</span><ul><li>capture &lt;1&gt;.png <span class=\"attachment-size\">20,480 bytes</span> <span class=\"text-muted\">Screen</span></li><li>notes.txt</li></ul>",
            links, StringComparison.Ordinal);
        Assert.Equal(4, Regex.Count(links, "href="));
    }

    [Fact]
    public void TestPointsShowWhereTheCaseIsPlannedAndItsLatestOutcomeThere()
    {
        string html = TestCaseReportStructureTests.Body(ReportFixture.Model("detailed"));
        string points = html[html.IndexOf("<section class=\"points\">", StringComparison.Ordinal)..html.IndexOf("<section class=\"diagnostics\"", StringComparison.Ordinal)];
        Assert.Contains("<h3 class=\"section-heading\" id=\"report-1-points\">Test points <span class=\"count\">4</span></h3>", points, StringComparison.Ordinal);
        Assert.Equal(["Test Plan", "Test Suite", "Configuration", "Latest outcome", "Tester", "Test run", "Last updated"],
            Regex.Matches(points, "<th scope=\"col\">([^<]+)</th>").Select(static match => match.Groups[1].Value));
        Assert.Equal(["passed", "failed", "other", "notrun"], Regex.Matches(points, "<tr data-point=\"[0-9]+\" data-outcome=\"([a-z]+)\">").Select(static match => match.Groups[1].Value));
        Assert.Contains("<tr data-point=\"2\" data-outcome=\"failed\"><td><a rel=\"noreferrer\" href=\"https://ado.example.test/tfs/Collection%20A/%C3%89quipe%20%2F%20Web%3F%23/_testPlans/define?planId=40\">Plan « Été » ",
            points, StringComparison.Ordinal);
        Assert.Contains("href=\"https://ado.example.test/tfs/Collection%20A/%C3%89quipe%20%2F%20Web%3F%23/_testPlans/define?planId=40&amp;suiteId=52\">Paiement &lt;b&gt; ", points, StringComparison.Ordinal);
        Assert.Contains("<td>Windows 11</td><td class=\"col-outcome\"><span class=\"status-badge status-failed\"><span aria-hidden=\"true\">✕</span> Failed</span></td><td>Fictional Tester</td>"
            + "<td><a rel=\"noreferrer\" href=\"https://ado.example.test/tfs/Collection%20A/%C3%89quipe%20%2F%20Web%3F%23/_testManagement/runs?_a=runCharts&amp;runId=702\">702 ", points, StringComparison.Ordinal);
        Assert.Contains("</a></td><td>9/14/2026 10:30 AM</td></tr>", points, StringComparison.Ordinal);
        // A plan or suite without a name shows its ID; an outcome that is neither pass nor failure is named.
        Assert.Contains("define?planId=41\">41 ", points, StringComparison.Ordinal);
        Assert.Contains("<span class=\"status-badge status-other\"><span aria-hidden=\"true\">–</span> Other</span> <span class=\"text-muted\">Blocked</span></td>", points, StringComparison.Ordinal);
        Assert.Contains("<span class=\"status-badge status-notrun\"><span aria-hidden=\"true\">–</span> Not run</span></td><td>Fictional Tester</td><td></td><td></td></tr>", points, StringComparison.Ordinal);
        Assert.Contains("<header id=\"report-1-overview\" data-case=\"10\" data-status=\"complete\" data-failed>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void PointsThatWereNotReadAreLeftOutAndACaseInNoSuiteSaysSo()
    {
        string none = Body(Detail(points: []));
        Assert.Contains("<h3 class=\"section-heading\" id=\"report-1-points\">Test points <span class=\"count\">0</span></h3>\n"
            + "<p class=\"text-muted\">This test case has no test point: it is in no suite of the project.</p>\n</section>", none, StringComparison.Ordinal);
        Assert.DoesNotContain("data-failed", none, StringComparison.Ordinal);
        AdoTestCaseDetail unread = new()
        {
            Id = 10, IsResolved = false,
            Diagnostics = [DiagnosticMessageRenderer.Create(DiagnosticCodes.TestCaseDetailUnavailable, CultureInfo.GetCultureInfo("en-US")),
                DiagnosticMessageRenderer.Create(DiagnosticCodes.TestPointsUnavailable, CultureInfo.GetCultureInfo("en-US"), arguments: [ReportFixture.Project])],
        };
        string html = Body(unread);
        Assert.DoesNotContain("class=\"points\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"links\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"description\"", html, StringComparison.Ordinal);
        // The report says what is missing, as a warning that does not change the status of the case.
        Assert.Contains("<a href=\"#report-1-diagnostics\">Diagnostics 2</a>", html, StringComparison.Ordinal);
        Assert.Contains("data-status=\"complete\"", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"diagnostic\" data-severity=\"warning\" data-diagnostic=\"TestPointsUnavailable\"><strong>Warning · TestPointsUnavailable</strong>"
            + "<p>The test points of project Équipe / Web?# could not be read; plans, suites, configurations and latest outcomes are left out.</p></div>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void LookupWarningsFollowTheReportCultureWhateverCultureReadThem()
    {
        string html = TestCaseReportStructureTests.Body(ReportFixture.Model("detailed", "fr-CA"));
        Assert.Contains("<strong>Avertissement · UnresolvedLinkedWorkItem</strong><p>L’élément de travail lié 3099 est introuvable ou inaccessible; le lien vers son ID est conservé.</p>", html, StringComparison.Ordinal);
        Assert.Contains("<h3 class=\"section-heading\" id=\"report-1-points\">Points de test <span class=\"count\">4</span></h3>", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Dernier résultat</th>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"status-badge status-notrun\"><span aria-hidden=\"true\">–</span> Non exécuté</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"strip-label\">Balises</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ADocumentShowsTheOutcomesInItsContentsAndCanFilterTheFailedOnes()
    {
        // Case 11 has no failing point; 12 was not read.
        Dictionary<int, AdoTestCaseDetail> details = new()
        {
            [10] = DetailFixture.Detail(10),
            [11] = DetailFixture.Detail(11, [DetailFixture.Point(9, 40, "Plan", 51, "Connexion", "Windows 11", "Passed", 700)]),
        };
        ReportDocumentModel model = ReportModelBuilder.Build(MultiCaseFixture.Cases(), new AdoConnection { CollectionUri = ReportFixture.Collection }, ReportFixture.Options(details: details));
        Assert.True(model.HasDetail);
        string html = TestCaseReportStructureTests.Body(model);
        Assert.Contains("<label><input type=\"checkbox\" data-toggle=\"failed\">Latest outcome failed</label>", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Steps</th><th scope=\"col\">Latest outcome</th><th scope=\"col\">Status</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"colgroup\" colspan=\"6\">Racine › Connexion</th>", html, StringComparison.Ordinal);
        Assert.Contains("<td class=\"col-outcome\"><span class=\"point-count status-failed\"><span aria-hidden=\"true\">✕</span><span class=\"sr-only\">Failed</span> 1</span>"
            + "<span class=\"point-count status-passed\"><span aria-hidden=\"true\">✓</span><span class=\"sr-only\">Passed</span> 1</span>"
            + "<span class=\"point-count status-other\">Other 1</span><span class=\"point-count status-notrun\">Not run 1</span></td>", html, StringComparison.Ordinal);
        Assert.Contains("<tr data-index-for=\"tc-12\"><td class=\"col-number\">12</td><td class=\"col-title\"><a href=\"#tc-12\">Synthetic case 12 &lt;title&gt;</a></td><td>Ready</td>"
            + "<td class=\"col-number\">1</td><td class=\"col-outcome\"><span class=\"text-muted\">—</span></td>", html, StringComparison.Ordinal);
        Assert.Equal([true, false, true, false], Regex.Matches(html, "<header id=\"tc-[0-9-]+-overview\" data-case=\"[0-9]+\" data-status=\"[a-z]+\"( data-failed)?>").Select(static match => match.Groups[1].Success));
        Assert.False(MultiCaseFixture.Model().HasDetail);
    }

    [Theory]
    [InlineData(ReportFormat.Markdown)]
    [InlineData(ReportFormat.Json)]
    public void OtherFormatsAreTheSameWithOrWithoutDetails(ReportFormat format) =>
        Assert.Equal(GoldenReportTests.Render(ReportFixture.Model("parameterized"), format), GoldenReportTests.Render(ReportFixture.Model("detailed"), format));

    [Fact]
    public void WithoutDetailsNoneOfTheirSectionsOrLabelsAppear()
    {
        string html = TestCaseReportStructureTests.Body(ReportFixture.Model("review"));
        foreach (string absent in new[] { "Description", "Tags", "Created", "Automated test", "Test assembly", "Hyperlinks", "Attachments", "Test points", "Latest outcome", "Link type", "data-failed" })
            Assert.DoesNotContain(absent, html, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportReadsDetailsOnlyForAnHtmlReportThatWillBeWritten()
    {
        using TestDirectory directory = new();
        int reads = 0;
        TestCaseExportOptions Options(ReportFormat format) => new()
        {
            Format = format, SessionCulture = CultureInfo.GetCultureInfo("en-US"), Path = directory.Root, GeneratedAt = ReportFixture.Timestamp, ToolkitVersion = "2.1.0-test",
            ReadDetails = () => { reads++; return DetailFixture.Details(10); },
        };
        TestCaseExporter exporter = new(new SilentLauncher());
        AdoConnection connection = new() { CollectionUri = ReportFixture.Collection };
        // -WhatIf, or a declined confirmation: nothing is requested and nothing is written.
        Assert.Null(exporter.Export([ReportFixture.Case("parameterized")], connection, Options(ReportFormat.Html), _ => false, _ => { }, TestContext.Current.CancellationToken));
        Assert.Equal(0, reads);
        foreach (ReportFormat format in new[] { ReportFormat.Markdown, ReportFormat.Json })
        {
            ArgumentException error = Assert.Throws<ArgumentException>(() => exporter.Export([ReportFixture.Case("parameterized")], connection, Options(format), _ => true, _ => { },
                TestContext.Current.CancellationToken));
            Assert.StartsWith("IncludeDetail requires the HTML report format.", error.Message, StringComparison.Ordinal);
        }
        Assert.Equal(0, reads);
        Assert.Empty(Directory.GetFiles(directory.Root));
        FileInfo file = exporter.Export([ReportFixture.Case("parameterized")], connection, Options(ReportFormat.Html), _ => true, _ => { }, TestContext.Current.CancellationToken)!;
        Assert.Equal(1, reads);
        Assert.Contains("id=\"report-1-points\"", File.ReadAllText(file.FullName), StringComparison.Ordinal);
        // A lookup that stops the export leaves no file behind.
        File.Delete(file.FullName);
        TestCaseExportOptions failing = new()
        {
            SessionCulture = CultureInfo.GetCultureInfo("en-US"), Path = directory.Root, GeneratedAt = ReportFixture.Timestamp, ToolkitVersion = "2.1.0-test",
            ReadDetails = () => throw new AdoAuthorizationException(Messages.Get(AdoMessage.Authorization, CultureInfo.InvariantCulture)),
        };
        Assert.Throws<AdoAuthorizationException>(() => exporter.Export([ReportFixture.Case("parameterized")], connection, failing, _ => true, _ => { }, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(directory.Root));
    }

    private static AdoTestCaseDetail Detail(IReadOnlyList<AdoTestPoint> points) => DetailFixture.Detail(10, points);

    private static string Body(AdoTestCaseDetail detail) => TestCaseReportStructureTests.Body(ReportModelBuilder.Build(ReportFixture.Case("parameterized"),
        new AdoConnection { CollectionUri = ReportFixture.Collection }, ReportFixture.Options(details: new Dictionary<int, AdoTestCaseDetail> { [10] = detail })));

    private sealed class SilentLauncher : IDocumentLauncher
    {
        public void Open(string path) { }
    }
}
