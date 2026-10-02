using AdoToolkit.Core.Http;

namespace AdoToolkit.Core.Tests.Http;

// The fan-out helper of failed-test retrieval, and the request counter it shares between stages.
public sealed class OrderedParallelTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public async Task ResultsFollowInputOrderWhateverOrderTheItemsFinishIn(int degree)
    {
        int[] items = [.. Enumerable.Range(0, 40)];
        int running = 0, peak = 0;
        Lock gate = new();
        IReadOnlyList<string> results = await OrderedParallel.RunAsync(items, degree, async (item, token) =>
        {
            lock (gate) peak = Math.Max(peak, ++running);
            // Later items finish first.
            await Task.Delay(TimeSpan.FromMilliseconds((40 - item) % 7), token);
            lock (gate) running--;
            return "item " + item.ToString(CultureInfo.InvariantCulture);
        }, TestContext.Current.CancellationToken);
        Assert.Equal(items.Select(static item => "item " + item.ToString(CultureInfo.InvariantCulture)), results);
        Assert.InRange(peak, 1, degree);
        if (degree > 1) Assert.True(peak >= 2);
    }

    [Fact]
    public async Task DegreeOfOneRunsTheItemsOneAfterAnotherInInputOrderAndStopsAtTheFirstFailure()
    {
        List<int> started = [];
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => OrderedParallel.RunAsync<int, int>([1, 2, 3, 4, 5], 1,
            (item, _) =>
            {
                started.Add(item);
                return item == 3 ? throw new InvalidOperationException("third") : Task.FromResult(item);
            }, TestContext.Current.CancellationToken));
        Assert.Equal("third", error.Message);
        Assert.Equal([1, 2, 3], started);
    }

    // Two items fail; the one that fails first in time is the later one in input order. The helper
    // throws the earliest input index among the failures it observed.
    [Fact]
    public async Task TheFailureWithTheLowestInputIndexAmongThoseObservedIsThrown()
    {
        using SemaphoreSlim bothStarted = new(0);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<IReadOnlyList<int>> run = OrderedParallel.RunAsync<int, int>([0, 1], 2, async (item, _) =>
        {
            bothStarted.Release();
            // Neither item observes the cancellation that the other's failure causes.
            await release.Task;
            throw new InvalidOperationException("item " + item.ToString(CultureInfo.InvariantCulture));
        }, TestContext.Current.CancellationToken);
        await bothStarted.WaitAsync(TestContext.Current.CancellationToken);
        await bothStarted.WaitAsync(TestContext.Current.CancellationToken);
        release.SetResult();
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => run);
        Assert.Equal("item 0", error.Message);
    }

    [Fact]
    public async Task TryRunReturnsEveryObservedFailureInInputOrderAndTheCompletedResults()
    {
        using SemaphoreSlim started = new(0);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<OrderedParallelOutcome<int>> run = OrderedParallel.TryRunAsync<int, int>([0, 1, 2], 3, async (item, _) =>
        {
            started.Release();
            await release.Task;
            return item == 1 ? item : throw new ArgumentException("item " + item.ToString(CultureInfo.InvariantCulture));
        }, TestContext.Current.CancellationToken);
        for (int index = 0; index < 3; index++) await started.WaitAsync(TestContext.Current.CancellationToken);
        release.SetResult();
        OrderedParallelOutcome<int> outcome = await run;
        Assert.Equal([0, 2], outcome.Failures.Select(static failure => failure.Index));
        Assert.Equal(["item 0", "item 2"], outcome.Failures.Select(static failure => failure.Error.Message));
        Assert.Equal(1, outcome.Results[1]);
    }

    // A failure stops the items that have not finished; an item stopped that way reports nothing.
    [Fact]
    public async Task AFailureCancelsTheOtherItemsAndOnlyTheFailureIsReported()
    {
        int cancelled = 0;
        using SemaphoreSlim waiting = new(0);
        OrderedParallelOutcome<int> outcome = await OrderedParallel.TryRunAsync<int, int>([0, 1, 2, 3], 4, async (item, token) =>
        {
            if (item == 2)
            {
                // Fail only once the three other items are running.
                for (int other = 0; other < 3; other++) await waiting.WaitAsync(token);
                throw new InvalidOperationException("stop");
            }
            waiting.Release();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { Interlocked.Increment(ref cancelled); throw; }
            return item;
        }, TestContext.Current.CancellationToken);
        Assert.Equal((2, "stop"), (Assert.Single(outcome.Failures).Index, outcome.Failures[0].Error.Message));
        Assert.Equal(3, cancelled);
    }

    [Fact]
    public async Task CancellationByTheCallerWinsOverAnyFailure()
    {
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => OrderedParallel.RunAsync<int, int>([0, 1, 2], 2, async (item, token) =>
        {
            if (item == 0)
            {
                await caller.CancelAsync();
                throw new InvalidOperationException("after cancellation");
            }
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return item;
        }, caller.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => OrderedParallel.TryRunAsync<int, int>([0], 1,
            (item, _) => Task.FromResult(item), caller.Token));
    }

    [Fact]
    public async Task EmptyInputAndInvalidDegreeAreHandledBeforeAnyBodyRuns()
    {
        Assert.Empty(await OrderedParallel.RunAsync<int, int>([], 6, (_, _) => throw new InvalidOperationException(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => OrderedParallel.RunAsync<int, int>([1], 0,
            (item, _) => Task.FromResult(item), TestContext.Current.CancellationToken));
    }

    // A stage's budget is its own: requests counted elsewhere never spend it, every request it
    // accepts is also counted by the enclosing counter, and no request beyond the limit is accepted
    // however many threads ask at once.
    [Fact]
    public async Task ChildCounterEnforcesItsOwnLimitAtomicallyAndCountsInItsParent()
    {
        RequestCounter total = new();
        for (int index = 0; index < 50; index++) total.Increment();
        RequestCounter stage = total.Child(25);
        Assert.False(stage.IsSpent);
        int accepted = 0, refused = 0;
        await Parallel.ForEachAsync(Enumerable.Range(0, 200), new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = TestContext.Current.CancellationToken }, (_, _) =>
        {
            try { stage.Increment(); Interlocked.Increment(ref accepted); }
            catch (RequestBudgetExceededException) { Interlocked.Increment(ref refused); }
            return ValueTask.CompletedTask;
        });
        Assert.Equal((25, 175), (accepted, refused));
        Assert.Equal(25, stage.Count);
        Assert.True(stage.IsSpent);
        Assert.Equal(75, total.Count);
        // The enclosing counter has no limit of its own.
        total.Increment();
        Assert.Equal(76, total.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => total.Child(0));
    }
}
