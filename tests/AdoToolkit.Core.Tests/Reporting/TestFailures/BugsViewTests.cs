using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// The Bugs view: one entry per bug with the tests linked to it, built from the bugs of each failure.
public sealed class BugsViewTests
{
    private const string OtherProject = "Autre projet";

    // Four tests: A has bugs 801, 804 and 805; B has 799, 801 and 803; C has 799, 801, 804 and 805; D has none.
    // Bugs 799 and 803 could not be read. Bug 805 lives in another project.
    private static TestFailureReportModel Model(string culture = "en-US") => Build(culture,
        Failure("A", Read(801, result: true, testCase: true), Read(804, result: true), Read(805, testCase: true, project: OtherProject)),
        Failure("B", Unread(799), Read(801, result: true), Unread(803)),
        Failure("C", Unread(799), Read(801, testCase: true), Read(804, testCase: true), Read(805, result: true, project: OtherProject)),
        Failure("D"));

    [Fact]
    public void EachBugIsListedOnceWithEveryTestLinkedToIt()
    {
        TestFailureReportModel model = Model();
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        string view = View(html);
        // Read bugs first, the most tests first, then by ID; unread bugs last, by ID; then the tests
        // that no open bug tracks.
        Assert.Equal(["801", "804", "805", "799", "803"], Regex.Matches(view, "<tbody class=\"error-cluster\" data-bug=\"([0-9]+)\">").Select(m => m.Groups[1].Value));
        Assert.Equal(["3", "2", "2", "2", "1", "1"], Regex.Matches(view, "<span class=\"cluster-count\">([0-9]+)</span>").Select(m => m.Groups[1].Value));
        // Rows are in report order, and a test with several bugs has a row under each.
        Assert.Equal(["f-1", "f-2", "f-3"], Rows(Entry(view, 801)));
        Assert.Equal(["f-1", "f-3"], Rows(Entry(view, 804)));
        Assert.Equal(["f-1", "f-3"], Rows(Entry(view, 805)));
        Assert.Equal(["f-2", "f-3"], Rows(Entry(view, 799)));
        Assert.Equal(["f-2"], Rows(Entry(view, 803)));
        Assert.Equal(["f-4"], Rows(view[view.IndexOf("<tbody class=\"error-cluster\" id=\"no-bug\">", StringComparison.Ordinal)..]));
        // The Open bugs column belongs to the other tables: here the heading names the bug with its chip.
        Assert.DoesNotContain("<th scope=\"col\">Open bugs</th>", view, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"col-bug\"", view, StringComparison.Ordinal);
        Assert.Equal(3, Regex.Count(view, "<div class=\"bug-head\"><span class=\"cluster-count\">[0-9]+</span> <a class=\"open-bug-marker\""));
        Assert.Equal(2, Regex.Count(view, "<div class=\"bug-head\"><span class=\"cluster-count\">[0-9]+</span> <a class=\"bug-marker bug-unread\""));
        Assert.Contains("<th scope=\"col\">Linked through</th></tr></thead>", view, StringComparison.Ordinal);
        Assert.Equal(["Test result, Test Case", "Test result", "Test Case"],
            Regex.Matches(Entry(view, 801), "<td class=\"col-source\">([^<]*)</td>").Select(m => m.Groups[1].Value));
        // The heading spans every column: number, Test Case, test, class, trend, attempts and the source.
        Assert.Equal(6, Regex.Count(view, "<tr class=\"cluster-heading\"><th scope=\"rowgroup\" colspan=\"7\">"));
    }

