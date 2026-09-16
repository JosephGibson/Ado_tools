using System.Collections.ObjectModel;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using AdoToolkit.Core.IO;

namespace AdoToolkit.Core.TestManagement;

/// <summary>Pure parsers for the isolated V-03 assumptions; no shared-set retrieval or join.</summary>
public static class ParameterDataParser
{
    public static ParameterDocument Parse(string? declarations, string? localDataSource, CultureInfo culture, int? workItemId = null)
    {
        ArgumentNullException.ThrowIfNull(culture);
        List<AdoDiagnostic> diagnostics = [];
        IReadOnlyList<string> names = Array.Empty<string>();
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows = Array.Empty<IReadOnlyDictionary<string, string>>();
        Dictionary<string, int> mapping = new(StringComparer.Ordinal);
        void Malformed() => diagnostics.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.MalformedParameterData, culture, workItemId));
        if (!string.IsNullOrWhiteSpace(declarations))
        {
            try { names = ParseNames(declarations); }
            catch (XmlException) { Malformed(); }
        }
        AdoParameterSource source = AdoParameterSource.None;
        if (!string.IsNullOrWhiteSpace(localDataSource))
        {
            bool xml = localDataSource.AsSpan().TrimStart().StartsWith("<", StringComparison.Ordinal);
            source = xml ? AdoParameterSource.Local : AdoParameterSource.Shared;
            try
            {
                if (xml) rows = ParseRows(localDataSource);
                else mapping = ParseMapping(localDataSource);
            }
            catch (XmlException) { Malformed(); }
            catch (JsonException) { Malformed(); }
        }
        return new ParameterDocument
        {
            Parameters = new AdoTestParameters
            {
                Source = source,
                Names = names,
                Rows = rows,
                SharedParameterSets = Array.AsReadOnly(mapping.Values.Distinct().Select(id => new AdoSharedParameterInfo { Id = id }).ToArray()),
            },
            SharedMapping = new ReadOnlyDictionary<string, int>(mapping),
            Diagnostics = diagnostics.AsReadOnly(),
        };
    }

    /// <summary>[V-03] Shared sets are assumed to contain the same DataSet-style row XML.</summary>
    public static ParameterDocument ParseSharedSet(string? xml, CultureInfo culture, int? workItemId = null)
    {
        ArgumentNullException.ThrowIfNull(culture);
        try
        {
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows = ParseRows(xml ?? string.Empty);
            return new ParameterDocument { Parameters = new AdoTestParameters
            {
                Source = AdoParameterSource.Shared,
                Names = Array.AsReadOnly(rows.SelectMany(row => row.Keys).Distinct(StringComparer.Ordinal).ToArray()),
                Rows = rows,
            } };
        }
        catch (XmlException)
        {
            return new ParameterDocument
            {
                Parameters = new AdoTestParameters { Source = AdoParameterSource.Shared },
                Diagnostics = Array.AsReadOnly(new[] { DiagnosticMessageRenderer.Create(DiagnosticCodes.MalformedParameterData, culture, workItemId) }),
            };
        }
    }

    private static ReadOnlyCollection<string> ParseNames(string xml)
    {
        XElement root = SafeXml.Parse(xml).Root!;
        if (root.Name.LocalName != "parameters") throw new XmlException();
        List<string> names = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (XElement parameter in root.Elements())
        {
            string? name = parameter.Attribute("name")?.Value;
            if (parameter.Name.LocalName != "param" || string.IsNullOrEmpty(name) || !seen.Add(name)) throw new XmlException();
            names.Add(name);
        }
        return names.AsReadOnly();
    }

    private static ReadOnlyCollection<IReadOnlyDictionary<string, string>> ParseRows(string xml)
    {
        XElement root = SafeXml.Parse(xml).Root!;
        // [V-03] Root and row labels vary; only the DataSet row/column structure is assumed.
        List<IReadOnlyDictionary<string, string>> rows = [];
        foreach (XElement row in root.Elements())
        {
            if (row.Name == XName.Get("schema", "http://www.w3.org/2001/XMLSchema")) continue;
            if (!row.HasElements && !string.IsNullOrWhiteSpace(row.Value)) throw new XmlException();
            Dictionary<string, string> values = new(StringComparer.Ordinal);
            foreach (XElement column in row.Elements())
            {
                if (column.HasElements || !values.TryAdd(XmlConvert.DecodeName(column.Name.LocalName), column.Value)) throw new XmlException();
            }
            rows.Add(new ReadOnlyDictionary<string, string>(values));
        }
        if (!root.HasElements && !string.IsNullOrWhiteSpace(root.Value)) throw new XmlException();
        return rows.AsReadOnly();
    }

    private static Dictionary<string, int> ParseMapping(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        Dictionary<string, int> mapping = new(StringComparer.Ordinal);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (string.IsNullOrEmpty(property.Name) || property.Value.ValueKind != JsonValueKind.Number
                || !property.Value.TryGetInt32(out int id) || id <= 0 || !mapping.TryAdd(property.Name, id)) throw new JsonException();
        }
        return mapping;
    }
}
