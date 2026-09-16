using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Reporting.Highlighting;

public static class UrlDetector
{
    private static readonly Regex Candidates = new("[A-Za-z][A-Za-z0-9+.-]*:[^\\s<>\"'`]+", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static IReadOnlyList<CodeToken> Lex(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<CodeToken> tokens = [];
        int start = 0;
        foreach (Match match in Candidates.Matches(text))
        {
            // Do not link an embedded scheme in another URI or identifier.
            if (match.Index > 0 && (char.IsLetterOrDigit(text[match.Index - 1]) || ":/_-".Contains(text[match.Index - 1], StringComparison.Ordinal))) continue;
            int end = match.Index + match.Length;
            int parentheses = 0, brackets = 0;
            foreach (char c in match.Value)
            {
                if (c == '(') parentheses++;
                if (c == ')') parentheses--;
                if (c == '[') brackets++;
                if (c == ']') brackets--;
            }
            while (end > match.Index)
            {
                char c = text[end - 1];
                if (".,;:!?".Contains(c, StringComparison.Ordinal)) end--;
                else if (c == ')' && parentheses < 0) { end--; parentheses++; }
                else if (c == ']' && brackets < 0) { end--; brackets++; }
                else break;
            }
            string candidate = text[match.Index..end];
            if (!TryGetUri(candidate, out _)) continue;
            Tokenize.Add(tokens, CodeTokenKind.Plain, text[start..match.Index]);
            Tokenize.Add(tokens, CodeTokenKind.Url, candidate);
            start = end;
        }
        Tokenize.Add(tokens, CodeTokenKind.Plain, text[start..]);
        return tokens.AsReadOnly();
    }

    internal static bool TryGetUri(string text, out Uri? uri) =>
        Uri.TryCreate(text, UriKind.Absolute, out uri) && uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0;
}
