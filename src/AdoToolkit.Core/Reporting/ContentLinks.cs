using System.Text;
using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Reporting;

public static partial class ContentLinks
{
    [GeneratedRegex("(?<![\\p{L}\\p{N}_:/])(?:https?://|mailto:)[^\\s<>\\\"']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();

    public static string Html(string text) => Render(text, html: true, SinkEncoding.Html);
    public static string Markdown(string text) => Render(text, html: false, SinkEncoding.Markdown);

    // Links as Html does; the text between the links goes through the caller's HTML encoder.
    internal static string Html(string text, Func<string, string> encode) => Render(text, html: true, encode);

    public static string HtmlLink(string text, Uri url) => "<a rel=\"noreferrer\" href=\"" + SinkEncoding.Attribute(url.AbsoluteUri) + "\">" + SinkEncoding.Html(text) + "</a>";

    public static string MarkdownLink(string text, Uri url) => "[" + SinkEncoding.Markdown(text) + "](" +
        url.AbsoluteUri.Replace("(", "%28", StringComparison.Ordinal).Replace(")", "%29", StringComparison.Ordinal)
            .Replace("<", "%3C", StringComparison.Ordinal).Replace(">", "%3E", StringComparison.Ordinal) + ")";

    private static string Render(string text, bool html, Func<string, string> encode)
    {
        ArgumentNullException.ThrowIfNull(text);
        StringBuilder result = new();
        int position = 0;
        foreach (Match match in UrlPattern().Matches(text))
        {
            string candidate = match.Value.TrimEnd('.', ',', ';', '!', '?');
            int balance = 0;
            foreach (char character in candidate)
            {
                if (character == '(') balance++;
                else if (character == ')') balance--;
            }
            int end = candidate.Length;
            while (balance < 0 && end > 0 && candidate[end - 1] == ')') { end--; balance++; }
            if (end < candidate.Length) candidate = candidate[..end];
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https" or "mailto")) continue;
            result.Append(encode(text[position..match.Index]));
            result.Append(html ? HtmlLink(candidate, uri) : MarkdownLink(candidate, uri));
            position = match.Index + candidate.Length;
        }
        return result.Append(encode(text[position..])).ToString();
    }
}
