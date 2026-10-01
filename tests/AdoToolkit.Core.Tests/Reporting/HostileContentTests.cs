using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.Html;
using AdoToolkit.Core.Reporting.TestFailures;
using AdoToolkit.Core.TestManagement;
using AdoToolkit.Core.Tests.Reporting.TestFailures;

namespace AdoToolkit.Core.Tests.Reporting;

[Trait("Acceptance", "S2-2")]
public sealed class HostileContentTests
{
    [Theory]
    [InlineData(ReportFormat.Html, false)]
    [InlineData(ReportFormat.Html, true)]
    [InlineData(ReportFormat.Markdown, false)]
    public void Fixture26PassesThroughParserModelAndSink(ReportFormat format, bool withSource)
    {
        string xml = File.ReadAllText(Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", "Steps", "26-hostile.xml"));
        StepNode node = Assert.Single(StepsXmlParser.Parse(xml, 10, 3, CultureInfo.GetCultureInfo("en-US")).Nodes);
        AdoTestCase testCase = new()
        {
            Id = 10, Rev = 3, CollectionUri = ReportFixture.Collection, TeamProject = ReportFixture.Project,
            WorkItemType = "Test Case", State = "Ready", WebUrl = ReportFixture.Untrusted,
            Title = "\"><script>alert(1)</script>",
            // With its source the HTML report renders the step from the markup, not from the plain text.
            Steps = [new() { Number = "1", Sequence = 1, Kind = AdoTestStepKind.Action, Action = node.Action, ExpectedResult = node.ExpectedResult,
                ActionSource = withSource ? node.ActionSource : null, ExpectedResultSource = withSource ? node.ExpectedResultSource : null }],
        };
        ReportDocumentModel model = ReportModelBuilder.Build(testCase, new AdoConnection { CollectionUri = ReportFixture.Collection }, ReportFixture.Options());
        string output = Markup(GoldenReportTests.Render(model, format), format);
        Assert.Equal(withSource, output.Contains("<div class=\"rich-text\"><p>Safe label<span class=\"rich-image\">[image]</span></p></div>", StringComparison.Ordinal));
        Assert.Contains("Safe label", output, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style", output.Replace("<style>\n", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick=", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror=", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hidden{color", output, StringComparison.Ordinal);
        Assert.DoesNotContain("images.example.test", output, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ReportFormat.Html)]
    [InlineData(ReportFormat.Markdown)]
    public void HostileTitlesParametersAndStepTextCannotCreateMarkup(ReportFormat format)
    {
        string rendered = GoldenReportTests.Render(ReportFixture.Model("parameterized"), format);
        string output = Markup(rendered, format);
        Assert.DoesNotContain("<script", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;title&gt;", output, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", output, StringComparison.Ordinal);
        if (format != ReportFormat.Html) return;
        // Defense in depth: nothing loads, and the only script that runs is the static asset, by its hash.
        string script = Assert.Single(TestFailureMarkup.Scripts().Matches(rendered)).Groups["body"].Value;
        Assert.Equal(TestFailureAssets.Read(HtmlTestCaseRenderer.ScriptAsset), script);
        Assert.Equal("default-src 'none'; script-src 'sha256-" + ContentSecurityPolicy.Hash(Encoding.UTF8.GetBytes(script))
            + "'; style-src 'unsafe-inline'; img-src 'self' data:; base-uri 'none'; form-action 'none'", TestFailureMarkup.Policy(rendered));
    }

    [Fact]
    public void AttributesAndMarkdownSyntaxCannotBreakOut()
    {
        Assert.Equal("&quot;&gt;&lt;img onerror=&#x27;x&#x27;&gt;&amp;", SinkEncoding.Attribute("\"><img onerror='x'>&"));
        Assert.Equal("\\\\\\`\\*\\_\\[\\]\\#\\|\n\\- a\n\\+ b\n1\\. c\n&lt;x&gt; ’ « »", SinkEncoding.Markdown("\\`*_[]#|\n- a\n+ b\n1. c\n<x> ’ « »"));
        Assert.Equal("\n\nstart\n", SinkEncoding.Markdown("\n\nstart\n"));
        Assert.Equal("&amp;lt;script&amp;gt;", SinkEncoding.Markdown("&lt;script&gt;"));
        Assert.Equal("a<br>b<br>c", SinkEncoding.Html("a\r\nb\rc"));
    }

    // The report without its own script element: what content could have added.
    private static string Markup(string output, ReportFormat format) => format == ReportFormat.Html ? TestFailureMarkup.WithoutScripts(output) : output;
}
