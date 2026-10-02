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

    // 200 failures with 14 attempts that all carry the same message and 40-frame trace: the largest
    // report the guides describe, now that no attempt refers to an earlier one for its text.
    [Fact]
    public void TwoHundredFailuresWithFourteenIdenticalAttemptsRenderAndValidate()
    {
        string trace = "OpenQA.Selenium.WebDriverTimeoutException: Timed out after 30 seconds\n" + string.Join('\n', Enumerable.Range(1, 40).Select(i =>
            "   at Synthetic.Ui.Framework.Layer" + i.ToString(CultureInfo.InvariantCulture) + ".Helper.InvokeStep(String name, Int32 timeout) in C:\\agent\\_work\\1\\s\\src\\Layer"
            + i.ToString(CultureInfo.InvariantCulture) + "\\Helper.cs:line 42"));
        TestFailureReportModel model = Model([.. Enumerable.Range(1, 200).Select(ordinal => Failure(ordinal, 14, Message, trace))]);
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        HtmlTestFailureRenderer.Render(model, writer);
        string html = writer.ToString();
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.Equal(200 * 14, Regex.Count(html, "<code class=\"lang-stacktrace\">"));
        long bytes = Encoding.UTF8.GetByteCount(html);
        Assert.True(bytes < SizeBudget, "The report is " + bytes.ToString("N0", English) + " bytes.");
    }

    // Measured at 46,904,124 bytes when the cross-references between attempts were removed.
    private const long SizeBudget = 48_000_000;

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
