using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.TestManagement;

namespace AdoToolkit.Core.Tests.Reporting;

[Trait("Acceptance", "S2-2")]
public sealed class HostileContentTests
{
    [Theory]
    [InlineData(ReportFormat.Html)]
    [InlineData(ReportFormat.Markdown)]
    public void Fixture26PassesThroughParserModelAndSink(ReportFormat format)
    {
        string xml = File.ReadAllText(Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", "Steps", "26-hostile.xml"));
        StepNode node = Assert.Single(StepsXmlParser.Parse(xml, 10, 3, CultureInfo.GetCultureInfo("en-US")).Nodes);
        AdoTestCase testCase = new()
        {
            Id = 10, Rev = 3, CollectionUri = ReportFixture.Collection, TeamProject = ReportFixture.Project,
            WorkItemType = "Test Case", State = "Ready", WebUrl = ReportFixture.Untrusted,
            Title = "\"><script>alert(1)</script>",
            Steps = [new() { Number = "1", Sequence = 1, Kind = AdoTestStepKind.Action, Action = node.Action, ExpectedResult = node.ExpectedResult }],
        };
        ReportDocumentModel model = ReportModelBuilder.Build(testCase, new AdoConnection { CollectionUri = ReportFixture.Collection }, ReportFixture.Options());
        string output = GoldenReportTests.Render(model, format);
        Assert.Contains("Safe label", output, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick=", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror=", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("images.example.test", output, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ReportFormat.Html)]
    [InlineData(ReportFormat.Markdown)]
    public void HostileTitlesParametersAndStepTextCannotCreateMarkup(ReportFormat format)
    {
        string output = GoldenReportTests.Render(ReportFixture.Model("parameterized"), format);
        Assert.DoesNotContain("<script", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;title&gt;", output, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", output, StringComparison.Ordinal);
        if (format == ReportFormat.Html)
            Assert.Contains("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data:\">", output, StringComparison.Ordinal);
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
}
