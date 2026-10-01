using System.Xml;

namespace AdoToolkit.Core.Tests.Support;

// Reads the toolkit's string catalog straight from the Core .resx files.
internal static class ResourceCatalog
{
    internal static Dictionary<string, string> English() => Read("Strings.resx");
    internal static Dictionary<string, string> French() => Read("Strings.fr.resx");

    private static Dictionary<string, string> Read(string file)
    {
        string path = Path.Combine(TestDirectory.RepositoryRoot, "src", "AdoToolkit.Core", "Resources", file);
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
