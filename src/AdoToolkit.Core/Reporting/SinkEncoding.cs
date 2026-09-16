using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace AdoToolkit.Core.Reporting;

public static class SinkEncoding
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    public static string Attribute(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        StringBuilder result = new();
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] is not (' ' or ' ')) continue;
            result.Append(Encoder.Encode(text[start..index])).Append(text[index]);
            start = index + 1;
        }
        return result.Append(Encoder.Encode(text[start..])).ToString();
    }

    public static string Html(string text) => string.Join("<br>", NormalizeLines(text).Split('\n').Select(Attribute));

    public static string Markdown(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        StringBuilder result = new();
        bool firstLine = true;
        foreach (string line in NormalizeLines(text).Split('\n'))
        {
            if (!firstLine) result.Append('\n');
            firstLine = false;
            int first = 0;
            while (first < line.Length && line[first] == ' ') first++;
            int digits = first;
            while (digits < line.Length && char.IsAsciiDigit(line[digits])) digits++;
            for (int index = 0; index < line.Length; index++)
            {
                char value = line[index];
                if (value == '&') result.Append("&amp;");
                else if (value == '<') result.Append("&lt;");
                else if (value == '>') result.Append("&gt;");
                else
                {
                    if ("\\`*_[]#|".Contains(value, StringComparison.Ordinal) ||
                        (index == first && value is '-' or '+') ||
                        (index == digits && digits > first && value is '.' or ')')) result.Append('\\');
                    result.Append(value);
                }
            }
        }
        return result.ToString();
    }

    internal static string NormalizeLines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
