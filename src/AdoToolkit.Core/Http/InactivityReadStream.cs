namespace AdoToolkit.Core.Http;

// Non-owning adapter; the response consumer disposes the underlying stream. Each read starts its
// own inactivity timer, on the given time provider (a test's manual clock) or the system's.
internal sealed class InactivityReadStream(Stream source, TimeSpan timeout, string operation, CultureInfo culture, TimeProvider? time = null) : Stream
{
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

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource timer = new(timeout, time ?? TimeProvider.System);
        using CancellationTokenSource inactivity = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        try { return await source.ReadAsync(buffer, inactivity.Token).ConfigureAwait(false); }
        catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AdoTimeoutException(Messages.Get(AdoMessage.Timeout, culture), error) { Operation = operation };
        }
    }
}
