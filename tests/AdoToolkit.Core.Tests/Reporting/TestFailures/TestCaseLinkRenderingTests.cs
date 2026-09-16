using AdoToolkit.Core.Reporting;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

[Trait("Acceptance", "S5-8")]
public sealed class TestCaseLinkRenderingTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-CA")]
    public void ResolvedAndUnresolvedReferencesKeepOnlyTheCorrectTitleAndState(string culture)
    {
        string resolved = TestFailureReportFixture.Render("failed", culture);
        Assert.Contains("/_workitems/edit/901\">#901 Valider la commande", resolved, StringComparison.Ordinal);
        Assert.Contains("<span>Ready</span>", resolved, StringComparison.Ordinal);
        string unresolved = TestFailureReportFixture.Render("partial", culture);
        Assert.Contains("/_workitems/edit/902\">#902 <span", unresolved, StringComparison.Ordinal);
        Assert.DoesNotContain("#902 Valider", unresolved, StringComparison.Ordinal);
        Assert.DoesNotContain("<span>Ready</span>", unresolved, StringComparison.Ordinal);
        Assert.Contains("data-diagnostic=\"UnresolvedTestCase\"", unresolved, StringComparison.Ordinal);
        Assert.Contains(SinkEncoding.Attribute(TestFailureReportFixture.Model("partial", culture).Diagnostics[1].Message), unresolved, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidReferenceHasNoTestCaseLinkAndKeepsItsDiagnostic()
    {
        string html = TestFailureReportFixture.Render("hostile");
        int start = html.IndexOf("<article class=\"card failure-card\" id=\"f-1\"", StringComparison.Ordinal);
        string card = html[start..html.IndexOf("</article>", start, StringComparison.Ordinal)];
        // Null storage sorts before the hostile fixture's assembly, so InvalidReference is f-1.
        Assert.Contains("InvalidReference", card, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"test-case-link\"", card, StringComparison.Ordinal);
        Assert.Contains("data-diagnostic=\"InvalidTestCaseReference\"", html, StringComparison.Ordinal);
    }
}
