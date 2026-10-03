using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AdoToolkit.Core.Http;

// One decoding policy for every JSON endpoint, including bounded error bodies.
//
// A body declared UTF-8, or declared with no charset, is parsed as it arrives, unless it starts with a
// UTF-16 or UTF-32 byte order mark. Any other body is read whole and decoded first. Either way the
// body may hold at most the limit once decompressed: a larger one is a format error, raised before
// the rest is read. A malformed body is a JsonException, as before, which the caller maps.
internal static class ResponseJson
{
    // Far above any JSON response the toolkit has read, work item batches included (0.4.0 left JSON
    // uncapped so that no legitimate batch would fail); it bounds what a compressed body can inflate to.
    internal const long MaximumBytes = 256L * 1024 * 1024;

    internal static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> type, EndpointDefinition endpoint,
        CultureInfo culture, CancellationToken cancellationToken, long limit = MaximumBytes)
    {
        (JsonResponseStream body, bool streamed) = await OpenAsync(response, endpoint, culture, limit, cancellationToken).ConfigureAwait(false);
        return streamed ? await JsonSerializer.DeserializeAsync(body, type, cancellationToken).ConfigureAwait(false)
            : JsonSerializer.Deserialize(await DecodeAsync(body, response, cancellationToken).ConfigureAwait(false), type);
    }

    internal static async Task<JsonDocument> ParseAsync(HttpResponseMessage response, EndpointDefinition endpoint, CultureInfo culture,
        CancellationToken cancellationToken, long limit = MaximumBytes)
    {
        (JsonResponseStream body, bool streamed) = await OpenAsync(response, endpoint, culture, limit, cancellationToken).ConfigureAwait(false);
        return streamed ? await JsonDocument.ParseAsync(body, default, cancellationToken).ConfigureAwait(false)
            : JsonDocument.Parse(await DecodeAsync(body, response, cancellationToken).ConfigureAwait(false));
    }

    internal static string Decode(ReadOnlySpan<byte> bytes, string? charset)
    {
        try
        {
            Encoding encoding = string.IsNullOrWhiteSpace(charset) ? new UTF8Encoding(false, true)
                : Encoding.GetEncoding(charset.Trim('"'), EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            // A BOM identifies the actual encoding. Check UTF-32 before its UTF-16 prefix.
            foreach (Encoding candidate in new Encoding[] { new UTF32Encoding(false, true, true),
                new UTF32Encoding(true, true, true), new UTF8Encoding(true, true),
                new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
            {
                byte[] preamble = candidate.GetPreamble();
                if (!bytes.StartsWith(preamble)) continue;
                return candidate.GetString(bytes[preamble.Length..]);
            }
            return encoding.GetString(bytes);
        }
        catch (ArgumentException error)
        {
            // Includes invalid/unsupported charsets and strict decoder fallback errors.
            throw new JsonException(null, error);
        }
    }

    // The counted body, and whether it is parsed as it arrives. A Content-Length over the limit fails
    // before anything is read; a compressed response has none, so its decoded bytes are counted.
    private static async Task<(JsonResponseStream Body, bool Streamed)> OpenAsync(HttpResponseMessage response, EndpointDefinition endpoint,
        CultureInfo culture, long limit, CancellationToken cancellationToken)
    {
        AdoResponseFormatException TooLarge() => new(Messages.Get(AdoMessage.ResponseTooLarge, culture, limit / (1024 * 1024)))
        { Operation = endpoint.Name };
        if (response.Content.Headers.ContentLength > limit) throw TooLarge();
        JsonResponseStream body = new(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), limit, TooLarge);
        ReadOnlyMemory<byte> start = await body.PeekAsync(4, cancellationToken).ConfigureAwait(false);
        return (body, IsUtf8(response.Content.Headers.ContentType?.CharSet) && !StartsWithWideMark(start.Span));
    }

    private static async Task<string> DecodeAsync(Stream body, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using MemoryStream copy = new();
        await body.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        return Decode(copy.GetBuffer().AsSpan(0, (int)copy.Length), response.Content.Headers.ContentType?.CharSet);
    }

    // An unknown charset is not UTF-8: Decode reports it as before.
    private static bool IsUtf8(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset)) return true;
        try { return Encoding.GetEncoding(charset.Trim('"')).CodePage == Encoding.UTF8.CodePage; }
        catch (ArgumentException) { return false; }
    }

    // UTF-32 big endian, then UTF-16 in either order, which also covers UTF-32 little endian.
    private static bool StartsWithWideMark(ReadOnlySpan<byte> start) =>
        start.StartsWith((ReadOnlySpan<byte>)[0x00, 0x00, 0xFE, 0xFF]) || start.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE])
        || start.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]);
}
