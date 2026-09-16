using System.Text.RegularExpressions;
using System.Xml;

namespace AdoToolkit.Core.Tests.Localization;

public sealed class ResourceParityTests
{
    [Theory]
    [Trait("Acceptance", "S0-7")]
    [InlineData("Core")]
    [InlineData("PowerShell")]
    public void EnglishAndFrenchHaveMatchingKeysAndPlaceholders(string project)
    {
        string resources = Path.Combine(TestDirectory.RepositoryRoot, "src", "AdoToolkit." + project, "Resources");
        Dictionary<string, string> english = Read(Path.Combine(resources, "Strings.resx"));
        Dictionary<string, string> french = Read(Path.Combine(resources, "Strings.fr.resx"));
        Assert.Equal(english.Keys.Order(StringComparer.Ordinal), french.Keys.Order(StringComparer.Ordinal));
        foreach ((string key, string value) in english)
        {
            Assert.False(string.IsNullOrWhiteSpace(value));
            Assert.False(string.IsNullOrWhiteSpace(french[key]));
            Assert.Equal(Placeholders(value), Placeholders(french[key]));
        }
    }

    private static string[] Placeholders(string value) => Regex.Matches(value, @"\{\d+(?:[^}]*)\}", RegexOptions.CultureInvariant)
        .Select(match => match.Value).Order(StringComparer.Ordinal).ToArray();

    private static Dictionary<string, string> Read(string path)
    {
        XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1048576 };
        using XmlReader reader = XmlReader.Create(path, settings);
        XmlDocument document = new() { XmlResolver = null };
        document.Load(reader);
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        foreach (XmlElement entry in document.SelectNodes("/root/data")!)
        {
            Assert.False(entry.HasAttribute("type"));
            result.Add(entry.GetAttribute("name"), entry["value"]!.InnerText);
        }
        return result;
    }
}
