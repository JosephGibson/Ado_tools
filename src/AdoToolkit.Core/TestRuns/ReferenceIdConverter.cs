using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdoToolkit.Core.TestRuns;

// Shallow reference IDs are documented as strings. Preserve numeric tokens verbatim too,
// so the reference validator can diagnose fractions, exponents and out-of-range IDs.
internal sealed class ReferenceIdConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return reader.GetString();
        if (reader.TokenType != JsonTokenType.Number) throw new JsonException();
        using JsonDocument number = JsonDocument.ParseValue(ref reader);
        return number.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}
