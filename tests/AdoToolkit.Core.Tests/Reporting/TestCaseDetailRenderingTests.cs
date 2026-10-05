using System.Text.RegularExpressions;
using AdoToolkit.Core.IO;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.Reporting;

// What -IncludeDetail adds to the HTML report, and to no other format.
public sealed class TestCaseDetailRenderingTests
{
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
