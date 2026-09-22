using AdoToolkit.Core.Reporting.Charts;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.Charts;

[Trait("Acceptance", "S5-3")]
public sealed class StripTests
{
    [Theory]
    [InlineData("en-US", "Attempt 1 of 3", "Passed")]
    [InlineData("fr-CA", "Tentative 1 sur 3", "Réussi")]
    public void AttemptStripKeepsEveryAttemptAndRawOutcomeWithGlyphAndLabel(string cultureName, string attemptLabel, string passed)
    {
        AdoTestAttempt[] attempts = [Attempt(1, "Failed", AdoTestOutcomeClass.Failure), Attempt(2, "Mystery<script>", AdoTestOutcomeClass.Other), Attempt(3, "Passed", AdoTestOutcomeClass.Pass)];
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        AttemptStrip.Write(writer, attempts, CultureInfo.GetCultureInfo(cultureName));
        string html = writer.ToString();
        Assert.Contains(attemptLabel, html, StringComparison.Ordinal);
        Assert.Contains(passed, html, StringComparison.Ordinal);
        foreach (string glyph in new[] { "✕", "–", "✓" }) Assert.Contains(glyph, html, StringComparison.Ordinal);
        Assert.Contains("Mystery&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Count(html, "<li>"));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void HistoryKeepsAllSixStatesTitlesCurrentMarkerAndToolkitLinksWithoutALegend(string cultureName)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        AdoTestHistoryEntry[] entries = Enum.GetValues<AdoTestHistoryOutcome>().Select((status, i) => new AdoTestHistoryEntry
        {
            BuildId = i + 1, BuildNumber = "<build>" + i.ToString(CultureInfo.InvariantCulture), Outcome = status, IsCurrent = i == 5,
            WebUrl = new Uri("javascript:alert(1)"),
        }).ToArray();
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        HistoryStrip.Write(writer, entries, new Uri("https://ado.example.test/Collection/"), "Project", culture);
        string html = writer.ToString();
        foreach (AdoTestHistoryOutcome status in Enum.GetValues<AdoTestHistoryOutcome>())
        {
            Assert.Contains(StatusPresentation.Glyph(status), html, StringComparison.Ordinal);
            Assert.Contains(StatusPresentation.Label(status, culture), html, StringComparison.Ordinal);
        }
        Assert.Contains("aria-current=\"true\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("status-legend", html, StringComparison.Ordinal);
        Assert.Equal(entries.Length, System.Text.RegularExpressions.Regex.Count(html, " title=\""));
        Assert.Contains("title=\"&lt;build&gt;0: " + StatusPresentation.Label(entries[0].Outcome, culture) + "\"", html, StringComparison.Ordinal);
        Assert.Contains("ms.vss-test-web.build-test-results-tab", html, StringComparison.Ordinal);
        Assert.Contains("&lt;build&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", html, StringComparison.Ordinal);
    }

    private static AdoTestAttempt Attempt(int number, string outcome, AdoTestOutcomeClass kind) => new()
    { Number = number, RunId = 1, ResultId = number, Outcome = outcome, OutcomeClass = kind };
}
