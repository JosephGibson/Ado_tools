using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// What a card in Details says before its attempts: the class in its title, then the full name, then
// the run history, the bugs and the attachments on rows that share one label column, then the metadata.
public sealed class CardFactsTests
{
    // The parts of a card in the order it shows them.
    private static readonly string[] Parts = ["<header>", "<div class=\"name-section\">", "<div class=\"card-facts\"><div class=\"card-history\">", "<div class=\"card-bugs\">",
        "<div class=\"card-attachments\">", "<dl class=\"metadata-grid\">", "<details class=\"attempt\""];

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void TheCardReadsFromItsTitleToItsAttempts(string culture)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("failed", culture);
        string card = Card(TestFailureReportFixture.Render(model), "f-1");
        Assert.Contains("<h3 data-short-name><span class=\"card-class\">CheckoutTests.</span>SubmitOrder</h3>", card, StringComparison.Ordinal);
        int[] order = [.. Parts.Select(part => card.IndexOf(part, StringComparison.Ordinal))];
        Assert.All(order, static index => Assert.True(index > 0));
        Assert.Equal(order.Order(), order);
        // One label width for the facts and the metadata, wider in French.
        string css = TestFailureAssets.Read("test-failures.css");
        Assert.Contains("--label-width: 7.5rem; }", css, StringComparison.Ordinal);
        Assert.Contains(":root:lang(fr) { --label-width: 10rem; }", css, StringComparison.Ordinal);
    }

    // One chip per build with its glyph and the day it finished, in the report's culture; the build
    // number and status stay its title and name. The dates are text of the card, so search finds them.
    [Theory]
    [InlineData("en-US", "9/13/2026", "9/16/2026")]
    [InlineData("fr-CA", "2026-09-13", "2026-09-16")]
    public void TheRunHistoryDatesEachBuild(string culture, string first, string last)
    {
        string card = Card(TestFailureReportFixture.Render("failed", culture), "f-1");
        string strip = card[card.IndexOf("<ol class=\"history-strip\"", StringComparison.Ordinal)..card.IndexOf("</ol>", StringComparison.Ordinal)];
        Assert.Equal(4, Regex.Count(strip, "<span class=\"history-glyph\" aria-hidden=\"true\">[^<]</span><span class=\"history-date\">[^<]+</span></a></li>"));
        Assert.Contains("<span class=\"history-date\">" + first + "</span>", strip, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"true\"><span class=\"history-glyph\" aria-hidden=\"true\">✕</span><span class=\"history-date\">" + last + "</span></a></li>",
            strip, StringComparison.Ordinal);
        Assert.Contains(first, TestFailureMarkup.Text(card), StringComparison.Ordinal);
        // The current build is the last chip, its date in bold, without the shared stylesheet's ring.
        string css = TestFailureAssets.Read("test-failures.css");
        Assert.Contains(".history-cell[aria-current=\"true\"] { outline: none; }", css, StringComparison.Ordinal);
        Assert.Contains(".history-cell[aria-current=\"true\"] .history-date { font-weight: 600; }", css, StringComparison.Ordinal);
    }

    // The card lists each file name once, from the last attempt that has it, linked to its original,
    // with its size and the attempt that holds its local copy. A name in several attempts says how many,
    // and which, as its title.
    [Fact]
    public void TheCardListsEachAttachmentOnceFromItsLastAttempt()
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("large");
        AdoTestFailure submit = model.Failures.Single(static failure => failure.ShortName == "SubmitOrder");
        string anchor = "f-" + submit.Ordinal.ToString(CultureInfo.InvariantCulture);
        string html = TestFailureReportFixture.Render(model);
        string list = Card(html, anchor);
        list = list[list.IndexOf("<div class=\"card-attachments\">", StringComparison.Ordinal)..list.IndexOf("</ul></div>", list.IndexOf("<div class=\"card-attachments\">", StringComparison.Ordinal), StringComparison.Ordinal)];
        Assert.Equal(["Screenshot.png", "console.log", "network.json"], Regex.Matches(list, "<li><a rel=\"noreferrer\" href=\"[^\"]+\">([^<]+)</a>").Select(static m => m.Groups[1].Value));
        AdoTestAttachment screenshot = submit.Attempts[2].Attachments[0];
        Assert.Equal(("Screenshot.png", 3), (screenshot.FileName, submit.Attempts[2].Number));
        Assert.Contains("<a class=\"attachment-attempt\" href=\"#" + anchor + "-a3-att" + screenshot.Id.ToString(CultureInfo.InvariantCulture) + "\">Attempt 3 of 4</a>"
            + " <span class=\"attachment-more\" title=\"Attempts: 1, 3\">×2</span></li>", list, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(list, "attachment-more"));
        // Each entry leads to the listing of its attempt, which holds its local copy and preview.
        foreach (Match link in Regex.Matches(list, "<a class=\"attachment-attempt\" href=\"#([^\"]+)\">"))
            Assert.Contains("<li id=\"" + link.Groups[1].Value + "\"", html, StringComparison.Ordinal);
        Assert.Equal(4, Regex.Count(Card(html, anchor), "<li id=\"" + anchor + "-a[0-9]-att"));
        // A test without attachments has no such row.
        Assert.DoesNotContain("card-attachments", Card(html, "f-" + model.Failures.Single(static failure => failure.ShortName == "UpdateAvatar").Ordinal
            .ToString(CultureInfo.InvariantCulture)), StringComparison.Ordinal);
    }

    private static string Card(string html, string anchor)
    {
        int start = html.IndexOf("<article class=\"card failure-card\" id=\"" + anchor + "\"", StringComparison.Ordinal);
        Assert.True(start >= 0, anchor);
        return html[start..html.IndexOf("</article>", start, StringComparison.Ordinal)];
    }
}
