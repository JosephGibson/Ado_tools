using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.Charts;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// The Runs and history view, on the grouped and partial fixtures. The grouped build has four runs
// in two stages, the first of them outside the attachment window, and no earlier build. The
// partial build has one run and three earlier builds.
public sealed class RunsViewTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData("grouped", "en-US", "Test runs")]
    [InlineData("partial", "fr-CA", "Séries de tests")]
    public void HistoryComesFirstThenTheRunsTableUnderItsOwnHeading(string variant, string culture, string heading)
    {
        string view = View(Render(Model(variant, culture)));
        // The chart and its table right under the view's heading, then the runs.
        int chart = view.IndexOf("</h2>\n<section class=\"history-panel\" id=\"history\">\n<h3>", StringComparison.Ordinal);
        int runs = view.IndexOf("</section>\n<h3>" + heading + "</h3>\n<div class=\"table-scroll\"><table class=\"runs-table\">", StringComparison.Ordinal);
        Assert.True(chart > 0 && runs > chart);
    }

    [Fact]
    public void RunsTableShowsFailedDurationAndReportedTests()
    {
        // Run 201 gets statistics and an end time, run 202 statistics without a failure; the other
        // runs keep neither.
        string html = Render(Model("grouped", change: run => run.Id switch
        {
            201 => Copy(run, completed: run.StartedDate!.Value.AddSeconds(3725),
                counts: new Dictionary<string, int>(StringComparer.Ordinal) { ["Passed"] = 35, ["Failed"] = 2, ["Error"] = 1, ["Timeout"] = 1, ["NotExecuted"] = 1 }),
            202 => Copy(run, counts: new Dictionary<string, int>(StringComparer.Ordinal) { ["Passed"] = 40 }),
            _ => run,
        }));
        string view = View(html);
        Assert.Contains("<tr><th scope=\"col\">Test run</th><th scope=\"col\" class=\"num\">ID</th><th scope=\"col\">Stage</th>", view, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\" class=\"num\">Duration</th><th scope=\"col\" class=\"num\">Tests</th><th scope=\"col\" class=\"num\">Passed</th>"
            + "<th scope=\"col\" class=\"num\">Failed</th><th scope=\"col\" class=\"num\">Reported tests</th><th scope=\"col\">Attachments</th></tr>", view, StringComparison.Ordinal);
        // ID, duration, tests, passed, failed (the failure-class outcomes), reported tests. Passed and
        // failed counts carry their status glyph.
        Assert.Equal(["201", "1:02:05", "40", "✓ 38", "✕ 4", "2"], Numbers(Row(view, 201)));
        Assert.Contains("<td class=\"num\"><span class=\"run-count status-failed\"><span aria-hidden=\"true\">✕</span> 4</span></td>", Row(view, 201), StringComparison.Ordinal);
        Assert.Contains("<td class=\"num\"><span class=\"run-count status-passed\"><span aria-hidden=\"true\">✓</span> 38</span></td>", Row(view, 201), StringComparison.Ordinal);
        // No end time and no statistics: both cells are empty, not zero. A count of zero is muted.
        Assert.Equal(["200", "", "40", "✓ 38", "", "1"], Numbers(Row(view, 200)));
        Assert.Equal(["202", "", "40", "✓ 38", "0", "2"], Numbers(Row(view, 202)));
        Assert.Contains("<td class=\"num\"><span class=\"text-muted\">0</span></td>", Row(view, 202), StringComparison.Ordinal);
        Assert.Equal(["203", "", "40", "✓ 38", "", "1"], Numbers(Row(view, 203)));
    }

    [Theory]
    [InlineData("grouped")]
    [InlineData("partial")]
    public void NumericColumnsAreRightAligned(string variant)
    {
        string html = Render(Model(variant));
        string view = View(html);
        foreach (string header in new[] { "ID", "Duration", "Tests", "Passed", "Failed", "Reported tests" })
            Assert.Contains("<th scope=\"col\" class=\"num\">" + header + "</th>", view, StringComparison.Ordinal);
        Assert.Equal(6 * Regex.Count(view, "<tr data-run="), Regex.Count(Section(view, "<table class=\"runs-table\">", "</table>"), "<td class=\"num\">"));
        Assert.Matches(@"\.num \{ text-align: end; \}", html);
        // The history table keeps its markup; its four count columns are aligned by position.
        Assert.Matches(@"\.history-data th:nth-child\(n\+4\):nth-child\(-n\+7\), \.history-data td:nth-child\(n\+4\):nth-child\(-n\+7\) \{ text-align: end; \}", html);
    }

    // Only the attempt levels that some run has. The heading names them, so a lone number is never
    // read as another level's attempt, and a run without one of them shows a dash in its place.
    [Theory]
    [InlineData("en-US", "Attempts (instance)", "Attempts (stage / instance)")]
    [InlineData("fr-CA", "Tentatives (instance)", "Tentatives (phase / instance)")]
    public void AttemptNumbersShowOnlyTheLevelsThatExist(string culture, string instance, string stageAndInstance)
    {
        string grouped = View(Render(Model("grouped", culture)));
        Assert.Contains("<th scope=\"col\">" + instance + "</th>", grouped, StringComparison.Ordinal);
        Assert.Contains("<td>2</td>", Row(grouped, 201), StringComparison.Ordinal);
        Assert.Contains("<td>1</td>", Row(grouped, 202), StringComparison.Ordinal);
        string staged = View(Render(Model("grouped", culture, change: run => run.Id == 203 ? Copy(run, stageAttempt: 3) : run)));
        Assert.Contains("<th scope=\"col\">" + stageAndInstance + "</th>", staged, StringComparison.Ordinal);
        Assert.Contains("<td>– / 2</td>", Row(staged, 201), StringComparison.Ordinal);
        Assert.Contains("<td>– / 1</td>", Row(staged, 202), StringComparison.Ordinal);
        Assert.Contains("<td>3 / 2</td>", Row(staged, 203), StringComparison.Ordinal);
        // No run has an attempt number: no column at all.
        string partial = View(Render(Model("partial", culture)));
        Assert.DoesNotContain("(instance)", partial, StringComparison.Ordinal);
        Assert.DoesNotContain(" / –", partial, StringComparison.Ordinal);
    }

    [Fact]
    public void StateColumnAppearsOnlyWhenARunIsNotCompleted()
    {
        foreach (string variant in new[] { "grouped", "partial" })
        {
            string view = View(Render(Model(variant)));
            Assert.DoesNotContain("<th scope=\"col\">State</th>", view, StringComparison.Ordinal);
            Assert.DoesNotContain("<td>Completed</td>", view, StringComparison.Ordinal);
        }
        string running = View(Render(Model("grouped", change: run => run.Id == 203 ? Copy(run, state: "InProgress") : run)));
        Assert.Contains("<th scope=\"col\" class=\"num\">Duration</th><th scope=\"col\">State</th><th scope=\"col\" class=\"num\">Tests</th>", running, StringComparison.Ordinal);
        Assert.Contains("<td>InProgress</td>", Row(running, 203), StringComparison.Ordinal);
        Assert.Contains("<td>Completed</td>", Row(running, 201), StringComparison.Ordinal);
    }

    [Fact]
    public void AttachmentCellCountsListedAndDownloadedFiles()
    {
        using TestDirectory directory = new();
        TestFailureReportModel model = Model("grouped");
        string view = View(Render(model));
        Assert.Contains("<td>Outside the window</td>", Row(view, 200), StringComparison.Ordinal);
        Assert.Contains("<td>0 listed, 0 downloaded</td>", Row(view, 201), StringComparison.Ordinal);
        Assert.Contains("<td>2 listed, 0 downloaded</td>", Row(view, 202), StringComparison.Ordinal);

        // One of the two files of run 202 is downloaded.
        string folder = Directory.CreateDirectory(Path.Combine(directory.Root, "files")).FullName;
        File.WriteAllText(Path.Combine(folder, "r202-13-a63.txt"), "agent output");
        AdoTestFailure first = model.Failures[0];
        AdoTestAttempt third = first.Attempts[2];
        AdoTestFailure downloaded = first.WithAttempts([first.Attempts[0], first.Attempts[1],
            third.WithAttachments([.. third.Attachments.Select(a => a.Id == 63 ? a.WithDownload(AdoTestAttachmentStatus.Downloaded, "files/r202-13-a63.txt") : a)]), first.Attempts[3]]);
        model = TestFailureReportModelBuilder.WithAttachments(model, [downloaded, model.Failures[1]], [], new TestFailureLocalAttachments
        {
            FolderName = "files", SourceFolder = folder, MaximumInlineJsonBytes = 262144,
            Files = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["r202-13-a63.txt"] = new FileInfo(Path.Combine(folder, "r202-13-a63.txt")).Length },
        });
        string html = Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model, folder);
        Assert.Contains("<td>2 listed, 1 downloaded</td>", Row(View(html), 202), StringComparison.Ordinal);
        Assert.Contains("<td>2 répertoriées, 0 téléchargées</td>", Row(View(Render(Model("grouped", "fr-CA"))), 202), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-US", "Latest run")]
    [InlineData("fr-CA", "Dernière série de tests")]
    public void LatestRunIsMarkedAndRunsOutsideTheWindowAreMuted(string culture, string label)
    {
        string html = Render(Model("grouped", culture));
        string view = View(html);
        // Attempt order puts the second French run last: it is the run whose larger attachments are downloaded.
        Assert.StartsWith("<tr data-run=\"203\" data-latest-run>", Row(view, 203), StringComparison.Ordinal);
        // The label ends the run cell, so it lines up; the ID has a column of its own.
        Assert.Contains(">Synthetic Tests_FR</a> <span class=\"latest-run\">" + label + "</span></span></td><td class=\"num\">203</td>", Row(view, 203), StringComparison.Ordinal);
        Assert.Matches(@"\.runs-table tr\[data-latest-run\] > td:first-child \{ box-shadow: inset 3px 0 0 var\(--current\); \}", html);
        Assert.Equal(1, Regex.Count(view, "data-latest-run"));
        Assert.Equal(1, Regex.Count(view, "class=\"latest-run\""));
        Assert.StartsWith("<tr data-run=\"200\" class=\"outside-window\">", Row(view, 200), StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(view, "class=\"outside-window\""));
        Assert.Matches(@"\.runs-table \.outside-window td \{ color: var\(--text-muted\); \}", html);
        // The only run of the partial build is its latest.
        Assert.StartsWith("<tr data-run=\"201\" data-latest-run>", Row(View(Render(Model("partial", culture))), 201), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("grouped", "en-US", 1)]
    [InlineData("grouped", "fr-CA", 1)]
    [InlineData("partial", "en-US", 0)]
    public void WindowNoteIsASentenceUnderTheRunsTable(string variant, string culture, int omitted)
    {
        TestFailureReportModel model = Model(variant, culture);
        string view = View(Render(model));
        string start = model.AttachmentWindowStart.ToOffset(model.GeneratedAt.Offset).ToString("g", model.Culture);
        string sentence = SinkEncoding.Attribute(Messages.Get(AdoMessage.TestReportWindowNote, model.Culture, start, omitted));
        Assert.Contains("</tbody></table></div>\n<p class=\"window-note\">" + sentence + "</p>\n<h3>", view, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(view, "class=\"window-note\""));
        string text = TestFailureMarkup.Text(sentence);
        Assert.Contains(start, text, StringComparison.Ordinal);
        Assert.EndsWith(culture == "en-US" ? "Left out from older runs: " + omitted.ToString(English) + "." : "Omises des séries plus anciennes : 1.", text, StringComparison.Ordinal);
        Assert.StartsWith(culture == "en-US" ? "Attachments are listed for runs started on or after " : "Les pièces jointes sont répertoriées pour les séries de tests commencées le ", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-US", "History by test", "Trend", "Since 9/15/2026")]
    [InlineData("fr-CA", "Historique par test", "Tendance", "Depuis le 2026-09-15")]
    public void HistoryByTestHasOneRowPerTestOneCellPerBuildAndTheTrend(string culture, string heading, string trend, string since)
    {
        TestFailureReportModel model = Model("flaky", culture);
        string view = View(Render(model));
        int start = view.IndexOf("<h3>" + heading + "</h3>\n<div class=\"table-scroll\"><table class=\"failure-table history-by-test\">", StringComparison.Ordinal);
        Assert.True(start > view.IndexOf("<table class=\"runs-table\">", StringComparison.Ordinal));
        string table = view[start..view.IndexOf("</table>", start, StringComparison.Ordinal)];
        // Number, test, class, one column per build, oldest first, then the trend.
        Assert.Contains("<th scope=\"col\">#</th><th scope=\"col\">Test</th><th scope=\"col\">" + (culture == "en-US" ? "Class" : "Classe") + "</th>"
            + string.Concat(model.History.Select(static build => "<th scope=\"col\" class=\"col-build\">" + build.BuildNumber + "</th>"))
            + "<th scope=\"col\" class=\"col-trend\">" + trend + "</th></tr></thead>", table, StringComparison.Ordinal);
        Assert.Equal(["f-1", "f-2"], Regex.Matches(table, "<tr data-index-for=\"([^\"]+)\">").Select(static match => match.Groups[1].Value));
        string failed = Section(table, "<tr data-index-for=\"f-1\">", "</tr>"), flaky = Section(table, "<tr data-index-for=\"f-2\">", "</tr>");
        // Unavailable, not run, failed, failed: failing since the build before this one.
        Assert.Equal(["status-unavailable", "status-notrun", "status-failed", "status-failed"], CellStatuses(failed));
        Assert.Matches("<td class=\"col-trend\"><a class=\"trend trend-since\"[^>]*>" + since + "</a></td>$", failed);
        // Failed in the build before and flaky in this one: flaky recurs too.
        Assert.Equal(["status-unavailable", "status-notrun", "status-failed", "status-flaky"], CellStatuses(flaky));
        Assert.Matches("<td class=\"col-trend\"><a class=\"trend trend-since\"[^>]*>" + since + "</a></td>$", flaky);
        Assert.Contains("<td class=\"col-test\"><a href=\"#f-2\"><span class=\"test-name\" title=\"RetryPayment\">RetryPayment</span></a></td>"
            + "<td class=\"col-class\"><span class=\"class-name\" title=\"CheckoutTests\">CheckoutTests</span></td>", flaky, StringComparison.Ordinal);
        // Every cell names its outcome for a reader who cannot see the glyph.
        Assert.Equal(8, Regex.Count(table, "<td class=\"col-build\"><span class=\"status-glyph status-[a-z]+\" role=\"img\" aria-label=\"[^\"]+\">"));
    }

    // The grouped fixture: its tests carry no history cells, so they have no trend; a failure after a
    // pass is new, whatever came before.
    [Fact]
    public void HistoryByTestShowsDashesWithoutCellsAndANewFailureAfterAPass()
    {
        string grouped = View(Render(Model("grouped")));
        string row = Section(grouped, "<table class=\"failure-table history-by-test\">", "</table>");
        Assert.Equal(2, Regex.Count(row, "<td class=\"col-build\"><span class=\"text-muted\">—</span></td><td class=\"col-trend\"></td></tr>"));

        AdoBuildTestFailureSet set = TestFailureReportFixture.Set("partial");
        AdoTestFailure source = set.Failures[0];
        AdoTestHistoryEntry Cell(int index, AdoTestHistoryOutcome outcome) => new()
        { BuildId = source.History[index].BuildId, BuildNumber = source.History[index].BuildNumber, Outcome = outcome, IsCurrent = index == 3, WebUrl = source.History[index].WebUrl };
        AdoTestFailure passedBefore = new()
        {
            Ordinal = 1, Classification = source.Classification, ShortName = source.ShortName, TestName = source.TestName, Storage = source.Storage, CollectionUri = source.CollectionUri,
            Attempts = source.Attempts,
            History = [Cell(0, AdoTestHistoryOutcome.Failed), Cell(1, AdoTestHistoryOutcome.Failed), Cell(2, AdoTestHistoryOutcome.Passed), Cell(3, AdoTestHistoryOutcome.Failed)],
        };
        string view = View(Render(TestFailureReportModelBuilder.Build(new AdoBuildTestFailureSet
        {
            Build = set.Build, Runs = set.Runs, Summary = set.Summary, History = set.History, Failures = [passedBefore], FailedCount = 1, RetrievedAt = set.RetrievedAt,
            CollectionUri = set.CollectionUri,
        }, TestFailureReportFixture.Options("en-US"))));
        Assert.EndsWith("<td class=\"col-trend\"><span class=\"trend trend-new\"><span aria-hidden=\"true\">✦</span> New</span></td>",
            Section(view, "<tr data-index-for=\"f-1\">", "</tr>"), StringComparison.Ordinal);
        // No failure, no table.
        TestFailureReportModel empty = TestFailureReportModelBuilder.Build(new AdoBuildTestFailureSet
        { Build = set.Build, Runs = set.Runs, Summary = set.Summary, History = set.History, RetrievedAt = set.RetrievedAt, CollectionUri = set.CollectionUri },
            TestFailureReportFixture.Options("en-US"));
        Assert.DoesNotContain("history-by-test\"", View(Render(empty)), StringComparison.Ordinal);
    }

    [Fact]
    public void ChartIsDrawnAtItsNaturalSizeAndGrowsWithTheBuilds()
    {
        string one = Render(Model("grouped")), four = Render(Model("partial"));
        (int Width, int Height) Size(string html)
        {
            Match svg = Regex.Match(html, "<svg class=\"history-chart\"[^>]* width=\"([0-9]+)\" height=\"([0-9]+)\" viewBox=\"0 0 ([0-9]+) ([0-9]+)\">");
            Assert.True(svg.Success);
            // The attributes repeat the viewBox, so one unit is one pixel and nothing is scaled.
            Assert.Equal((svg.Groups[1].Value, svg.Groups[2].Value), (svg.Groups[3].Value, svg.Groups[4].Value));
            return (int.Parse(svg.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(svg.Groups[2].Value, CultureInfo.InvariantCulture));
        }
        Assert.Equal(Size(one).Height, Size(four).Height);
        Assert.True(Size(four).Width > Size(one).Width);
        Assert.Matches(@"\.history-chart \{ display: block; width: auto; min-width: 0; max-width: none; \}", one);
        Assert.DoesNotContain("max-height: 220px", one, StringComparison.Ordinal);
        Assert.Contains("<div class=\"chart-scroll\"><svg class=\"history-chart\"", one, StringComparison.Ordinal);
    }

    [Fact]
    public void BarsAreLabelledWithTheBuildNumberAndTheSlotFitsTheLongestOne()
    {
        string html = Render(Model("partial"));
        Assert.Equal(["20260916.0", "20260916.1", "20260916.2", "20260916.3"],
            Regex.Matches(html, "<text class=\"chart-caption\" x=\"[0-9.]+\" y=\"[0-9.]+\">(20260916\\.[0-3])</text>").Select(static match => match.Groups[1].Value));
        Assert.DoesNotMatch("<text class=\"chart-caption\"[^>]*>[0-9]</text>", html);
        // No arrow after a link or a caption: every one of them goes to Azure DevOps.
        Assert.DoesNotContain("↗", html, StringComparison.Ordinal);

        static string Chart(params string[] numbers)
        {
            using StringWriter writer = new(CultureInfo.InvariantCulture);
            RunHistoryChart.Write(writer, [.. numbers.Select((number, index) => new AdoBuildTestSummary
            {
                BuildId = index + 1, BuildNumber = number, Passed = 5, Failed = 1, IsAvailable = true, WebUrl = TestFailureReportFixture.Untrusted,
            })], TestFailureReportFixture.Collection, TestFailureReportFixture.Project, English);
            return writer.ToString();
        }
        static int Width(string chart) => int.Parse(Regex.Match(chart, "<svg class=\"history-chart\"[^>]* width=\"([0-9]+)\"").Groups[1].Value, CultureInfo.InvariantCulture);
        string shortNumbers = Chart("1", "2", "3"), longNumbers = Chart("Nightly-main-20260916.3", "2", "3");
        Assert.True(Width(longNumbers) > Width(shortNumbers));
        // A number of more than 28 characters is cut in the label only; the title and the table keep it whole.
        string name = new('x', 40);
        string cut = Chart(name);
        Assert.Contains(">" + new string('x', 27) + "…</text>", cut, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(cut, name));
    }

    [Fact]
    public void FailedSegmentStaysVisibleItsCountStandsAboveTheBarAndTheChartHasALegend()
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        RunHistoryChart.Write(writer, [new AdoBuildTestSummary
        {
            BuildId = 1, BuildNumber = "1", Passed = 2999, Failed = 1, Other = 0, IsAvailable = true, IsCurrent = true, WebUrl = TestFailureReportFixture.Untrusted,
        }], TestFailureReportFixture.Collection, TestFailureReportFixture.Project, English);
        string chart = writer.ToString();
        Match failed = Regex.Match(chart, "<g data-outcome=\"failed\" data-count=\"1\"><title>[^<]*</title><rect class=\"chart-fail\" x=\"[0-9.]+\" y=\"([0-9.]+)\" width=\"52\" height=\"([0-9.]+)\"/>");
        Assert.True(failed.Success);
        // One of 3,000 would be 0.04 of a unit high.
        Assert.Equal(6, double.Parse(failed.Groups[2].Value, CultureInfo.InvariantCulture));
        Match passed = Regex.Match(chart, "<rect class=\"chart-pass\" x=\"[0-9.]+\" y=\"([0-9.]+)\" width=\"52\" height=\"([0-9.]+)\"/>");
        // The minimum is taken from the tallest segment: the bar keeps the full height, 120 units since 0.8.0.
        Assert.Equal(120, double.Parse(passed.Groups[2].Value, CultureInfo.InvariantCulture) + 6, 3);
        Match count = Regex.Match(chart, "<text class=\"chart-failed\" x=\"[0-9.]+\" y=\"([0-9.]+)\">✕ 1</text>");
        Assert.True(count.Success);
        Assert.True(double.Parse(count.Groups[1].Value, CultureInfo.InvariantCulture) < double.Parse(failed.Groups[1].Value, CultureInfo.InvariantCulture));

        string html = Render(Model("partial", "fr-CA"));
        Assert.Equal(["Réussi", "Échec", "Autre", "Indisponible"], Regex.Matches(Section(html, "<ul class=\"chart-legend\">", "</ul>"),
            "</svg> ([^<]+)</li>").Select(static match => match.Groups[1].Value));
        // An available build shows its failed count, zero included; an unavailable one shows none.
        Assert.Equal(3, Regex.Count(html, "<text class=\"chart-failed\""));
        Assert.Contains(">✕ 0</text>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("grouped")]
    [InlineData("partial")]
    public void HistoryTableIsVisibleWithoutAScript(string variant)
    {
        TestFailureReportModel model = Model(variant);
        string html = Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.Contains("</svg></div><div class=\"history-data\"><div class=\"table-scroll\"><table><caption>History data</caption>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<details class=\"history-data\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<summary>History data</summary>", html, StringComparison.Ordinal);
        // The script only scrolls to the row; there is nothing left to open.
        string script = TestFailureAssets.Read("test-failures.js");
        Assert.DoesNotContain("closest('details')", script, StringComparison.Ordinal);
        Assert.Contains("show('runs');\n        row.scrollIntoView", script, StringComparison.Ordinal);
    }

    // Expand all and Collapse all act on the cards, so they show in Details alone: a history cell that
    // shows the Runs view with the hash still on #details hides them too (0.7.5 finding B5).
    [Fact]
    public void ExpandAllAndCollapseAllShowInDetailsAlone()
    {
        string html = Render(Model("grouped"));
        foreach (string action in new[] { "expand", "collapse" })
            Assert.Contains("<button type=\"button\" class=\"interactive\" data-enhance hidden data-action=\"" + action + "\" data-details-only>", html, StringComparison.Ordinal);
        string script = TestFailureAssets.Read("test-failures.js");
        string show = script[script.IndexOf("const show = id => {", StringComparison.Ordinal)..script.IndexOf("const select = card =>", StringComparison.Ordinal)];
        Assert.Contains("all('[data-details-only]').forEach(control => { control.hidden = target.id !== 'details'; });", show, StringComparison.Ordinal);
        // The buttons no longer move to Details themselves: they are never shown anywhere else.
        Assert.DoesNotContain("location.hash = 'details'", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("grouped", "en-US", "Generated on")]
    [InlineData("partial", "fr-CA", "Généré le")]
    public void ProvenanceIsAFooterUnderEveryView(string variant, string culture, string generated)
    {
        TestFailureReportModel model = Model(variant, culture);
        string html = Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        int footer = html.IndexOf("</main>\n<footer class=\"report-footer\"><dl class=\"secondary-line\"><div><dt>", StringComparison.Ordinal);
        Assert.True(footer > 0);
        string line = Section(html[footer..], "<footer", "</footer>");
        Assert.StartsWith(generated, TestFailureMarkup.Text(line), StringComparison.Ordinal);
        foreach (string value in new[] { model.ToolkitVersion, "https://ado.example.test", "/tfs/Collection%20A/", TestFailureReportFixture.Project })
            Assert.Contains(SinkEncoding.Attribute(value), line, StringComparison.Ordinal);
        // Once, and no longer inside the Runs view.
        Assert.Equal(1, Regex.Count(html, "class=\"secondary-line\""));
        Assert.DoesNotContain("secondary-line", View(html), StringComparison.Ordinal);
        Assert.Matches(@"\.report-footer \{ max-width: 1600px;", html);
    }

    private static string Render(TestFailureReportModel model) => TestFailureReportFixture.Render(model);

    private static TestFailureReportModel Model(string variant, string culture = "en-US", Func<AdoTestRun, AdoTestRun>? change = null)
    {
        AdoBuildTestFailureSet set = TestFailureReportFixture.Set(variant);
        if (change is not null)
            set = new AdoBuildTestFailureSet
            {
                Build = set.Build, Runs = [.. set.Runs.Select(change)], Summary = set.Summary, History = set.History, Failures = set.Failures, FailedCount = set.FailedCount,
                FlakyCount = set.FlakyCount, Status = set.Status, Diagnostics = set.Diagnostics, RetrievedAt = set.RetrievedAt, CollectionUri = set.CollectionUri,
            };
        return TestFailureReportModelBuilder.Build(set, TestFailureReportFixture.Options(culture));
    }

    private static AdoTestRun Copy(AdoTestRun run, string? state = null, DateTimeOffset? completed = null, IReadOnlyDictionary<string, int>? counts = null,
        int? stageAttempt = null) => new()
        {
            Id = run.Id, Name = run.Name, BuildId = run.BuildId, State = state ?? run.State, StartedDate = run.StartedDate, CompletedDate = completed ?? run.CompletedDate,
            PipelineAttempt = run.PipelineAttempt, StageAttempt = stageAttempt ?? run.StageAttempt, PhaseAttempt = run.PhaseAttempt, StageName = run.StageName,
            PhaseName = run.PhaseName, JobName = run.JobName, TotalTests = run.TotalTests, PassedTests = run.PassedTests,
            OutcomeCounts = counts ?? run.OutcomeCounts, TeamProject = run.TeamProject, CollectionUri = run.CollectionUri,
        };

    private static string View(string html) => Section(html, "<section class=\"view\" id=\"runs\" data-view>", "</section>\n<section class=\"view\"", "</section>\n</main>");

    private static string Row(string view, int run) => Section(view, "<tr data-run=\"" + run.ToString(CultureInfo.InvariantCulture) + "\"", "</tr>");

    // The text of each numeric cell.
    private static IEnumerable<string> Numbers(string row) =>
        Regex.Matches(row, "<td class=\"num\">(.*?)</td>").Select(static match => TestFailureMarkup.Text(match.Groups[1].Value));

    private static IEnumerable<string> CellStatuses(string row) =>
        Regex.Matches(row, "<td class=\"col-build\"><span class=\"status-glyph (status-[a-z]+)\"").Select(static match => match.Groups[1].Value);

    // From the opening text up to, not including, the first of the closing texts that follows it.
    private static string Section(string html, string opening, params string[] closings)
    {
        int start = html.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, opening);
        int end = closings.Select(closing => html.IndexOf(closing, start + opening.Length, StringComparison.Ordinal)).Where(static index => index >= 0).DefaultIfEmpty(-1).Min();
        Assert.True(end > start, opening);
        return html[start..end];
    }
}
