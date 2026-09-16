using System.Text;
using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Reporting;

public static partial class ContentLinks
{
    [GeneratedRegex("(?<![\\p{L}\\p{N}_:/])(?:https?://|mailto:)[^\\s<>\\\"']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();

    public static string Html(string text) => Render(text, html: true);
    public static string Markdown(string text) => Render(text, html: false);

    public static string HtmlLink(string text, Uri url) => "<a rel=\"noreferrer\" href=\"" + SinkEncoding.Attribute(url.AbsoluteUri) + "\">" + SinkEncoding.Html(text) + "</a>";

    public static string MarkdownLink(string text, Uri url) => "[" + SinkEncoding.Markdown(text) + "](" +
        url.AbsoluteUri.Replace("(", "%28", StringComparison.Ordinal).Replace(")", "%29", StringComparison.Ordinal)
            .Replace("<", "%3C", StringComparison.Ordinal).Replace(">", "%3E", StringComparison.Ordinal) + ")";

    private static string Render(string text, bool html)
    {
        ArgumentNullException.ThrowIfNull(text);
        Func<string, string> encode = html ? SinkEncoding.Html : SinkEncoding.Markdown;
        StringBuilder result = new();
        int position = 0;
        foreach (Match match in UrlPattern().Matches(text))
        {
            string candidate = match.Value.TrimEnd('.', ',', ';', '!', '?');
            while (candidate.EndsWith(')') && candidate.Count(c => c == ')') > candidate.Count(c => c == '(')) candidate = candidate[..^1];
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https" or "mailto")) continue;
            result.Append(encode(text[position..match.Index]));
            result.Append(html ? HtmlLink(candidate, uri) : MarkdownLink(candidate, uri));
            position = match.Index + candidate.Length;
        }
        return result.Append(encode(text[position..])).ToString();
    }
}
