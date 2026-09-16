using System.Text;

namespace AdoToolkit.Core.Reporting.Json;

// Bridges Utf8JsonWriter to non-stream TextWriters without buffering a document.
internal sealed class TextWriterUtf8Stream(TextWriter writer) : Stream
{
    private readonly Decoder decoder = new UTF8Encoding(false, true).GetDecoder();
    private readonly char[] characters = new char[4096];
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        while (!buffer.IsEmpty)
        {
            decoder.Convert(buffer, characters, flush: false, out int bytesUsed, out int charsUsed, out _);
            writer.Write(characters.AsSpan(0, charsUsed));
            buffer = buffer[bytesUsed..];
        }
    }

    public override void Flush() => writer.Flush();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
