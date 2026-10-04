using System.Text.Json;
using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.WorkItems;

// Optional values of a work item field bag, for a read that makes no claim it cannot support: an
// absent, null or unexpected value is null, never a diagnostic. A missing optional field is not a
// lookup problem.
internal static class WorkItemFieldValues
{
    // A known date field, in UTC, as FieldValueMapper reads one; null when it is absent or is not a
    // date the server wrote.
    internal static DateTimeOffset? Date(Dictionary<string, JsonElement>? fields, string name) =>
        Value(fields, name) is { ValueKind: JsonValueKind.String } value && value.TryGetDateTimeOffset(out DateTimeOffset date)
            ? date.ToUniversalTime() : null;

    // An identity field, which never loses a name the payload holds: a bare string is the name the
    // server sent, and an object is named by the first of displayName, uniqueName and id it carries.
    // Null means nobody is named — which the caller reads as unassigned — not that the read failed.
    internal static AdoIdentityRef? Identity(Dictionary<string, JsonElement>? fields, string name)
    {
        if (Value(fields, name) is not { } value) return null;
        if (value.ValueKind == JsonValueKind.String)
            return NonEmpty(value.GetString()) is { } text ? new AdoIdentityRef { DisplayName = text } : null;
        if (value.ValueKind != JsonValueKind.Object) return null;
        string? Text(string property) => value.TryGetProperty(property, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? NonEmpty(element.GetString()) : null;
        string? id = Text("id"), unique = Text("uniqueName");
        return (Text("displayName") ?? unique ?? id) is { } display ? new AdoIdentityRef { DisplayName = display, Id = id, UniqueName = unique } : null;
    }

    // The field's value, matched by name as the server cases it; null when the bag has no such field.
    private static JsonElement? Value(Dictionary<string, JsonElement>? fields, string name)
    {
        if (fields is null) return null;
        foreach ((string key, JsonElement value) in fields)
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) return value;
        return null;
    }

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
