using System.Net;
using System.Text;

namespace AdoToolkit.Core.RichText;

// Renders formatted rich text as report HTML. The output is built from a fixed set of elements:
// no tag name, attribute or URL of the source is copied, and every text run goes through the
// caller's sink function, which encodes it. Nesting is repaired, so the result is always balanced.
// The text matches PlainTextConverter (§11.2, §11.3): the same tokenizer, the same dropped
// elements, the same link and image text, and markup that is escaped more than once is read again.
internal static class RichTextHtmlRenderer
{
    private const int MaximumPasses = 8;
    private const int MaximumOpenElements = 64;
    private const int MaximumSpan = 1000;

    private enum Kind { Inline, Paragraph, Block, Pre, List, Item, Table, Section, Row, Cell }

    // A class, not a record: an open element is found on the stack by identity, and equal elements nest.
    private sealed class Open(string name, Kind kind, string close, string? href = null, int labelStart = 0)
    {
        internal string Name { get; } = name;
        internal Kind Kind { get; } = kind;
        internal string Close { get; } = close;
        internal string? Href { get; } = href;
        internal int LabelStart { get; } = labelStart;
    }

    // The cultures in which a step may have been converted: image text is the only localized part.
    private static readonly CultureInfo[] Cultures = [CultureInfo.GetCultureInfo("en"), CultureInfo.GetCultureInfo("fr")];

    // True when the markup is what the plain text was converted from, so that a report shows the
    // same content in every format. Source that was not formatted, or that belongs to other text,
    // is not rendered.
    internal static bool IsSourceOf(string? source, string plain)
    {
        ArgumentNullException.ThrowIfNull(plain);
        return !string.IsNullOrEmpty(source) && plain.Length > 0 && !string.Equals(source, plain, StringComparison.Ordinal)
            && Cultures.Any(culture => string.Equals(PlainTextConverter.Convert(source, culture).Text, plain, StringComparison.Ordinal));
    }

