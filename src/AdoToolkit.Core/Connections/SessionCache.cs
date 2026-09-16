namespace AdoToolkit.Core.Connections;

public sealed class SessionCache<T> where T : class
{
    private readonly int capacity;
    private readonly TimeSpan lifetime;
    private readonly Func<DateTimeOffset> now;
    private readonly Dictionary<string, (T Value, DateTimeOffset Expires, LinkedListNode<string> Node)> entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> order = new();
    private readonly object sync = new();

    public SessionCache(int capacity = 256, TimeSpan? lifetime = null, Func<DateTimeOffset>? now = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        this.capacity = capacity;
        this.lifetime = lifetime ?? TimeSpan.FromMinutes(15);
        this.now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public bool TryGet(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (sync)
        {
            if (entries.TryGetValue(key, out var entry))
            {
                order.Remove(entry.Node);
                if (entry.Expires > now())
                {
                    order.AddLast(entry.Node);
                    value = entry.Value;
                    return true;
                }
                entries.Remove(key);
            }
            value = null;
            return false;
        }
    }

    public void Set(string key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        lock (sync)
        {
            if (entries.Remove(key, out var previous)) order.Remove(previous.Node);
            entries[key] = (value, now() + lifetime, order.AddLast(key));
            while (entries.Count > capacity)
            {
                string oldest = order.First!.Value;
                order.RemoveFirst();
                entries.Remove(oldest);
            }
        }
    }

    public void Clear() { lock (sync) { entries.Clear(); order.Clear(); } }
}
