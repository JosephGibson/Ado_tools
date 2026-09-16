using System.Text;

namespace AdoToolkit.Core.Reporting.TestFailures;

internal static class TestFailureAssets
{
    // Normalize before hashing AND writing: HTML parsers normalize raw-text CRLF to LF.
    internal static string Read(string name)
    {
        using Stream stream = typeof(TestFailureAssets).Assembly.GetManifestResourceStream("AdoToolkit.Core.Reporting.Assets." + name)!;
        using StreamReader reader = new(stream, new UTF8Encoding(false, true));
        return SinkEncoding.NormalizeLines(reader.ReadToEnd());
    }
}
