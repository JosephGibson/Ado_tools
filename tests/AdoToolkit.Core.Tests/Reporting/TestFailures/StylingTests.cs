using System.Text.RegularExpressions;
using AdoToolkit.Core.Builds;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// How the failed-test report looks: what the markup carries for the stylesheet, and the rules that use it.
public sealed class StylingTests
{
    private static readonly string Css = TestFailureAssets.Read("test-failures.css");
    // Where each diagnostic of the severity test arrived, in the order the report shows them.
    private static readonly int[] ArrivalIndexes = [2, 4, 0, 3, 5, 1];

    [Fact]
    public void ByErrorAndBugsTablesHaveNoStripesAndKeepTheHover()
    {
        string html = TestFailureReportFixture.Render("partial");
        Assert.Contains("<table class=\"failure-table\">", View(html, "overview"), StringComparison.Ordinal);
        Assert.Contains("<table class=\"failure-table by-error\">", View(html, "by-error"), StringComparison.Ordinal);
        Assert.Contains("<table class=\"failure-table by-error by-bug\">", View(html, "bugs"), StringComparison.Ordinal);
        int stripes = Css.IndexOf(".by-error tbody tr:nth-child(even) { background: none; }", StringComparison.Ordinal);
        int hover = Css.IndexOf(".failure-table tbody tr:hover { background: var(--surface); }", StringComparison.Ordinal);
        // Both rules have the same specificity, so the hover wins only because it comes later.
        Assert.True(stripes >= 0 && stripes < hover);
    }

    [Fact]
    public void InPageLinksKeepTheirColourAfterAVisit()
    {
        int rule = Css.IndexOf("a:where([href^=\"#\"]):visited { color: var(--link); }", StringComparison.Ordinal);
        Assert.True(rule >= 0);
        // The rule weighs as much as the base a:visited, so a later rule still gives the tabs and the Partial link their own colour.
        Assert.True(rule < Css.IndexOf(".view-links a {", StringComparison.Ordinal));
        Assert.True(rule < Css.IndexOf(".partial-link:visited { color: var(--flaky); }", StringComparison.Ordinal));
        // An attempt square keeps the colour of its status.
        foreach ((string status, string colour) in new[] { ("passed", "pass"), ("failed", "fail"), ("other", "other") })
            Assert.Contains("a.sq.status-" + status + ":visited { color: var(--" + colour + "); }", Css, StringComparison.Ordinal);
        string html = TestFailureReportFixture.Render("flaky");
        Assert.Contains("<td class=\"col-test\"><a href=\"#f-1\">", html, StringComparison.Ordinal);
        Assert.Contains("<a class=\"sq status-passed\" href=\"#f-2-a2\"", html, StringComparison.Ordinal);
    }

