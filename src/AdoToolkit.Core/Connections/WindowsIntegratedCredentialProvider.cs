using System.Net;
using System.Net.Http;

namespace AdoToolkit.Core.Connections;

public sealed class WindowsIntegratedCredentialProvider : IAdoCredentialProvider
{
    public void Configure(SocketsHttpHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        handler.Credentials = CredentialCache.DefaultCredentials;
        handler.UseProxy = true;
        handler.Proxy = null;
        handler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
        handler.AllowAutoRedirect = false;
        handler.UseCookies = false;
        handler.PooledConnectionLifetime = TimeSpan.FromMinutes(15);
    }
}
