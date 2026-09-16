namespace AdoToolkit.Core.RichText;

internal static class FrenchTypography
{
    internal static string ConvertSpaces(string text)
    {
        char[] chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] is not ('\u00a0' or '\u202f')) continue;
            char previous = i > 0 ? text[i - 1] : '\0';
            char next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (previous != '«' && next is not (':' or ';' or '!' or '?' or '»')
                && !(char.IsDigit(previous) && char.IsDigit(next))) chars[i] = ' ';
        }
        return new string(chars);
    }
}
