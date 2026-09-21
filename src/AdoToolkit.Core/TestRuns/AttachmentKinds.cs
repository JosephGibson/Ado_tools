using System.Text;
using System.Text.Json;
using System.Text.Unicode;

namespace AdoToolkit.Core.TestRuns;

// Kind comes from the remote file name extension, compared ordinally ignoring case (§15.13).
// Only JSON and text are ever downloaded; PNG, HTML and other kinds stay links to Azure DevOps.
// Content checks confirm the kind after download; a failed check makes the file `.bin`.
internal static class AttachmentKinds
{
    private static ReadOnlySpan<byte> ByteOrderMark => [0xEF, 0xBB, 0xBF];
    private static readonly JsonReaderOptions JsonOptions = new()
    { MaxDepth = 64, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false };

    internal static AdoTestAttachmentKind FromFileName(string? fileName)
    {
        string extension = string.IsNullOrEmpty(fileName) ? "" : Path.GetExtension(fileName);
        return extension switch
        {
            _ when extension.Equals(".png", StringComparison.OrdinalIgnoreCase) => AdoTestAttachmentKind.Png,
            _ when extension.Equals(".json", StringComparison.OrdinalIgnoreCase) => AdoTestAttachmentKind.Json,
            _ when extension.Equals(".html", StringComparison.OrdinalIgnoreCase) => AdoTestAttachmentKind.Html,
            _ when extension.Equals(".htm", StringComparison.OrdinalIgnoreCase) => AdoTestAttachmentKind.Html,
            _ when extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) => AdoTestAttachmentKind.Text,
            _ when extension.Equals(".log", StringComparison.OrdinalIgnoreCase) => AdoTestAttachmentKind.Text,
            _ => AdoTestAttachmentKind.Other,
        };
    }

    internal static bool IsDownloadable(AdoTestAttachmentKind kind) => kind is AdoTestAttachmentKind.Json or AdoTestAttachmentKind.Text;

    // .log files are saved as .txt so a browser shows them instead of offering a download.
    internal static string LocalExtension(AdoTestAttachmentKind kind) => kind switch
    {
        AdoTestAttachmentKind.Json => ".json",
        AdoTestAttachmentKind.Text => ".txt",
        _ => ".bin",
    };

    internal static bool HasExpectedContent(AdoTestAttachmentKind kind, string path) => kind switch
    {
        AdoTestAttachmentKind.Json => IsJson(path),
        AdoTestAttachmentKind.Text => IsText(path),
        _ => false,
    };

    // Streams the file through Utf8JsonReader: one value, depth 64, no comments or trailing
    // commas, and strict UTF-8 in names and strings. Memory is bounded by the largest token.
    internal static bool IsJson(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan);
        byte[] buffer = new byte[65536];
        int length = stream.ReadAtLeast(buffer, ByteOrderMark.Length, throwOnEndOfStream: false);
        int start = buffer.AsSpan(0, length).StartsWith(ByteOrderMark) ? ByteOrderMark.Length : 0;
        JsonReaderState state = new(JsonOptions);
        bool any = false;
        try
        {
            while (true)
            {
                int read = length == buffer.Length ? -1 : stream.Read(buffer, length, buffer.Length - length);
                if (read > 0) length += read;
                bool final = read == 0;
                Utf8JsonReader reader = new(buffer.AsSpan(start, length - start), final, state);
                while (reader.Read())
                {
                    any = true;
                    if (reader.TokenType is JsonTokenType.String or JsonTokenType.PropertyName && !Utf8.IsValid(reader.ValueSpan)) return false;
                }
                if (final) return any;
                state = reader.CurrentState;
                int consumed = start + (int)reader.BytesConsumed;
                Buffer.BlockCopy(buffer, consumed, buffer, 0, length - consumed);
                length -= consumed;
                start = 0;
                // A token larger than the read buffer needs more room before the next read.
                if (length == buffer.Length) Array.Resize(ref buffer, checked(buffer.Length * 2));
            }
        }
        catch (JsonException) { return false; }
    }

    // Text is strict UTF-8 (byte order mark optional) without NUL characters, or UTF-16 with a
    // byte order mark. Streams the file; memory is bounded by the read buffer.
    internal static bool IsText(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan);
        byte[] buffer = new byte[65536];
        int read = stream.ReadAtLeast(buffer, ByteOrderMark.Length, throwOnEndOfStream: false);
        if (read >= 2 && ((buffer[0] == 0xFF && buffer[1] == 0xFE) || (buffer[0] == 0xFE && buffer[1] == 0xFF))) return true;
        int start = buffer.AsSpan(0, read).StartsWith(ByteOrderMark) ? ByteOrderMark.Length : 0;
        Decoder decoder = new UTF8Encoding(false, true).GetDecoder();
        char[] chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
        try
        {
            while (read > 0)
            {
                ReadOnlySpan<byte> bytes = buffer.AsSpan(start, read - start);
                if (bytes.Contains((byte)0)) return false;
                decoder.GetChars(bytes, chars, flush: false);
                start = 0;
                read = stream.Read(buffer);
            }
            decoder.GetChars(ReadOnlySpan<byte>.Empty, chars, flush: true);
            return true;
        }
        catch (DecoderFallbackException) { return false; }
    }
}
