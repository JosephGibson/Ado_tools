namespace AdoToolkit.Core.Http;

// Counts issued requests so a stage can enforce its own request budget (§15.12).
// Retries count individually: the budget limits work sent to the server.
// Requests may be issued from several threads at once, so the limit is enforced atomically.
//
// A budget can set requests aside for work it has planned (Reserve). They count against its limit
// at once and are never given back, so what is left for later work is fixed when the work is
// planned, whatever order its requests are then sent in.
internal sealed class RequestCounter
{
    private readonly RequestCounter? parent;
    private readonly int limit;
    private readonly Lock gate = new();
    private int count;
    // Requests this counter has set aside and its reservations have not sent yet.
    private int reserved;
    // For a reservation: the requests its parent set aside for it that it has not sent yet.
    private int prepaid;

    internal RequestCounter() : this(null, int.MaxValue, 0) { }

    private RequestCounter(RequestCounter? parent, int limit, int prepaid)
    {
        this.parent = parent;
        this.limit = limit;
        this.prepaid = prepaid;
    }

    internal int Count => Volatile.Read(ref count);

    // True once the requests sent and set aside reach the limit: every further request that was not
    // set aside is refused.
    internal bool IsSpent
    {
        get
        {
            lock (gate) return count + reserved >= limit;
        }
    }

    // Counts one request here and in every enclosing counter, or refuses it when the limit is
    // reached. A refusal therefore means that the budget is fully spent.
    internal void Increment() => Add(false);

    // A stage's own counter: at most maximumRequests requests, each also counted by this counter.
    // Requests of other stages that run at the same time never spend the stage's budget.
    internal RequestCounter Child(int maximumRequests)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRequests);
        return new RequestCounter(this, maximumRequests, 0);
    }

    // Sets aside the requests of several pieces of planned work, all or none, and returns one
    // counter per piece to send them through; null when their total does not fit in what is left.
    // A piece's first requests are the ones set aside for it. Any request beyond them takes from
    // what is left when it is sent, and is refused when nothing is.
    internal IReadOnlyList<RequestCounter>? Reserve(IReadOnlyList<int> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        long total = 0;
        foreach (int value in requests)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(requests));
            total += value;
        }
        lock (gate)
        {
            if (total > limit - count - reserved) return null;
            reserved += (int)total;
        }
        return [.. requests.Select(value => new RequestCounter(this, int.MaxValue, value))];
    }

    // fromReservation: the request was set aside here by Reserve, so the limit has already
    // allowed it.
    private void Add(bool fromReservation)
    {
        bool prepaidAbove = false;
        lock (gate)
        {
            if (fromReservation) reserved--;
            else if (prepaid > 0)
            {
                prepaid--;
                prepaidAbove = true;
            }
            else if (count + reserved >= limit) throw new RequestBudgetExceededException();
            count++;
        }
        try
        {
            parent?.Add(prepaidAbove);
        }
        catch (RequestBudgetExceededException)
        {
            // Refused by an enclosing counter: never sent, so not counted here either.
            lock (gate)
            {
                count--;
                if (fromReservation) reserved++;
                else if (prepaidAbove) prepaid++;
            }
            throw;
        }
    }
}

internal sealed class RequestBudgetExceededException : Exception;
