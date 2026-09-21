using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.TestManagement;
using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Tests.Reporting;

[Trait("Acceptance", "S2-6")]
public sealed class RenderedHeaderAndLinkTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongTrailingUrlPunctuationUsesBoundedAllocations(bool markdown)
    {
        Func<string, string> render = markdown ? ContentLinks.Markdown : ContentLinks.Html;
        const string url = "https://example.test/path(a)";
        string suffix = new(')', 4000);
        string input = url + suffix;
        string expected = render(url) + suffix;
        _ = render(input[..100]); // Warm the regex and encoder before measuring this thread.
        long before = GC.GetAllocatedBytesForCurrentThread();
        string output = render(input);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(expected, output);
        Assert.InRange(allocated, 0, 1_000_000);
    }

    [Theory]
    [InlineData("direct", "en-US")]
    [InlineData("parameterized", "fr-CA")]
    [InlineData("partial", "en-US")]
    public void SectionNavigationHasUniqueRealTargetsAndOmitsUnavailableSections(string variant, string culture)
    {
        string html = GoldenReportTests.Render(ReportFixture.Model(variant, culture), ReportFormat.Html);
        string[] identifiers = Regex.Matches(html, "\\bid=\"([^\"]+)\"", RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(identifiers.Length, identifiers.Distinct(StringComparer.Ordinal).Count());
        string[] targets = Regex.Matches(html, "href=\"#([^\"]+)\"", RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.NotEmpty(targets);
        Assert.All(targets, target => Assert.Contains(target, identifiers));
        Assert.Equal(variant == "parameterized", targets.Contains("report-1-parameters", StringComparer.Ordinal));
        Assert.Equal(variant == "partial", targets.Contains("report-1-diagnostics", StringComparer.Ordinal));
        Assert.Contains(culture == "fr-CA" ? "aria-label=\"Sections du rapport\"" : "aria-label=\"Report sections\"", html, StringComparison.Ordinal);
        if (variant == "parameterized") Assert.Contains("role=\"region\" tabindex=\"0\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Fixture29LinksOnlySafeSchemesAfterParsing()
    {
        string xml = File.ReadAllText(Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", "Steps", "29-urls.xml"));
        StepNode node = Assert.Single(StepsXmlParser.Parse(xml, 10, 3, CultureInfo.GetCultureInfo("en-US")).Nodes);
        string output = ContentLinks.Html(node.Action);
        Assert.Contains("href=\"http://docs.example.test/a\"", output, StringComparison.Ordinal);
        Assert.Contains("href=\"https://docs.example.test/b\"", output, StringComparison.Ordinal);
        Assert.Contains("href=\"mailto:sample@example.test\"", output, StringComparison.Ordinal);
        foreach (string scheme in new[] { "javascript:", "data:", "file:" })
            Assert.DoesNotContain("href=\"" + scheme, output, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ReportFormat.Html)]
    [InlineData(ReportFormat.Markdown)]
    public void MetadataAndEveryConstructedLinkAreRendered(ReportFormat format)
    {
        ReportDocumentModel document = ReportFixture.Model("review");
        TestCaseReportModel model = document.Cases[0];
        string output = GoldenReportTests.Render(document, format);
        Func<string, string> encode = format == ReportFormat.Html ? SinkEncoding.Html : SinkEncoding.Markdown;
        Assert.Contains(encode(model.Project), output, StringComparison.Ordinal);
        Assert.Contains(encode(model.CollectionUri.AbsoluteUri), output, StringComparison.Ordinal);
        Assert.Contains("Fictional Person", output, StringComparison.Ordinal);
        foreach (Uri url in new[] { model.WebUrl, model.Suite!.PlanWebUrl!, model.Suite.WebUrl!, model.Parameters.SharedParameterSets[0].WebUrl! }
            .Concat(model.Rows.Where(row => row.SharedStep is not null).Select(row => row.SharedStep!.WebUrl!)))
            Assert.Contains(format == ReportFormat.Html ? "href=\"" + SinkEncoding.Attribute(url.AbsoluteUri) + "\"" : "](" + url.AbsoluteUri + ")", output, StringComparison.Ordinal);
        Assert.DoesNotContain(ReportFixture.Untrusted.AbsoluteUri, output, StringComparison.Ordinal);
        string absent = GoldenReportTests.Render(ReportFixture.Model(), format);
        foreach (string key in new[] { "AssignedTo", "Priority", "AutomationStatus", "AreaPath", "IterationPath", "TestPlan", "TestSuite", "ChangedBy" })
            Assert.DoesNotContain(encode(model.Labels[key]), absent, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyAllowedAbsoluteContentSchemesBecomeLinks()
    {
        const string text = "http://plain.example.test/a https://safe.example.test/a(b)?x=1&y=2 mailto:qa@example.test javascript:alert(1) data:text/html,evil file:///C:/file";
        string html = ContentLinks.Html(text);
        Assert.Equal(3, html.Split("<a rel=\"noreferrer\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("href=\"https://safe.example.test/a(b)?x=1&amp;y=2\"", html, StringComparison.Ordinal);
        foreach (string scheme in new[] { "javascript:", "data:", "file:" })
        {
            Assert.Contains(scheme, html, StringComparison.Ordinal);
            Assert.DoesNotContain("href=\"" + scheme, html, StringComparison.OrdinalIgnoreCase);
        }
        string markdown = ContentLinks.Markdown(text);
        Assert.Contains("](https://safe.example.test/a%28b%29?x=1&y=2)", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("](javascript:", markdown, StringComparison.OrdinalIgnoreCase);
    }
}
