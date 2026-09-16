using AdoToolkit.Core.Reporting;

namespace AdoToolkit.Core.Tests.Reporting;

[Trait("Acceptance", "S2-7")]
public sealed class FrenchTypographyTests
{
    [Theory]
    [InlineData(" : ; ! ? « été » 1 234")]
    [InlineData(" : ; ! ? « été » 1 234")]
    public void TypographyIsLiteralAtBothSinks(string text)
    {
        Assert.Equal(text, SinkEncoding.Html(text));
        Assert.Equal(text, SinkEncoding.Markdown(text));
    }

    [Theory]
    [InlineData(ReportFormat.Html)]
    [InlineData(ReportFormat.Markdown)]
    public void RenderedFrenchContentPreservesSpacesQuotesAndDecomposedAccents(ReportFormat format)
    {
        string output = GoldenReportTests.Render(ReportFixture.Model("french", "fr-CA"), format);
        Assert.Contains("« L’été : 1 234 ! »", output, StringComparison.Ordinal);
        Assert.Contains("é 日本語", output, StringComparison.Ordinal);
        Assert.Contains(format == ReportFormat.Html ? "&#x1F600;" : "😀", output, StringComparison.Ordinal);
        Assert.DoesNotContain("&#xA0;", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("&#x202F;", output, StringComparison.OrdinalIgnoreCase);
    }
}
