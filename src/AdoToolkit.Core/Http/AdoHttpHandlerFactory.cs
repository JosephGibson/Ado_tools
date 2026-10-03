using System.Net;
using System.Net.Http;
using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.Http;

public static class AdoHttpHandlerFactory
{
    // Every request accepts gzip and deflate. A server that does not compress answers as before, and
    // a compressed body is decoded before anything reads it, so every byte limit counts decoded bytes.
    public static SocketsHttpHandler CreateHandler(IAdoCredentialProvider? provider = null)
    {
        SocketsHttpHandler handler = new() { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
        try { (provider ?? new WindowsIntegratedCredentialProvider()).Configure(handler); return handler; }
        catch { handler.Dispose(); throw; }
    }

    public static HttpClient CreateClient() => new(CreateHandler(), disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
}
