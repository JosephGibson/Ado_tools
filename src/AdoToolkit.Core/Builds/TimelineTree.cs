namespace AdoToolkit.Core.Builds;

// Uses IDs and parent links only. Iterative traversal also handles deeply nested server input.
internal sealed class TimelineTree
{
    internal IReadOnlyList<AdoTimelineRecord> Ordered { get; }
    internal IReadOnlyDictionary<Guid, AdoTimelineRecord> ById { get; }

    internal TimelineTree(IReadOnlyList<AdoTimelineRecord> records, CultureInfo culture, CancellationToken cancellationToken)
    {
        Dictionary<Guid, AdoTimelineRecord> byId = [];
        foreach (AdoTimelineRecord record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (record is null || record.Id == Guid.Empty || !byId.TryAdd(record.Id, record)) throw FormatError(culture);
        }
        Dictionary<Guid, List<AdoTimelineRecord>> children = [];
        List<AdoTimelineRecord> roots = [];
        foreach (AdoTimelineRecord record in records)
        {
            if (record.ParentId is not Guid parent) roots.Add(record);
            else
            {
                if (!byId.ContainsKey(parent)) throw FormatError(culture);
                if (!children.TryGetValue(parent, out List<AdoTimelineRecord>? siblings)) children[parent] = siblings = [];
                siblings.Add(record);
            }
        }
        List<AdoTimelineRecord> ordered = [];
        Stack<AdoTimelineRecord> pending = new(roots.OrderByDescending(record => record.Order).ThenByDescending(record => record.Id));
        while (pending.TryPop(out AdoTimelineRecord? record))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ordered.Add(record);
            if (children.TryGetValue(record.Id, out List<AdoTimelineRecord>? siblings))
                foreach (AdoTimelineRecord child in siblings.OrderByDescending(item => item.Order).ThenByDescending(item => item.Id)) pending.Push(child);
        }
        // A cycle cannot be reached from a root in a single-parent graph.
        if (ordered.Count != records.Count) throw FormatError(culture);
        Ordered = ordered.AsReadOnly();
        ById = byId;
    }

    internal static AdoResponseFormatException FormatError(CultureInfo culture) =>
        new(Messages.Get(AdoMessage.ResponseFormat, culture)) { Operation = "BuildTimeline" };
}
