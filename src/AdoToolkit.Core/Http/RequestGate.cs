namespace AdoToolkit.Core.Http;

// Bounds how many requests one retrieval has in flight. Pipelines that share a gate share its
// slots; a pipeline built without a gate sends every request at once, as the other cmdlets do.
internal sealed class RequestGate : IDisposable
{
    private readonly SemaphoreSlim slots;

    internal RequestGate(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        Maximum = maximum;
        slots = new SemaphoreSlim(maximum, maximum);
    }

    internal int Maximum { get; }

    // Waits for a free slot. Dispose the slot once, when the whole operation has ended.
    internal async ValueTask<Slot> EnterAsync(CancellationToken cancellationToken)
    {
        await slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Slot(slots);
    }

    // Every operation that took a slot must have ended first.
    public void Dispose() => slots.Dispose();

    internal readonly struct Slot(SemaphoreSlim? slots) : IDisposable
    {
        public void Dispose() => slots?.Release();
    }
}
