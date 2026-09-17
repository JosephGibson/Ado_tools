namespace AdoToolkit.Core.Http;

// Counts issued requests so a stage can enforce its own request budget (§15.12).
// Retries count individually: the budget limits work sent to the server.
internal sealed class RequestCounter
{
    private int count;
    internal int Count => Volatile.Read(ref count);

    private int limit = int.MaxValue;

    internal void Increment()
    {
        if (Count >= limit) throw new RequestBudgetExceededException();
        Interlocked.Increment(ref count);
    }

    // Retrieval stages are sequential. Dispose restores the enclosing stage's limit.
    internal IDisposable Limit(int maximumRequests)
    {
        int previous = limit;
        limit = (int)Math.Min(previous, (long)Count + maximumRequests);
        return new BudgetScope(() => limit = previous);
    }

    private sealed class BudgetScope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

internal sealed class RequestBudgetExceededException : Exception;
