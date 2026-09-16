using System.Text.Json;
using System.Text.Unicode;

namespace AdoToolkit.Core.TestRuns;

// Kind comes from the remote file name extension, compared ordinally ignoring case (§15.13).
// Content checks confirm the kind after download; a failed check makes the file `.bin`.
internal static class AttachmentKinds
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
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
            _ => AdoTestAttachmentKind.Other,
        };
    }

    internal static string LocalExtension(AdoTestAttachmentKind kind) => kind switch
    {
        AdoTestAttachmentKind.Png => ".png",
        AdoTestAttachmentKind.Json => ".json",
        AdoTestAttachmentKind.Html => ".html",
        _ => ".bin",
    };

    internal static bool HasExpectedContent(AdoTestAttachmentKind kind, string path) => kind switch
    {
        AdoTestAttachmentKind.Png => HasPngSignature(path),
        AdoTestAttachmentKind.Json => IsJson(path),
        _ => true,
    };

    internal static bool HasPngSignature(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Span<byte> head = stackalloc byte[8];
        return stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false) == head.Length && head.SequenceEqual(PngSignature);
    }

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
                // A token larger than the buffer needs more room before the next read.
                if (length == buffer.Length) Array.Resize(ref buffer, checked(buffer.Length * 2));
            }
        }
        catch (JsonException) { return false; }
    }
}