    [Fact]
    public void EntryShowsTheBugInItsOwnProjectAndMarksAnUnreadBug()
    {
        string view = View(TestFailureReportFixture.Render(Model()));
        string prefix = TestFailureReportFixture.Collection.AbsoluteUri;
        string other = Entry(view, 805);
        Assert.Contains("href=\"" + prefix + Uri.EscapeDataString(OtherProject) + "/_workitems/edit/805\" title=\"Bug 805\"><span class=\"sr-only\">Open bug </span>#805</a>",
            other, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bug-title\">Bug 805</span> <span class=\"bug-state\">Active</span> <span class=\"bug-state\">Project: Autre projet</span>",
            other, StringComparison.Ordinal);
        Assert.DoesNotContain("bug-unread", other, StringComparison.Ordinal);
        string unread = Entry(view, 799);
        // An unread bug may be closed: its chip is grey, never red.
        Assert.Contains("<a class=\"bug-marker bug-unread\" rel=\"noreferrer\" href=\"" + prefix + Uri.EscapeDataString(TestFailureReportFixture.Project)
            + "/_workitems/edit/799\" title=\"Not read\">#799</a>", unread, StringComparison.Ordinal);
        Assert.DoesNotContain("open-bug-marker", unread, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bug-unread\">Not read</span>", unread, StringComparison.Ordinal);
        Assert.DoesNotContain("bug-title", unread, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-US", "Open bugs", "Not read", "Linked through")]
    [InlineData("fr-CA", "Bogues ouverts", "Non lu", "Lié par")]
    public void ViewSitsAfterByErrorWithItsEntryCountInTheTab(string culture, string heading, string unread, string source)
    {
        string html = TestFailureReportFixture.Render(Model(culture));
        // Tabs and sections share one order: Overview, Runs and history, Details, By error, Open bugs.
        Assert.Equal(["overview", "runs", "details", "by-error", "bugs"], Regex.Matches(html, "<a href=\"#([a-z-]+)\" data-view-link=").Select(m => m.Groups[1].Value));
        Assert.Equal(["overview", "runs", "details", "by-error", "bugs"], Regex.Matches(html, "<section class=\"view\" id=\"([a-z-]+)\"").Select(m => m.Groups[1].Value));
        Assert.Contains("<a href=\"#bugs\" data-view-link=\"bugs\">" + heading + " 5</a></nav>", html, StringComparison.Ordinal);
        string view = View(html);
        Assert.Contains("<h2 class=\"section-heading\">" + heading + "</h2>", view, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bug-unread\">" + unread + "</span>", view, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">" + source + "</th>", view, StringComparison.Ordinal);
        Assert.Contains("<p data-no-matches hidden>", view, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-US", "Open bugs", "No open bug is linked to a test in this report.", "No failed or flaky tests to report.")]
    [InlineData("fr-CA", "Bogues ouverts", "Aucun bogue ouvert n’est lié à un test de ce rapport.", "Aucun test en échec ou instable à signaler.")]
    public void ViewIsPresentWithoutBugsAndWithoutFailures(string culture, string heading, string noBugs, string noFailures)
    {
        TestFailureReportModel model = Build(culture, Failure("A"), Failure("B"));
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.Contains("<a href=\"#bugs\" data-view-link=\"bugs\">" + heading + " 0</a>", html, StringComparison.Ordinal);
        string view = View(html);
        Assert.Contains(noBugs, TestFailureMarkup.Text(view), StringComparison.Ordinal);
        Assert.DoesNotContain(noFailures, TestFailureMarkup.Text(view), StringComparison.Ordinal);
        Assert.DoesNotContain("<table", view, StringComparison.Ordinal);

        TestFailureReportModel empty = Build(culture);
        html = TestFailureReportFixture.Render(empty);
        TestFailureReportValidator.Validate(new StringReader(html), empty);
        view = View(html);
        Assert.Contains(noFailures, TestFailureMarkup.Text(view), StringComparison.Ordinal);
        Assert.DoesNotContain(noBugs, TestFailureMarkup.Text(view), StringComparison.Ordinal);
        Assert.DoesNotContain("<table", view, StringComparison.Ordinal);
    }

    // Grouped builds show one status column per group, as By error does, and the source after them.
    [Fact]
    public void GroupedBuildKeepsOneStatusColumnPerGroup()
    {
        AdoBuildTestFailureSet grouped = TestFailureReportFixture.Set("grouped");
        TestFailureReportModel model = TestFailureReportModelBuilder.Build(grouped, TestFailureReportFixture.Options("en-US"));
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        string view = View(html);
        Assert.Contains("<th scope=\"col\">Tests_EN</th><th scope=\"col\">Tests_FR</th><th scope=\"col\">Linked through</th></tr></thead>", view, StringComparison.Ordinal);
        Assert.Contains("<tbody class=\"error-cluster\" data-bug=\"804\"><tr class=\"cluster-heading\"><th scope=\"rowgroup\" colspan=\"8\">", view, StringComparison.Ordinal);
        // The heading says in which group the bug's tests fail.
        Assert.Contains("<span class=\"fact-group\"><span class=\"fact\">Tests_EN <strong>1</strong></span><span class=\"fact\">Tests_FR <strong>1</strong></span></span>",
            Entry(view, 804), StringComparison.Ordinal);
        Assert.Equal(["f-1"], Rows(Entry(view, 804)));
        Assert.Contains("<td class=\"col-source\">Test Case</td>", view, StringComparison.Ordinal);
    }

    // Each bug's heading is one line: count, chip, title, state, then what the bug covers and where its
    // tests fail. In the large fixture bug 5101 reaches SubmitOrder through one of its four failed
    // results only, AddItem shares SubmitOrder's Test Case without the bug, and two more tests fail
    // with the same timeout.
    [Theory]
    [InlineData("en-US", "Test result: 1 of 4 failed results", "Same Test Case, not linked", "Same error, not linked")]
    [InlineData("fr-CA", "Résultat du test : 1 des 4 résultats en échec", "Même cas de test, non lié", "Même erreur, non lié")]
    public void EachBugSaysWhatItCoversAndWhatItLeavesOut(string culture, string covered, string sameCase, string sameError)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("large", culture);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        string view = View(html);
        string entry = Entry(view, 5101);
        string Anchor(string name) => "f-" + model.Failures.Single(failure => failure.ShortName == name).Ordinal.ToString(CultureInfo.InvariantCulture);
        string facts = entry[entry.IndexOf("<span class=\"facts\">", StringComparison.Ordinal)..entry.IndexOf("</th></tr>", StringComparison.Ordinal)];
        Assert.Contains("<span class=\"fact fact-untracked\">" + sameCase + " <strong>1</strong></span>", facts, StringComparison.Ordinal);
        Assert.Contains("<a class=\"fact fact-untracked\" href=\"#e-1\">" + sameError + " <strong>2</strong></a>", facts, StringComparison.Ordinal);
        Assert.Contains("<span class=\"fact\">Tests_EN <strong>1</strong></span><span class=\"fact\">Tests_FR <strong>1</strong></span>", facts, StringComparison.Ordinal);
        // The linked test, then the test of the same Test Case, muted; tests with the same error are counted, not listed.
        Assert.Equal([Anchor("SubmitOrder")], Rows(entry));
        Assert.Contains("<tr data-index-for=\"" + Anchor("AddItem") + "\" class=\"related\">", entry, StringComparison.Ordinal);
        Assert.EndsWith("<td class=\"col-source\">" + sameCase + "</td></tr>\n", entry, StringComparison.Ordinal);
        Assert.Contains("<td class=\"col-source\">" + covered + "</td>", entry, StringComparison.Ordinal);
        Assert.Matches(@"\.failure-table tr\.related \.col-test a \{ font-weight: 400; \}", html);
    }

    // Reruns share their parent result's bugs, so coverage counts results. A bug on every failed result
    // says only how it is linked, and so does one linked through the Test Case too.
    [Fact]
    public void ALinkThroughResultsCountsTheFailedResultsItReaches()
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("large");
        string view = View(TestFailureReportFixture.Render(model));
        string entry = Entry(view, 5104);
        Assert.Equal(["Test result: 1 of 2 failed results", "Test result"], Regex.Matches(entry, "<td class=\"col-source\">([^<]*)</td>").Select(static m => m.Groups[1].Value));
        // In the small fixture, bug 801 is linked through both.
        Assert.Equal(["Test result, Test Case", "Test result", "Test Case"],
            Regex.Matches(Entry(View(TestFailureReportFixture.Render(Model())), 801), "<td class=\"col-source\">([^<]*)</td>").Select(static m => m.Groups[1].Value));
    }

    // The tests that no open bug tracks close the view, when some test has one and some other has none.
    // A bug that could not be read is not an open bug.
    [Fact]
    public void TheTestsWithoutAnOpenBugCloseTheView()
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("large");
        string view = View(TestFailureReportFixture.Render(model));
        string group = view[view.IndexOf("<tbody class=\"error-cluster\" id=\"no-bug\">", StringComparison.Ordinal)..];
        Assert.Contains("<div class=\"bug-head\"><span class=\"cluster-count\">10</span> <span class=\"bug-summary\"><span class=\"bug-title\">Without an open bug</span></span></div>",
            group, StringComparison.Ordinal);
        Assert.Equal(model.Failures.Where(static failure => !failure.HasOpenBug).Select(static failure => "f-" + failure.Ordinal.ToString(CultureInfo.InvariantCulture)), Rows(group));
        Assert.Contains("f-" + model.Failures.Single(static failure => failure.ShortName == "GetOrder").Ordinal.ToString(CultureInfo.InvariantCulture), Rows(group));
        Assert.Equal(10, Regex.Count(group, "<td class=\"col-source\"></td></tr>"));
        // Every test with an open bug, or none with one: no such group.
        Assert.DoesNotContain("id=\"no-bug\"", TestFailureReportFixture.Render("failed"), StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"no-bug\"", TestFailureReportFixture.Render(Build("en-US", Failure("A"), Failure("B"))), StringComparison.Ordinal);
    }

    // A bug can live in another project than the build. The attempt names it through that project,
    // like the bug list of the card and this view.
    [Fact]
    public void AnAttemptLinksItsBugThroughTheProjectOfTheBug()
    {
        TestFailureReportModel model = Build("en-US", new AdoTestFailure
        {
            Ordinal = 1, Classification = AdoTestFailureClassification.Failed, ShortName = "A", TestName = "Synthetic.BugTests.A", Storage = "Synthetic.Tests.dll",
            CollectionUri = TestFailureReportFixture.Collection, Bugs = [Read(805, result: true, project: OtherProject)],
            Attempts = [new AdoTestAttempt { Number = 1, RunId = 201, ResultId = 11, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure,
                ErrorMessage = "Failed A", AssociatedBugIds = [805] }],
        });
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.Contains(SinkEncoding.Attribute(AdoWebLinks.WorkItem(TestFailureReportFixture.Collection, OtherProject, 805).AbsoluteUri), html, StringComparison.Ordinal);
        Assert.DoesNotContain(SinkEncoding.Attribute(AdoWebLinks.WorkItem(TestFailureReportFixture.Collection, TestFailureReportFixture.Project, 805).AbsoluteUri),
            html, StringComparison.Ordinal);
    }

    private static string View(string html)
    {
        int start = html.IndexOf("<section class=\"view\" id=\"bugs\" data-view>", StringComparison.Ordinal);
        Assert.True(start >= 0);
        return html[start..html.IndexOf("</section>", start, StringComparison.Ordinal)];
    }

    private static string Entry(string view, int bug)
    {
        int start = view.IndexOf("<tbody class=\"error-cluster\" data-bug=\"" + bug.ToString(CultureInfo.InvariantCulture) + "\">", StringComparison.Ordinal);
        Assert.True(start >= 0);
        return view[start..view.IndexOf("</tbody>", start, StringComparison.Ordinal)];
    }

    private static IEnumerable<string> Rows(string entry) => Regex.Matches(entry, "<tr data-index-for=\"([^\"]+)\">").Select(m => m.Groups[1].Value);

    private static AdoTestBug Read(int id, bool result = false, bool testCase = false, string? project = null) => new()
    {
        Id = id, Title = "Bug " + id.ToString(CultureInfo.InvariantCulture), State = "Active", WorkItemType = "Bug", StateCategory = "InProgress",
        TeamProject = project ?? TestFailureReportFixture.Project, IsOpen = true, IsResolved = true, IsAssociatedWithResult = result, IsLinkedToTestCase = testCase,
        WebUrl = TestFailureReportFixture.Untrusted,
    };

    private static AdoTestBug Unread(int id) => new() { Id = id, IsAssociatedWithResult = true, WebUrl = TestFailureReportFixture.Untrusted };

    private static AdoTestFailure Failure(string name, params AdoTestBug[] bugs) => new()
    {
        Ordinal = 1, Classification = AdoTestFailureClassification.Failed, ShortName = name, TestName = "Synthetic.BugTests." + name, Storage = "Synthetic.Tests.dll",
        CollectionUri = TestFailureReportFixture.Collection, Bugs = bugs,
        Attempts = [new AdoTestAttempt { Number = 1, RunId = 201, ResultId = 11, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure, ErrorMessage = "Failed " + name }],
    };

    private static TestFailureReportModel Build(string culture, params AdoTestFailure[] failures)
    {
        AdoBuildTestFailureSet source = TestFailureReportFixture.Set("failed");
        return TestFailureReportModelBuilder.Build(new AdoBuildTestFailureSet
        {
            Build = source.Build, Runs = source.Runs, Summary = source.Summary, History = source.History, Failures = failures, FailedCount = failures.Length,
            Status = source.Status, RetrievedAt = source.RetrievedAt, CollectionUri = source.CollectionUri,
        }, TestFailureReportFixture.Options(culture));
    }
}
