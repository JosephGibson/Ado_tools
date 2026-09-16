using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Reporting.Highlighting;

public static class JsonLexer
{
    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = 64, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false };
    private static readonly Regex Parts = new("(?<string>\"(?:[^\"\\\\]|\\\\.)*\")|(?<number>-?[0-9]+(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)|(?<literal>true|false|null)|(?<punct>[{}\\[\\],:])",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    // Formatting is explicitly separate: Lex always preserves the exact string supplied to it.
    public static bool TryFormat(string text, int maximumInlineBytes, out string formatted)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumInlineBytes);
        formatted = text;
        if (Encoding.UTF8.GetByteCount(text) > maximumInlineBytes) return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(text, DocumentOptions);
            using MemoryStream buffer = new();
            // Tokens are encoded at the HTML sink, so keep Unicode and markup characters readable here.
            // Reports use LF only, whatever the platform newline.
            using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions
            { Indented = true, IndentSize = 2, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) document.WriteTo(writer);
            formatted = Encoding.UTF8.GetString(buffer.ToArray());
            return true;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException) { return false; }
    }

    public static IReadOnlyList<CodeToken> Lex(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            using JsonDocument document = JsonDocument.Parse(text, DocumentOptions);
            List<CodeToken> result = [];
            Tokenize.Matches(result, text, Parts, match =>
            {
                if (match.Groups["string"].Success)
                {
                    int next = match.Index + match.Length;
                    while (next < text.Length && char.IsWhiteSpace(text[next])) next++;
                    return next < text.Length && text[next] == ':' ? CodeTokenKind.Property : CodeTokenKind.QuotedString;
                }
                return match.Groups["number"].Success ? CodeTokenKind.Number :
                    match.Groups["literal"].Success ? CodeTokenKind.Literal : CodeTokenKind.Punctuation;
            });
            return result.AsReadOnly();
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        { return text.Length == 0 ? [] : [new() { Kind = CodeTokenKind.Plain, Text = text }]; }
    }
}
