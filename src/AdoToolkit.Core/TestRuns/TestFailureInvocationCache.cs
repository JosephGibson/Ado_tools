namespace AdoToolkit.Core.TestRuns;

// Invocation-scoped history caches (§17): piped builds of one definition reuse both the build
// window listing and each earlier build's derived classification. Public and opaque so the
// shell can hold one cache for a whole invocation while creating a service per pipeline record.
public sealed class TestFailureInvocationCache
{
    private readonly Dictionary<string, IReadOnlyList<HistoryBuild>> windows = new(StringComparer.Ordinal);
    private readonly Dictionary<int, HistoryBuildData> builds = [];

    internal bool TryGetWindow(string key, out IReadOnlyList<HistoryBuild> window) => windows.TryGetValue(key, out window!);

    internal void AddWindow(string key, IReadOnlyList<HistoryBuild> window) => windows[key] = window;

    internal bool TryGetBuild(int buildId, out HistoryBuildData data) => builds.TryGetValue(buildId, out data!);

    internal void AddBuild(int buildId, HistoryBuildData data) => builds[buildId] = data;
}
