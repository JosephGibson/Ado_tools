using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Reporting.Highlighting;

internal static class Tokenize
{
    internal static void Add(List<CodeToken> tokens, CodeTokenKind kind, string text)
    {
        if (text.Length > 0) tokens.Add(new() { Kind = kind, Text = text });
    }

    internal static void WithUrls(List<CodeToken> tokens, CodeTokenKind kind, string text)
    {
        foreach (CodeToken token in UrlDetector.Lex(text)) Add(tokens, token.Kind == CodeTokenKind.Url ? token.Kind : kind, token.Text);
    }

    internal static void Matches(List<CodeToken> tokens, string text, Regex regex, Func<Match, CodeTokenKind> kind)
    {
        int start = 0;
        foreach (Match match in regex.Matches(text))
        {
            Add(tokens, CodeTokenKind.Plain, text[start..match.Index]);
            Add(tokens, kind(match), match.Value);
            start = match.Index + match.Length;
        }
        Add(tokens, CodeTokenKind.Plain, text[start..]);
    }
}
