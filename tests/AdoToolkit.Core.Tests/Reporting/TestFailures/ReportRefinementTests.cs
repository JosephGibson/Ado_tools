using System.Net;
using System.Text.RegularExpressions;
using AdoToolkit.Core.Builds;
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
        Assert.Equal(4, Regex.Count(card, "class=\"history-cell status-[a-z]+\"[^>]* title=\"[^\"]+\""));
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

    // To keep the band short, it names the branch without refs/heads/ and gives the finish time
    // without its label; the full ref and the label become their titles. Any other ref is shown whole.
    [Theory]
    [InlineData("en-US", "Finished", "9/16/2026 1:18 PM")]
    [InlineData("fr-CA", "Fin", "2026-09-16 13 h 18")]
    public void TheBandNamesTheBranchAndTheFinishTimeAndKeepsTheirFullTextAsTitles(string culture, string label, string finished)
    {
        string title = Section(TestFailureReportFixture.Render("failed", culture), "<div class=\"title-line\">", "</div>");
        Assert.Contains("<code title=\"refs/heads/main\">main</code>", title, StringComparison.Ordinal);
        Assert.Contains("<span title=\"" + label + "\">" + finished + "</span>", title, StringComparison.Ordinal);
        Assert.DoesNotContain(">refs/heads/", title, StringComparison.Ordinal);
        Assert.DoesNotContain(label + " " + finished, title, StringComparison.Ordinal);
        foreach (string branch in new[] { "refs/pull/7/merge", "refs/heads/", "main" })
        {
            string other = Section(TestFailureReportFixture.Render(WithBranch(branch, culture)), "<div class=\"title-line\">", "</div>");
            Assert.Contains("<code>" + branch + "</code>", other, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void OverviewLinksTheLowestOpenBugInItsOwningProjectAndTheCardMarksOnlyTheUnreadBug(string culture)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model(culture: culture);
        AdoTestFailure original = model.Failures[0];
        AdoTestFailure failure = new()
        {
            Ordinal = original.Ordinal, ShortName = original.ShortName, CollectionUri = original.CollectionUri,
            Classification = original.Classification, Attempts = original.Attempts,
            // Bug 900 could not be read, so it is not known to be open.
            Bugs = [Bug(950, true), Bug(920, true, "Bugs / été"), Bug(900, null)],
        };
        model = TestFailureReportModelBuilder.WithAttachments(model, [failure], [], null);
        string html = TestFailureReportFixture.Render(model);
        string row = Section(html, "<tr data-index-for=\"f-1\">", "</tr>");
        Assert.Contains("<a class=\"open-bug-marker\" rel=\"noreferrer\" href=\"https://ado.example.test/tfs/Collection%20A/Bugs%20%2F%20%C3%A9t%C3%A9/_workitems/edit/920\">",
            row, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(row, "class=\"open-bug-marker\""));
        Assert.Matches(@"\.open-bug-marker, \.open-bug-marker:visited \{[^{}]*color: var\(--fail\);[^{}]*border-color: var\(--fail\);", html);
        // The card has no Open badge; it marks the one bug that was not read.
        string card = Section(html, "<article class=\"card failure-card\"", "</article>");
        Assert.DoesNotContain("bug-open", html, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(card, "class=\"bug-unread\""));
        Assert.Contains("class=\"bug-unread\"", Section(card, "<li data-bug=\"900\">", "</li>"), StringComparison.Ordinal);
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

    // A table cell ignores max-width, so a long name widened the cell and pushed the Open bug marker
    // out of sight. The name itself is cut, with the whole name as its title, and the marker follows it.
    [Fact]
    public void ALongNameIsCutBeforeTheOpenBugMarkerAndKeepsItsWholeTextAsTitle()
    {
        TestFailureReportModel model = TestFailureReportFixture.Model();
        string name = "Submit" + string.Concat(Enumerable.Repeat("AndConfirmTheOrderTwice", 8));
        AdoTestFailure original = model.Failures[0];
        AdoTestFailure failure = new()
        {
            Ordinal = original.Ordinal, ShortName = name, TestName = "Synthetic.CheckoutTests." + name, CollectionUri = original.CollectionUri,
            Classification = original.Classification, Attempts = original.Attempts, Bugs = [Bug(920, true)],
        };
        model = TestFailureReportModelBuilder.WithAttachments(model, [failure], [], null);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        string row = Section(html, "<tr data-index-for=\"f-1\">", "</tr>");
        Assert.Contains("<td class=\"col-test\"><a href=\"#f-1\"><span class=\"test-name\" title=\"" + name + "\">" + name + "</span></a> <a class=\"open-bug-marker\"",
            row, StringComparison.Ordinal);
        string css = TestFailureAssets.Read("test-failures.css");
        Assert.Matches(@"\.col-test \.test-name \{[^{}]*display: inline-block;[^{}]*max-width: [^;]+;[^{}]*overflow: hidden;[^{}]*text-overflow: ellipsis;", css);
        // The cell keeps its line but no longer claims a width it cannot have.
        Assert.DoesNotMatch(@"\.col-test \{[^{}]*max-width", css);
    }

    // French puts a no-break space before a colon. These texts joined a label and a value in code,
    // so they had none: an attempt square's name, a history cell's title and a chart segment's title.
    [Theory]
    [InlineData("en-US", ": ")]
    [InlineData("fr-CA", " : ")]
    public void LabelsAndValuesAreJoinedByTheCatalogSeparator(string culture, string separator)
    {
        string html = TestFailureReportFixture.Render("flaky", culture);
        string[] squares = [.. Regex.Matches(html, "class=\"sq status-[a-z]+\" href=\"#f-[0-9]+-a[0-9]+\" aria-label=\"([^\"]+)\"").Select(static match => match.Groups[1].Value)];
        string[] cells = [.. Regex.Matches(html, "<a class=\"history-cell status-[a-z]+\"[^>]* title=\"([^\"]+)\"").Select(static match => match.Groups[1].Value)];
        string[] segments = [.. Regex.Matches(html, "<g data-outcome=\"[a-z]+\" data-count=\"[0-9]+\"><title>([^<]+)</title>").Select(static match => match.Groups[1].Value)];
        foreach (string[] texts in new[] { squares, cells, segments })
        {
            Assert.NotEmpty(texts);
            Assert.All(texts, text => Assert.Equal(1, text.Split(separator).Length - 1));
            Assert.All(texts, text => Assert.Equal(1, text.Split(':').Length - 1));
        }
    }

    // A heading row names the rows of its tbody; there is no colgroup for it to name.
    [Theory]
    [InlineData("failed")]
    [InlineData("grouped")]
    [InlineData("partial")]
    public void ErrorAndBugHeadingsNameTheirRowGroup(string variant)
    {
        string html = TestFailureReportFixture.Render(variant);
        foreach (string view in new[] { "by-error", "bugs" })
        {
            string section = Section(html, "<section class=\"view\" id=\"" + view + "\" data-view>", "</section>");
            Assert.Equal(Regex.Count(section, "<tr class=\"cluster-heading\">"), Regex.Count(section, "<tr class=\"cluster-heading\"><th scope=\"rowgroup\" colspan=\"[0-9]+\">"));
            Assert.True(Regex.Count(section, "<tr class=\"cluster-heading\">") > 0, view);
        }
        Assert.DoesNotContain("scope=\"colgroup\"", html, StringComparison.Ordinal);
    }

    // Back from a card, or a view link, shows a table of tests again. It returns to the row of the
    // test that was open, in the middle of the screen and focused, instead of to the top of the table.
    [Fact]
    public void ReturningToATableOfTestsBringsTheCurrentTestsRowBack()
    {
        string script = TestFailureAssets.Read("test-failures.js");
        string fragment = Section(script, "const fragment = () => {", "\n  };");
        Assert.Contains("['overview', 'by-error', 'bugs'].includes(shown.id)", fragment, StringComparison.Ordinal);
        Assert.Contains("row.scrollIntoView({ block: 'center' });\n      row.querySelector('.col-test a').focus({ preventScroll: true });", fragment, StringComparison.Ordinal);
        // The top of the page stays the answer when no test is current.
        Assert.Contains("if (!row) { window.scrollTo(0, 0); return; }", fragment, StringComparison.Ordinal);
    }

    // Search folds case and accents on both sides, so "echec" finds « Échec ».
    [Fact]
    public void SearchIgnoresCaseAndAccents()
    {
        string script = TestFailureAssets.Read("test-failures.js");
        Assert.Contains("const fold = value => value.normalize('NFD').replace(/\\p{M}/gu, '').toLocaleLowerCase(lang);", script, StringComparison.Ordinal);
        Assert.Contains("if (value === undefined) { value = fold(node.textContent); texts.set(node, value); }", script, StringComparison.Ordinal);
        Assert.Contains("const terms = fold(filter.value).split(/\\s+/).filter(Boolean);", script, StringComparison.Ordinal);
        Assert.DoesNotContain(".toLocaleLowerCase(lang).split", script, StringComparison.Ordinal);
    }

    // Reaching a test, by a link to its card, by j or k in Details, or by Enter on its row, opens the
    // attempt behind its latest error and the group that holds it. The markup keeps every attempt
    // closed, as the validator requires, and names that attempt on the card.
    [Theory]
    [InlineData("grouped", "f-1", "f-1-a4")]
    [InlineData("flaky", "f-2", "f-2-a1")]
    public void ReachingATestOpensTheAttemptOfItsLatestError(string variant, string card, string attempt)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model(variant);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.Matches("<article class=\"card failure-card\" id=\"" + card + "\"[^>]* data-latest-error=\"" + attempt + "\">", html);
        Assert.Contains("<details class=\"attempt\" id=\"" + attempt + "\">", html, StringComparison.Ordinal);
        string script = TestFailureAssets.Read("test-failures.js");
        Assert.Contains("for (let node = document.getElementById(card.dataset.latestError); node && node !== card; node = node.parentElement) if (node.matches('details')) node.open = true;",
            script, StringComparison.Ordinal);
        // j and k in Details go through focus; a link to the card itself goes through fragment.
        Assert.Contains("    select(card);\n    arrive(card);\n    card.focus({ preventScroll: true });", script, StringComparison.Ordinal);
        Assert.Contains("      if (target === card) arrive(card);", script, StringComparison.Ordinal);
    }

    // A stack trace with a frame of the test's own code opens with the framework frames hidden, and an
    // error message opens wrapped, each with its button pressed. The script does it at load, so with
    // scripts blocked every frame shows unwrapped; copy reads the whole text and print shows every frame.
    [Fact]
    public void TracesOpenOnTheTestsOwnCodeAndMessagesOpenWrapped()
    {
        string html = TestFailureReportFixture.Render("failed");
        Assert.Contains("<span class=\"first-user-frame\">", html, StringComparison.Ordinal);
        Assert.DoesNotMatch("<button[^>]* aria-pressed=\"true\"", html);
        Assert.DoesNotContain("class=\"code-section hide-framework", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"code-section wrap-code", html, StringComparison.Ordinal);
        string script = TestFailureAssets.Read("test-failures.js");
        Assert.Contains("  all('.code-section').forEach(section => {\n    const button = section.querySelector(section.querySelector('code.lang-error') ? '[data-action=\"wrap\"]'\n"
            + "      : section.querySelector('.first-user-frame') ? '[data-action=\"framework\"]' : null);\n    if (button) set(button, true);\n  });", script, StringComparison.Ordinal);
        Assert.Contains("await navigator.clipboard.writeText(target.textContent);", script, StringComparison.Ordinal);
        string css = TestFailureAssets.Read("test-failures.css");
        Assert.Contains("  .code-section.hide-framework .framework-frame { display: inline; }", css[css.IndexOf("@media print", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    // The failed fixture, built from a branch other than refs/heads/main.
    private static TestFailureReportModel WithBranch(string branch, string culture)
    {
        AdoBuildTestFailureSet source = TestFailureReportFixture.Set("failed");
        AdoBuild build = new()
        {
            Id = source.Build.Id, BuildNumber = source.Build.BuildNumber, Definition = source.Build.Definition, SourceBranch = branch,
            SourceVersion = source.Build.SourceVersion, RepositoryType = source.Build.RepositoryType, RepositoryId = source.Build.RepositoryId,
            Result = source.Build.Result, FinishTime = source.Build.FinishTime, TeamProject = source.Build.TeamProject,
            CollectionUri = source.Build.CollectionUri, WebUrl = source.Build.WebUrl,
        };
        return TestFailureReportModelBuilder.Build(new AdoBuildTestFailureSet
        {
            Build = build, Runs = source.Runs, Summary = source.Summary, History = source.History, Failures = source.Failures,
            FailedCount = source.FailedCount, FlakyCount = source.FlakyCount, Status = source.Status, Diagnostics = source.Diagnostics,
            RetrievedAt = source.RetrievedAt, CollectionUri = source.CollectionUri,
        }, TestFailureReportFixture.Options(culture));
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
