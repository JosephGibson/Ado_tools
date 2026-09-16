using AdoToolkit.Core.Connections;

namespace AdoToolkit.Core.Tests.Connections;

public sealed class SessionCacheTests
{
    [Fact]
    public void ExpiresAtFifteenMinutesWithoutSlidingOnReads()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        SessionCache<string> cache = new(now: () => now);
        cache.Set("project", "value");
        now += TimeSpan.FromMinutes(14);
        Assert.True(cache.TryGet("project", out string? value));
        Assert.Equal("value", value);
        now += TimeSpan.FromMinutes(1);
        Assert.False(cache.TryGet("project", out _));
    }

    [Fact]
    public void EvictsLeastRecentlyUsedAndClearsAllEntries()
    {
        SessionCache<string> cache = new(capacity: 2);
        cache.Set("one", "1");
        cache.Set("two", "2");
        Assert.True(cache.TryGet("one", out _));
        cache.Set("three", "3");
        Assert.False(cache.TryGet("two", out _));
        cache.Set("one", "replacement");
        Assert.True(cache.TryGet("one", out string? value));
        Assert.Equal("replacement", value);
        cache.Clear();
        Assert.False(cache.TryGet("one", out _));
        Assert.False(cache.TryGet("three", out _));
    }
}

