using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Reporting.Highlighting;

public static class ErrorMessageLexer
{
    private static readonly Regex Parts = new(
        "(?<type>^(?:[\\p{L}_][\\p{L}\\p{N}_.+`]*)?(?:Exception|Error)\\b)|(?<string>\"(?:[^\"\\\\]|\\\\.)*\"|'(?:[^'\\\\]|\\\\.)*')|(?<keyword>\\b(?:Expected|Actual|But was|Attendu|Réel|Obtenu|Mais était|Mais a été)\\b)|(?<number>[+-]?[0-9]+(?:[.,][0-9]+)?(?:[eE][+-]?[0-9]+)?)|(?<punct>[<>])",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static IReadOnlyList<CodeToken> Lex(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<CodeToken> result = [];
        foreach (CodeToken token in UrlDetector.Lex(text))
        {
            if (token.Kind == CodeTokenKind.Url) result.Add(token);
            else Tokenize.Matches(result, token.Text, Parts, match =>
                match.Groups["type"].Success ? CodeTokenKind.Type :
                match.Groups["string"].Success ? CodeTokenKind.QuotedString :
                match.Groups["keyword"].Success ? CodeTokenKind.Keyword :
                match.Groups["number"].Success ? CodeTokenKind.Number : CodeTokenKind.Punctuation);
        }
        return result.AsReadOnly();
    }
}
