using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Reporting.Highlighting;

public static class StackTraceLexer
{
    private static readonly Regex Frame = new("^(?<space>\\s*)(?<keyword>at|à)(?<gap> +)(?<method>[^\\r\\n(]+)(?<open>\\()(?<parameters>[^\\r\\n)]*)(?<close>\\))(?<tail>[^\\r\\n]*)$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex Location = new("^(?<keyword> +(?:in|dans) +)(?<path>.+):(?<keyword2>line|ligne)(?<gap> +)(?<line>[0-9]+)(?<space> *)$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex Header = new("^(?<space>\\s*(?:---> )?)(?<type>(?:[\\p{L}_][\\p{L}\\p{N}_.+`]*)?(?:Exception|Error))(?<colon>:)(?<message>.*)$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly string[] FrameworkPrefixes = ["System.", "Microsoft.", "NUnit.", "Xunit.", "Microsoft.VisualStudio.TestPlatform."];

    public static IReadOnlyList<CodeToken> Lex(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<CodeToken> result = [];
        bool foundUser = false;
        int start = 0;
        while (start < text.Length)
        {
            int end = start;
            while (end < text.Length && text[end] is not ('\r' or '\n')) end++;
            string line = text[start..end];
            int next = end;
            if (next < text.Length && text[next] == '\r') next++;
            if (next < text.Length && text[next] == '\n') next++;
            List<CodeToken> tokens = [];
            Match frame = Frame.Match(line);
            bool framework = false, firstUser = false;
            if (frame.Success)
            {
                string method = frame.Groups["method"].Value;
                framework = FrameworkPrefixes.Any(prefix => method.StartsWith(prefix, StringComparison.Ordinal));
                firstUser = !framework && !foundUser;
                foundUser |= !framework;
                AddGroup(tokens, frame, "space", CodeTokenKind.Plain);
                AddGroup(tokens, frame, "keyword", CodeTokenKind.Keyword);
                AddGroup(tokens, frame, "gap", CodeTokenKind.Plain);
                Method(tokens, method);
                AddGroup(tokens, frame, "open", CodeTokenKind.Punctuation);
                AddGroup(tokens, frame, "parameters", CodeTokenKind.Parameter);
                AddGroup(tokens, frame, "close", CodeTokenKind.Punctuation);
                Match location = Location.Match(frame.Groups["tail"].Value);
                if (location.Success)
                {
                    AddGroup(tokens, location, "keyword", CodeTokenKind.Keyword);
                    AddGroup(tokens, location, "path", CodeTokenKind.Path);
                    Tokenize.Add(tokens, CodeTokenKind.Punctuation, ":");
                    AddGroup(tokens, location, "keyword2", CodeTokenKind.Keyword);
                    AddGroup(tokens, location, "gap", CodeTokenKind.Plain);
                    AddGroup(tokens, location, "line", CodeTokenKind.Line);
                    AddGroup(tokens, location, "space", CodeTokenKind.Plain);
                }
                else AddGroup(tokens, frame, "tail", CodeTokenKind.Plain);
            }
            else if (IsSeparator(line.Trim())) Tokenize.Add(tokens, CodeTokenKind.Keyword, line);
            else
            {
                Match header = Header.Match(line);
                if (header.Success)
                {
                    AddGroup(tokens, header, "space", CodeTokenKind.Plain);
                    AddGroup(tokens, header, "type", CodeTokenKind.Type);
                    AddGroup(tokens, header, "colon", CodeTokenKind.Punctuation);
                    AddGroup(tokens, header, "message", CodeTokenKind.Plain);
                }
                else Tokenize.WithUrls(tokens, CodeTokenKind.Plain, line);
            }
            Tokenize.Add(tokens, CodeTokenKind.Plain, text[end..next]);
            result.AddRange(tokens.Select(token => new CodeToken
            {
                Kind = token.Kind, Text = token.Text, IsFrameworkFrame = framework, IsFirstUserFrame = firstUser,
            }));
            start = next;
        }
        return result.AsReadOnly();
    }

    private static void AddGroup(List<CodeToken> tokens, Match match, string group, CodeTokenKind kind) =>
        Tokenize.WithUrls(tokens, kind, match.Groups[group].Value);

    private static bool IsSeparator(string line) => line is
        "--- End of inner exception stack trace ---" or "--- End of stack trace from previous location ---" or
        "--- Fin de la trace de la pile d'exception interne ---" or "--- Fin de la trace de la pile à partir de l'emplacement précédent ---" or
        "--- Fin de la trace de la pile d’exception interne ---" or "--- Fin de la trace de la pile à partir de l’emplacement précédent ---";

    private static void Method(List<CodeToken> tokens, string method)
    {
        List<int> dots = [];
        int depth = 0;
        for (int i = 0; i < method.Length; i++)
        {
            if (method[i] is '<' or '[') depth++;
            else if (method[i] is '>' or ']') depth = Math.Max(0, depth - 1);
            else if (method[i] == '.' && depth == 0) dots.Add(i);
        }
        int methodStart = dots.Count > 0 ? dots[^1] + 1 : 0;
        int typeStart = dots.Count > 1 ? dots[^2] + 1 : 0;
        // Runtime async state machines are nested types: Cart.<Run>d__1.MoveNext().
        if (dots.Count > 2 && method[typeStart] == '<') typeStart = dots[^3] + 1;
        Tokenize.Add(tokens, CodeTokenKind.Namespace, method[..typeStart]);
        Tokenize.Add(tokens, CodeTokenKind.Type, method[typeStart..methodStart]);
        Tokenize.Add(tokens, CodeTokenKind.Method, method[methodStart..]);
    }
}