    // Every link of the report goes to Azure DevOps or within the page, so no link carries an arrow,
    // and nothing in a link is an image of its own.
    [Theory]
    [InlineData("failed")]
    [InlineData("flaky")]
    [InlineData("partial")]
    [InlineData("hostile")]
    [InlineData("grouped")]
    public void NoLinkCarriesAnArrow(string variant)
    {
        string html = TestFailureReportFixture.Render(variant);
        Assert.DoesNotContain("↗", html, StringComparison.Ordinal);
        Assert.DoesNotMatch("<a [^>]*>[^<]*<span [^>]*role=\"img\"", html);
        Assert.DoesNotContain("[role=\"img\"]", Css, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinalHasItsOwnRightAlignedSpanInEveryTable()
    {
        string html = TestFailureReportFixture.Render("flaky");
        int rows = Regex.Count(html, "<tr data-index-for=\"f-[0-9]+\">");
        // Two tests in Overview, By error and History by test; in Open bugs, the one with a bug, then
        // the other one without an open bug.
        Assert.Equal(8, rows);
        Assert.Equal(rows, Regex.Count(html,
            "<tr data-index-for=\"f-[0-9]+\"><td class=\"col-number\"><span class=\"status-glyph status-[a-z]+\" role=\"img\" aria-label=\"[^\"]+\">[^<]</span> <span class=\"ordinal\">[0-9]+</span></td>"));
        // As wide as the largest ordinal, so the glyph sits close to it and 1, 10 and 100 end at the same place.
        Assert.Contains(".ordinal { display: inline-block; min-width: 2ch; text-align: end; }", Css, StringComparison.Ordinal);
        Assert.Contains(":root:has(#overview tbody > tr:nth-child(100)) .ordinal { min-width: 3ch; }", Css, StringComparison.Ordinal);
        Assert.Contains(":root:has(#overview tbody > tr:nth-child(1000)) .ordinal { min-width: 4ch; }", Css, StringComparison.Ordinal);
    }

    // The rule counts the Overview's rows, so its table has exactly one row per test: 100 tests make the
    // hundredth row, and 99 do not.
    [Theory]
    [InlineData(99)]
    [InlineData(100)]
    public void TheOverviewHasOneRowPerTestForTheOrdinalWidth(int count)
    {
        TestFailureReportModel model = Build("en-US", failures: [.. Enumerable.Range(1, count).Select(index => Failure("T" + index.ToString(CultureInfo.InvariantCulture), "Failed"))]);
        string html = TestFailureReportFixture.Render(model);
        string body = Section(View(html, "overview"), "<tbody>", "</tbody>");
        Assert.Equal(count, Regex.Count(body, "<tr[ >]"));
        Assert.Equal(count, Regex.Count(body, "<tr data-index-for=\"f-[0-9]+\">"));
        Assert.Contains("<span class=\"ordinal\">" + count.ToString(CultureInfo.InvariantCulture) + "</span>", body, StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataPairsAreSeparatedByALeadingRule()
    {
        Assert.Matches(@"\.metadata-grid > div \{[^{}]*padding-inline-start: var\(--space-2\); border-inline-start: 1px solid var\(--separator\); \}", Css);
        Assert.Contains("<dl class=\"metadata-grid\"><div><dt>", TestFailureReportFixture.Render(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void AttemptSummaryHasADurationColumnAndAMachineColumn(string culture)
    {
        string html = TestFailureReportFixture.Render("flaky", culture);
        foreach (string attempt in new[] { "f-1-a1", "f-2-a1", "f-2-a2" })
            Assert.Matches("^<details class=\"attempt\" id=\"f-[12]-a[12]\"><summary><span class=\"attempt-title\">[^<]+</span> <span class=\"status-badge status-[a-z]+\">.*?</span> "
                + "<span class=\"attempt-meta attempt-duration\">[^<]+ s</span> <span class=\"attempt-meta attempt-machine\" title=\"SYNTHETIC-AGENT\">SYNTHETIC-AGENT</span>",
                Section(html, "<details class=\"attempt\" id=\"" + attempt + "\">", "</summary>"));
        // The title has a fixed width, wider in French.
        Assert.Matches(@"\.attempt-title \{ display: inline-block; min-width: 7rem;", Css);
        Assert.Contains(":lang(fr) .attempt-title { min-width: 8.5rem; }", Css, StringComparison.Ordinal);
        Assert.Matches(@"\.attempt-duration \{ display: inline-block; min-width: [0-9.]+rem; text-align: end; \}", Css);
        Assert.Matches(@"\.attempt-machine \{ display: inline-block; min-width: [0-9.]+rem; \}", Css);
        Assert.Matches(@"\.attempt-meta\.attempt-machine \{ max-width: 12rem; overflow: hidden; text-overflow: ellipsis;", Css);
        // Failed and Passed badges take the same room, so what follows them lines up.
        Assert.Matches(@"\.attempt > summary \.status-badge \{ min-width: [0-9.]+rem; \}", Css);
        // The summary stays a list item: a flex or grid summary would lose its marker.
        MatchCollection summaries = Regex.Matches(Css, @"\.attempt(\[open\])? > summary \{[^{}]*\}");
        Assert.Equal(2, summaries.Count);
        foreach (Match summary in summaries) Assert.DoesNotContain("display:", summary.Value, StringComparison.Ordinal);

        // A missing duration or machine keeps its place; the server's own outcome follows the machine.
        TestFailureReportModel model = Build(culture, failures: [new AdoTestFailure
        {
            Ordinal = 1, Classification = AdoTestFailureClassification.Failed, ShortName = "A", TestName = "Synthetic.StylingTests.A", CollectionUri = TestFailureReportFixture.Collection,
            Attempts = [new AdoTestAttempt { Number = 1, RunId = 201, ResultId = 11, Outcome = "Timeout", OutcomeClass = AdoTestOutcomeClass.Failure, ErrorMessage = "Timed out" }],
        }]);
        string bare = Section(TestFailureReportFixture.Render(model), "<details class=\"attempt\" id=\"f-1-a1\">", "</summary>");
        Assert.Matches("</span> <span class=\"attempt-meta attempt-duration\"></span> <span class=\"attempt-meta attempt-machine\"></span> <span class=\"attempt-outcome\">Timeout</span> "
            + "<span class=\"attempt-error\">Timed out</span>$", bare);
    }

    [Theory]
    [InlineData("succeeded", "build-result status-passed")]
    [InlineData("SUCCEEDED", "build-result status-passed")]
    [InlineData("partiallySucceeded", "build-result status-flaky")]
    [InlineData("PartiallySucceeded", "build-result status-flaky")]
    [InlineData("failed", "build-result status-failed")]
    [InlineData("Canceled", "build-result status-other")]
    [InlineData("none", "build-result")]
    [InlineData("status-failed\" onclick=\"fixture()", "build-result")]
    public void BuildResultIsABadgeWhoseColourComesFromAFixedList(string result, string css)
    {
        TestFailureReportModel model = Build("en-US", result: result);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        string title = Section(html, "<div class=\"title-line\">", "</div>");
        // The text is what the server sent, whatever its case; only a listed value picks a class.
        Assert.Contains("<span class=\"" + css + "\">" + SinkEncoding.Attribute(result) + "</span>", title, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(title, "class=\"build-result"));
        Assert.Matches(@"\.build-result \{[^{}]*border: 1px solid currentColor;", Css);
    }

    [Fact]
    public void BuildWithoutAResultHasNoBadge() =>
        Assert.DoesNotContain("build-result", Section(TestFailureReportFixture.Render(Build("en-US", result: null)), "<div class=\"title-line\">", "</div>"), StringComparison.Ordinal);

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void NoLegendAboveATableOfTests(string culture)
    {
        // The glyphs and chips carry their words, and every status cell says its counts as its title.
        TestFailureReportModel model = TestFailureReportFixture.Model("partial", culture);
        string html = TestFailureReportFixture.Render(model);
        foreach (string view in new[] { "overview", "bugs" })
            Assert.Contains("</h2>\n<p data-no-matches hidden>", View(html, view), StringComparison.Ordinal);
        Assert.Contains("</h2>\n<p class=\"cluster-summary\">", View(html, "by-error"), StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"text-muted legend\"", html, StringComparison.Ordinal);
        Assert.False(model.Labels.ContainsKey("GroupLegend"));
        Assert.False(Enum.TryParse<AdoMessage>("TestReportGroupLegend", out _));
        Assert.Matches("<span class=\"group-status status-failed\" title=\"[^\"]+\">", View(html, "overview"));
    }

    [Fact]
    public void LatestErrorCellHasItsWholeLineAsTheTitle()
    {
        string line = "Expected \"a\" & <b> " + new string('x', 300);
        TestFailureReportModel model = Build("en-US", failures: [Failure("A", "\n  " + line + "\nsecond line"), Failure("B", null)]);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        // The first line, cut at 240 characters as before; the cell and its title hold the same text.
        string shown = SinkEncoding.Attribute(line[..240] + "…");
        string overview = View(html, "overview");
        Assert.Contains("<td class=\"col-error\" title=\"" + shown + "\">" + shown + "</td></tr>", Section(overview, "<tr data-index-for=\"f-1\">", "\n"), StringComparison.Ordinal);
        // No error line, no title.
        Assert.Contains("<td class=\"col-error\"></td></tr>", Section(overview, "<tr data-index-for=\"f-2\">", "\n"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-US", "Error", "Warning", "Information")]
    [InlineData("fr-CA", "Erreur", "Avertissement", "Information")]
    public void DiagnosticsAreCompactRowsBySeverityWithACountOfEachInTheHeading(string culture, string error, string warning, string information)
    {
        CultureInfo english = CultureInfo.GetCultureInfo("en-US");
        AdoDiagnostic[] arrival =
        [
            DiagnosticMessageRenderer.Create(DiagnosticCodes.UnresolvedTestCase, english, 902, ["902"]),
            DiagnosticMessageRenderer.Create(DiagnosticCodes.NoTestRuns, english, arguments: ["401"]),
            DiagnosticMessageRenderer.Create(DiagnosticCodes.FailureLimitExceeded, english, arguments: ["8", "1"]),
            DiagnosticMessageRenderer.Create(DiagnosticCodes.HistoryUnavailable, english, arguments: ["400"]),
            DiagnosticMessageRenderer.Create(DiagnosticCodes.ResolutionLimitExceeded, english),
            DiagnosticMessageRenderer.Create(DiagnosticCodes.UnresolvedTestCase, english, 903, ["903"]),
        ];
        TestFailureReportModel model = Build(culture, diagnostics: arrival);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        string view = View(html, "diagnostics");
        Assert.Contains("<h2 class=\"section-heading\">Diagnostics <span class=\"diagnostic-count\" data-severity=\"error\">" + error + " <strong>2</strong></span>"
            + " <span class=\"diagnostic-count\" data-severity=\"warning\">" + warning + " <strong>3</strong></span>"
            + " <span class=\"diagnostic-count\" data-severity=\"info\">" + information + " <strong>1</strong></span></h2>\n<ul class=\"diagnostic-list\">\n",
            view, StringComparison.Ordinal);
        // Errors, warnings, information; within each, the order of arrival.
        Assert.Equal(["error FailureLimitExceeded", "error ResolutionLimitExceeded", "warning UnresolvedTestCase", "warning HistoryUnavailable",
                "warning UnresolvedTestCase", "info NoTestRuns"],
            Regex.Matches(view, "<li data-severity=\"([a-z]+)\" data-diagnostic=\"([A-Za-z]+)\">").Select(static match => match.Groups[1].Value + " " + match.Groups[2].Value));
        string[] messages = [.. Regex.Matches(view, "<span class=\"diagnostic-message\">([^<]*)</span></li>").Select(static match => match.Groups[1].Value)];
        Assert.Equal(ArrivalIndexes.Select(index => SinkEncoding.Attribute(model.Diagnostics[index].Message)), messages);
        Assert.Contains("<li data-severity=\"error\" data-diagnostic=\"FailureLimitExceeded\"><span class=\"diagnostic-severity\">" + error
            + "</span> <code>FailureLimitExceeded</code> <span class=\"diagnostic-message\">", view, StringComparison.Ordinal);
        // No card per diagnostic any more.
        Assert.DoesNotContain("class=\"diagnostic\"", view, StringComparison.Ordinal);
        Assert.Contains("data-view-link=\"diagnostics\">Diagnostics 6</a>", html, StringComparison.Ordinal);
        Assert.Matches(@"\.diagnostic-list > li \{[^{}]*grid-template-columns: subgrid;[^{}]*padding: 3px var\(--space-2\);", Css);
    }

    [Theory]
    [InlineData("en-US", "Not read")]
    [InlineData("fr-CA", "Non lu")]
    public void CardMarksAnUnreadBugAndHasNoOpenBadge(string culture, string unread)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("partial", culture);
        string html = TestFailureReportFixture.Render(model);
        string card = Section(html, "<article class=\"card failure-card\" id=\"f-1\"", "</article>");
        // Every bug that was read is open, so only the one that was not read says something.
        Assert.EndsWith("<span class=\"bug-state\">Active</span>", Section(card, "<li data-bug=\"801\" data-open-bug>", "</li>"), StringComparison.Ordinal);
        Assert.EndsWith("</a> <span class=\"bug-unread\">" + unread + "</span>", Section(card, "<li data-bug=\"802\">", "</li>"), StringComparison.Ordinal);
        Assert.DoesNotContain("bug-open", html, StringComparison.Ordinal);
        Assert.False(model.Labels.ContainsKey("Open"));
        Assert.False(Enum.TryParse<AdoMessage>("TestReportOpen", out _));
    }

    [Theory]
    [InlineData("en-US", 0L, 0d, "bytes")]
    [InlineData("en-US", 1024L, 1024d, "bytes")]
    [InlineData("en-US", 1025L, 1.0, "KB")]
    [InlineData("en-US", 2048L, 2.0, "KB")]
    [InlineData("en-US", 1048524L, 1023.9, "KB")]
    // 1,023.95 KB would print as 1,024.0 KB.
    [InlineData("en-US", 1048525L, 1.0, "MB")]
    [InlineData("en-US", 1048576L, 1.0, "MB")]
    [InlineData("en-US", 5368709120L, 5120.0, "MB")]
    [InlineData("fr-CA", 512L, 512d, "octets")]
    [InlineData("fr-CA", 1536L, 1.5, "Ko")]
    [InlineData("fr-CA", 3145728L, 3.0, "Mo")]
    public void SizeShowsKilobytesOrMegabytesAbove1024BytesWithTheExactBytesAsTitle(string culture, long bytes, double shown, string unit)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model(culture: culture);
        CultureInfo format = model.Culture;
        AdoTestAttachment attachment = new()
        {
            Id = 51, RunId = 201, ResultId = 11, FileName = "capture.png", Size = bytes, Kind = AdoTestAttachmentKind.Png,
            DownloadStatus = AdoTestAttachmentStatus.Downloaded, LocalRelativePath = "files/capture.png",
        };
        AdoTestFailure failure = model.Failures[0];
        failure = failure.WithAttempts([failure.Attempts[0].WithAttachments([attachment])]);
        TestFailureLocalAttachments local = new()
        {
            FolderName = "files", SourceFolder = "unused", MaximumInlineJsonBytes = 1,
            Files = new Dictionary<string, long>(StringComparer.Ordinal) { ["capture.png"] = bytes },
        };
        // Rendered only: the validator would look for the local file, and no file of these sizes is written.
        model = TestFailureReportModelBuilder.WithAttachments(model, [failure], [], local);
        string html = TestFailureReportFixture.Render(model);
        string exact = SinkEncoding.Attribute(bytes.ToString("N0", format) + " " + (format.Name == "fr-CA" ? "octets" : "bytes"));
        bool large = bytes > 1024;
        string text = large ? SinkEncoding.Attribute(shown.ToString("N1", format) + " " + unit) : exact;
        string title = large ? " title=\"" + exact + "\"" : "";
        string item = Section(html, "<li id=\"f-1-a1-att51\"", "</li>");
        // The size the server declared, then the size of the local copy.
        Assert.Contains("</a> <span class=\"attachment-size\"" + title + ">" + text + "</span>", item, StringComparison.Ordinal);
        Assert.Contains("<code>capture.png</code> <span" + title + ">" + text + "</span>", item, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("failed", 5)]
    [InlineData("hostile", 6)]
    [InlineData("grouped", 5)]
    public void CardTitleIsOneLevelBelowTheViewHeading(string variant, int views)
    {
        string html = TestFailureMarkup.WithoutScripts(TestFailureReportFixture.Render(variant));
        MatchCollection cards = Regex.Matches(html, "<article class=\"card failure-card\".*?</article>", RegexOptions.Singleline);
        Assert.NotEmpty(cards);
        foreach (Match card in cards)
        {
            Assert.Matches("<header><span class=\"failure-number\" aria-hidden=\"true\">[0-9]+</span><span class=\"status-badge[^>]+>.*?</span><h3 data-short-name>", card.Value);
            Assert.Equal(1, Regex.Count(card.Value, "<h3[ >]"));
            Assert.DoesNotMatch("<h[12][ >]", card.Value);
        }
        string outside = Regex.Replace(html, "<article class=\"card failure-card\".*?</article>", "", RegexOptions.Singleline);
        Assert.Equal(1, Regex.Count(outside, "<h1>"));
        Assert.Equal(views, Regex.Count(outside, "<h2 class=\"section-heading\">"));
        Assert.DoesNotMatch("<h[4-6][ >]", outside);
    }

    [Fact]
    public void HeadingsInsideACardGoDownOneLevelAndStopAtSix()
    {
        TestFailureReportModel source = TestFailureReportFixture.Model();
        AdoTestSubResult Sub(int id, params AdoTestSubResult[] children) => new()
        { Id = id, DisplayName = "Row " + id.ToString(CultureInfo.InvariantCulture), Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure, SubResults = children };
        AdoTestAttempt original = source.Failures[0].Attempts[0];
        AdoTestAttempt attempt = new()
        {
            Number = 1, RunId = original.RunId, ResultId = original.ResultId, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure,
            SubResults = [Sub(301, Sub(302, Sub(303, Sub(304))))], Iterations = original.Iterations, Attachments = original.Attachments,
            CustomFields = original.CustomFields, AdditionalFields = original.AdditionalFields,
        };
        TestFailureReportModel model = TestFailureReportModelBuilder.WithAttachments(source, [source.Failures[0].WithAttempts([attempt])], [], null);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        string card = Section(html, "<article class=\"card failure-card\" id=\"f-1\"", "</article>");
        foreach (string label in new[] { "SubResults", "Iterations", "Attachments", "CustomFields", "AdditionalFields" })
            Assert.Contains("<h4>" + model.Labels[label] + "</h4>", card, StringComparison.Ordinal);
        Assert.Contains("data-sub-result=\"301\"><h5>Row 301</h5>", card, StringComparison.Ordinal);
        // There is no level below six.
        foreach (int id in new[] { 302, 303, 304 })
            Assert.Contains("data-sub-result=\"" + id.ToString(CultureInfo.InvariantCulture) + "\"><h6>Row " + id.ToString(CultureInfo.InvariantCulture) + "</h6>", card, StringComparison.Ordinal);
        Assert.Contains("data-iteration=\"7\"><h5>" + model.Labels["Iterations"] + " 7</h5>", card, StringComparison.Ordinal);
        Assert.Contains("<h6>" + Messages.Get(AdoMessage.ReportParameters, model.Culture) + "</h6>", card, StringComparison.Ordinal);
        // The look is the one the levels above had.
        Assert.Contains(".failure-card > header h3 { overflow-wrap: anywhere; margin: 0; font-size: var(--text-16); letter-spacing: -.01em; color: var(--text); }", Css, StringComparison.Ordinal);
        Assert.Contains("h3, .failure-card h4 { font-size: var(--text-14); margin: var(--space-3) 0 var(--space-1); color: var(--text-muted); }", Css, StringComparison.Ordinal);
        Assert.Contains(".failure-card h4 { line-height: 1.3; font-weight: 600; }", Css, StringComparison.Ordinal);
        Assert.Contains("  .failure-card > header h3 { flex-basis: 100%; }", Css, StringComparison.Ordinal);
        Assert.DoesNotContain("header h2", Css, StringComparison.Ordinal);
    }

    // WCAG 2.2 SC 2.4.11: an element that gets focus, by k or by Shift+Tab, stays below the sticky
    // header. The page keeps the header's height as scroll padding; a margin on some targets added to
    // it and missed every other one.
    [Fact]
    public void KeyboardFocusStaysClearOfTheStickyHeader()
    {
        Assert.Contains("html { scroll-padding-top: calc(var(--report-header-height, 11rem) + var(--space-4)); }", Css, StringComparison.Ordinal);
        Assert.DoesNotContain("scroll-margin-top", Css, StringComparison.Ordinal);
        string script = TestFailureAssets.Read("test-failures.js");
        Assert.Contains("root.style.setProperty('--report-header-height', `${height}px`);", script, StringComparison.Ordinal);
        Assert.DoesNotContain("--report-header-offset", script, StringComparison.Ordinal);
    }

    // A card or attempt reached by a link, j or k landed up to a line under the band, in two ways.
    // Details shows Expand all and Collapse all, which can make the band a line taller than in the
    // other views, and the scroll came before the ResizeObserver reported it: changing the view now
    // measures the band at once. And the scroll itself lays out the cards that content-visibility
    // skipped; on a short report the page then grows a scrollbar, the band rewraps and the position
    // no longer clears it: a scroll to the top is now repeated when the band's height changed.
    [Fact]
    public void ATargetScrolledToTheTopClearsTheBandEvenWhenItsHeightChanges()
    {
        string script = TestFailureAssets.Read("test-failures.js");
        string show = Section(script, "const show = id => {", "\n  };");
        Assert.EndsWith("all('[data-details-only]').forEach(control => { control.hidden = target.id !== 'details'; });\n    measure();", show, StringComparison.Ordinal);
        // Defined before show, so no call can meet it uninitialized.
        Assert.True(script.IndexOf("const measure = () => {", StringComparison.Ordinal) is >= 0 and var position
            && position < script.IndexOf("const show = id => {", StringComparison.Ordinal));
        Assert.Contains("    if (height === bandHeight) return false;", script, StringComparison.Ordinal);
        Assert.Contains("  const reveal = node => {\n    node.scrollIntoView({ block: 'start' });\n    if (measure()) node.scrollIntoView({ block: 'start' });\n  };",
            script, StringComparison.Ordinal);
        // Every scroll to the top goes through it.
        Assert.Equal(2, Regex.Count(script, @"scrollIntoView\(\{ block: 'start' \}\)"));
        Assert.Contains("    card.focus({ preventScroll: true });\n    reveal(card);", script, StringComparison.Ordinal);
        Assert.Contains("    reveal(target);", script, StringComparison.Ordinal);
    }

    // The band is two lines: the counts first, then the build; the views and the filters share the
    // second line. A zero count is muted, the generic title is quiet, and the keys are in the footer.
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void TheBandLeadsWithTheCountsAndHoldsTheViewsAndFiltersOnOneLine(string culture)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("failed", culture);
        string html = TestFailureReportFixture.Render(model);
        string band = Section(html, "<header class=\"top-bar\">", "</header>");
        Assert.StartsWith("<header class=\"top-bar\"><div class=\"top-bar-inner\">\n<div class=\"title-line\"><span class=\"count-chip status-failed\"><span class=\"count-label\">✕ ",
            band, StringComparison.Ordinal);
        Assert.Contains("<span class=\"count-chip status-flaky\" data-zero><span class=\"count-label\">≈ " + model.Labels["Flaky"] + "</span><strong class=\"count-value\">0</strong></span>"
            + "<span class=\"count-chip status-attachments\"><span class=\"count-label\">" + model.Labels["Attachments"] + "</span><strong class=\"count-value\">2</strong></span>"
            + "<span class=\"report-brand\">", band, StringComparison.Ordinal);
        Assert.Contains("</div>\n<div class=\"nav-line\"><nav class=\"view-links\" aria-label=\"", band, StringComparison.Ordinal);
        Assert.Contains("</nav>\n<div class=\"interactive filter-controls\" data-enhance hidden>", band, StringComparison.Ordinal);
        Assert.DoesNotContain("keyboard-hint", band, StringComparison.Ordinal);
        string footer = Section(html, "<footer class=\"report-footer\">", "</footer>");
        Assert.EndsWith("</dl><p class=\"interactive keyboard-hint\" data-enhance hidden>" + SinkEncoding.Attribute(model.Labels["NavigationHint"]) + "</p>",
            footer, StringComparison.Ordinal);
        Assert.Contains(".top-bar h1 { margin: 0; font-size: var(--text-13); font-weight: 400; color: var(--text-muted); }", Css, StringComparison.Ordinal);
        Assert.Contains(".count-chip[data-zero] { color: var(--text-muted); }", Css, StringComparison.Ordinal);
        // With scripts on, a count that can filter becomes a button for its filter; a zero count does not.
        string script = TestFailureAssets.Read("test-failures.js");
        Assert.Contains("const chips = all('.count-chip:not([data-zero])')", script, StringComparison.Ordinal);
        Assert.Contains("chip.setAttribute('role', 'button');", script, StringComparison.Ordinal);
        Assert.Contains("chips.forEach(([chip, kind]) => chip.setAttribute('aria-pressed', String(pressed(kind))));", script, StringComparison.Ordinal);
    }

    // One type scale: five sizes, the body at 14px/1.45, tables at 13 and code at 12. Only the
    // brand goes below 12, the minimum for controls and running text.
    [Fact]
    public void OneTypeScaleOfFiveSizes()
    {
        Assert.StartsWith(":root { --text-11: 11px; --text-12: 12px; --text-13: 13px; --text-14: 14px; --text-16: 16px;", Css, StringComparison.Ordinal);
        Assert.Contains("body { font-size: var(--text-14); line-height: 1.45; font-variant-numeric: tabular-nums; }", Css, StringComparison.Ordinal);
        Assert.Contains(".failure-table, .runs-table { font-size: var(--text-13); }", Css, StringComparison.Ordinal);
        Assert.Contains("code { font-size: var(--text-12); }", Css, StringComparison.Ordinal);
        Assert.Contains(".code-section code { font-size: var(--text-12); line-height: 1.55; }", Css, StringComparison.Ordinal);
        string[] sizes = [.. Regex.Matches(Css, @"font-size: ([^;]+);").Select(static match => match.Groups[1].Value)];
        Assert.All(sizes, static size => Assert.Matches(@"^var\(--text-(11|12|13|14|16)\)$", size));
        string[] shorthands = [.. Regex.Matches(Css, @"\bfont: ([^;]+);").Select(static match => match.Groups[1].Value)];
        Assert.All(shorthands, static font => Assert.Matches(@"^(400 )?var\(--text-1[2-6]\) var\(--font-mono\)$", font));
        Assert.Equal([".report-brand"], Regex.Matches(Css, @"(?m)^([^{}\n]+) \{[^{}]*var\(--text-11\)").Select(static match => match.Groups[1].Value));
    }

    // Amber means flaky alone. Focus and what is current (the open view, this build, a search match,
    // the first frame of the test's own code) take the link colour, whose contrast ThemeContrastTests checks.
    [Fact]
    public void FocusAndCurrentAreNoLongerAmber()
    {
        Assert.Contains("--focus: var(--link); --current: var(--link); }", Css.Split('\n')[0], StringComparison.Ordinal);
        Assert.Contains(".view-links a[aria-current=\"page\"] { color: var(--text); background: var(--surface-2); box-shadow: inset 0 -2px 0 var(--current); }", Css, StringComparison.Ordinal);
        Assert.Contains(".attempt.is-match > summary, .attempt-group.is-match > summary { box-shadow: inset 3px 0 0 var(--current); }", Css, StringComparison.Ordinal);
        string shared = TestFailureAssets.Read("report-base.css");
        Assert.Contains("--flaky: #e3c17f;", shared, StringComparison.Ordinal);
        Assert.Contains(".first-user-frame { border-inline-start: 3px solid var(--focus); }", shared, StringComparison.Ordinal);
        Assert.DoesNotContain("--flaky", string.Concat(Css.Split('\n').Where(static line => line.Contains("is-current", StringComparison.Ordinal)
            || line.Contains("aria-current", StringComparison.Ordinal) || line.Contains("is-match", StringComparison.Ordinal))), StringComparison.Ordinal);
    }

    // The History data table has the cells of the other tables, and its row headers no background.
    // Tables outside Details get more room than 0.8.0 gave them: 4 by 12 pixels and a line height of 1.4.
    [Fact]
    public void HistoryDataTableIsAsDenseAsTheOthers()
    {
        Assert.Contains(".failure-table th, .failure-table td, .runs-table th, .runs-table td { padding: 4px var(--space-3); vertical-align: middle; line-height: 1.4; }",
            Css, StringComparison.Ordinal);
        Assert.Contains(".history-data th, .history-data td { padding: 4px var(--space-3); }", Css, StringComparison.Ordinal);
        Assert.Contains(".history-data tbody th { background: none; }", Css, StringComparison.Ordinal);
        Assert.Contains(":where(#overview, #runs, #by-error, #bugs) h3 { margin: var(--space-5) 0 var(--space-2); }", Css, StringComparison.Ordinal);
    }

    // A long report lays out only the cards near the screen. Print lays out every card.
    [Fact]
    public void CardsOutOfSightAreNotLaidOutOnScreen()
    {
        Assert.Matches(@"\.failure-card \{ content-visibility: auto; contain-intrinsic-size: auto [0-9]+px;", Css);
        string print = Css[Css.IndexOf("@media print", StringComparison.Ordinal)..];
        Assert.Contains("  .failure-card { content-visibility: visible; }", print, StringComparison.Ordinal);
    }

    // Browsers leave out background colours when printing unless told otherwise, and a failed
    // attempt's square is told apart by its fill.
    [Fact]
    public void PrintedStatusSquaresKeepTheirFill()
    {
        string print = Css[Css.IndexOf("@media print", StringComparison.Ordinal)..];
        Assert.Contains("  .sq, .history-cell { print-color-adjust: exact; }", print, StringComparison.Ordinal);
        Assert.Contains(".sq.status-failed { background: var(--fail); }", Css, StringComparison.Ordinal);
    }

    private static AdoTestFailure Failure(string name, string? error) => new()
    {
        Ordinal = 1, Classification = AdoTestFailureClassification.Failed, ShortName = name, TestName = "Synthetic.StylingTests." + name, Storage = "Synthetic.Tests.dll",
        CollectionUri = TestFailureReportFixture.Collection,
        Attempts = [new AdoTestAttempt { Number = 1, RunId = 201, ResultId = 11, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure, ErrorMessage = error }],
    };

    // The failed fixture with its build result, failures or diagnostics replaced.
    private static TestFailureReportModel Build(string culture, string? result = "failed", AdoTestFailure[]? failures = null, AdoDiagnostic[]? diagnostics = null)
    {
        AdoBuildTestFailureSet source = TestFailureReportFixture.Set("failed");
        AdoBuild build = new()
        {
            Id = source.Build.Id, BuildNumber = source.Build.BuildNumber, Definition = source.Build.Definition, SourceBranch = source.Build.SourceBranch,
            SourceVersion = source.Build.SourceVersion, RepositoryType = source.Build.RepositoryType, RepositoryId = source.Build.RepositoryId, Result = result,
            FinishTime = source.Build.FinishTime, TeamProject = source.Build.TeamProject, CollectionUri = source.Build.CollectionUri, WebUrl = source.Build.WebUrl,
        };
        return TestFailureReportModelBuilder.Build(new AdoBuildTestFailureSet
        {
            Build = build, Runs = source.Runs, Summary = source.Summary, History = source.History, Failures = failures ?? source.Failures,
            FailedCount = failures?.Length ?? source.FailedCount, Status = source.Status, Diagnostics = diagnostics ?? source.Diagnostics,
            RetrievedAt = source.RetrievedAt, CollectionUri = source.CollectionUri,
        }, TestFailureReportFixture.Options(culture));
    }

    private static string View(string html, string id) => Section(html, "<section class=\"view\" id=\"" + id + "\" data-view>", "</section>\n<section class=\"view\"", "</section>\n</main>");

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
