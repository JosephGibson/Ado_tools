using System.Text.Json;

namespace AdoToolkit.Core.Tests.Reporting.Highlighting;

internal static class LexerFixtures
{
    internal static string Read(string name) => File.ReadAllText(Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", name));

    internal static IEnumerable<string> EveryText()
    {
        foreach (string directory in new[] { "TestRuns", "Attachments" })
        foreach (string file in Directory.EnumerateFiles(Path.Combine(TestDirectory.RepositoryRoot, "tests", "Fixtures", directory)).Order(StringComparer.Ordinal))
        {
            if (!file.EndsWith(".txt", StringComparison.Ordinal) && !file.EndsWith(".json", StringComparison.Ordinal)) continue;
            string text = File.ReadAllText(file);
            yield return text;
            if (!file.EndsWith(".json", StringComparison.Ordinal)) continue;
            using JsonDocument document = JsonDocument.Parse(text);
            foreach (string value in Strings(document.RootElement)) yield return value;
        }
    }

    private static IEnumerable<string> Strings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) yield return element.GetString()!;
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                yield return property.Name;
                foreach (string value in Strings(property.Value)) yield return value;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement child in element.EnumerateArray())
            foreach (string value in Strings(child)) yield return value;
    }
}
