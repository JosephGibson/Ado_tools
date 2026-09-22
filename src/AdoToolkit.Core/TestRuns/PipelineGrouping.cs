namespace AdoToolkit.Core.TestRuns;

// Groups a build's runs by the pipeline names that ran them, so attempts from separate stages or
// jobs or named runs are shown and classified separately. The key is the stage, phase, job and
// run name. A confirmed job-retry suffix is ignored; ordinary numbers in run names are retained.
// Fewer than two keys means no visible grouping. Labels use the shortest distinguishing names.
internal sealed class PipelineGrouping
{
    private const string DefaultName = "__default";
    private const char Separator = '\u001f';
    private static readonly int[][] Subsets =
        [[0], [1], [2], [3], [0, 1], [0, 2], [0, 3], [1, 2], [1, 3], [2, 3], [0, 1, 2], [0, 1, 3], [0, 2, 3], [1, 2, 3], [0, 1, 2, 3]];
    private readonly Dictionary<int, int> groups;
    private readonly Dictionary<int, string> runKeys;

    private PipelineGrouping(IReadOnlyList<string?> labels, Dictionary<int, int> groups, Dictionary<int, string> runKeys)
    {
        Labels = labels;
        this.groups = groups;
        this.runKeys = runKeys;
    }

    internal static PipelineGrouping None { get; } = new([], [], []);

    // One label per group in first-run order. Null names the group of runs without any name.
    internal IReadOnlyList<string?> Labels { get; }
    internal bool IsGrouped => Labels.Count > 1;

    internal int? GroupOf(int runId) => IsGrouped && groups.TryGetValue(runId, out int index) ? index : null;

    internal string KeyOf(int runId) => runKeys[runId];

    // Extra run IDs (attempts whose run is not listed) join the group of runs without names.
    internal static PipelineGrouping Create(IEnumerable<AdoTestRun> runs, IEnumerable<int>? extraRunIds = null)
    {
        ArgumentNullException.ThrowIfNull(runs);
        List<string[]> keys = [];
        Dictionary<string, int> positions = new(StringComparer.Ordinal);
        Dictionary<int, int> map = [];
        Dictionary<int, string> runKeys = [];
        IReadOnlyList<AdoTestRun> ordered = AttemptGrouper.OrderRuns(runs);
        HashSet<string> originalKeys = new(ordered.Select(run => string.Join(Separator, Parts(run))), StringComparer.Ordinal);
        foreach (AdoTestRun run in ordered)
        {
            string[] parts = Parts(run);
            // Only the explicit suffix backed by the job attempt AND an unsuffixed sibling is
            // a retry. Never strip arbitrary digits, or infer retries from different run IDs.
            if (run.PipelineAttempt is > 1)
            {
                string suffix = " (attempt " + run.PipelineAttempt.Value.ToString(CultureInfo.InvariantCulture) + ")";
                if (parts[3].EndsWith(suffix, StringComparison.Ordinal))
                {
                    string[] candidate = [parts[0], parts[1], parts[2], parts[3][..^suffix.Length]];
                    if (candidate[3].Length > 0 && originalKeys.Contains(string.Join(Separator, candidate))) parts = candidate;
                }
            }
            string key = string.Join(Separator, parts);
            if (!positions.TryGetValue(key, out int position))
            {
                position = keys.Count;
                positions.Add(key, position);
                keys.Add(parts);
            }
            map[run.Id] = position;
            runKeys[run.Id] = key;
        }
        foreach (int runId in extraRunIds ?? [])
        {
            if (map.ContainsKey(runId)) continue;
            string key = string.Join(Separator, "", "", "", "");
            if (!positions.TryGetValue(key, out int position))
            {
                position = keys.Count;
                positions.Add(key, position);
                keys.Add(["", "", "", ""]);
            }
            map[runId] = position;
            runKeys[runId] = key;
        }
        return new PipelineGrouping(keys.Count < 2 ? [] : Label(keys), map, runKeys);
    }

    private static string[] Parts(AdoTestRun run) => [Name(run.StageName), Name(run.PhaseName), Name(run.JobName), run.Name.Trim()];

    private static string Name(string? value) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), DefaultName, StringComparison.OrdinalIgnoreCase) ? "" : value.Trim();

    private static string?[] Label(List<string[]> keys)
    {
        List<int> named = [.. Enumerable.Range(0, keys.Count).Where(index => keys[index].Any(static part => part.Length > 0))];
        string[]? chosen = null;
        foreach (int[] subset in Subsets)
        {
            string[] labels = [.. named.Select(index => string.Join(" › ", subset.Select(part => keys[index][part]).Where(static part => part.Length > 0)))];
            if (labels.All(static label => label.Length > 0) && labels.Distinct(StringComparer.Ordinal).Count() == labels.Length)
            {
                chosen = labels;
                break;
            }
        }
        chosen ??= [.. named.Select(index => string.Join(" › ", keys[index].Select(static part => part.Length > 0 ? part : "-")))];
        string?[] result = new string?[keys.Count];
        for (int position = 0; position < named.Count; position++) result[named[position]] = chosen[position];
        return result;
    }
}
