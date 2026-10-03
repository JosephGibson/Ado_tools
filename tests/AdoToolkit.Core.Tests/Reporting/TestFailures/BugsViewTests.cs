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
        // Read bugs first, the most tests first, then by ID; unread bugs last, by ID.
        Assert.Equal(["801", "804", "805", "799", "803"], Regex.Matches(view, "<tbody class=\"error-cluster\" data-bug=\"([0-9]+)\">").Select(m => m.Groups[1].Value));
        Assert.Equal(["3", "2", "2", "2", "1"], Regex.Matches(view, "<span class=\"cluster-count\">([0-9]+)</span>").Select(m => m.Groups[1].Value));
        // Rows are in report order, and a test with several bugs has a row under each.
        Assert.Equal(["f-1", "f-2", "f-3"], Rows(Entry(view, 801)));
        Assert.Equal(["f-1", "f-3"], Rows(Entry(view, 804)));
        Assert.Equal(["f-1", "f-3"], Rows(Entry(view, 805)));
        Assert.Equal(["f-2", "f-3"], Rows(Entry(view, 799)));
        Assert.Equal(["f-2"], Rows(Entry(view, 803)));
        Assert.DoesNotContain("data-index-for=\"f-4\"", view, StringComparison.Ordinal);
        // The Open bug marker belongs to the other tables: here the entry itself names the bug.
        Assert.DoesNotContain("open-bug-marker", view, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Linked through</th></tr></thead>", view, StringComparison.Ordinal);
        Assert.Equal(["Test result, Test Case", "Test result", "Test Case"],
            Regex.Matches(Entry(view, 801), "<td class=\"col-source\">([^<]*)</td>").Select(m => m.Groups[1].Value));
        // The heading spans every column: number, test, Test Case, attempts and the source.
        Assert.Contains("<tr class=\"cluster-heading\"><th scope=\"rowgroup\" colspan=\"5\">", view, StringComparison.Ordinal);
    }

    [Fact]
    public void EntryShowsTheBugInItsOwnProjectAndMarksAnUnreadBug()
    {
        string view = View(TestFailureReportFixture.Render(Model()));
        string prefix = TestFailureReportFixture.Collection.AbsoluteUri;
        string other = Entry(view, 805);
        Assert.Contains("href=\"" + prefix + Uri.EscapeDataString(OtherProject) + "/_workitems/edit/805\">#805", other, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bug-title\">Bug 805</span> <span class=\"bug-state\">Active</span></th>", other, StringComparison.Ordinal);
        Assert.DoesNotContain("bug-unread", other, StringComparison.Ordinal);
        string unread = Entry(view, 799);
        Assert.Contains("href=\"" + prefix + Uri.EscapeDataString(TestFailureReportFixture.Project) + "/_workitems/edit/799\">#799", unread, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bug-unread\">Not read</span></th>", unread, StringComparison.Ordinal);
        Assert.DoesNotContain("bug-title", unread, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en-US", "Open bugs", "Not read", "Linked through")]
    [InlineData("fr-CA", "Bogues ouverts", "Non lu", "Lié par")]
    public void ViewSitsAfterByErrorWithItsEntryCountInTheTab(string culture, string heading, string unread, string source)
    {
        string html = TestFailureReportFixture.Render(Model(culture));
        Assert.Contains("<a href=\"#by-error\" data-view-link=\"by-error\">", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"#bugs\" data-view-link=\"bugs\">" + heading + " 5</a><a href=\"#details\" data-view-link=\"details\">", html, StringComparison.Ordinal);
        int byError = html.IndexOf("<section class=\"view\" id=\"by-error\"", StringComparison.Ordinal);
        int bugs = html.IndexOf("<section class=\"view\" id=\"bugs\" data-view>", StringComparison.Ordinal);
        int details = html.IndexOf("<section class=\"view\" id=\"details\"", StringComparison.Ordinal);
        Assert.True(byError >= 0 && byError < bugs && bugs < details);
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
        Assert.Contains("<tbody class=\"error-cluster\" data-bug=\"804\"><tr class=\"cluster-heading\"><th scope=\"rowgroup\" colspan=\"6\">", view, StringComparison.Ordinal);
        Assert.Equal(["f-1"], Rows(Entry(view, 804)));
        Assert.Contains("<td class=\"col-source\">Test Case</td>", view, StringComparison.Ordinal);
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
