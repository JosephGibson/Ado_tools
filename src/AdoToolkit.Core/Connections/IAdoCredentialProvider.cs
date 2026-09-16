using System.Net.Http;

namespace AdoToolkit.Core.Connections;

public interface IAdoCredentialProvider
{
    void Configure(SocketsHttpHandler handler);
}
