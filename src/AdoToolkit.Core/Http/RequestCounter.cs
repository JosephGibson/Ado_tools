namespace AdoToolkit.Core.Http;

// Counts issued requests so a stage can enforce its own request budget (§15.12).
// Retries count individually: the budget limits work sent to the server.
// Requests may be issued from several threads at once, so the limit is enforced atomically.
internal sealed class RequestCounter
{
    private readonly RequestCounter? parent;
    private readonly int limit;
    private int count;

    internal RequestCounter() : this(null, int.MaxValue) { }

    private RequestCounter(RequestCounter? parent, int limit)
    {
        this.parent = parent;
        this.limit = limit;
    }

    internal int Count => Volatile.Read(ref count);

    // True once the limit is reached: every further request of this stage is refused.
    internal bool IsSpent => Count >= limit;

    // Counts one request here and in every enclosing counter, or refuses it when this counter's own
    // limit is reached. A refusal therefore means that the budget is fully spent.
    internal void Increment()
    {
        while (true)
        {
            int current = Volatile.Read(ref count);
            if (current >= limit) throw new RequestBudgetExceededException();
            if (Interlocked.CompareExchange(ref count, current + 1, current) == current) break;
        }
        parent?.Increment();
    }

    // A stage's own counter: at most maximumRequests requests, each also counted by this counter.
    // Requests of other stages that run at the same time never spend the stage's budget.
    internal RequestCounter Child(int maximumRequests)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRequests);
        return new RequestCounter(this, maximumRequests);
    }
}

internal sealed class RequestBudgetExceededException : Exception;
