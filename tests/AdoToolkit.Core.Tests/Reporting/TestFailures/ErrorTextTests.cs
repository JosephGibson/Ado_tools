using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

// Every attempt renders its own error message and stack trace, whatever an earlier attempt showed.
public sealed class ErrorTextTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private const string Message = "Assert.Equal() Failure: Expected: \"Confirmed\" Actual: \"Pending\"";

    [Fact]
    public void AttemptsWithTheSameMessageAndTraceEachRenderTheirOwnCopy()
    {
        const string Trace = "   at Synthetic.CheckoutTests.SubmitOrder() in C:\\synthetic\\Checkout.cs:line 42";
        TestFailureReportModel model = Model([Failure(1, 3, Message, Trace)]);
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        string card = html[html.IndexOf("<article class=\"card failure-card\" id=\"f-1\"", StringComparison.Ordinal)..html.IndexOf("</article>", StringComparison.Ordinal)];
        Assert.Equal(3, Regex.Count(card, "<code class=\"lang-error\">"));
        Assert.Equal(3, Regex.Count(card, "<code class=\"lang-stacktrace\">"));
        foreach (int number in new[] { 1, 2, 3 })
        {
            string opening = "<details class=\"attempt\" id=\"f-1-a" + number.ToString(CultureInfo.InvariantCulture) + "\">";
            int start = card.IndexOf(opening, StringComparison.Ordinal);
            int end = card.IndexOf("<details class=\"attempt\"", start + opening.Length, StringComparison.Ordinal);
            string text = TestFailureMarkup.Text(card[start..(end < 0 ? card.Length : end)]);
            // Once in the summary line and once in the message block; the trace only in its block.
            Assert.Equal(2, Regex.Count(text, Regex.Escape(Message)));
            Assert.Equal(1, Regex.Count(text, Regex.Escape(Trace)));
        }
        Assert.DoesNotContain("same-as", html, StringComparison.Ordinal);
    }

    // A summary cut at its character limit must not split a surrogate pair: the lone half reaches
    // the sink, where the encoder replaces it with U+FFFD, in the Overview cell and in its title.
    [Fact]
    public void SummaryCutAtItsLimitKeepsTheAstralCharacterWhole()
    {
        // The pair straddles the 240th character, so a raw slice keeps only its high half.
        string message = new string('x', 239) + "\U0001F600 timed out";
        Assert.Equal('\uD83D', message[239]);

        string? line = HtmlTestFailureRenderer.LatestError(Failure(1, 1, message, "   at Synthetic.Ui.Checkout.Submit()"));

        Assert.NotNull(line);
        Assert.DoesNotContain(line, char.IsSurrogate);
        string html = TestFailureReportFixture.Render(Model([Failure(1, 1, message, "   at Synthetic.Ui.Checkout.Submit()")]));
        Assert.DoesNotContain("&#xFFFD;", html, StringComparison.Ordinal);
    }

    // The largest report the guides describe: 200 failures, each with 7 English and 7 French attempts
    // that repeat its message and a 40-frame stack trace, with four attachments, dates and failure
    // fields per attempt. Now that no attempt refers to an earlier one for its text, it renders,
    // validates, keeps every trace and every attempt closed, and stays within the size budget. With
    // three errors per test, By error shows each test once more, muted, under each other error.
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void TwoHundredFailuresWithFourteenAttemptsRenderValidateAndStayWithinTheSizeBudget(int errors)
    {
        AdoBuildTestFailureSet grouped = TestFailureReportFixture.Set("grouped");
        DateTimeOffset clock = TestFailureReportFixture.Clock;
        string trace = "OpenQA.Selenium.WebDriverTimeoutException: Timed out after 30 seconds\n" + string.Join('\n', Enumerable.Range(1, 40).Select(i =>
            "   at Synthetic.Ui.Framework.Layer" + i.ToString(CultureInfo.InvariantCulture) + ".Helper.InvokeStep(String name, Int32 timeout) in C:\\agent\\_work\\1\\s\\src\\Layer"
            + i.ToString(CultureInfo.InvariantCulture) + "\\Helper.cs:line 42"));
        AdoTestRun[] runs = [.. Enumerable.Range(1, 14).Select(i => new AdoTestRun
        {
            Id = 1000 + i, Name = "Synthetic run", BuildId = 401, State = "Completed", StartedDate = clock.AddMinutes(-60 + i), StageName = i <= 7 ? "Tests_EN" : "Tests_FR",
            PipelineAttempt = 1 + ((i - 1) % 7), TeamProject = TestFailureReportFixture.Project, CollectionUri = TestFailureReportFixture.Collection,
        })];
        AdoTestFailure[] failures = [.. Enumerable.Range(1, 200).Select(f => new AdoTestFailure
        {
            Ordinal = f, Classification = AdoTestFailureClassification.Failed, ShortName = "Scenario" + f.ToString(CultureInfo.InvariantCulture),
            TestName = "Synthetic.Ui.CheckoutTests.Scenario" + f.ToString(CultureInfo.InvariantCulture), Storage = "Synthetic.Ui.dll", CollectionUri = TestFailureReportFixture.Collection,
            TestCase = new AdoTestCaseLink { Id = 12000 + f, Title = "Checkout scenario", State = "Ready", IsResolved = true, WebUrl = TestFailureReportFixture.Untrusted },
            Attempts = [.. Enumerable.Range(1, 14).Select(n => new AdoTestAttempt
            {
                Number = n, Source = n == 1 ? AdoTestAttemptSource.Single : AdoTestAttemptSource.RunAttempt, RunId = 1000 + n, ResultId = 100000 + f * 20 + n,
                Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure, StartedDate = clock.AddMinutes(-50), CompletedDate = clock.AddMinutes(-49),
                Duration = TimeSpan.FromSeconds(31.2), ComputerName = "SYNTHETIC-AGENT-07", FailureType = "Regression", ResolutionState = "Unresolved", FailingSinceBuildId = 399,
                ErrorMessage = "OpenQA.Selenium.WebDriverTimeoutException : Timed out after 30 seconds waiting for element '#submit-" + f.ToString(CultureInfo.InvariantCulture)
                    // A letter is text, where a number would be a value: three errors that every test shares.
                    + (errors == 1 ? "" : "-" + "abc"[n % errors]) + "' to be clickable.",
                StackTrace = trace,
                Attachments = [.. AttachmentNames.Select((name, k) => new AdoTestAttachment
                {
                    Id = n * 10 + k + 1, RunId = 1000 + n, ResultId = 100000 + f * 20 + n, FileName = name, Size = 20480, AttachmentType = "GeneralAttachment",
                    Kind = AttachmentKinds.FromFileName(name),
                })],
            })],
        })];
        TestFailureReportModel model = TestFailureReportModelBuilder.Build(new AdoBuildTestFailureSet
        {
            Build = grouped.Build, Runs = runs, Summary = grouped.Summary, History = grouped.History, Failures = failures, FailedCount = failures.Length,
            Status = grouped.Status, RetrievedAt = grouped.RetrievedAt, CollectionUri = grouped.CollectionUri,
        }, TestFailureReportFixture.Options("en-US"));
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.Equal(200 * 14, Regex.Count(html, "<code class=\"lang-stacktrace\">"));
        Assert.DoesNotMatch("<details[^>]*\\sopen[\\s>=]", html);
        Assert.Equal(200 * (errors - 1), Regex.Count(html, "<tr data-index-for=\"f-[0-9]+\" class=\"related\">"));
        long bytes = Encoding.UTF8.GetByteCount(html);
        TestContext.Current.TestOutputHelper?.WriteLine("Errors per test: " + errors.ToString(CultureInfo.InvariantCulture) + ", bytes: " + bytes.ToString("N0", English));
        Assert.True(bytes < SizeBudget, "The report is " + bytes.ToString("N0", English) + " bytes.");
    }

    private static readonly string[] AttachmentNames = ["screenshot.png", "page.html", "context.json", "console.txt"];

    // Measured at 52,911,987 bytes when the 100 × 14 size test with attachments joined this one, the
    // headroom of the two budgets before it (2.3 %).
    private const long SizeBudget = 54_100_000;

    private static AdoTestFailure Failure(int ordinal, int attempts, string message, string trace) => new()
    {
        Ordinal = ordinal, Classification = AdoTestFailureClassification.Failed, ShortName = "Scenario" + ordinal.ToString(CultureInfo.InvariantCulture),
        TestName = "Synthetic.Ui.CheckoutTests.Scenario" + ordinal.ToString(CultureInfo.InvariantCulture), Storage = "Synthetic.Ui.dll",
        CollectionUri = TestFailureReportFixture.Collection,
        Attempts = [.. Enumerable.Range(1, attempts).Select(number => new AdoTestAttempt
        {
            Number = number, Source = number == 1 ? AdoTestAttemptSource.Single : AdoTestAttemptSource.RunAttempt, RunId = 201, ResultId = ordinal * 100 + number,
            Outcome = "Failed", OutcomeClass = AdoTestOutcomeClass.Failure, Duration = TimeSpan.FromSeconds(31.2), ComputerName = "SYNTHETIC-AGENT-07",
            ErrorMessage = message, StackTrace = trace,
        })],
    };

    private static TestFailureReportModel Model(IReadOnlyList<AdoTestFailure> failures)
    {
        AdoBuildTestFailureSet source = TestFailureReportFixture.Set("failed");
        return TestFailureReportModelBuilder.Build(new AdoBuildTestFailureSet
        {
            Build = source.Build, Runs = source.Runs, Summary = source.Summary, History = source.History, Failures = failures, FailedCount = failures.Count,
            Status = source.Status, RetrievedAt = source.RetrievedAt, CollectionUri = source.CollectionUri,
        }, TestFailureReportFixture.Options("en-US"));
    }
}
