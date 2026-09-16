using System.Xml;
using System.Xml.Linq;

namespace AdoToolkit.Core.IO;

internal static class SafeXml
{
    internal const long MaximumCharacters = 10_485_760;

    internal static XmlReaderSettings CreateSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        ValidationType = ValidationType.None,
        MaxCharactersInDocument = MaximumCharacters,
    };

    internal static XDocument Parse(string text)
    {
        using StringReader input = new(text);
        using XmlReader reader = XmlReader.Create(input, CreateSettings());
        return XDocument.Load(reader, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
    }
}
