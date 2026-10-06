using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdoToolkit.Core.TestRuns;

// A string that the toolkit can do without: any other value, or a string that cannot be read, such
// as an escaped half of a surrogate pair, reads as none instead of failing the whole response, whose
// other fields stay strict.
internal sealed class LenientStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) { reader.Skip(); return null; }
        try { return reader.GetString(); }
        catch (InvalidOperationException) { return null; }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}
