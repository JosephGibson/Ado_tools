using System.Net.Http;

namespace AdoToolkit.Commands.Infrastructure;

internal sealed class ClientLease(HttpClient client, Action release) : IDisposable
{
    private Action? releaseAction = release;
    internal HttpClient Client { get; } = client;
    public void Dispose() => Interlocked.Exchange(ref releaseAction, null)?.Invoke();
}