    internal static string Render(string source, CultureInfo culture, Func<string, string> text)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(text);
        Writer writer = new(culture, text);
        writer.Walk(source, 1);
        return writer.Finish();
    }

    private sealed class Writer(CultureInfo culture, Func<string, string> text)
    {
        private readonly StringBuilder output = new();
        private readonly StringBuilder labels = new();
        private readonly List<Open> stack = [];
        private string? suppressed;
        private bool blockStart = true;

        internal string Finish()
        {
            PopTo(0);
            return output.ToString();
        }

        internal void Walk(string markup, int pass)
        {
            foreach (HtmlTokenizer.Token token in HtmlTokenizer.Tokenize(markup))
            {
                string name = token.Name;
                if (suppressed is not null)
                {
                    if (token.Closing && name == suppressed) suppressed = null;
                    continue;
                }
                if (name.Length == 0)
                {
                    string decoded = WebUtility.HtmlDecode(token.Text);
                    // Escaped markup is markup on the converter's next pass, so it is walked, not shown.
                    if (pass < MaximumPasses && NeedsAnotherPass(decoded)) Walk(decoded, pass + 1);
                    else Text(decoded);
                    continue;
                }
                if (name is "script" or "style" or "head")
                {
                    if (!token.Closing && !token.SelfClosing) suppressed = name;
                    continue;
                }
                if (token.Closing) End(name);
                else Start(name, token);
            }
        }

        private static bool NeedsAnotherPass(string decoded)
        {
            if (decoded.AsSpan().IndexOfAny('<', '&') < 0) return false;
            int length = 0;
            foreach (HtmlTokenizer.Token token in HtmlTokenizer.Tokenize(decoded))
            {
                if (token.Name.Length > 0 || !string.Equals(WebUtility.HtmlDecode(token.Text), token.Text, StringComparison.Ordinal)) return true;
                length += token.Text.Length;
            }
            // A comment is dropped by the tokenizer.
            return length != decoded.Length;
        }

        private void Start(string name, HtmlTokenizer.Token token)
        {
            switch (name)
            {
                case "br":
                    if (InFlow()) { output.Append("<br>"); blockStart = true; }
                    break;
                case "hr":
                    CloseParagraph(); EnsureFlow(); output.Append("<hr>"); blockStart = true;
                    break;
                case "img":
                    string? alt = token.Attributes.GetValueOrDefault("alt");
                    string image = string.IsNullOrEmpty(alt) ? Messages.Get(AdoMessage.RichTextImage, culture)
                        : Messages.Get(AdoMessage.RichTextImageAlt, culture, WebUtility.HtmlDecode(alt));
                    EnsureFlow();
                    output.Append("<span class=\"rich-image\">").Append(text(image)).Append("</span>");
                    Label(image);
                    blockStart = false;
                    break;
                case "p":
                    Block(name, Kind.Paragraph, "<p>", "</p>");
                    break;
                case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                    // A paragraph, so content cannot change the outline of the report.
                    Block(name, Kind.Paragraph, "<p class=\"rich-heading\">", "</p>");
                    break;
                case "div":
                    Block(name, Kind.Block, "<div>", "</div>");
                    break;
                case "blockquote":
                    Block(name, Kind.Block, "<blockquote>", "</blockquote>");
                    break;
                case "pre":
                    Block(name, Kind.Pre, "<pre>", "</pre>");
                    break;
                case "ul":
                    Block(name, Kind.List, "<ul>", "</ul>");
                    break;
                case "ol":
                    Block(name, Kind.List, Number(token, "start", int.MaxValue) is int start ? "<ol start=\"" + Invariant(start) + "\">" : "<ol>", "</ol>");
                    break;
                case "li":
                    Item();
                    break;
                case "table":
                    Block(name, Kind.Table, "<table>", "</table>");
                    break;
                case "thead" or "tbody" or "tfoot":
                    Section(name);
                    break;
                case "tr":
                    Row();
                    break;
                case "td" or "th":
                    Cell(name, token);
                    break;
                case "b" or "strong":
                    Inline(name, "<strong>", "</strong>");
                    break;
                case "i" or "em":
                    Inline(name, "<em>", "</em>");
                    break;
                case "u" or "ins":
                    Inline(name, "<u>", "</u>");
                    break;
                case "s" or "strike" or "del":
                    Inline(name, "<s>", "</s>");
                    break;
                case "code" or "tt" or "kbd" or "samp":
                    Inline(name, "<code>", "</code>");
                    break;
                case "sub":
                    Inline(name, "<sub>", "</sub>");
                    break;
                case "sup":
                    Inline(name, "<sup>", "</sup>");
                    break;
                case "span" or "font":
                    Styled(name, token.Attributes.GetValueOrDefault("style"));
                    break;
                case "a":
                    EnsureFlow();
                    Push(new Open(name, Kind.Inline, "", WebUtility.HtmlDecode(token.Attributes.GetValueOrDefault("href") ?? string.Empty), labels.Length));
                    break;
            }
            // Any other tag is removed and its content kept.
        }

        private void End(string name)
        {
            switch (name)
            {
                case "td" or "th":
                    if (TableContext() is { Kind: Kind.Cell } cell) PopThrough(cell);
                    break;
                case "tr":
                    if (TableContext() is { Kind: Kind.Cell or Kind.Row }) PopThrough(Nearest(Kind.Row)!);
                    break;
                case "thead" or "tbody" or "tfoot":
                    if (TableContext() is { Kind: not Kind.Table }) PopThrough(Nearest(Kind.Section)!);
                    break;
                case "table":
                    if (TableContext() is not null) PopThrough(Nearest(Kind.Table)!);
                    break;
                case "li":
                    // Never across a list: a stray end tag does not close the item that holds the list.
                    if (Find(entry => entry.Kind == Kind.Item, entry => entry.Kind is Kind.List or Kind.Cell or Kind.Table) is { } item) PopThrough(item);
                    break;
                case "a" or "b" or "strong" or "i" or "em" or "u" or "ins" or "s" or "strike" or "del" or "code" or "tt" or "kbd" or "samp"
                    or "sub" or "sup" or "span" or "font":
                    if (Find(entry => entry.Name == name, entry => entry.Kind != Kind.Inline) is { } inline) PopThrough(inline);
                    break;
                case "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "div" or "blockquote" or "pre" or "ul" or "ol":
                    if (Find(entry => entry.Name == name, entry => entry.Kind is Kind.Cell or Kind.Table) is { } block) PopThrough(block);
                    break;
            }
        }

        private void Text(string decoded)
        {
            string value = FrenchTypography.ConvertSpaces(decoded).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            if (stack.Exists(static entry => entry.Kind == Kind.Pre))
            {
                if (value.Length == 0) return;
                // Preformatted text keeps its lines and spacing.
                output.AppendJoin('\n', value.Split('\n').Select(text));
                Label(value);
                blockStart = false;
                return;
            }
            StringBuilder collapsed = new(value.Length);
            bool space = false;
            foreach (char character in value)
            {
                if (character is ' ' or '\t' or '\n' or '\f')
                {
                    if (!space) collapsed.Append(' ');
                    space = true;
                }
                else { collapsed.Append(character); space = false; }
            }
            value = blockStart ? collapsed.ToString().TrimStart(' ') : collapsed.ToString();
            // White space between the parts of a list or a table is not content.
            if (value.Length == 0 || (value == " " && !InFlow())) return;
            EnsureFlow();
            output.Append(text(value));
            Label(value);
            blockStart = false;
        }

        private void Label(string value)
        {
            if (stack.Exists(static entry => entry.Href is not null)) labels.Append(value);
        }

        private bool Block(string name, Kind kind, string open, string close)
        {
            CloseParagraph();
            EnsureFlow();
            if (!Push(new Open(name, kind, close), open)) return false;
            blockStart = true;
            return true;
        }

        private void Inline(string name, string open, string close)
        {
            EnsureFlow();
            Push(new Open(name, Kind.Inline, close), open);
        }

        // The editor writes bold, italic, underline and strike-through as styles of a span.
        private void Styled(string name, string? style)
        {
            StringBuilder open = new(), close = new();
            foreach (string declaration in WebUtility.HtmlDecode(style ?? string.Empty).Split(';'))
            {
                int colon = declaration.IndexOf(':', StringComparison.Ordinal);
                if (colon < 0) continue;
                string property = declaration[..colon].Trim().ToLowerInvariant(), value = declaration[(colon + 1)..].Trim().ToLowerInvariant();
                string? tag = property switch
                {
                    "font-weight" when value is "bold" or "bolder" || (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int weight) && weight >= 600) => "strong",
                    "font-style" when value is "italic" or "oblique" => "em",
                    "text-decoration" or "text-decoration-line" when value.Contains("underline", StringComparison.Ordinal) => "u",
                    "text-decoration" or "text-decoration-line" when value.Contains("line-through", StringComparison.Ordinal) => "s",
                    _ => null,
                };
                if (tag is null || open.ToString().Contains("<" + tag + ">", StringComparison.Ordinal)) continue;
                open.Append('<').Append(tag).Append('>');
                close.Insert(0, "</" + tag + ">");
            }
            Inline(name, open.ToString(), close.ToString());
        }

        private void Item()
        {
            // A new item ends the open item of the same list.
            if (Find(entry => entry.Kind is Kind.Item or Kind.List, entry => entry.Kind is Kind.Cell or Kind.Table) is { } nearest)
            {
                if (nearest.Kind == Kind.Item) PopThrough(nearest);
                else PopTo(stack.IndexOf(nearest) + 1);
            }
            else
            {
                CloseParagraph();
                EnsureFlow();
                if (!Push(new Open("ul", Kind.List, "</ul>"), "<ul>")) return;
            }
            Push(new Open("li", Kind.Item, "</li>"), "<li>", part: true);
            blockStart = true;
        }

        private void Section(string name)
        {
            Open? context = TableContext();
            if (context is null && !Block("table", Kind.Table, "<table>", "</table>")) return;
            if (context is { Kind: not Kind.Table }) PopThrough(Nearest(Kind.Section)!);
            Push(new Open(name, Kind.Section, "</" + name + ">"), "<" + name + ">", part: true);
        }

        private void Row()
        {
            Open? context = TableContext();
            if (context is null && !Block("table", Kind.Table, "<table>", "</table>")) return;
            if (context is { Kind: Kind.Cell or Kind.Row }) PopThrough(Nearest(Kind.Row)!);
            if (context is null or { Kind: Kind.Table }) Push(new Open("tbody", Kind.Section, "</tbody>"), "<tbody>", part: true);
            Push(new Open("tr", Kind.Row, "</tr>"), "<tr>", part: true);
        }

        private void Cell(string name, HtmlTokenizer.Token token)
        {
            Open? context = TableContext();
            if (context is null && !Block("table", Kind.Table, "<table>", "</table>")) return;
            if (context is { Kind: Kind.Cell }) PopThrough(context);
            if (context is null or { Kind: Kind.Table }) Push(new Open("tbody", Kind.Section, "</tbody>"), "<tbody>", part: true);
            if (context is null or { Kind: Kind.Table or Kind.Section }) Push(new Open("tr", Kind.Row, "</tr>"), "<tr>", part: true);
            StringBuilder open = new("<" + name);
            if (Number(token, "colspan", MaximumSpan) is int columns and > 1) open.Append(" colspan=\"").Append(Invariant(columns)).Append('"');
            if (Number(token, "rowspan", MaximumSpan) is int rows and > 1) open.Append(" rowspan=\"").Append(Invariant(rows)).Append('"');
            Push(new Open(name, Kind.Cell, "</" + name + ">"), open.Append('>').ToString(), part: true);
            blockStart = true;
        }

        // The innermost open table part; null outside a table.
        private Open? TableContext() => stack.FindLast(static entry => entry.Kind is Kind.Table or Kind.Section or Kind.Row or Kind.Cell);

        private Open? Nearest(Kind kind) => stack.FindLast(entry => entry.Kind == kind);

        // The nearest open element that matches, looking no further than a boundary.
        private Open? Find(Func<Open, bool> match, Func<Open, bool> boundary)
        {
            for (int index = stack.Count - 1; index >= 0; index--)
            {
                if (match(stack[index])) return stack[index];
                if (boundary(stack[index])) return null;
            }
            return null;
        }

        private Kind? Container() => stack.FindLast(static entry => entry.Kind != Kind.Inline)?.Kind;

        private bool InFlow() => Container() is not (Kind.List or Kind.Table or Kind.Section or Kind.Row);

        // Text and inline elements need an item inside a list and a cell inside a table.
        private void EnsureFlow()
        {
            switch (Container())
            {
                case Kind.List:
                    Push(new Open("li", Kind.Item, "</li>"), "<li>", part: true);
                    break;
                case Kind.Table:
                    Push(new Open("tbody", Kind.Section, "</tbody>"), "<tbody>", part: true);
                    goto case Kind.Section;
                case Kind.Section:
                    Push(new Open("tr", Kind.Row, "</tr>"), "<tr>", part: true);
                    goto case Kind.Row;
                case Kind.Row:
                    Push(new Open("td", Kind.Cell, "</td>"), "<td>", part: true);
                    break;
            }
        }

        // A paragraph cannot hold a block, so the block ends it.
        private void CloseParagraph()
        {
            if (stack.FindLast(static entry => entry.Kind != Kind.Inline) is { Kind: Kind.Paragraph } paragraph) PopThrough(paragraph);
        }

        // Beyond the limit a tag that starts a new level is dropped and its content kept, like an
        // unknown tag. The parts of an open list or table are always written: they replace each other.
        private bool Push(Open entry, string open = "", bool part = false)
        {
            if (!part && stack.Count >= MaximumOpenElements) return false;
            stack.Add(entry);
            output.Append(open);
            return true;
        }

        private void PopThrough(Open entry) => PopTo(stack.IndexOf(entry));

        private void PopTo(int count)
        {
            if (count < 0) return;
            while (stack.Count > count)
            {
                Open entry = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                if (entry.Href is { } href)
                {
                    // As in plain text: the address follows the link text, so it is always visible.
                    string label = labels.ToString(entry.LabelStart, labels.Length - entry.LabelStart).Trim();
                    if (Uri.TryCreate(href, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https"
                        && !string.Equals(label, href, StringComparison.Ordinal))
                    {
                        output.Append(text(" (" + href + ")"));
                        Label(" (" + href + ")");
                    }
                    if (!stack.Exists(static open => open.Href is not null)) labels.Clear();
                }
                output.Append(entry.Close);
                if (entry.Kind != Kind.Inline) blockStart = true;
            }
        }

        private static int? Number(HtmlTokenizer.Token token, string attribute, int maximum) =>
            int.TryParse(token.Attributes.GetValueOrDefault(attribute), NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                && value >= 1 && value <= maximum ? value : null;

        private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
