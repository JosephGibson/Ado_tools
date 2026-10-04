using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Tests.Architecture;

// tools/check.ps1 runs every test with the en-US culture and, beside it, every test without
// Trait("Culture", "Invariant") with fr-CA, so a new class runs in both cultures. A class may carry
// the trait only when the process culture cannot change what it asserts: it lies outside
// Localization, Reporting and RichText, reads no process culture, catalog text, French typography,
// French culture or message text, and uses no fixture that reads the process culture.
public sealed partial class CultureSelectionTests
{
    private static readonly string Root = Path.Combine(TestDirectory.RepositoryRoot, "tests", "AdoToolkit.Core.Tests");
    private static readonly string[] CultureFolders = ["Localization/", "Reporting/", "RichText/"];

    [Fact]
    public void InvariantTestsLieOutsideTheCultureFoldersAndReadNoCultureOrMessageText()
    {
        // This file names the trait and the patterns.
        TestSources.Source[] sources = [.. TestSources.Sources(Root).Where(static source => source.Path != "Architecture/CultureSelectionTests.cs")];
        TestSources.Source[] traited = [.. sources.Where(static source => CultureTrait().IsMatch(source.Text))];
        string[] otherValues = [.. traited.SelectMany(static source => CultureTrait().Matches(source.Text))
            .Select(static match => match.Groups[1].Value).Where(static value => value != "Invariant").Distinct(StringComparer.Ordinal)];
        string[] inCultureFolders = [.. traited
            .Where(static source => CultureFolders.Any(folder => source.Path.StartsWith(folder, StringComparison.Ordinal)))
            .Select(static source => source.Path)];
        string[] readingCulture = [.. traited.Where(static source => CultureReader().IsMatch(source.Text)).Select(static source => source.Path)];
        // A fixture that hands the process culture to the code under test makes its callers depend on it.
        string[] processCultureTypes = [.. sources.Where(static source => !TestMethod().IsMatch(source.Text) && ProcessCulture().IsMatch(source.Text))
            .SelectMany(static source => TypeDeclaration().Matches(source.Text)).Select(static match => match.Groups[1].Value).Distinct(StringComparer.Ordinal)];
        string[] usingProcessCulture = [.. traited
            .Where(source => processCultureTypes.Any(type => Regex.IsMatch(source.Text, @"\b" + type + @"\b", RegexOptions.CultureInvariant)))
            .Select(static source => source.Path)];

        Assert.NotEmpty(traited);
        Assert.Contains("ExpansionFixture", processCultureTypes);
        Assert.Empty(otherValues);
        Assert.Empty(inCultureFolders);
        Assert.Empty(readingCulture);
        Assert.Empty(usingProcessCulture);
    }

    // The trait alone or beside other attributes, on a class or on a method.
    [GeneratedRegex(@"\bTrait\(\s*""Culture""\s*,\s*""([^""]*)""\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex CultureTrait();

    // The process culture, the catalog, French typography, a French culture, the test culture or
    // the text of a message.
    [GeneratedRegex(@"CultureInfo\.Current(UI)?Culture|CurrentThread\.Current(UI)?Culture|DefaultThreadCurrent|\bMessages\.|\bAdoMessage\b|DiagnosticMessageRenderer|FrenchTypography|""fr(-[A-Z]{2})?""|ADOTOOLKIT_TEST_CULTURE|\.Message\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex CultureReader();

    [GeneratedRegex(@"CultureInfo\.Current(UI)?Culture|CurrentThread\.Current(UI)?Culture", RegexOptions.CultureInvariant)]
    private static partial Regex ProcessCulture();

    [GeneratedRegex(@"\b(?:class|record|struct)\s+([A-Z]\w*)", RegexOptions.CultureInvariant)]
    private static partial Regex TypeDeclaration();

    [GeneratedRegex(@"\[(?:Fact|Theory)\b", RegexOptions.CultureInvariant)]
    private static partial Regex TestMethod();
}
