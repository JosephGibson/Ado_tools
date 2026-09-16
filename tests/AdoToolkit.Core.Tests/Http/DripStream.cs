namespace AdoToolkit.Core.Tests.Http;

internal sealed class DripStream(int chunks, TimeSpan delay) : MemoryStream
{
    private int remaining = chunks;
    internal bool Disposed { get; private set; }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (remaining-- <= 0) return 0;
        await Task.Delay(delay, cancellationToken);
        buffer.Span[0] = (byte)'x';
        return 1;
    }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}
