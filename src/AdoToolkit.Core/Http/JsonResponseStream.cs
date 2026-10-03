namespace AdoToolkit.Core.Http;

// The body of one JSON response, read once from start to end. It counts the bytes it reads, after any
// decompression, and fails once they pass the limit. The first bytes can be looked at before parsing:
// later reads return them first. Non-owning adapter; the response consumer disposes the source.
internal sealed class JsonResponseStream(Stream source, long limit, Func<AdoResponseFormatException> tooLarge) : Stream
{
    private byte[] peeked = [];
    private int next;
    private long read;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    // Up to count bytes from the start of the body; fewer when the body is shorter. Call it before reading.
    internal async ValueTask<ReadOnlyMemory<byte>> PeekAsync(int count, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[count];
        int length = await source.ReadAtLeastAsync(buffer, count, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        Count(length);
        peeked = buffer[..length];
        next = 0;
        return peeked;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (next < peeked.Length)
        {
            int replayed = Math.Min(buffer.Length, peeked.Length - next);
            peeked.AsMemory(next, replayed).CopyTo(buffer);
            next += replayed;
            return replayed;
        }
        int length = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Count(length);
        return length;
    }

    private void Count(int length)
    {
        read += length;
        if (read > limit) throw tooLarge();
    }
}
