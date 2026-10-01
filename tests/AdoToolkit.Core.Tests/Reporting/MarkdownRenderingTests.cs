using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.Reporting;

public sealed class MarkdownRenderingTests
{
    // A line of "=" under text is a setext heading, "~~~" opens a code fence that would swallow
    // every later step, and "~" pairs strike text through.
    [Fact]
    [Trait("Acceptance", "S2-2")]
    public void BlockSyntaxAtLineStartAndTildesAreEscaped()
    {
        Assert.Equal("Title\n\\===\n\\~\\~\\~\n  \\= indented\na = b \\~c\\~", SinkEncoding.Markdown("Title\n===\n~~~\n  = indented\na = b ~c~"));
        Assert.Equal("\\=", SinkEncoding.Markdown("="));
    }

    [Fact]
    [Trait("Acceptance", "S2-2")]
    public void StepTextCannotOpenACodeFenceOrASetextHeading()
    {
        string output = GoldenReportTests.Render(Model("Run the script\n~~~\nGet-Item\n===", "```\nDone\n---"), ReportFormat.Markdown);
        string[] lines = output.Split('\n');
        Assert.DoesNotContain(lines, static line => line.TrimStart().StartsWith("~~~", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, static line => line.TrimStart().StartsWith("```", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, static line => line.TrimStart().StartsWith('='));
        Assert.DoesNotContain(lines, static line => line.TrimStart().StartsWith('-'));
        // Both steps are still headings, so nothing was swallowed.
        Assert.Equal(2, lines.Count(static line => line.StartsWith("### ", StringComparison.Ordinal)));
    }

    // A Markdown viewer joins the lines of one paragraph. Each line of multi-line step text ends
    // with a break; a blank line stays a paragraph boundary.
    [Fact]
    public void MultiLineStepTextKeepsItsLineBreaks()
    {
        string output = GoldenReportTests.Render(Model("First line\nSecond line\n\nNew paragraph", "- one\n- two"), ReportFormat.Markdown);
        Assert.Contains("\n\nFirst line<br>\nSecond line\n\nNew paragraph\n\n", output, StringComparison.Ordinal);
        Assert.Contains("\n\n\\- one<br>\n\\- two\n\n", output, StringComparison.Ordinal);
        Assert.DoesNotContain("<br>\n\n", output, StringComparison.Ordinal);
        Assert.Equal(2, output.Split('\n').Count(static line => line.StartsWith("### ", StringComparison.Ordinal)));
    }

    private static ReportDocumentModel Model(string action, string expected) => ReportModelBuilder.Build(new AdoTestCase
    {
        Id = 10, Rev = 3, CollectionUri = ReportFixture.Collection, TeamProject = ReportFixture.Project,
        WorkItemType = "Test Case", State = "Ready", WebUrl = ReportFixture.Untrusted, Title = "Synthetic lines",
        Steps =
        [
            new() { Number = "1", Sequence = 1, Kind = AdoTestStepKind.Action, Action = action, ExpectedResult = expected },
            new() { Number = "2", Sequence = 2, Kind = AdoTestStepKind.Validate, Action = "Next", ExpectedResult = "Result" },
        ],
    }, new AdoConnection { CollectionUri = ReportFixture.Collection }, ReportFixture.Options());
}
