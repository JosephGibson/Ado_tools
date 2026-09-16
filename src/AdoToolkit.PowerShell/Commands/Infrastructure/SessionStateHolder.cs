using System.Net.Http;
using AdoToolkit.Core.Http;

namespace AdoToolkit.Commands.Infrastructure;

internal sealed class SessionStateHolder
{
    private readonly object sync = new();
    private readonly Dictionary<string, ClientEntry> clients = new(StringComparer.Ordinal);
    internal SessionCache<IReadOnlyList<AdoProject>> Projects { get; } = new();
    internal SessionCache<IReadOnlyList<string>> TestCategories { get; } = new();
    internal AdoConnection? Connection { get; private set; }

    internal static string Key(AdoConnection connection) => connection.CollectionUri.AbsoluteUri.TrimEnd('/') + "|" + connection.Authentication;

    internal void Connect(AdoConnection connection)
    {
        lock (sync) { RetireClients(); Connection = connection; }
    }

    internal void Disconnect()
    {
        lock (sync) { Connection = null; RetireClients(); }
    }

    internal ClientLease Acquire(AdoConnection connection)
    {
        lock (sync)
        {
            string key = Key(connection);
            if (!clients.TryGetValue(key, out ClientEntry? entry))
                clients.Add(key, entry = new ClientEntry(AdoHttpHandlerFactory.CreateClient()));
            entry.Users++;
            return new ClientLease(entry.Client, () =>
            {
                lock (sync)
                {
                    entry.Users--;
                    if (entry.Retired && entry.Users == 0) entry.Client.Dispose();
                }
            });
        }
    }

    private void RetireClients()
    {
        foreach (ClientEntry entry in clients.Values)
        {
            entry.Retired = true;
            if (entry.Users == 0) entry.Client.Dispose();
        }
        clients.Clear();
        Projects.Clear();
        TestCategories.Clear();
    }

    private sealed class ClientEntry(HttpClient client)
    {
        internal HttpClient Client { get; } = client;
        internal int Users { get; set; }
        internal bool Retired { get; set; }
    }
}
