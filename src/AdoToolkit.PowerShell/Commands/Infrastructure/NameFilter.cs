using System.Text;

namespace AdoToolkit.Commands.Infrastructure;

// A -Name wildcard filter (§11.5): case-insensitive, accents significant, and composed and
// decomposed accents match because both sides are compared in NFC. Output keeps the original text.
internal sealed class NameFilter
{
    private readonly WildcardPattern? pattern;

    internal NameFilter(string? name) =>
        pattern = name is null ? null : new WildcardPattern(Nfc(name), WildcardOptions.IgnoreCase | WildcardOptions.CultureInvariant);

    internal bool IsMatch(string value) => pattern is null || pattern.IsMatch(Nfc(value));

    // Text with an unpaired surrogate cannot be normalized; it is compared as it is.
    private static string Nfc(string value)
    {
        try { return value.Normalize(NormalizationForm.FormC); }
        catch (ArgumentException) { return value; }
    }
}
