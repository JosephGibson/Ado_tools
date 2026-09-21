using AdoToolkit.Core.Reporting;

namespace AdoToolkit.Core.Tests.Reporting.TestFailures;

[Trait("Acceptance", "S5-8")]
public sealed class TestCaseLinkRenderingTests
{
    // The number shows in the overview row and in the card heading, both linked to the work item.
    [Theory]
    [InlineData("en-US", "Test Case")]
    [InlineData("fr-CA", "Cas de test")]
    public void ResolvedAndUnresolvedReferencesKeepOnlyTheCorrectTitleAndState(string culture, string label)
    {
        string resolved = TestFailureReportFixture.Render("failed", culture);
        string row = Section(resolved, "<tr data-index-for=\"f-1\">", "</tr>");
        Assert.Contains("/_workitems/edit/901\">#901 <span role=\"img\"", row, StringComparison.Ordinal);
        string heading = Section(resolved, "<article class=\"card failure-card\" id=\"f-1\"", "</header>");
        Assert.Contains("/_workitems/edit/901\">" + label + " #901 <span role=\"img\"", heading, StringComparison.Ordinal);
        Assert.Contains("<span class=\"test-case-title\">Valider la commande</span>", heading, StringComparison.Ordinal);
        Assert.Contains("<span class=\"test-case-state\">Ready</span>", heading, StringComparison.Ordinal);
        Assert.Contains("data-test-case=\"901\"", heading, StringComparison.Ordinal);
        string unresolved = TestFailureReportFixture.Render("partial", culture);
        Assert.Contains("/_workitems/edit/902\">#902 <span", Section(unresolved, "<tr data-index-for=\"f-1\">", "</tr>"), StringComparison.Ordinal);
        Assert.Contains("/_workitems/edit/902\">" + label + " #902 <span", unresolved, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"test-case-title\"", unresolved, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"test-case-state\"", unresolved, StringComparison.Ordinal);
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
        Assert.DoesNotContain("data-test-case", card, StringComparison.Ordinal);
        Assert.Contains("data-diagnostic=\"InvalidTestCaseReference\"", html, StringComparison.Ordinal);
    }

    private static string Section(string html, string start, string end)
    {
        int index = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(index >= 0, start);
        return html[index..html.IndexOf(end, index, StringComparison.Ordinal)];
    }
}
