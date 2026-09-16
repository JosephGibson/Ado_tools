using AdoToolkit.Core.Reporting.Highlighting;

namespace AdoToolkit.Core.Tests.Reporting.Highlighting;

[Trait("Acceptance", "S5-4")]
public sealed class JsonLexerTests
{
    [Fact]
    public void FormattingPrecedesLexingAndBothOriginalAndFormattedTextRoundTrip()
    {
        string original = LexerFixtures.Read("Attachments/valid.json");
        Assert.True(JsonLexer.TryFormat(original, 262144, out string pretty));
        Assert.Contains("\n  \"title\":", pretty, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', pretty);
        Assert.Contains("\n    \"enabled\": true", pretty, StringComparison.Ordinal);
        Assert.NotEqual(original, pretty);
        Assert.Contains("<script>", pretty, StringComparison.Ordinal);
        foreach (string text in new[] { original, pretty })
        {
            IReadOnlyList<CodeToken> tokens = JsonLexer.Lex(text);
            Assert.Equal(text, string.Concat(tokens.Select(static token => token.Text)));
            Assert.Contains(tokens, static token => token.Kind == CodeTokenKind.Property && token.Text == "\"title\"");
            Assert.Contains(tokens, static token => token.Kind == CodeTokenKind.Number);
            Assert.Contains(tokens, static token => token.Kind == CodeTokenKind.QuotedString);
            foreach (string literal in new[] { "true", "false", "null" })
                Assert.Contains(tokens, token => token.Kind == CodeTokenKind.Literal && token.Text == literal);
        }
    }

    [Theory]
    [InlineData("{\"x\":1,}")]
    [InlineData("{/* no comments */\"x\":1}")]
    [InlineData("{\"x\": NaN}")]
    [InlineData("\"unterminated")]
    public void InvalidContentDegradesToPlain(string text)
    {
        Assert.False(JsonLexer.TryFormat(text, 262144, out string unchanged));
        Assert.Equal(text, unchanged);
        Assert.Equal(CodeTokenKind.Plain, Assert.Single(JsonLexer.Lex(text)).Kind);
    }

    [Fact]
    public void PrettyJsonPreservesFrenchValuesAndWriterEncodesEveryToken()
    {
        const string text = "{\"message\":\"« Échec : <script> »\"}";
        Assert.True(JsonLexer.TryFormat(text, 262144, out string pretty));
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(pretty);
        Assert.Equal("« Échec : <script> »", document.RootElement.GetProperty("message").GetString());
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        HighlightedCodeWriter.Write(writer, text, CodeLanguage.Json, CultureInfo.GetCultureInfo("fr-CA"));
        Assert.DoesNotContain("<script>", writer.ToString(), StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", writer.ToString(), StringComparison.Ordinal);
        Assert.Contains(" ", writer.ToString(), StringComparison.Ordinal);
        Assert.Contains(" ", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedUnicodeIsGeneratedAtRuntimeAndFallsBackToPlain()
    {
        // Attribute metadata transcodes strings, so an InlineData argument cannot carry a lone surrogate.
        string text = new(['"', (char)0xd800, '"']);
        Assert.False(JsonLexer.TryFormat(text, 262144, out string unchanged));
        Assert.Equal(text, unchanged);
        Assert.Equal(text, Assert.Single(JsonLexer.Lex(text)).Text);
    }

    [Fact]
    public void DepthAndUtf8ByteLimitsAreStrictAndGeneratedInTests()
    {
        string depth64 = new string('[', 64) + "0" + new string(']', 64);
        string depth65 = new string('[', 65) + "0" + new string(']', 65);
        Assert.True(JsonLexer.TryFormat(depth64, 262144, out _));
        Assert.False(JsonLexer.TryFormat(depth65, 262144, out _));
        Assert.Equal(CodeTokenKind.Plain, Assert.Single(JsonLexer.Lex(depth65)).Kind);
        string aboveLimit = "\"" + new string('é', 131072) + "\"";
        Assert.False(JsonLexer.TryFormat(aboveLimit, 262144, out _));
        const string exact = "\"é\"";
        Assert.True(JsonLexer.TryFormat(exact, 4, out _));
        Assert.False(JsonLexer.TryFormat(exact, 3, out _));
        Assert.False(JsonLexer.TryFormat(LexerFixtures.Read("Attachments/malformed.json.txt"), 262144, out _));
    }
}
