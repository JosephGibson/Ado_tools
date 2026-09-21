namespace AdoToolkit.Core.TestRuns;

// Groups a build's runs by the pipeline names that ran them, so attempts from separate stages or
// jobs (one per language, for example) are shown and classified separately. The key is the stage,
// phase and job name; empty names and __default count as absent. Fewer than two keys means no
// grouping. Labels use the shortest name set that still tells the groups apart.
internal sealed class PipelineGrouping
{
    private const string DefaultName = "__default";
    private const char Separator = '\u001f';
    private static readonly int[][] Subsets = [[0], [1], [2], [0, 1], [0, 2], [1, 2], [0, 1, 2]];
    private readonly Dictionary<int, int> groups;

    private PipelineGrouping(IReadOnlyList<string?> labels, Dictionary<int, int> groups)
    {
        Labels = labels;
        this.groups = groups;
    }

    internal static PipelineGrouping None { get; } = new([], []);

    // One label per group in first-run order. Null names the group of runs without any name.
    internal IReadOnlyList<string?> Labels { get; }
    internal bool IsGrouped => Labels.Count > 1;

    internal int? GroupOf(int runId) => IsGrouped && groups.TryGetValue(runId, out int index) ? index : null;

    internal static string Key(AdoTestRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return string.Join(Separator, Parts(run));
    }

    // Extra run IDs (attempts whose run is not listed) join the group of runs without names.
    internal static PipelineGrouping Create(IEnumerable<AdoTestRun> runs, IEnumerable<int>? extraRunIds = null)
    {
        ArgumentNullException.ThrowIfNull(runs);
        List<string[]> keys = [];
        Dictionary<string, int> positions = new(StringComparer.Ordinal);
        Dictionary<int, int> map = [];
        foreach (AdoTestRun run in AttemptGrouper.OrderRuns(runs))
        {
            string[] parts = Parts(run);
            string key = string.Join(Separator, parts);
            if (!positions.TryGetValue(key, out int position))
            {
                position = keys.Count;
                positions.Add(key, position);
                keys.Add(parts);
            }
            map[run.Id] = position;
        }
        foreach (int runId in extraRunIds ?? [])
        {
            if (map.ContainsKey(runId)) continue;
            string key = string.Join(Separator, "", "", "");
            if (!positions.TryGetValue(key, out int position))
            {
                position = keys.Count;
                positions.Add(key, position);
                keys.Add(["", "", ""]);
            }
            map[runId] = position;
        }
        return keys.Count < 2 ? None : new PipelineGrouping(Label(keys), map);
    }

    private static string[] Parts(AdoTestRun run) => [Name(run.StageName), Name(run.PhaseName), Name(run.JobName)];

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
