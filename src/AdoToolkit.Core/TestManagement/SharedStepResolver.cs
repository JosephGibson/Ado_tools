namespace AdoToolkit.Core.TestManagement;

internal sealed class SharedStepResolver(TestWorkItemReader reader)
{
    private static readonly string[] Fields = ["System.Id", "System.Rev", "System.Title", "System.WorkItemType",
        "System.TeamProject", "Microsoft.VSTS.TCM.Steps", "Microsoft.VSTS.TCM.Parameters"];

    internal async Task<IReadOnlyDictionary<int, ResolvedStepDocument>> ResolveAsync(
        IReadOnlyList<ResolvedStepDocument> roots, IReadOnlyList<int> rootIds, IReadOnlyList<TestWorkItem> rootItems,
        IEnumerable<int> sharedParameterIds, int maximumDepth, int maximumDocuments,
        CultureInfo culture, CancellationToken token, Action? batchCompleted = null)
    {
        Dictionary<int, ResolvedStepDocument> cache = roots.ToDictionary(root => root.Item!.Id);
        foreach (TestWorkItem item in rootItems)
            if (!cache.ContainsKey(item.Id))
                cache.Add(item.Id, new() { Item = item, Document = StepsXmlParser.Parse(item.Text("Microsoft.VSTS.TCM.Steps"), item.Id, item.Rev, culture) });
        foreach (int id in rootIds) cache.TryAdd(id, new());
        int[] frontier = roots.SelectMany(root => References(root.Document!)).Concat(sharedParameterIds).Distinct().ToArray();
        int fetched = 0;
        // Roots are already retrieved; the budget bounds additional resolution requests, including omitted IDs.
        for (int level = 1; frontier.Length != 0 && level <= maximumDepth; level++)
        {
            token.ThrowIfCancellationRequested();
            int[] pending = frontier.Where(id => !cache.ContainsKey(id)).ToArray();
            int[] fetch = pending.Take(Math.Max(0, maximumDocuments - fetched)).ToArray();
            foreach (int id in pending.Skip(fetch.Length)) cache.Add(id, new() { ResolutionLimited = true });
            IReadOnlyList<TestWorkItem> items = await reader.ReadAsync(fetch, Fields, culture, token, batchCompleted).ConfigureAwait(false);
            fetched += fetch.Length;
            foreach (int id in fetch) cache.Add(id, new());
            List<int> next = [];
            foreach (TestWorkItem item in items)
            {
                StepDocument document = StepsXmlParser.Parse(item.Text("Microsoft.VSTS.TCM.Steps"), item.Id, item.Rev, culture);
                cache[item.Id] = new() { Item = item, Document = document };
                next.AddRange(References(document));
            }
            frontier = next.Distinct().ToArray();
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<int, ResolvedStepDocument>(cache);
    }

    private static IEnumerable<int> References(StepDocument document) => document.Nodes
        .Where(node => node.Kind == AdoTestStepKind.SharedStep && node.SharedStepId.HasValue).Select(node => node.SharedStepId!.Value);
}
