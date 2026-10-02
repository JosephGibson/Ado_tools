namespace AdoToolkit.Completion;

internal static class NameCompletion
{
    internal static WildcardPattern PrefixPattern(string word)
    {
        // PowerShell supplies the decoded prefix with its surrounding quotes. Remove only
        // that pair, keeping any quote characters that belong to the name itself.
        if (word.Length >= 2 && word[0] is '\'' or '"' && word[^1] == word[0]) word = word[1..^1];
        return new WildcardPattern(WildcardPattern.Escape(word) + "*", WildcardOptions.IgnoreCase | WildcardOptions.CultureInvariant);
    }
}
