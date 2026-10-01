namespace AdoToolkit.Core.TestRuns;

// Invocation-scoped history caches (§17). Piped builds of one definition reuse each earlier
// build's derived classification. A build window listing is keyed by its current build, so it is
// reused only when the same build arrives again. Public and opaque so the shell can hold one
// cache for a whole invocation while creating a service per pipeline record.
public sealed class TestFailureInvocationCache
{
    private readonly Dictionary<string, IReadOnlyList<HistoryBuild>> windows = new(StringComparer.Ordinal);
    private readonly Dictionary<int, (HistoryBuildData Data, bool Unreadable)> builds = [];

    internal bool TryGetWindow(string key, out IReadOnlyList<HistoryBuild> window) => windows.TryGetValue(key, out window!);

    internal void AddWindow(string key, IReadOnlyList<HistoryBuild> window) => windows[key] = window;

    // Unreadable marks a build whose read failed, so every set that reuses it repeats the warning.
    internal bool TryGetBuild(int buildId, out HistoryBuildData data, out bool unreadable)
    {
        bool found = builds.TryGetValue(buildId, out (HistoryBuildData Data, bool Unreadable) entry);
        data = entry.Data;
        unreadable = entry.Unreadable;
        return found;
    }

    internal void AddBuild(int buildId, HistoryBuildData data, bool unreadable) => builds[buildId] = (data, unreadable);
}
