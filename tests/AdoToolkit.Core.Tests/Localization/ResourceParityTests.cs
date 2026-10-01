using System.Reflection;
using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Tests.Localization;

public sealed partial class ResourceParityTests
{
    [Fact]
    [Trait("Acceptance", "S0-7")]
    public void EnglishAndFrenchHaveMatchingKeysAndPlaceholders()
    {
        Dictionary<string, string> english = ResourceCatalog.English();
        Dictionary<string, string> french = ResourceCatalog.French();
        Assert.Equal(english.Keys.Order(StringComparer.Ordinal), french.Keys.Order(StringComparer.Ordinal));
        foreach ((string key, string value) in english)
        {
            Assert.False(string.IsNullOrWhiteSpace(value));
            Assert.False(string.IsNullOrWhiteSpace(french[key]));
            Assert.Equal(Placeholders(value), Placeholders(french[key]));
        }
    }

    // Every message key and diagnostic code has a string, and no string is left without a key.
    [Fact]
    public void CatalogHoldsExactlyTheMessageKeysAndDiagnosticCodes()
    {
        IEnumerable<string> codes = typeof(DiagnosticCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral).Select(static field => (string)field.GetRawConstantValue()!);
        Assert.Equal(Enum.GetNames<AdoMessage>().Concat(codes).Order(StringComparer.Ordinal),
            ResourceCatalog.English().Keys.Order(StringComparer.Ordinal));
    }

    // Core holds the only string catalog. A second one in the PowerShell project repeated Core
    // strings, so a wording fix had to be made twice and most of its entries were never read.
    [Fact]
    public void CoreHoldsTheOnlyStringCatalog()
    {
        EnumerationOptions options = new() { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        string[] catalogs = [.. Directory.EnumerateFiles(Path.Combine(TestDirectory.RepositoryRoot, "src"), "*.resx", options)
            .Select(static path => Path.GetRelativePath(TestDirectory.RepositoryRoot, path).Replace('\\', '/'))
            .Where(static path => !path.Split('/').Any(static part => part is "obj" or "bin"))
            .Order(StringComparer.Ordinal)];
        Assert.Equal(["src/AdoToolkit.Core/Resources/Strings.fr.resx", "src/AdoToolkit.Core/Resources/Strings.resx"], catalogs);
    }

    private static string[] Placeholders(string value) => Placeholder().Matches(value)
        .Select(match => match.Value).Order(StringComparer.Ordinal).ToArray();

    [GeneratedRegex(@"\{\d+(?:[^}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
