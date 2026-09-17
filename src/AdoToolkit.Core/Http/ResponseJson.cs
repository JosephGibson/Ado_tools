using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AdoToolkit.Core.Http;

// One decoding policy for every JSON endpoint, including bounded error bodies.
internal static class ResponseJson
{
    internal static async Task<string> ReadAsync(HttpResponseMessage response, CancellationToken token) =>
        Decode(await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false), response.Content.Headers.ContentType?.CharSet);

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
}
