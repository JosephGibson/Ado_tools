using System.Collections.ObjectModel;
using System.Text.Json;
using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.WorkItems;

internal static class FieldValueMapper
{
    // Only known date fields are dates. Unknown strings retain their exact text.
    private static readonly HashSet<string> DateFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.CreatedDate", "System.ChangedDate", "System.AuthorizedDate", "System.RevisedDate",
        "Microsoft.VSTS.Common.ActivatedDate", "Microsoft.VSTS.Common.ResolvedDate", "Microsoft.VSTS.Common.ClosedDate",
        "Microsoft.VSTS.Common.StateChangeDate", "Microsoft.VSTS.Scheduling.StartDate", "Microsoft.VSTS.Scheduling.FinishDate",
    };

    internal static IReadOnlyDictionary<string, object?> MapFields(Dictionary<string, JsonElement> fields)
    {
        Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, JsonElement value) in fields)
        {
            object? mapped = DateFields.Contains(name) && value.ValueKind != JsonValueKind.Null
                ? value.GetDateTimeOffset().ToUniversalTime() : MapValue(value);
            if (!result.TryAdd(name, mapped)) throw new JsonException();
        }
        return new ReadOnlyDictionary<string, object?>(result);
    }

    internal static IReadOnlyDictionary<string, object?> MapObject(IEnumerable<KeyValuePair<string, JsonElement>> values)
    {
        Dictionary<string, object?> result = new(StringComparer.Ordinal);
        foreach ((string name, JsonElement value) in values)
            if (!result.TryAdd(name, MapValue(value))) throw new JsonException();
        return new ReadOnlyDictionary<string, object?>(result);
    }

    internal static object? MapValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.TryGetInt64(out long integer) ? (object)integer : value.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => Array.AsReadOnly(value.EnumerateArray().Select(MapValue).ToArray()),
        JsonValueKind.Object => MapJsonObject(value),
        _ => throw new JsonException(),
    };

    private static object MapJsonObject(JsonElement value)
    {
        if (value.TryGetProperty("displayName", out JsonElement display) && display.ValueKind == JsonValueKind.String
            && ((value.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.String)
                || (value.TryGetProperty("uniqueName", out JsonElement unique) && unique.ValueKind == JsonValueKind.String)))
        {
            return new AdoIdentityRef
            {
                DisplayName = display.GetString()!,
                Id = value.TryGetProperty("id", out id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null,
                UniqueName = value.TryGetProperty("uniqueName", out unique) && unique.ValueKind == JsonValueKind.String ? unique.GetString() : null,
            };
        }
        return MapObject(value.EnumerateObject().Select(static p => new KeyValuePair<string, JsonElement>(p.Name, p.Value)));
    }
}
