using System.Net.Http;
using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.Http;

public static class AdoHttpHandlerFactory
{
    public static SocketsHttpHandler CreateHandler(IAdoCredentialProvider? provider = null)
    {
        SocketsHttpHandler handler = new();
        try { (provider ?? new WindowsIntegratedCredentialProvider()).Configure(handler); return handler; }
        catch { handler.Dispose(); throw; }
    }

    public static HttpClient CreateClient() => new(CreateHandler(), disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
}
