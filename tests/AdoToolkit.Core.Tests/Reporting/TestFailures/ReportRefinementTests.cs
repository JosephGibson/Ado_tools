using System.Net;
using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

public sealed class ReportRefinementTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void DetailsKeepHistoryCellsAndTitlesWithoutALegend(string culture)
    {
        string html = TestFailureReportFixture.Render("failed", culture);
        string card = Section(html, "<article class=\"card failure-card\"", "</article>");
        Assert.DoesNotContain("status-legend", html, StringComparison.Ordinal);
        Assert.Contains("<ol class=\"history-strip\"", card, StringComparison.Ordinal);
        Assert.Equal(4, Regex.Count(card, "class=\"history-cell\"[^>]* title=\"[^\"]+\""));
    }

    [Theory]
    [InlineData("en-US", "failed", false)]
    [InlineData("fr-CA", "failed", false)]
    [InlineData("en-US", "partial", true)]
    [InlineData("fr-CA", "partial", true)]
    public void CountChipsAndPartialLinkShareTheWrappingTitleLine(string culture, string variant, bool partial)
    {
        string html = TestFailureReportFixture.Render(variant, culture);
        string title = Section(html, "<div class=\"title-line\">", "</div>");
        Assert.Contains("<h1>", title, StringComparison.Ordinal);
        Assert.Contains("class=\"build-number\"", title, StringComparison.Ordinal);
        foreach (string chip in new[] { "failed", "flaky", "attachments" })
            Assert.Contains("class=\"count-chip status-" + chip + "\"", title, StringComparison.Ordinal);
        Assert.Equal(partial, title.Contains("class=\"partial-link\" href=\"#diagnostics\"", StringComparison.Ordinal));
        Assert.DoesNotContain("count-line", html, StringComparison.Ordinal);
        Assert.Matches(@"\.title-line[^{}]*\{[^{}]*display: flex;[^{}]*flex-wrap: wrap;", html);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void OverviewLinksTheLowestOpenBugInItsOwningProjectAndKeepsTheCardBadgeColor(string culture)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model(culture: culture);
        AdoTestFailure original = model.Failures[0];
        AdoTestFailure failure = new()
        {
            Ordinal = original.Ordinal, ShortName = original.ShortName, CollectionUri = original.CollectionUri,
            Classification = original.Classification, Attempts = original.Attempts,
            Bugs = [Bug(950, true), Bug(920, true, "Bugs / été"), Bug(910, false), Bug(900, null)],
        };
        model = TestFailureReportModelBuilder.WithAttachments(model, [failure], [], null);
        string html = TestFailureReportFixture.Render(model);
        string row = Section(html, "<tr data-index-for=\"f-1\">", "</tr>");
        Assert.Contains("<a class=\"open-bug-marker\" rel=\"noreferrer\" href=\"https://ado.example.test/tfs/Collection%20A/Bugs%20%2F%20%C3%A9t%C3%A9/_workitems/edit/920\">",
            row, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(row, "class=\"open-bug-marker\""));
        Assert.Matches(@"\.open-bug-marker, \.open-bug-marker:visited \{[^{}]*color: var\(--fail\);[^{}]*border-color: var\(--fail\);", html);
        Assert.Matches(@"\.bug-open[^{}]*\{[^{}]*color: var\(--info\);", html);
        Assert.Contains("class=\"bug-open\"", Section(html, "<article class=\"card failure-card\"", "</article>"), StringComparison.Ordinal);
        TestFailureReportValidator.Validate(new StringReader(html), model);
    }

    [Theory]
    [InlineData("en-US", null)]
    [InlineData("fr-CA", 301)]
    public void EveryAttachmentNameLinksToItsOwnDownloadIncludingNonTextFiles(string culture, int? subResult)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model(culture: culture);
        string[] names = ["image.png", "page.html", "context.json", "console.txt", "archive.zip", TestFailureReportFixture.Hostile];
        AdoTestAttachment[] attachments = [.. names.Select((name, index) => new AdoTestAttachment
        {
            Id = 51 + index, RunId = 201, ResultId = 11, SubResultId = subResult, FileName = name, Kind = AttachmentKinds.FromFileName(name),
        })];
        AdoTestFailure failure = model.Failures[0];
        failure = failure.WithAttempts([failure.Attempts[0].WithAttachments(attachments)]);
        model = TestFailureReportModelBuilder.WithAttachments(model, [failure], [], null);
        string html = TestFailureReportFixture.Render(model);
        foreach (AdoTestAttachment attachment in attachments)
        {
            string item = Section(html, "<li id=\"f-1-a1-att" + attachment.Id.ToString(CultureInfo.InvariantCulture) + "\"", "</li>");
            string href = WebUtility.HtmlDecode(Regex.Match(item, "href=\"([^\"]+)\"").Groups[1].Value);
            string expected = "https://ado.example.test/tfs/Collection%20A/%C3%89quipe%20%2F%20Web%3F%23/_apis/test/Runs/201/Results/11/attachments/"
                + attachment.Id.ToString(CultureInfo.InvariantCulture) + "?api-version=6.0-preview.1"
                + (subResult.HasValue ? "&testSubResultId=301" : "");
            Assert.Equal(expected, href);
            Assert.Contains(attachment.FileName, TestFailureMarkup.Text(item), StringComparison.Ordinal);
            Assert.DoesNotContain("data-local-file", item, StringComparison.Ordinal);
            Assert.DoesNotContain("<script", item, StringComparison.Ordinal);
        }
        TestFailureReportValidator.Validate(new StringReader(html), model);
    }

    private static AdoTestBug Bug(int id, bool? open, string? project = null) => new()
    { Id = id, IsOpen = open, TeamProject = project, WebUrl = TestFailureReportFixture.Untrusted };

    private static string Section(string html, string start, string end)
    {
        int position = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(position >= 0, start);
        return html[position..html.IndexOf(end, position + start.Length, StringComparison.Ordinal)];
    }
}
