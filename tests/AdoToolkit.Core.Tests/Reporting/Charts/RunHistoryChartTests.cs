using System.Net.Http;
using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.Charts;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.Tests.TestRuns;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.Charts;

[Trait("Acceptance", "S5-3")]
public sealed class RunHistoryChartTests
{
    [Theory]
    [InlineData("en-US", "This run", "Unavailable")]
    [InlineData("fr-CA", "Cette exécution", "Indisponible")]
    public void CurrentOutlineUnavailableHatchingAndEquivalentTableArePresent(string cultureName, string current, string unavailable)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        AdoBuildTestSummary[] history = [Summary(1, 3, 2, 1, 1), Summary(2, 0, 0, 0, 0, available: false), Summary(3, 1, 0, 1, 0, current: true)];
        string html = Render(history, culture);
        Assert.Contains("<svg", html, StringComparison.Ordinal);
        // The table is always visible: no <details> that only a script could open.
        Assert.Contains("<div class=\"history-data\"><div class=\"table-scroll\"><table><caption>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<details", html, StringComparison.Ordinal);
        Assert.Contains("class=\"chart-current\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"chart-hatch\"", html, StringComparison.Ordinal);
        Assert.Contains(current, html, StringComparison.Ordinal);
        Assert.Contains(unavailable, html, StringComparison.Ordinal);
        Assert.Contains("data-build-id=\"3\" aria-current=\"true\"", html, StringComparison.Ordinal);
        // In the table, this build's row says so in a pill after its link; no arrow follows a link.
        Assert.Contains("\" aria-current=\"true\">" + SinkEncoding.Attribute(history[2].BuildNumber) + "</a> <span class=\"this-run\">" + current + "</span></th>",
            html, StringComparison.Ordinal);
        Assert.DoesNotContain("↗", html, StringComparison.Ordinal);
        Assert.DoesNotContain("remote.example.test", html, StringComparison.Ordinal);
        foreach (AdoBuildTestSummary item in history)
        {
            string link = SinkEncoding.Attribute(AdoWebLinks.BuildTestResult(TestRunFixture.Connection.CollectionUri, "Équipe Web", item.BuildId).AbsoluteUri);
            Assert.Equal(2, Regex.Count(html, Regex.Escape("href=\"" + link + "\"")));
            Assert.Contains(SinkEncoding.Attribute(item.BuildNumber), html, StringComparison.Ordinal);
            Assert.Contains(SinkEncoding.Attribute(item.SourceBranch!), html, StringComparison.Ordinal);
            Assert.Contains(SinkEncoding.Attribute(item.FinishTime!.Value.ToString("g", culture)), html, StringComparison.Ordinal);
            foreach (int count in new[] { item.Passed, item.Failed, item.Flaky, item.Other })
                Assert.Contains("<td>" + count.ToString(culture) + "</td>", html, StringComparison.Ordinal);
        }
        Assert.Contains("data-outcome=\"passed\" data-count=\"3\"", html, StringComparison.Ordinal);
        Assert.Contains("data-outcome=\"failed\" data-count=\"2\"", html, StringComparison.Ordinal);
        Assert.Contains("data-outcome=\"other\" data-count=\"1\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-outcome=\"flaky\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SvgGeometryIsInvariantAndZeroEmptyAndLargeCountsRemainFinite()
    {
        AdoBuildTestSummary[] history = [Summary(1, 1, 2, 0, 4), Summary(2, 0, 0, 0, 0, current: true), Summary(3, int.MaxValue, int.MaxValue, 0, int.MaxValue)];
        string en = Render(history, CultureInfo.GetCultureInfo("en-US")), fr = Render(history, CultureInfo.GetCultureInfo("fr-CA"));
        const string geometry = "(?:x|y|width|height|viewBox|d)=\"[^\"]*\"";
        Assert.Equal(Regex.Matches(en, geometry).Select(static match => match.Value), Regex.Matches(fr, geometry).Select(static match => match.Value));
        Assert.Contains("class=\"chart-zero\"", en, StringComparison.Ordinal);
        Assert.DoesNotContain("NaN", en, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", en, StringComparison.Ordinal);
        Assert.Contains("<tbody></tbody>", Render([], CultureInfo.GetCultureInfo("en-US")), StringComparison.Ordinal);
    }

    [Fact]
    public void AFinishTimeAtTheLimitOfTheCalendarKeepsItsOwnOffset()
    {
        CultureInfo culture = CultureInfo.GetCultureInfo("en-US");
        AdoBuildTestSummary summary = new()
        {
            BuildId = 1, BuildNumber = "20260916.1", FinishTime = DateTimeOffset.MinValue, Passed = 1, IsAvailable = true,
            WebUrl = new Uri("https://remote.example.test/wrong"),
        };
        using StringWriter writer = new(culture);
        RunHistoryChart.Write(writer, [summary], TestRunFixture.Connection.CollectionUri, "Équipe Web", culture, TimeSpan.FromHours(-4));
        Assert.Contains(DateTimeOffset.MinValue.ToString("g", culture), writer.ToString(), StringComparison.Ordinal);
    }

    // A bar caption cut at its character limit must not split a surrogate pair: the lone half
    // reaches the sink, where the encoder replaces it with U+FFFD under a correct tooltip.
    [Fact]
    public void BarCaptionCutAtItsLimitKeepsTheAstralCharacterWhole()
    {
        // The pair straddles the last character the caption keeps, so a raw slice keeps its high half.
        string number = new string('N', 26) + "\U0001F680 integration suite";
        Assert.Equal('\uD83D', number[26]);
        AdoBuildTestSummary item = new()
        {
            BuildId = 1, BuildNumber = number, SourceBranch = "refs/heads/main",
            FinishTime = new DateTimeOffset(2026, 9, 16, 12, 30, 0, TimeSpan.Zero),
            Passed = 1, Failed = 0, Flaky = 0, Other = 0, IsAvailable = true, IsCurrent = true,
            WebUrl = new Uri("https://remote.example.test/wrong"),
        };

        string html = Render([item], CultureInfo.GetCultureInfo("en-US"));

        // The sink encodes a lone half as the replacement character, so the caption would
        // contradict the tooltip, which carries the whole build number.
        Assert.DoesNotContain("&#xFFFD;", html, StringComparison.Ordinal);
    }

    private static string Render(IReadOnlyList<AdoBuildTestSummary> history, CultureInfo culture)
    {
        using StringWriter writer = new(culture);
        RunHistoryChart.Write(writer, history, TestRunFixture.Connection.CollectionUri, "Équipe Web", culture);
        return writer.ToString();
    }

    private static AdoBuildTestSummary Summary(int id, int passed, int failed, int flaky, int other, bool available = true, bool current = false) => new()
    {
        BuildId = id, BuildNumber = "Build <script>\"" + id.ToString(CultureInfo.InvariantCulture), SourceBranch = "refs/heads/<branch>&é",
        FinishTime = new DateTimeOffset(2026, 9, 16, 12, 30, 0, TimeSpan.Zero), Passed = passed, Failed = failed, Flaky = flaky, Other = other,
        IsAvailable = available, IsCurrent = current, WebUrl = new Uri("https://remote.example.test/wrong"),
    };
}
