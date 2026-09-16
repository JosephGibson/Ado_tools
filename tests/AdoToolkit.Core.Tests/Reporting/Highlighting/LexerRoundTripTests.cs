using AdoToolkit.Core.Reporting.Highlighting;

namespace AdoToolkit.Core.Tests.Reporting.Highlighting;

[Trait("Acceptance", "S5-4")]
public sealed class LexerRoundTripTests
{
    [Fact]
    public void EveryFixtureAndEveryDecodedJsonStringRoundTripsThroughEveryLexer()
    {
        foreach (string text in LexerFixtures.EveryText()) RoundTrip(text);
    }

    [Fact]
    public void GeneratedLargeDeepTruncatedAndUnusualInputsRoundTrip()
    {
        string large = "first line\r\n" + new string('x', 2 * 1024 * 1024 - 12);
        string deep = new string('[', 65) + "0" + new string(']', 65);
        foreach (string text in new[] { "", "\r\n\n\r", "at broken(<\"'", "\0\ud800x\udfff", "é\r\n😀\n\tend\r", large, deep,
            HighlightedCodeWriter.DisplayHead(large), new string('x', 1024 * 1024), "{\"a\":1,}", "// comment\n{}" }) RoundTrip(text);
    }

    private static void RoundTrip(string text)
    {
        foreach (Func<string, IReadOnlyList<CodeToken>> lexer in new Func<string, IReadOnlyList<CodeToken>>[]
            { StackTraceLexer.Lex, ErrorMessageLexer.Lex, JsonLexer.Lex, UrlDetector.Lex })
        {
            IReadOnlyList<CodeToken> tokens = lexer(text);
            Assert.Equal(text, string.Concat(tokens.Select(static token => token.Text)));
            Assert.All(tokens, static token => Assert.NotEmpty(token.Text));
            Assert.Equal(tokens.Select(static token => (token.Kind, token.Text, token.IsFrameworkFrame, token.IsFirstUserFrame)),
                lexer(text).Select(static token => (token.Kind, token.Text, token.IsFrameworkFrame, token.IsFirstUserFrame)));
        }
    }

    [Fact]
    public void DeterministicMixedContentNeverLosesCharactersOrThrows()
    {
        Random random = new(52);
        string[] pieces = ["at ", "à ", " in ", " dans ", ":line 2", ":ligne 3", "\r", "\n", "\t", " ", "(", ")", "[", "]", "<", ">", "'", "\"",
            "http://", "https://", "javascript:", "data:", "file:", "repo.example.test", "\\", "&", ":", ";", "0", "-2.5", "null", "true", "é", "😀", "\ud800", "\udfff"];
        for (int i = 0; i < 200; i++)
            RoundTrip(string.Concat(Enumerable.Range(0, random.Next(1, 50)).Select(_ => pieces[random.Next(pieces.Length)])));
    }
}
