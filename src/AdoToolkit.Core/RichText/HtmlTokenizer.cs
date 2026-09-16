namespace AdoToolkit.Core.RichText;

/// <summary>A tolerant lexical scanner only; never executes markup or resolves resources.</summary>
internal static class HtmlTokenizer
{
    internal sealed record Token(string Name, string Text, bool Closing, bool SelfClosing,
        IReadOnlyDictionary<string, string> Attributes);

    internal static IEnumerable<Token> Tokenize(string input)
    {
        int offset = 0;
        while (offset < input.Length)
        {
            if (input.AsSpan(offset).StartsWith("<!--", StringComparison.Ordinal))
            {
                int end = input.IndexOf("-->", offset + 4, StringComparison.Ordinal);
                offset = end < 0 ? input.Length : end + 3;
                continue;
            }
            if (input[offset] == '<' && TryTag(input, offset, out Token? tag, out int next))
            {
                yield return tag!;
                offset = next;
                continue;
            }
            int start = offset++;
            while (offset < input.Length && input[offset] != '<') offset++;
            yield return new Token(string.Empty, input[start..offset], false, false,
                System.Collections.ObjectModel.ReadOnlyDictionary<string, string>.Empty);
        }
    }

    private static bool TryTag(string input, int start, out Token? token, out int next)
    {
        token = null;
        next = start;
        int i = start + 1;
        bool closing = i < input.Length && input[i] == '/';
        if (closing) i++;
        if (i >= input.Length || !char.IsAsciiLetter(input[i])) return false;
        int nameStart = i++;
        while (i < input.Length && (char.IsAsciiLetterOrDigit(input[i]) || input[i] is ':' or '-')) i++;
        string name = input[nameStart..i].ToLowerInvariant();
        Dictionary<string, string> attributes = new(StringComparer.OrdinalIgnoreCase);
        while (i < input.Length)
        {
            while (i < input.Length && char.IsWhiteSpace(input[i])) i++;
            if (i >= input.Length) return false;
            bool selfClosing = input[i] == '/' && i + 1 < input.Length && input[i + 1] == '>';
            if (input[i] == '>' || selfClosing)
            {
                next = i + (selfClosing ? 2 : 1);
                token = new Token(name, string.Empty, closing, selfClosing, attributes);
                return true;
            }
            int attrStart = i;
            if (input[i] == '<') return false;
            while (i < input.Length && !char.IsWhiteSpace(input[i]) && input[i] is not ('=' or '>' or '/' or '<')) i++;
            if (i == attrStart) { i++; continue; }
            string attribute = input[attrStart..i];
            while (i < input.Length && char.IsWhiteSpace(input[i])) i++;
            string value = string.Empty;
            if (i < input.Length && input[i] == '=')
            {
                i++;
                while (i < input.Length && char.IsWhiteSpace(input[i])) i++;
                if (i >= input.Length) return false;
                char quote = input[i];
                if (quote is '\'' or '"')
                {
                    int valueStart = ++i;
                    while (i < input.Length && input[i] != quote) i++;
                    if (i == input.Length) return false;
                    value = input[valueStart..i++];
                }
                else
                {
                    int valueStart = i;
                    while (i < input.Length && !char.IsWhiteSpace(input[i]) && input[i] is not ('>' or '<')) i++;
                    value = input[valueStart..i];
                }
            }
            attributes.TryAdd(attribute, value);
        }
        return false;
    }
}
