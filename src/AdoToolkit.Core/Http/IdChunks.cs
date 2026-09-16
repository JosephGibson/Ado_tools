namespace AdoToolkit.Core.Http;

internal static class IdChunks
{
    internal static async Task<IReadOnlyList<T>> FetchAsync<T>(IReadOnlyList<int> ids, int size,
        Func<int[], CancellationToken, Task<IReadOnlyList<T>>> fetch, Func<T, int> identity,
        CultureInfo culture, CancellationToken cancellationToken) where T : class
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        List<T> result = [];
        foreach (int[] chunk in ids.Chunk(size))
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<T> returned = await fetch(chunk, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            HashSet<int> requested = [.. chunk];
            Dictionary<int, T> byId = [];
            foreach (T item in returned)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item is null) continue;
                int id = identity(item);
                if (!requested.Contains(id) || !byId.TryAdd(id, item))
                    throw new AdoResponseFormatException(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = "WorkItemsBatch" };
            }
            foreach (int id in chunk)
                if (byId.TryGetValue(id, out T? item)) result.Add(item);
        }
        return result.AsReadOnly();
    }
}
