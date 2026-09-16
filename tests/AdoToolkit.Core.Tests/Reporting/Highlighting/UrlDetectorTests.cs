using AdoToolkit.Core.Reporting.Highlighting;

namespace AdoToolkit.Core.Tests.Reporting.Highlighting;

[Trait("Acceptance", "S5-4")]
public sealed class UrlDetectorTests
{
    [Theory]
    [InlineData("https://repo.example.test/a?x=1&y=2.,;:!?", "https://repo.example.test/a?x=1&y=2")]
    [InlineData("(https://repo.example.test/a(b)).", "https://repo.example.test/a(b)")]
    [InlineData("[https://repo.example.test/a[b]].", "https://repo.example.test/a[b]")]
    [InlineData("https://repo.example.test/a)]?!", "https://repo.example.test/a")]
    [InlineData("https://[::1]/a.", "https://[::1]/a")]
    [InlineData("HTTPS://repo.example.test/é", "HTTPS://repo.example.test/é")]
    public void TrimsOnlyExcludedPunctuation(string text, string expected) =>
        Assert.Equal(expected, Assert.Single(UrlDetector.Lex(text), static token => token.Kind == CodeTokenKind.Url).Text);

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("\"")]
    [InlineData("'")]
    [InlineData("`")]
    public void AllTerminatorsEndTheUrl(string delimiter)
    {
        const string url = "https://repo.example.test/a";
        Assert.Equal(url, Assert.Single(UrlDetector.Lex(url + delimiter + "rest"), static token => token.Kind == CodeTokenKind.Url).Text);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,https://repo.example.test/a")]
    [InlineData("file:///C:/test.txt")]
    [InlineData("mailto:person@example.test")]
    [InlineData("https://person@repo.example.test/a")]
    [InlineData("https://")]
    [InlineData("http://[bad]/a")]
    [InlineData("javascript:https://repo.example.test")]
    [InlineData("abchttps://repo.example.test")]
    public void InvalidOrUnsafeSchemesStayText(string text) =>
        Assert.DoesNotContain(UrlDetector.Lex(text), static token => token.Kind == CodeTokenKind.Url);
}
