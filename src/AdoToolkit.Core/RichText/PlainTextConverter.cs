using System.Net;
using System.Text;

namespace AdoToolkit.Core.RichText;

public static class PlainTextConverter
{
    public static PlainTextResult Convert(string? source, CultureInfo culture, int? workItemId = null)
    {
        ArgumentNullException.ThrowIfNull(culture);
        string text = source ?? string.Empty;
        int changes = 0;
        for (int pass = 0; pass < 8; pass++)
        {
            string converted = ConvertPass(text, culture);
            if (string.Equals(text, converted, StringComparison.Ordinal)) break;
            changes++;
            text = converted;
        }
        return new PlainTextResult
        {
            Text = text,
            Diagnostics = changes > 1
                ? Array.AsReadOnly(new[] { DiagnosticMessageRenderer.Create(DiagnosticCodes.NestedEncodingDecoded, culture, workItemId) })
                : Array.Empty<AdoDiagnostic>(),
        };
    }

    private static string ConvertPass(string source, CultureInfo culture)
    {
        StringBuilder output = new();
        Stack<(string Name, int Count)> lists = new();
        Stack<(int Start, string Href)> links = new();
        Stack<(int Cells, int? ContentStart)> tables = new();
        int listContentStart = -1;
        string? suppressed = null;
        foreach (HtmlTokenizer.Token token in HtmlTokenizer.Tokenize(source))
        {
            string name = token.Name;
            if (suppressed is not null)
            {
                if (token.Closing && name == suppressed) suppressed = null;
                continue;
            }
            if (name is "script" or "style" or "head")
            {
                if (!token.Closing && !token.SelfClosing) suppressed = name;
                continue;
            }
            if (name.Length == 0) { output.Append(WebUtility.HtmlDecode(token.Text)); continue; }
            if (name == "br") { output.Append('\n'); continue; }
            if (name is "ul" or "ol")
            {
                Boundary(output);
                if (!token.Closing) lists.Push((name, 0));
                else if (lists.Count > 0) lists.Pop();
                continue;
            }
            if (name == "li")
            {
                Boundary(output);
                if (!token.Closing)
                {
                    string prefix = "- ";
                    if (lists.Count > 0)
                    {
                        (string listName, int count) = lists.Pop();
                        lists.Push((listName, count + 1));
                        if (listName == "ol") prefix = (count + 1).ToString(CultureInfo.InvariantCulture) + ". ";
                    }
                    output.Append(' ', Math.Max(0, lists.Count - 1) * 2).Append(prefix);
                    listContentStart = output.Length;
                }
                continue;
            }
            if (name == "table")
            {
                Boundary(output);
                if (!token.Closing) tables.Push((0, null));
                else if (tables.Count > 0) tables.Pop();
                continue;
            }
            if (name == "tr")
            {
                Boundary(output);
                if (tables.Count > 0) { tables.Pop(); tables.Push((0, null)); }
                continue;
            }
            if (name is "td" or "th")
            {
                if (tables.Count > 0)
                {
                    (int cells, _) = tables.Pop();
                    if (!token.Closing)
                    {
                        if (cells > 0) output.Append(" | ");
                        tables.Push((cells + 1, output.Length));
                    }
                    else
                    {
                        while (output.Length > 0 && output[^1] is ' ' or '\t') output.Length--;
                        tables.Push((cells, null));
                    }
                }
                continue;
            }
            if (name is "p" or "div" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "blockquote" or "pre")
            {
                if (tables.TryPeek(out var table) && table.ContentStart is int cellStart)
                {
                    if (output.Length > cellStart && output[^1] is not (' ' or '\n')) output.Append(' ');
                }
                else if (output.Length != listContentStart) Boundary(output);
                continue;
            }
            if (name == "img" && !token.Closing)
            {
                string? alt = token.Attributes.GetValueOrDefault("alt");
                output.Append(string.IsNullOrEmpty(alt) ? Messages.Get(AdoMessage.RichTextImage, culture)
                    : Messages.Get(AdoMessage.RichTextImageAlt, culture, WebUtility.HtmlDecode(alt)));
                continue;
            }
            if (name == "a")
            {
                if (!token.Closing) links.Push((output.Length, WebUtility.HtmlDecode(token.Attributes.GetValueOrDefault("href") ?? string.Empty)));
                else if (links.Count > 0)
                {
                    (int start, string href) = links.Pop();
                    string label = output.ToString(start, output.Length - start).Trim();
                    if (Uri.TryCreate(href, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https"
                        && !string.Equals(label, href, StringComparison.Ordinal)) output.Append(" (").Append(href).Append(')');
                }
            }
        }
        return Normalize(output.ToString());
    }

    private static void Boundary(StringBuilder output)
    {
        if (output.Length > 0 && output[^1] != '\n') output.Append('\n');
    }

    private static string Normalize(string text)
    {
        text = FrenchTypography.ConvertSpaces(text).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        StringBuilder result = new();
        int newlines = 0;
        foreach (string line in text.Split('\n'))
        {
            StringBuilder normalized = new();
            int indent = 0;
            while (indent < line.Length && line[indent] == ' ') indent++;
            // Preserve only indentation of list rows, including on later whole-conversion passes.
            bool listRow = line.AsSpan(indent).StartsWith("- ", StringComparison.Ordinal);
            int numberEnd = indent;
            while (numberEnd < line.Length && char.IsAsciiDigit(line[numberEnd])) numberEnd++;
            listRow |= numberEnd > indent && line.AsSpan(numberEnd).StartsWith(". ", StringComparison.Ordinal);
            int start = listRow ? indent : 0;
            if (listRow) normalized.Append(' ', indent);
            bool space = false;
            for (int i = start; i < line.Length; i++)
            {
                char character = line[i];
                if (character is ' ' or '\t')
                {
                    if (!space) normalized.Append(' ');
                    space = true;
                }
                else { normalized.Append(character); space = false; }
            }
            string value = normalized.ToString().TrimEnd(' ', '\t');
            if (result.Length > 0 && newlines < 2) { result.Append('\n'); newlines++; }
            if (value.Length > 0) { result.Append(value); newlines = 0; }
        }
        return result.ToString().Trim();
    }
}
