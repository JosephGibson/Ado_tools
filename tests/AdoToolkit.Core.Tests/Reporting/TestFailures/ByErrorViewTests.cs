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
        Assert.Equal(5, Regex.Count(view, "<tr class=\"cluster-facts\"><td colspan=\"10\">"));
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
        Assert.Contains("<th scope=\"col\">Tests_FR</th><th scope=\"col\">Values</th><th scope=\"col\">Errors</th></tr></thead>", large, StringComparison.Ordinal);
        Assert.Contains("<td class=\"col-values\"><span class=\"values\" title=\"4 · 0\">4 · 0</span></td><td class=\"col-errors\">", Cluster(large, 5), StringComparison.Ordinal);
        Assert.Contains("<td class=\"col-values\"></td><td class=\"col-errors\">", Cluster(large, 4), StringComparison.Ordinal);
        string failed = View(TestFailureReportFixture.Render("failed"));
        Assert.DoesNotContain("Values", failed, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Open bugs</th><th scope=\"col\">Attempts</th><th scope=\"col\">Errors</th></tr></thead>", failed, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"rowgroup\" colspan=\"8\">", failed, StringComparison.Ordinal);
    }

    // Generic errors follow the specific ones under a heading of their own, which says what made them
    // generic; the tests without a message still come last. The summary counts the generic errors.
    [Theory]
    [InlineData("en-US", "Distinct errors: 9 · Generic: 4 · Tests: 8")]
    [InlineData("fr-CA", "Erreurs distinctes : 9 · Génériques : 4 · Tests : 8")]
    public void GenericErrorsFollowTheSpecificOnesUnderTheirOwnHeading(string culture, string summary)
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("bilingual", culture);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        string view = View(html);
        Assert.Contains("</h2>\n<p class=\"cluster-summary\">" + summary + "</p>\n", view, StringComparison.Ordinal);
        Assert.Equal(["e-1", "e-2", "e-3", "e-4", "e-5", "generic-errors", "e-6 generic", "e-7 generic", "e-8 generic", "e-9 generic", "e-10"],
            Regex.Matches(view, "<tbody class=\"(?:error-cluster|cluster-section)\" id=\"([^\"]+)\"( data-generic)?>")
                .Select(static m => m.Groups[1].Value + (m.Groups[2].Success ? " generic" : "")));
        Assert.Contains("<tbody class=\"cluster-section\" id=\"generic-errors\"><tr class=\"group-heading\"><th scope=\"rowgroup\" colspan=\"10\">"
            + model.Labels["GenericErrors"] + " <span class=\"section-note\">" + model.Labels["GenericNote"] + "</span></th></tr></tbody>", view, StringComparison.Ordinal);
        // Without a generic error there is no such heading and the summary says nothing of them.
        string large = View(TestFailureReportFixture.Render("large"));
        Assert.DoesNotContain("generic", large, StringComparison.OrdinalIgnoreCase);
    }

    // A rule names the cluster it made, in the report language when it is built in and as configured
    // otherwise; the line then keeps its exception type.
    [Theory]
    [InlineData("en-US", "Rule: WebDriver session failed", "WebDriver session failed", "Rule: Test data reset")]
    [InlineData("fr-CA", "Règle : Échec de la session WebDriver", "Échec de la session WebDriver", "Règle : Test data reset")]
    public void ARuleNamesTheClusterItMade(string culture, string title, string name, string configured)
    {
        string view = View(TestFailureReportFixture.Render("bilingual", culture));
        Assert.Contains("<div class=\"cluster-head\"><span class=\"cluster-count\">1</span> <span class=\"cluster-rule\" title=\"" + title + "\">" + name + "</span> "
            + "<code class=\"cluster-line\">OpenQA.Selenium.WebDriverException: The HTTP request to the remote WebDriver server for URL "
            + "http://selenium.example.test:4444/session/9a1e/element timed out after 60 seconds.</code></div>", Cluster(view, 6), StringComparison.Ordinal);
        Assert.Contains("<span class=\"cluster-rule\" title=\"" + configured + "\">Test data reset</span>", Cluster(view, 7), StringComparison.Ordinal);
        // An error that is no test's primary error counts its tests as another error.
        Assert.Contains("<span class=\"cluster-count\" title=\"" + (culture == "en-US" ? "As the primary error: 0 · As another error: 1" : "Comme erreur principale : 0 · Comme autre erreur : 1")
            + "\">0 +1</span>", Cluster(view, 8), StringComparison.Ordinal);
    }

    // A test's row sits under its primary error and says where that error came from and which other
    // errors the test had; under each other error it comes back muted, with a link to its primary error.
    [Fact]
    public void ATestComesBackMutedUnderItsOtherErrors()
    {
        TestFailureReportModel model = TestFailureReportFixture.Model("bilingual");
        string view = View(TestFailureReportFixture.Render(model));
        string cart = "f-" + model.Failures.Single(static failure => failure.ShortName == "AddToCart").Ordinal.ToString(CultureInfo.InvariantCulture);
        const string Welcome = "Assert.AreEqual failed. Expected:&lt;Welcome&gt;. Actual:&lt;Error&gt;.";
        Assert.Contains("<tr data-index-for=\"" + cart + "\">", Cluster(view, 2), StringComparison.Ordinal);
        Assert.EndsWith("<td class=\"col-values\"></td><td class=\"col-errors\"><span class=\"placement\">3 of 4 failed attempts</span> "
            + "<span class=\"other-errors\">Other errors: <a href=\"#e-1\" title=\"" + Welcome + "\">" + Welcome + "</a> ×1</span> "
            + "<span class=\"previous-error\">Other error in build 20261005.9</span></td></tr>\n", Cluster(view, 2), StringComparison.Ordinal);
        string muted = Section(Cluster(view, 1), "<tr data-index-for=\"" + cart + "\" class=\"related\">", "</tr>");
        Assert.EndsWith("<td class=\"col-values\"><span class=\"values\" title=\"Welcome · Error\">Welcome · Error</span></td><td class=\"col-errors\">"
            + "<span class=\"placement\">1 of 4 failed attempts</span> <span class=\"other-errors\">Primary error: <a href=\"#e-2\" "
            + "title=\"Synthetic.Shop.CartException: Le panier est vide après l’ajout de 3 articles\">CartException: Le panier est vide après l’ajout de 3 article…</a></span></td>",
            muted, StringComparison.Ordinal);
        // The heading counts the test as another error; the muted row follows the primary ones.
        Assert.Contains("<span class=\"cluster-count\" title=\"As the primary error: 1 · As another error: 1\">1 +1</span>", Cluster(view, 1), StringComparison.Ordinal);
        Assert.True(Cluster(view, 1).IndexOf("class=\"related\"", StringComparison.Ordinal) > Cluster(view, 1).IndexOf("<tr data-index-for=", StringComparison.Ordinal));
        // A generic other error is named by its rule.
        Assert.Contains("<span class=\"placement\">3 of 4 failed attempts</span> <span class=\"other-errors\">Other errors: <a href=\"#e-8\" "
            + "title=\"System.Net.Sockets.SocketException: No connection could be made because the target machine actively refused it. (orders.example.test:5001)\">Connection refused</a> ×1</span>",
            Cluster(view, 3), StringComparison.Ordinal);
        // An error of every failed attempt says so rather than "4 of 4".
        Assert.Contains("<td class=\"col-errors\"><span class=\"placement\">Every failed attempt</span></td>", Cluster(view, 4), StringComparison.Ordinal);
        Assert.Contains("<td class=\"col-errors\"></td>", Cluster(view, 10), StringComparison.Ordinal);
    }

    // Two wordings joined by pairing are facts of their error, with the test and groups that joined
    // them; the facts also say in how many classes the error's tests are.
    [Fact]
    public void TheFormsOfAnErrorAndWhatJoinedThemAreFacts()
    {
        string view = View(TestFailureReportFixture.Render("bilingual"));
        string facts = Section(Cluster(view, 1), "<tr class=\"cluster-facts\">", "<details");
        foreach (string fact in new[]
        {
            "<span class=\"fact\">Forms <strong>2</strong></span><span class=\"fact\">Paired by CheckTitle (Tests_EN, Tests_FR)</span>",
            "<span class=\"fact\">Common frame <code title=\"Synthetic.Shop.Pages.HomePage.CheckTitle\">HomePage.CheckTitle()</code> <strong>2</strong></span>",
            "<span class=\"fact\">Classes <strong>2</strong></span>",
        })
            Assert.Contains(fact, facts, StringComparison.Ordinal);
        // One test whose two wordings were paired has facts too; one test with one wording has none.
        Assert.Contains("<span class=\"fact\">Paired by OpenHome (Tests_EN, Tests_FR)</span>", Cluster(view, 4), StringComparison.Ordinal);
        Assert.DoesNotContain("cluster-facts", Cluster(view, 3), StringComparison.Ordinal);
        // The members of one wording show their values; two wordings in one cluster mark nothing.
        Assert.Contains("<span class=\"error-var\" title=\"Differs between tests: Bienvenue, Welcome\">Bienvenue</span>", Cluster(view, 1), StringComparison.Ordinal);
        Assert.DoesNotContain("error-var", Cluster(view, 4), StringComparison.Ordinal);
    }

    // A test's placement: how many of its failed attempts had the error, which of two equally frequent
    // errors came later, or that the message came from an attempt that did not fail.
    [Fact]
    public void EachRowSaysWhereItsErrorCameFrom()
    {
        AdoTestFailure tie = Failure(1, "Tie", ("Payment service returned no token", true), ("Cart total is negative", true));
        AdoTestFailure passed = Failure(2, "Passed", (null, true), ("Retried after a warning: stale element", false));
        string view = View(TestFailureReportFixture.Render(Model(tie, passed)));
        Assert.Contains("<span class=\"placement\">1 of 2 failed attempts, later than an equally frequent error</span> <span class=\"other-errors\">Other errors: "
            + "<a href=\"#e-3\" title=\"Payment service returned no token\">Payment service returned no token</a> ×1</span>", view, StringComparison.Ordinal);
        Assert.Contains("<span class=\"placement\">From an attempt that did not fail</span>", view, StringComparison.Ordinal);
    }

    // The sample is the latest attempt that had the cluster's error, not the test's latest error.
    [Fact]
    public void TheSampleIsTheLatestAttemptWithTheClustersError()
    {
        AdoTestFailure first = Failure(1, "A", ("Payment service returned no token", true), ("Payment service returned no token", true), ("Cart is empty", true));
        AdoTestFailure second = Failure(2, "B", ("Payment service returned no token", true), ("Order total is negative", true), ("Order total is negative", true));
        Assert.Equal("Payment service returned no token", Sample(Model(first, second)));
    }

    // A test's row says whether its previous failed build ended with the same error, read from the
    // start of the messages that build listed; the facts count the tests whose previous error was the
    // same. A muted row says nothing of it: the comparison is the test's primary error's.
    [Theory]
    [InlineData("en-US", "Same error in build 20261005.9", "Other error in build 20261005.9", "Same error in the previous failed build")]
    [InlineData("fr-CA", "Même erreur dans le build 20261005.9", "Autre erreur dans le build 20261005.9", "Même erreur dans le précédent build en échec")]
    public void EachRowComparesItsErrorWithThePreviousFailedBuild(string culture, string same, string other, string fact)
    {
        string view = View(TestFailureReportFixture.Render("bilingual", culture));
        Assert.Contains("</span> <span class=\"previous-error\">" + same + "</span></td>", Cluster(view, 1), StringComparison.Ordinal);
        Assert.Contains("</span> <span class=\"previous-error\">" + other + "</span></td>", Cluster(view, 2), StringComparison.Ordinal);
        Assert.Contains("<span class=\"fact\">" + fact + " <strong>" + (culture == "en-US" ? "1 of 1" : "1 sur 1") + "</strong></span>", Cluster(view, 1), StringComparison.Ordinal);
        Assert.DoesNotContain("previous-error", Section(Cluster(view, 1), "class=\"related\"", "</tr>"), StringComparison.Ordinal);
        // Without a listed message, nothing is said.
        Assert.DoesNotContain("previous-error", View(TestFailureReportFixture.Render("large")), StringComparison.Ordinal);
    }

    // A recognized NUnit message under an exception line shows that line, without the type that the
    // heading shows apart, in the heading, the Overview and a label.
    [Fact]
    public void ARecognizedMessageUnderAnExceptionLineShowsThatLine()
    {
        const string Message = "System.Net.Http.HttpRequestException: Connection refused\n  Expected: 0\n  But was:  3";
        TestFailureReportModel model = Model(Failure(1, "A", (Message, true)), Failure(2, "B", (Message, true), ("Cart is empty", true), ("Cart is empty", true)));
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.Contains("<code class=\"exception-type\" title=\"System.Net.Http.HttpRequestException\">HttpRequestException</code> "
            + "<code class=\"cluster-line\">Connection refused · Expected: 0 · But was:  3</code>", View(html), StringComparison.Ordinal);
        Assert.Contains(">HttpRequestException: Connection refused · Expected: 0 · But…</a> ×1", View(html), StringComparison.Ordinal);
        Assert.Contains("<span class=\"glance-line\">Connection refused · Expected: 0 · But was:  3</span>", html, StringComparison.Ordinal);
    }

    // The tests without an error message share what a group of two tests or more shares, without a sample.
    [Fact]
    public void TheTestsWithoutAMessageHaveTheirFactsToo()
    {
        string view = View(TestFailureReportFixture.Render(Model(Failure(1, "A", (null, true)), Failure(2, "B", (null, true)))));
        Assert.Contains("<tr class=\"cluster-facts\">", Cluster(view, 1), StringComparison.Ordinal);
        Assert.DoesNotContain("cluster-sample", Cluster(view, 1), StringComparison.Ordinal);
    }

    // A rule that joins two wordings in two tests: each member shows its own line.
    [Fact]
    public void TheMembersOfARuleWithTwoWordingsShowTheirOwnLines()
    {
        AdoBuildTestFailureSet source = TestFailureReportFixture.Set("failed");
        AdoTestFailure[] failures = [Failure(1, "A", ("Home page did not load within 30 seconds", true)),
            Failure(2, "B", ("La page d'accueil ne s'est pas chargée en moins de 30 secondes", true))];
        TestFailureReportModel model = TestFailureReportModelBuilder.Build(new AdoBuildTestFailureSet
        {
            Build = source.Build, Runs = source.Runs, Summary = source.Summary, History = source.History, Failures = failures, FailedCount = failures.Length,
            Status = source.Status, RetrievedAt = source.RetrievedAt, CollectionUri = source.CollectionUri,
        }, TestFailureReportFixture.Options("en-US", [new ErrorRuleOptions { Name = "Home page", Generic = false,
            Patterns = ["Home page did not load within * seconds", "La page d'accueil ne s'est pas chargée en moins de * secondes"] }]));
        string cluster = Cluster(View(TestFailureReportFixture.Render(model)), 1);
        Assert.Contains("<span class=\"cluster-count\">2</span> <span class=\"cluster-rule\" title=\"Rule: Home page\">Home page</span>", cluster, StringComparison.Ordinal);
        Assert.Contains("<td class=\"col-values\"><span class=\"values\" title=\"Home page did not load within 30 seconds\">Home page did not load within 30 seconds</span></td>",
            cluster, StringComparison.Ordinal);
        Assert.Contains("<span class=\"values\" title=\"La page d&#x27;accueil ne s&#x27;est pas chargée en moins de 30 secondes\">", cluster, StringComparison.Ordinal);
        Assert.DoesNotContain("error-var", cluster, StringComparison.Ordinal);
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

    private static AdoTestFailure Failure(int ordinal, string name, string message) => Failure(ordinal, name, (message, true));

    // One attempt per message, in order; a message of an attempt that did not fail belongs to a pass.
    private static AdoTestFailure Failure(int ordinal, string name, params (string? Message, bool Failed)[] attempts) => new()
    {
        Ordinal = ordinal, Classification = AdoTestFailureClassification.Failed, ShortName = name, TestName = "Synthetic.ClusterTests." + name, Storage = "Synthetic.Tests.dll",
        CollectionUri = TestFailureReportFixture.Collection,
        Attempts = [.. attempts.Select((attempt, index) => new AdoTestAttempt
        {
            Number = index + 1, RunId = 201, ResultId = 10 * ordinal + index, Outcome = attempt.Failed ? "Failed" : "Passed",
            OutcomeClass = attempt.Failed ? AdoTestOutcomeClass.Failure : AdoTestOutcomeClass.Pass, ErrorMessage = attempt.Message,
        })],
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

    // A cluster of a generic error also carries data-generic.
    private static string Cluster(string view, int number) =>
        Section(view, "<tbody class=\"error-cluster\" id=\"e-" + number.ToString(CultureInfo.InvariantCulture) + "\"", "</tbody>");

    private static string Section(string html, string start, string end)
    {
        int position = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(position >= 0, start);
        int stop = html.IndexOf(end, position + start.Length, StringComparison.Ordinal);
        Assert.True(stop > position, end);
        return html[position..stop];
    }
}
