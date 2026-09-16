namespace AdoToolkit.Core.Builds;

public static class BuildFailureDeriver
{
    public static IReadOnlyList<AdoBuildFailure> Derive(AdoBuild build, IReadOnlyList<AdoTimelineRecord> records,
        IReadOnlyDictionary<int, int?> lineCounts, bool includeWarnings, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(lineCounts);
        TimelineTree tree = new(records, culture, cancellationToken);
        Dictionary<string, int> attempts = new(StringComparer.Ordinal);
        foreach (AdoTimelineRecord record in tree.Ordered)
        {
            if (!record.CollectionUri.Equals(build.CollectionUri))
                throw new AdoConnectionMismatchException(Messages.Get(AdoMessage.ConnectionMismatch, culture));
            if (record.BuildId != build.Id) throw TimelineTree.FormatError(culture);
            if (!string.IsNullOrEmpty(record.Identifier))
                attempts[record.Identifier] = Math.Max(attempts.GetValueOrDefault(record.Identifier), record.Attempt);
        }
        HashSet<Guid> removed = [];
        foreach (AdoTimelineRecord record in tree.Ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((record.ParentId is Guid parent && removed.Contains(parent))
                || (!string.IsNullOrEmpty(record.Identifier) && record.Attempt < attempts[record.Identifier])) removed.Add(record.Id);
        }
        bool canceled = string.Equals(build.Result, "canceled", StringComparison.OrdinalIgnoreCase);
        HashSet<Guid> hasQualifyingDescendant = [];
        HashSet<Guid> selected = [];
        foreach (AdoTimelineRecord record in tree.Ordered.Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (removed.Contains(record.Id)) continue;
            bool qualifies = string.Equals(record.Result, "failed", StringComparison.OrdinalIgnoreCase)
                || (canceled && string.Equals(record.Result, "canceled", StringComparison.OrdinalIgnoreCase))
                || (includeWarnings && string.Equals(record.Result, "succeededWithIssues", StringComparison.OrdinalIgnoreCase));
            bool descendant = hasQualifyingDescendant.Contains(record.Id);
            if (qualifies && !descendant) selected.Add(record.Id);
            if ((qualifies || descendant) && record.ParentId is Guid parent) hasQualifyingDescendant.Add(parent);
        }
        List<AdoBuildFailure> failures = [];
        foreach (AdoTimelineRecord record in tree.Ordered)
        {
            if (!selected.Contains(record.Id)) continue;
            Stack<string> path = new();
            AdoTimelineRecord? ancestor = record;
            while (ancestor is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                path.Push(ancestor.Name);
                ancestor = ancestor.ParentId is Guid parent ? tree.ById[parent] : null;
            }
            failures.Add(new AdoBuildFailure
            {
                BuildId = build.Id, BuildNumber = build.BuildNumber, Definition = build.Definition, Branch = build.SourceBranch,
                Path = string.Join(" › ", path), RecordType = record.Type, RecordName = record.Name, Result = record.Result!,
                ErrorIssues = Array.AsReadOnly(record.Issues.Where(issue => string.Equals(issue.Type, "error", StringComparison.OrdinalIgnoreCase)).ToArray()),
                LogId = record.LogId, LogLineCount = record.LogId is int logId ? lineCounts.GetValueOrDefault(logId) : null,
                ErrorCount = record.ErrorCount, WarningCount = record.WarningCount, StartTime = record.StartTime,
                FinishTime = record.FinishTime, Attempt = record.Attempt, CollectionUri = build.CollectionUri,
            });
        }
        return failures.AsReadOnly();
    }
}
