namespace AdoToolkit.Core.Reporting.Highlighting;

public static class HighlightedCodeWriter
{
    public const int MaximumDisplayCharacters = 1024 * 1024;

    public static string DisplayHead(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= MaximumDisplayCharacters) return text;
        int end = text.LastIndexOfAny(['\r', '\n'], MaximumDisplayCharacters - 1, MaximumDisplayCharacters);
        if (end >= 0)
        {
            // Keep complete line endings, including CRLF when it fits.
            end++;
            if (text[end - 1] == '\r' && end < MaximumDisplayCharacters && text[end] == '\n') end++;
            else if (text[end - 1] == '\r' && text[end] == '\n') end--;
        }
        else end = MaximumDisplayCharacters;
        if (end > 0 && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end])) end--;
        return text[..end];
    }

    public static AdoDiagnostic? Write(TextWriter writer, string text, CodeLanguage language, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);
        string displayed = language == CodeLanguage.Json ? text : DisplayHead(text);
        (string css, AdoMessage label, IReadOnlyList<CodeToken> tokens) = language switch
        {
            CodeLanguage.StackTrace => ("stacktrace", AdoMessage.TestReportStackTrace, StackTraceLexer.Lex(displayed)),
            CodeLanguage.ErrorMessage => ("error", AdoMessage.TestReportErrorMessage, ErrorMessageLexer.Lex(displayed)),
            CodeLanguage.Json => ("json", AdoMessage.TestReportJson, JsonLexer.Lex(displayed)),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };
        writer.Write("<div class=\"code-block\"><div class=\"language-label\">");
        writer.Write(SinkEncoding.Attribute(Messages.Get(label, culture)));
        writer.Write("</div><pre><code class=\"lang-");
        writer.Write(css);
        writer.Write("\">");
        string? frame = null;
        foreach (CodeToken token in tokens)
        {
            string? nextFrame = token.IsFrameworkFrame ? "framework-frame" : token.IsFirstUserFrame ? "first-user-frame" : null;
            if (frame != nextFrame)
            {
                if (frame is not null) writer.Write("</span>");
                if (nextFrame is not null) { writer.Write("<span class=\""); writer.Write(nextFrame); writer.Write("\">"); }
                frame = nextFrame;
            }
            writer.Write("<span class=\"");
            writer.Write(TokenClass(token.Kind));
            writer.Write("\">");
            if (token.Kind == CodeTokenKind.Url && UrlDetector.TryGetUri(token.Text, out Uri? uri))
            {
                writer.Write("<a rel=\"noreferrer\" href=\"");
                writer.Write(SinkEncoding.Attribute(uri!.AbsoluteUri));
                writer.Write("\">"); writer.Write(SinkEncoding.Attribute(token.Text)); writer.Write("</a>");
            }
            else writer.Write(SinkEncoding.Attribute(token.Text));
            writer.Write("</span>");
        }
        if (frame is not null) writer.Write("</span>");
        writer.Write("</code></pre>");
        AdoDiagnostic? diagnostic = displayed.Length < text.Length
            ? DiagnosticMessageRenderer.Create(DiagnosticCodes.TestTextTruncated, culture) : null;
        if (diagnostic is not null)
        {
            writer.Write("<p class=\"diagnostic\" data-diagnostic=\"TestTextTruncated\">");
            writer.Write(SinkEncoding.Attribute(diagnostic.Message)); writer.Write("</p>");
        }
        writer.Write("</div>");
        return diagnostic;
    }

    private static string TokenClass(CodeTokenKind kind) => kind switch
    {
        CodeTokenKind.Keyword => "tok-keyword", CodeTokenKind.Type => "tok-type", CodeTokenKind.Method => "tok-method",
        CodeTokenKind.Namespace => "tok-namespace", CodeTokenKind.Parameter => "tok-parameter", CodeTokenKind.QuotedString => "tok-string",
        CodeTokenKind.Number => "tok-number", CodeTokenKind.Path => "tok-path", CodeTokenKind.Line => "tok-line",
        CodeTokenKind.Property => "tok-property", CodeTokenKind.Literal => "tok-literal", CodeTokenKind.Punctuation => "tok-punct",
        CodeTokenKind.Url => "tok-url", _ => "tok-plain",
    };
}
