using AdoToolkit.Core.Reporting.Highlighting;

namespace AdoToolkit.Core.Tests.Reporting.Highlighting;

[Trait("Acceptance", "S5-4")]
public sealed class StackTraceLexerTests
{
    [Theory]
    [InlineData("stack-english.txt", "at", "in", "line", "Demo.Tests.")]
    [InlineData("stack-french.txt", "à", "dans", "ligne", "Démo.Tests.")]
    public void FramesHeadersSeparatorsAndFirstUserFrameAreRecognized(string fixture, string frame, string path, string line, string ns)
    {
        IReadOnlyList<CodeToken> tokens = StackTraceLexer.Lex(LexerFixtures.Read("TestRuns/" + fixture));
        Has(tokens, CodeTokenKind.Keyword, frame);
        Has(tokens, CodeTokenKind.Keyword, " " + path + " ");
        Has(tokens, CodeTokenKind.Keyword, line);
        Has(tokens, CodeTokenKind.Line, "42");
        Has(tokens, CodeTokenKind.Namespace, ns);
        Has(tokens, CodeTokenKind.Type, "System.InvalidOperationException");
        Has(tokens, CodeTokenKind.Method, "MoveNext");
        Assert.Contains(tokens, static token => token.Kind == CodeTokenKind.Path && token.Text.StartsWith("C:\\agent", StringComparison.Ordinal));
        Assert.Contains(tokens, static token => token.Kind == CodeTokenKind.Parameter && token.Text.Contains("List<int>", StringComparison.Ordinal));
        Assert.Contains(tokens, static token => token.Kind == CodeTokenKind.Type && token.Text.Contains("<List<int>>", StringComparison.Ordinal) ||
            token.Kind == CodeTokenKind.Type && token.Text.Contains("<System.Collections.Generic.List<int>>", StringComparison.Ordinal));
        Assert.Contains(tokens, static token => token.Kind == CodeTokenKind.Keyword && token.Text.Contains("---", StringComparison.Ordinal));
        Assert.Contains(tokens, static token => token.IsFrameworkFrame && token.Kind == CodeTokenKind.Method && token.Text == "ThrowArgumentException");
        Assert.Equal("MoveNext", Assert.Single(tokens, static token => token.IsFirstUserFrame && token.Kind == CodeTokenKind.Method).Text);
        Assert.DoesNotContain(tokens, static token => token.IsFirstUserFrame && token.IsFrameworkFrame);
    }

    [Theory]
    [InlineData("System.")]
    [InlineData("Microsoft.")]
    [InlineData("NUnit.")]
    [InlineData("Xunit.")]
    [InlineData("Microsoft.VisualStudio.TestPlatform.")]
    public void FrameworkPrefixAndFirstUserMarkerAreOrdinalAndPerInvocation(string prefix)
    {
        string text = $"   at {prefix}Runner.Go()\n   at Demo.First.Go()\n   at Demo.Second.Go()";
        IReadOnlyList<CodeToken> tokens = StackTraceLexer.Lex(text);
        Assert.Contains(tokens, static token => token.IsFrameworkFrame);
        Assert.Equal("First.", Assert.Single(tokens, static token => token.IsFirstUserFrame && token.Kind == CodeTokenKind.Type).Text);
        Assert.False(StackTraceLexer.Lex("at system.Runner.Go()")[0].IsFrameworkFrame);
    }

    [Theory]
    [InlineData("unknown <List<int>> text")]
    [InlineData("at incomplete(no close")]
    public void UnknownLinesStayPlain(string text) => Assert.Equal(CodeTokenKind.Plain, Assert.Single(StackTraceLexer.Lex(text)).Kind);

    private static void Has(IReadOnlyList<CodeToken> tokens, CodeTokenKind kind, string text) =>
        Assert.Contains(tokens, token => token.Kind == kind && token.Text == text);
}
