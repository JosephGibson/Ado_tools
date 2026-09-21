using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestRuns;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

[Trait("Acceptance", "S5-5")]
public sealed class StructuralCompletenessTests
{
    [Theory]
    [InlineData("failed", "en-US")]
    [InlineData("flaky", "fr-CA")]
    [InlineData("partial", "fr-CA")]
    [InlineData("hostile", "en-US")]
    [InlineData("grouped", "fr-CA")]
    public void EveryFailureAttemptAndPresentDetailSurvivesWithScriptsIgnored(string variant, string culture)
    {
        var model = TestFailureReportFixture.Model(variant, culture);
        string html = TestFailureMarkup.WithoutScripts(TestFailureReportFixture.Render(model));
        Assert.Contains("<head>\n<meta charset=\"utf-8\">", html, StringComparison.Ordinal);
        Assert.Contains("data-failure-count=\"" + model.Failures.Count.ToString(CultureInfo.InvariantCulture) + "\"", html, StringComparison.Ordinal);
        int previous = -1;
        foreach (var failure in model.Failures)
        {
            string anchor = "f-" + failure.Ordinal.ToString(CultureInfo.InvariantCulture);
            string card = Section(html, "<article class=\"card failure-card\" id=\"" + anchor + "\"", "</article>");
            int position = html.IndexOf(card, StringComparison.Ordinal);
            Assert.True(position > previous);
            previous = position;
            string text = TestFailureMarkup.Text(card);
            foreach (string? value in new[] { failure.ShortName, failure.TestName, failure.Storage, failure.Title, failure.Owner?.DisplayName, failure.Owner?.UniqueName })
                if (value is not null) Assert.Contains(value, text, StringComparison.Ordinal);
            foreach (AdoTestAttempt attempt in failure.Attempts)
            {
                string opening = "<details class=\"attempt\" id=\"" + anchor + "-a" + attempt.Number.ToString(CultureInfo.InvariantCulture) + "\"";
                int start = card.IndexOf(opening, StringComparison.Ordinal);
                Assert.True(start >= 0);
                string tag = card[start..(card.IndexOf('>', start) + 1)];
                // Every attempt starts collapsed, failed or not.
                Assert.DoesNotContain(" open", tag, StringComparison.Ordinal);
                int end = card.IndexOf("<details class=\"attempt\"", start + opening.Length, StringComparison.Ordinal);
                string body = card[start..(end < 0 ? card.Length : end)];
                string detail = TestFailureMarkup.Text(body);
                // A plain Passed or Failed is the status badge itself; any other outcome shows beside it.
                string? outcome = attempt.Outcome is "Passed" or "Failed" ? null : attempt.Outcome;
                foreach (string? value in new[] { outcome, attempt.ComputerName, attempt.RunBy?.DisplayName,
                    attempt.RunBy?.UniqueName, attempt.FailureType, attempt.ResolutionState, attempt.Comment })
                    if (value is not null) Assert.Contains(value, detail, StringComparison.Ordinal);
                // A message or trace repeated from an earlier attempt is referenced instead of repeated.
                foreach (string? value in new[] { attempt.ErrorMessage, attempt.StackTrace })
                    if (value is not null) Assert.True(detail.Contains(value, StringComparison.Ordinal) || body.Contains("class=\"same-as\"", StringComparison.Ordinal));
                if (attempt.Duration.HasValue) Assert.Contains(attempt.Duration.Value.TotalSeconds.ToString("#,0.###", model.Culture), detail, StringComparison.Ordinal);
                if (attempt.StartedDate.HasValue) Assert.Contains(attempt.StartedDate.Value.ToString("g", model.Culture), detail, StringComparison.Ordinal);
                if (attempt.CompletedDate.HasValue) Assert.Contains(attempt.CompletedDate.Value.ToString("g", model.Culture), detail, StringComparison.Ordinal);
                foreach (AdoTestSubResult sub in attempt.SubResults) AssertSubResult(sub, detail);
                foreach (var iteration in attempt.Iterations)
                {
                    Assert.Contains("data-iteration=\"" + iteration.Id.ToString(CultureInfo.InvariantCulture) + "\"", body, StringComparison.Ordinal);
                    Assert.Contains(iteration.ErrorMessage!, detail, StringComparison.Ordinal);
                    foreach (var parameter in iteration.Parameters)
                    { Assert.Contains(parameter.Name, detail, StringComparison.Ordinal); Assert.Contains(parameter.Value!, detail, StringComparison.Ordinal); }
                    foreach (var action in iteration.ActionResults)
                    foreach (string? value in new[] { action.ActionPath, action.StepIdentifier, action.Outcome, action.ErrorMessage })
                        if (value is not null) Assert.Contains(value, detail, StringComparison.Ordinal);
                }
                foreach (var attachment in attempt.Attachments)
                {
                    Assert.Contains("id=\"" + anchor + "-a" + attempt.Number.ToString(CultureInfo.InvariantCulture) + "-att" + attachment.Id.ToString(CultureInfo.InvariantCulture) + "\"", body, StringComparison.Ordinal);
                    Assert.Contains(attachment.FileName, detail, StringComparison.Ordinal);
                    Assert.Contains(attachment.Size!.Value.ToString("N0", model.Culture), detail, StringComparison.Ordinal);
                    Assert.Contains("runId=" + attachment.RunId.ToString(CultureInfo.InvariantCulture) + "&amp;resultId=" + attachment.ResultId.ToString(CultureInfo.InvariantCulture), body, StringComparison.Ordinal);
                }
                foreach ((string key, object? value) in attempt.CustomFields.Concat(attempt.AdditionalFields))
                {
                    Assert.Contains(key, detail, StringComparison.Ordinal);
                    if (value is not null) Assert.Contains(value is IFormattable f ? f.ToString(null, model.Culture) : value.ToString()!, detail, StringComparison.Ordinal);
                }
            }
        }
        foreach (var summary in model.History)
        {
            string row = Section(html, "<tr data-build-id=\"" + summary.BuildId.ToString(CultureInfo.InvariantCulture) + "\">", "</tr>");
            foreach (int count in new[] { summary.Passed, summary.Failed, summary.Flaky, summary.Other })
                Assert.Contains("<td>" + count.ToString(model.Culture) + "</td>", row, StringComparison.Ordinal);
        }
        foreach (var diagnostic in model.Diagnostics)
        {
            Assert.Contains("data-diagnostic=\"" + diagnostic.Code + "\"", html, StringComparison.Ordinal);
            Assert.Contains(SinkEncoding.Attribute(diagnostic.Message), html, StringComparison.Ordinal);
        }
        Assert.DoesNotContain(TestFailureReportFixture.Untrusted.AbsoluteUri, html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"" + TestFailureReportFixture.Hostile, html, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyFailureSetAndAbsentOptionalFieldsRenderWithoutDeadContent()
    {
        var source = TestFailureReportFixture.Set("failed");
        var empty = new AdoBuildTestFailureSet { Build = source.Build, Summary = source.Summary, CollectionUri = source.CollectionUri, RetrievedAt = source.RetrievedAt };
        var model = TestFailureReportModelBuilder.Build(empty, TestFailureReportFixture.Options("fr-CA"));
        string html = TestFailureReportFixture.Render(model);
        TestFailureReportValidator.Validate(new StringReader(html), model);
        Assert.Contains(model.Labels["NoFailures"], html, StringComparison.Ordinal);
        Assert.DoesNotContain("<article", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"diagnostics\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<dd></dd>", html, StringComparison.Ordinal);
    }

    private static void AssertSubResult(AdoTestSubResult sub, string text)
    {
        foreach (string? value in new[] { sub.DisplayName, sub.Outcome, sub.ErrorMessage, sub.StackTrace, sub.Comment, sub.ComputerName, sub.ResultGroupType })
            if (value is not null) Assert.Contains(value, text, StringComparison.Ordinal);
        foreach (AdoTestSubResult child in sub.SubResults) AssertSubResult(child, text);
    }

    private static string Section(string html, string opening, string closing)
    {
        int start = html.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, opening);
        int end = html.IndexOf(closing, start, StringComparison.Ordinal);
        Assert.True(end >= start);
        return html[start..(end + closing.Length)];
    }
}
