using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// The By error view: one table, a row group per cluster of tests that share their latest error.
// Most cases use the large fixture, whose clusters ErrorClustersTests describes.
public sealed class ByErrorViewTests
{
    [Theory]
    [InlineData("en-US", "Distinct errors: 7 · Tests: 16", "Differs between tests: ")]
    [InlineData("fr-CA", "Erreurs distinctes\u00A0: 7 · Tests\u00A0: 16", "Diffère d’un test à l’autre\u00A0: ")]
    public void EachClusterHeadingSaysItsCountTypeAndLineWithTheValuesThatDiffer(string culture, string summary, string differs)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("large", culture);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        string view = View(html);
        Assert.Contains("</h2>\n<p class=\"cluster-summary\">" + summary + "</p>\n", view, StringComparison.Ordinal);
        Assert.Equal(["e-1", "e-2", "e-3", "e-4", "e-5", "e-6", "e-7", "e-8"], Regex.Matches(view, "<tbody class=\"error-cluster\" id=\"(e-[0-9]+)\">").Select(static m => m.Groups[1].Value));
        // The line of the first test without its exception type, which shows by its short name with the
        // full name as title; each value that differs is marked and titled with every value.
        Assert.Contains("<div class=\"cluster-head\"><span class=\"cluster-count\">4</span> <code class=\"exception-type\" title=\"System.TimeoutException\">TimeoutException</code> "
            + "<code class=\"cluster-line\">Timed out after <span class=\"error-var\" title=\"" + differs + "3000, 4500, 30000\">3000</span> ms waiting for #submit</code></div>",
            Cluster(view, 1), StringComparison.Ordinal);
        Assert.Contains("<code class=\"cluster-line\">Could not find file &#x27;<span class=\"error-var\" title=\"" + differs
            + @"/home/agent/work/3/s/out/invoices.json, C:\agent\_work\7\s\data\customers.json, C:\agent\_work\12\s\data\orders-1.json"">/home/agent/work/3/s/out/invoices.json</span>&#x27;.</code>",
            Cluster(view, 2), StringComparison.Ordinal);
        // No type: the line is the error's first line, with two values that differ.
        Assert.Contains("<span class=\"cluster-count\">2</span> <code class=\"cluster-line\">Expected <span class=\"error-var\" title=\"" + differs + "4, 12\">4</span> results but found "
            + "<span class=\"error-var\" title=\"" + differs + "0, 9\">0</span>.</code>", Cluster(view, 5), StringComparison.Ordinal);
        // The tests without an error message come last.
        Assert.Contains("<span class=\"cluster-count\">1</span> <span class=\"cluster-line no-message\">" + model.Labels["NoErrorMessage"] + "</span>", Cluster(view, 8), StringComparison.Ordinal);
    }

    // A cluster of two tests or more says what its tests share and shows a sample; a single test does not.
    [Fact]
    public void FactsAndASampleFollowTheHeadingOfAClusterOfTwoTestsOrMore()
    {
        string view = View(TestFailureReportFixture.Render("large"));
        Assert.Equal(5, Regex.Count(view, "<tr class=\"cluster-facts\"><td colspan=\"9\">"));
        Assert.Equal(5, Regex.Count(view, "<details class=\"cluster-sample\">"));
        foreach (int number in new[] { 6, 7, 8 })
        {
            Assert.DoesNotContain("cluster-facts", Cluster(view, number), StringComparison.Ordinal);
            Assert.DoesNotContain("cluster-sample", Cluster(view, number), StringComparison.Ordinal);
        }
        string facts = Section(Cluster(view, 1), "<tr class=\"cluster-facts\">", "<details");
        foreach (string fact in new[]
        {
            "<span class=\"fact status-failed\"><span aria-hidden=\"true\">✕</span> Failed <strong>3</strong></span>",
            "<span class=\"fact status-flaky\"><span aria-hidden=\"true\">≈</span> Flaky <strong>1</strong></span>",
            "<span class=\"fact\"><span class=\"trend trend-new\"><span aria-hidden=\"true\">✦</span> New</span> <strong>1</strong></span>",
            "<span class=\"fact\"><span class=\"trend trend-since\">Recurring</span> <strong>3</strong></span>",
            // Bug 5101 was filed after the build was queued, so the facts line carries the marker too.
            "<span class=\"fact fact-tracked\">With an open bug <strong>1</strong> <a class=\"open-bug-marker bug-new\"",
            "✦</span> #5101</a></span>",
            "<span class=\"fact fact-untracked\">Without an open bug <strong>3</strong></span>",
            "<span class=\"fact\">Tests_EN <strong>3</strong></span><span class=\"fact\">Tests_FR <strong>3</strong></span>",
            "<span class=\"fact\">Common frame <code title=\"Synthetic.Web.Pages.CheckoutPage.Submit\">CheckoutPage.Submit()</code> <strong>4</strong></span>",
        })
            Assert.Contains(fact, facts, StringComparison.Ordinal);
        // Every test of the cluster with the API error has no open bug: bug 5103 could not be read.
        string api = Section(Cluster(view, 3), "<tr class=\"cluster-facts\">", "<details");
        Assert.DoesNotContain("fact-tracked", api, StringComparison.Ordinal);
        Assert.Contains("<span class=\"fact fact-untracked\">Without an open bug <strong>2</strong></span>", api, StringComparison.Ordinal);
        Assert.Contains("<details class=\"cluster-sample\"><summary>Sample message: AddItem</summary>", Cluster(view, 1), StringComparison.Ordinal);
    }

    // The sample is the first test's message up to 12 lines and 1,000 characters; the card holds all of it.
    [Fact]
    public void TheSampleIsTheStartOfTheFirstTestsMessage()
    {
        string lines = string.Join('\n', Enumerable.Range(1, 30).Select(static line => "Step " + line.ToString("D2", CultureInfo.InvariantCulture) + " failed"));
        string sample = Sample(Model(Failure(1, "A", lines), Failure(2, "B", lines.Replace("Step 01", "Step 99", StringComparison.Ordinal))));
        Assert.Equal(string.Join('\n', lines.Split('\n').Take(12)) + "\n…", sample);
        string wide = "Payload " + new string('x', 4992) + "\nsecond line";
        sample = Sample(Model(Failure(1, "A", wide), Failure(2, "B", wide)));
        Assert.Equal(wide[..1000] + "\n…", sample);
        Assert.Equal("Short message", Sample(Model(Failure(1, "A", "Short message"), Failure(2, "B", "Short message"))));
    }

    // The line is cut at the length of the other one-line summaries, and never inside a marked value:
    // a value that does not fit is left out whole.
    [Fact]
    public void ALongClusterLineIsCutNeverInsideAMarkedValue()
    {
        string start = "Lookup " + new string('x', 230) + " ";
        TestFailureReportModel model = Model(Failure(1, "A", start + "123456789 failed"), Failure(2, "B", start + "987654321 failed"));
        string heading = Cluster(View(TestFailureReportFixture.Render(model)), 1);
        Assert.Contains("<code class=\"cluster-line\">" + start + "…</code>", heading, StringComparison.Ordinal);
        Assert.DoesNotContain("error-var", heading, StringComparison.Ordinal);

        // A value that fits is marked whole, and the cut falls in the text after it.
        string shorter = "Lookup " + new string('x', 200) + " ";
        model = Model(Failure(1, "A", shorter + "123456789 " + new string('y', 60)), Failure(2, "B", shorter + "987654321 " + new string('y', 60)));
        heading = Cluster(View(TestFailureReportFixture.Render(model)), 1);
        Assert.Contains(shorter + "<span class=\"error-var\" title=\"Differs between tests: 123456789, 987654321\">123456789</span> " + new string('y', 22) + "…</code>",
            heading, StringComparison.Ordinal);
    }

    // The Values column shows what differs in each test's line, and only when some cluster has values that differ.
    [Fact]
    public void TheValuesColumnShowsOnlyWhenSomeValueDiffers()
    {
        string large = View(TestFailureReportFixture.Render("large"));
        Assert.Contains("<th scope=\"col\">Tests_FR</th><th scope=\"col\">Values</th></tr></thead>", large, StringComparison.Ordinal);
        Assert.Contains("<td class=\"col-values\"><span class=\"values\" title=\"4 · 0\">4 · 0</span></td></tr>", Cluster(large, 5), StringComparison.Ordinal);
        Assert.Contains("<td class=\"col-values\"></td></tr>", Cluster(large, 4), StringComparison.Ordinal);
        string failed = View(TestFailureReportFixture.Render("failed"));
        Assert.DoesNotContain("Values", failed, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Open bugs</th><th scope=\"col\">Attempts</th></tr></thead>", failed, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"rowgroup\" colspan=\"7\">", failed, StringComparison.Ordinal);
    }

    // After MSTest's "Test method X threw exception:", the attempt's summary shows the exception, as
    // By error does; the Overview and the console table keep the first line.
    [Fact]
    public void TheAttemptSummaryShowsTheExceptionAfterMsTestsFirstLine()
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("large");
        AdoTestFailure getOrder = model.Failures.Single(static failure => failure.ShortName == "GetOrder");
        string html = TestFailureReportFixture.Render(model);
        const string First = "Test method Synthetic.Web.Api.OrdersApiTests.GetOrder threw exception:";
        Assert.Equal(First, HtmlTestFailureRenderer.LatestError(getOrder));
        string anchor = "f-" + getOrder.Ordinal.ToString(CultureInfo.InvariantCulture);
        Assert.Contains("<td class=\"col-error\" title=\"" + First + "\">" + First + "</td>", Section(html, "<tr data-index-for=\"" + anchor + "\">", "</tr>"), StringComparison.Ordinal);
        Assert.EndsWith("<span class=\"attempt-error\">Synthetic.Web.Api.ApiException: Order 3f2504e0-4f89-11d3-9a0c-0305e82c3301 was not found (HTTP 404).</span>",
            Section(html, "<details class=\"attempt\" id=\"" + anchor + "-a1\">", "</summary>"), StringComparison.Ordinal);
    }

    private static string Sample(TestFailureReportModel model)
    {
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        return TestFailureMarkup.Text(Section(Section(html, "<details class=\"cluster-sample\">", "</details>"), "<code class=\"lang-error\">", "</code>")
            ["<code class=\"lang-error\">".Length..]);
    }

    private static AdoTestFailure Failure(int ordinal, string name, string message) => new()
    {
        Ordinal = ordinal, Classification = AdoTestFailureClassification.Failed, ShortName = name, TestName = "Synthetic.ClusterTests." + name, Storage = "Synthetic.Tests.dll",
        CollectionUri = TestFailureReportFixture.Collection,
        Attempts = [new AdoTestAttempt { Number = 1, RunId = 201, ResultId = 10 + ordinal, Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure, ErrorMessage = message }],
    };

    private static TestFailureReportModel Model(params AdoTestFailure[] failures)
    {
        AdoBuildTestFailureSet source = TestFailureReportFixture.Set("failed");
        return TestFailureReportModelBuilder.Build(new AdoBuildTestFailureSet
        {
            Build = source.Build, Runs = source.Runs, Summary = source.Summary, History = source.History, Failures = failures, FailedCount = failures.Length,
            Status = source.Status, RetrievedAt = source.RetrievedAt, CollectionUri = source.CollectionUri,
        }, TestFailureReportFixture.Options("en-US"));
    }

    private static string View(string html) => Section(html, "<section class=\"view\" id=\"by-error\" data-view>", "</section>");

    private static string Cluster(string view, int number) =>
        Section(view, "<tbody class=\"error-cluster\" id=\"e-" + number.ToString(CultureInfo.InvariantCulture) + "\">", "</tbody>");

    private static string Section(string html, string start, string end)
    {
        int position = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(position >= 0, start);
        int stop = html.IndexOf(end, position + start.Length, StringComparison.Ordinal);
        Assert.True(stop > position, end);
        return html[position..stop];
    }
}
