using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace AdoToolkit.Core.Http;

// Runs one asynchronous body per item, a bounded number at a time, and returns the results in
// input order, so what the caller builds never depends on which request finished first. A body
// returns its value and writes nothing shared. With a degree of one the items run one after
// another, in input order.
internal static class OrderedParallel
{
    // The first failure stops the items that have not finished. Of the failures observed, the one
    // with the lowest input index is thrown; cancellation by the caller is thrown before any of them.
    internal static async Task<IReadOnlyList<TResult>> RunAsync<TItem, TResult>(IReadOnlyList<TItem> items, int degree,
        Func<TItem, CancellationToken, Task<TResult>> body, CancellationToken cancellationToken)
    {
        OrderedParallelOutcome<TResult> outcome = await TryRunAsync(items, degree, body, cancellationToken).ConfigureAwait(false);
        if (outcome.Failures.Count > 0) ExceptionDispatchInfo.Capture(outcome.Failures[0].Error).Throw();
        return outcome.Results;
    }

    // As RunAsync, but every failure observed is returned, ordered by input index, for a caller that
    // chooses among them. Results holds a value only for the items that completed.
    internal static Task<OrderedParallelOutcome<TResult>> TryRunAsync<TItem, TResult>(IReadOnlyList<TItem> items, int degree,
        Func<TItem, CancellationToken, Task<TResult>> body, CancellationToken cancellationToken) =>
        RunCoreAsync(items, degree, body, stopOnFailure: true, cancellationToken);

    // As TryRunAsync, but a failure stops nothing: every item runs to its end, so the items that run
    // and the failures returned never depend on which item failed first.
    internal static Task<OrderedParallelOutcome<TResult>> TryRunAllAsync<TItem, TResult>(IReadOnlyList<TItem> items, int degree,
        Func<TItem, CancellationToken, Task<TResult>> body, CancellationToken cancellationToken) =>
        RunCoreAsync(items, degree, body, stopOnFailure: false, cancellationToken);

    private static async Task<OrderedParallelOutcome<TResult>> RunCoreAsync<TItem, TResult>(IReadOnlyList<TItem> items, int degree,
        Func<TItem, CancellationToken, Task<TResult>> body, bool stopOnFailure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(degree);
        TResult[] results = new TResult[items.Count];
        ConcurrentQueue<(int Index, Exception Error)> failures = new();
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await Parallel.ForEachAsync(Enumerable.Range(0, items.Count),
                new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = stop.Token }, async (index, token) =>
                {
                    try { results[index] = await body(items[index], token).ConfigureAwait(false); }
                    // An item stopped by another item's failure, or by the caller, has nothing of its own to report.
                    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                    catch (Exception error)
                    {
                        failures.Enqueue((index, error));
                        if (stopOnFailure) await stop.CancelAsync().ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        cancellationToken.ThrowIfCancellationRequested();
        return new OrderedParallelOutcome<TResult>(results, [.. failures.OrderBy(static failure => failure.Index)]);
    }
}

internal sealed record OrderedParallelOutcome<TResult>(IReadOnlyList<TResult> Results, IReadOnlyList<(int Index, Exception Error)> Failures);
