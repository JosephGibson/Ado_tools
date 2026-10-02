namespace AdoToolkit.Core.TestRuns;

// Invocation-scoped caches (§17). Piped builds of one definition reuse each earlier build's derived
// classification, the Test Case links, and the Bug category and state categories of each project.
// A build window listing is keyed by its current build, so it is reused only when the same build
// arrives again. Public and opaque so the shell can hold one cache for a whole invocation while
// creating a service per pipeline record.
//
// Builds are retrieved one after another. Within one retrieval the history flow uses only the
// window and build entries, and the main flow only the Test Case and bug metadata entries, so no
// entry is ever used by two flows at once. Parallel requests return their values; the
// coordinating method writes them here.
public sealed class TestFailureInvocationCache
{
    private readonly Dictionary<string, IReadOnlyList<HistoryBuild>> windows = new(StringComparer.Ordinal);
    private readonly Dictionary<int, (HistoryBuildData Data, bool Unreadable)> builds = [];
    // The link's WebUrl names the project of the build that referenced the Test Case.
    private readonly Dictionary<(string Project, int Id), (AdoTestCaseLink Link, IReadOnlyList<int> Linked)> testCases = new(new TestCaseKeyComparer());
    // Null marks metadata that could not be read. The server compares project and type names without case.
    private readonly Dictionary<string, IReadOnlyList<string>?> bugTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, IReadOnlyDictionary<string, string>?>> states = new(StringComparer.OrdinalIgnoreCase);

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

    // A link that is not resolved marks a Test Case that was not found; every set that references it
    // repeats the warning, as for an unreadable history build.
    internal bool TryGetTestCase(string project, int id, out AdoTestCaseLink link)
    {
        bool found = testCases.TryGetValue((project, id), out (AdoTestCaseLink Link, IReadOnlyList<int> Linked) entry);
        link = entry.Link;
        return found;
    }

    internal void AddTestCase(string project, int id, AdoTestCaseLink link, IReadOnlyList<int> linked) => testCases[(project, id)] = (link, linked);

    // The work items linked to a resolved Test Case by any link type; empty for any other ID.
    internal IReadOnlyList<int> LinkedWorkItems(string project, int id) =>
        testCases.TryGetValue((project, id), out (AdoTestCaseLink Link, IReadOnlyList<int> Linked) entry) ? entry.Linked : [];

    internal bool TryGetBugTypes(string project, out IReadOnlyList<string>? types) => bugTypes.TryGetValue(project, out types);

    internal void AddBugTypes(string project, IReadOnlyList<string>? types) => bugTypes[project] = types;

    // State name to state category for one work item type of one project.
    internal bool TryGetStates(string project, string type, out IReadOnlyDictionary<string, string>? known)
    {
        known = null;
        return states.TryGetValue(project, out Dictionary<string, IReadOnlyDictionary<string, string>?>? byType) && byType.TryGetValue(type, out known);
    }

    internal void AddStates(string project, string type, IReadOnlyDictionary<string, string>? known)
    {
        if (!states.TryGetValue(project, out Dictionary<string, IReadOnlyDictionary<string, string>?>? byType))
            states[project] = byType = new(StringComparer.OrdinalIgnoreCase);
        byType[type] = known;
    }

    private sealed class TestCaseKeyComparer : IEqualityComparer<(string Project, int Id)>
    {
        public bool Equals((string Project, int Id) x, (string Project, int Id) y) =>
            x.Id == y.Id && string.Equals(x.Project, y.Project, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Project, int Id) key) => HashCode.Combine(key.Id, StringComparer.OrdinalIgnoreCase.GetHashCode(key.Project));
    }
}
