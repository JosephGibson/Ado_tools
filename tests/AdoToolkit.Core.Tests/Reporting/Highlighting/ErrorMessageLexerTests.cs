using AdoToolkit.Core.Reporting.Highlighting;

namespace AdoToolkit.Core.Tests.Reporting.Highlighting;

[Trait("Acceptance", "S5-4")]
public sealed class ErrorMessageLexerTests
{
    [Fact]
    public void AssertionFormsTypesStringsAndNumbersAreRecognized()
    {
        IReadOnlyList<CodeToken> tokens = ErrorMessageLexer.Lex(LexerFixtures.Read("TestRuns/messages.txt"));
        Assert.Contains(tokens, static token => token.Kind == CodeTokenKind.Type && token.Text == "System.InvalidOperationException");
        foreach (string marker in new[] { "Expected", "Actual", "But was", "Attendu", "Réel", "Obtenu", "Mais était", "Mais a été" })
            Assert.Contains(tokens, token => token.Kind == CodeTokenKind.Keyword && token.Text == marker);
        foreach (string number in new[] { "1", "2", "1.25", "-2.5e+3", "1,25", "2,50" })
            Assert.Contains(tokens, token => token.Kind == CodeTokenKind.Number && token.Text == number);
        Assert.Contains(tokens, static token => token.Kind == CodeTokenKind.QuotedString && token.Text == "\"Synthetic\"");
        Assert.Contains(tokens, static token => token.Kind == CodeTokenKind.Punctuation && token.Text == "<");
        Assert.Contains(tokens, static token => token.Kind == CodeTokenKind.Punctuation && token.Text == ">");
    }

    [Fact]
    public void UrlInsideQuotesRemainsOneClickableToken()
    {
        IReadOnlyList<CodeToken> tokens = ErrorMessageLexer.Lex("Expected: \"https://repo.example.test/a?x=1&y=2\"");
        Assert.Equal("https://repo.example.test/a?x=1&y=2", Assert.Single(tokens, static token => token.Kind == CodeTokenKind.Url).Text);
    }

    [Theory]
    [InlineData("Exception")]
    [InlineData("Error")]
    [InlineData("System.Exception")]
    public void BareAndQualifiedExceptionHeadersAreTypesInBothLexers(string type)
    {
        Assert.Equal(type, Assert.Single(ErrorMessageLexer.Lex(type + ": failure"), static token => token.Kind == CodeTokenKind.Type).Text);
        Assert.Equal(type, Assert.Single(StackTraceLexer.Lex(type + ": failure"), static token => token.Kind == CodeTokenKind.Type).Text);
    }
}
