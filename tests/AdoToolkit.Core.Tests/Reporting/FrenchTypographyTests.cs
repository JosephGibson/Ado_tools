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
}
