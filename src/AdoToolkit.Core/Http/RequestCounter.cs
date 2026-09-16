namespace AdoToolkit.Core.Http;

// Counts issued requests so a stage can enforce its own request budget (§15.12).
// Retries count individually: the budget limits work sent to the server.
internal sealed class RequestCounter
{
    private int count;
    internal int Count => Volatile.Read(ref count);

    internal void Increment() => Interlocked.Increment(ref count);
}
