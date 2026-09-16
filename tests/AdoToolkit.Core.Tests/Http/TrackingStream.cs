namespace AdoToolkit.Core.Tests.Http;

internal sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
{
    internal bool Disposed { get; private set; }
    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}
