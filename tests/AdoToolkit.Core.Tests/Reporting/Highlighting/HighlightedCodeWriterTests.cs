using System.Net;
using System.Text.RegularExpressions;
using AdoToolkit.Core.Reporting;
using AdoToolkit.Core.Reporting.Highlighting;

namespace AdoToolkit.Core.Tests.Reporting.Highlighting;

[Trait("Acceptance", "S5-4")]
public sealed class HighlightedCodeWriterTests
{
    [Theory]
    [InlineData(CodeLanguage.StackTrace)]
    [InlineData(CodeLanguage.ErrorMessage)]
    [InlineData(CodeLanguage.Json)]
    public void HostileContentIsEncodedAndPreformattedTextSurvives(CodeLanguage language)
    {
        string input = LexerFixtures.Read("TestRuns/hostile-text.txt");
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        Assert.Null(HighlightedCodeWriter.Write(writer, input, language, CultureInfo.GetCultureInfo("fr-CA")));
        string html = writer.ToString();
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<pre><code class=\"lang-", html, StringComparison.Ordinal);
        string code = Regex.Match(html, "<code[^>]*>([\\s\\S]*)</code>").Groups[1].Value;
        Assert.Equal(input, WebUtility.HtmlDecode(Regex.Replace(code, "<[^>]*>", "")));
        Assert.Contains(" ", html, StringComparison.Ordinal);
        Assert.Contains(" ", html, StringComparison.Ordinal);
        if (language != CodeLanguage.Json)
        {
            Assert.Contains("<a rel=\"noreferrer\" href=\"https://repo.example.test/path?q=%22&amp;x=%3Cscript%3E\">", html, StringComparison.Ordinal);
            Assert.Contains("</a></span>", html, StringComparison.Ordinal);
        }
        string[] classes = ["tok-keyword", "tok-type", "tok-method", "tok-namespace", "tok-parameter", "tok-string", "tok-number",
            "tok-path", "tok-line", "tok-property", "tok-literal", "tok-punct", "tok-url", "tok-plain", "first-user-frame", "framework-frame"];
        foreach (Match match in Regex.Matches(code, "<span class=\"([^\"]+)\">")) Assert.Contains(match.Groups[1].Value, classes);
    }

    [Theory]
    [InlineData("en-US", "Error message")]
    [InlineData("fr-CA", "Message d’erreur")]
    public void GeneratedTwoMiBMessageIsTruncatedAtLineBoundaryWithInfoDiagnostic(string cultureName, string label)
    {
        string input = "first\r\n" + new string('x', 2 * 1024 * 1024 - 7);
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        AdoDiagnostic diagnostic = Assert.IsType<AdoDiagnostic>(HighlightedCodeWriter.Write(writer, input, CodeLanguage.ErrorMessage, culture));
        Assert.Equal(DiagnosticCodes.TestTextTruncated, diagnostic.Code);
        Assert.Equal(AdoDiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Equal("first\r\n", HighlightedCodeWriter.DisplayHead(input));
        Assert.Contains(label, writer.ToString(), StringComparison.Ordinal);
        Assert.Contains(SinkEncoding.Attribute(diagnostic.Message), writer.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("xxxx", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayBoundaryDoesNotSplitCrLfOrSurrogatePair()
    {
        int limit = HighlightedCodeWriter.MaximumDisplayCharacters;
        Assert.Equal(limit, HighlightedCodeWriter.DisplayHead(new string('a', limit)).Length);
        Assert.Equal(limit, HighlightedCodeWriter.DisplayHead(new string('a', limit + 1)).Length);
        Assert.Equal(limit - 1, HighlightedCodeWriter.DisplayHead(new string('a', limit - 1) + "😀end").Length);
        Assert.Equal(limit - 1, HighlightedCodeWriter.DisplayHead(new string('a', limit - 1) + "\r\nend").Length);
        Assert.Equal(limit, HighlightedCodeWriter.DisplayHead(new string('a', limit - 2) + "\r\nend").Length);
        Assert.Equal("a\r", HighlightedCodeWriter.DisplayHead("a\r" + new string('x', limit)));
    }

    [Fact]
    public void FramesHaveContainerClassesAndExactlyOneFirstUserMarker()
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        HighlightedCodeWriter.Write(writer, LexerFixtures.Read("TestRuns/stack-english.txt"), CodeLanguage.StackTrace, CultureInfo.GetCultureInfo("en-US"));
        string html = writer.ToString();
        Assert.Single(Regex.Matches(html, "class=\"first-user-frame\""));
        Assert.Contains("class=\"framework-frame\"", html, StringComparison.Ordinal);
        Assert.Contains("List&lt;int&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<br>", html, StringComparison.Ordinal);
    }
}
