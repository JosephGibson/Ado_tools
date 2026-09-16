namespace AdoToolkit.Core.Tests.Builds;

internal sealed class ChunkedLogStream(byte[] bytes, bool failAfterPrefix = false, Action? onRead = null) : MemoryStream(bytes)
{
    internal bool Disposed { get; private set; }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        onRead?.Invoke();
        if (failAfterPrefix && Position > 0) throw new IOException("Synthetic connection lost");
        return base.ReadAsync(buffer[..Math.Min(buffer.Length, 7)], cancellationToken);
    }
    protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
}
