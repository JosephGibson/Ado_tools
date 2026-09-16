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
        Assert.Contains("<details class=\"history-data\">", html, StringComparison.Ordinal);
        Assert.Contains("<table><caption>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"chart-current\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"chart-hatch\"", html, StringComparison.Ordinal);
        Assert.Contains(current, html, StringComparison.Ordinal);
        Assert.Contains(unavailable, html, StringComparison.Ordinal);
        Assert.Contains("data-build-id=\"3\" aria-current=\"true\"", html, StringComparison.Ordinal);
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
    public void HostileLabelsAreOnlyEncodedAndCannotSupplyLinks()
    {
        string html = Render([Summary(1, 2, 1, 0, 0)], CultureInfo.GetCultureInfo("en-US"));
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"javascript:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetrievedHistoryCountsFlowUnchangedToSvgAndCells()
    {
        TestRunFixture fixture = RunHistoryTests.History();
        using FakeHttpMessageHandler handler = fixture.Handler();
        using HttpClient client = new(handler);
        AdoBuildTestFailureSet set = await TestRunFixture.Service(client).GetAsync(TestRunFixture.Build(),
            new TestFailureQuery { HistoryCount = 4 }, CultureInfo.GetCultureInfo("en-US"), TestContext.Current.CancellationToken);
        string html = Render(set.History, CultureInfo.GetCultureInfo("en-US"));
        foreach (AdoBuildTestSummary summary in set.History.Where(static item => item.IsAvailable))
        {
            string bar = Regex.Match(html, "data-build-id=\"" + summary.BuildId.ToString(CultureInfo.InvariantCulture) + "\"[\\s\\S]*?</a>").Value;
            foreach ((string status, int count) in new[] { ("passed", summary.Passed), ("failed", summary.Failed), ("other", summary.Other) })
            {
                if (count > 0) Assert.Contains("data-outcome=\"" + status + "\" data-count=\"" + count.ToString(CultureInfo.InvariantCulture) + "\"", bar, StringComparison.Ordinal);
                else Assert.DoesNotContain("data-outcome=\"" + status + "\"", bar, StringComparison.Ordinal);
            }
        }
        // This fixture reports every identity; bars must agree with their rendered cells.
        foreach (AdoBuildTestSummary summary in set.History.Where(static item => item.IsAvailable))
        {
            AdoTestHistoryOutcome[] cells = set.Failures.Select(failure => failure.History.Single(cell => cell.BuildId == summary.BuildId).Outcome).ToArray();
            Assert.Equal(cells.Count(static cell => cell == AdoTestHistoryOutcome.Failed), summary.Failed);
            Assert.Equal(cells.Count(static cell => cell == AdoTestHistoryOutcome.Flaky), summary.Flaky);
            Assert.Equal(cells.Count(static cell => cell is AdoTestHistoryOutcome.Passed or AdoTestHistoryOutcome.Flaky), summary.Passed);
            Assert.Equal(cells.Count(static cell => cell == AdoTestHistoryOutcome.Other), summary.Other);
        }
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
