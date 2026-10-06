namespace AdoToolkit.Core.TestRuns;

// A build reduced to the window metadata history needs.
internal sealed class HistoryBuild
{
    internal required int Id { get; init; }
    internal required string BuildNumber { get; init; }
    internal string? SourceBranch { get; init; }
    internal DateTimeOffset? FinishTime { get; init; }
    internal string? Result { get; init; }
}

// One history build's derived counts and per-identity cells, from pass-1 data only (§15.12).
internal sealed class HistoryBuildData
{
    internal bool IsAvailable { get; init; }
    internal int Passed { get; init; }
    internal int Failed { get; init; }
    internal int Flaky { get; init; }
    internal int Other { get; init; }
    internal IReadOnlyDictionary<TestIdentity, AdoTestHistoryOutcome> Cells { get; init; }
        = new Dictionary<TestIdentity, AdoTestHistoryOutcome>();
    // The tests whose failed results listed a message, with the starts of those messages (D-8).
    internal IReadOnlyDictionary<TestIdentity, HistoryErrors> Errors { get; init; } = new Dictionary<TestIdentity, HistoryErrors>();

    internal static HistoryBuildData Unavailable { get; } = new() { IsAvailable = false };

    // Bars are tallied from the same cells they display, so the two always agree.
    internal static HistoryBuildData FromGroups(IReadOnlyList<TestIdentityGroup> groups, CancellationToken cancellationToken)
    {
        Dictionary<TestIdentity, AdoTestHistoryOutcome> cells = [];
        Dictionary<TestIdentity, HistoryErrors> errors = [];
        TextPool pool = new();
        int passed = 0, failed = 0, flaky = 0, other = 0;
        foreach (TestIdentityGroup group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AdoTestHistoryOutcome cell = group.Cell;
            cells[group.Identity] = cell;
            if (ErrorsOf(group, pool) is { } found) errors[group.Identity] = found;
            switch (cell)
            {
                case AdoTestHistoryOutcome.Flaky: passed++; flaky++; break;
                case AdoTestHistoryOutcome.Passed: passed++; break;
                case AdoTestHistoryOutcome.Failed: failed++; break;
                default: other++; break;
            }
        }
        return new HistoryBuildData
        {
            IsAvailable = true,
            Passed = passed,
            Failed = failed,
            Flaky = flaky,
            Other = other,
            Cells = cells,
            Errors = errors,
        };
    }

    // The distinct starts of the failed results' messages, in record order, at most MaximumStarts,
    // each with the pipeline key of its first result; null when none is kept.
    private static HistoryErrors? ErrorsOf(TestIdentityGroup group, TextPool pool)
    {
        List<string> starts = [], keys = [];
        bool dropped = false;
        foreach (TestResultRecord record in group.Records)
        {
            if (record.OutcomeClass != AdoTestOutcomeClass.Failure || record.ErrorMessage is not { } start || starts.Contains(start, StringComparer.Ordinal)) continue;
            if (starts.Count == HistoryErrors.MaximumStarts || pool.Keep(start) is not { } kept) { dropped = true; continue; }
            starts.Add(kept);
            keys.Add(record.PipelineKey);
        }
        return starts.Count == 0 ? null : new HistoryErrors { Starts = starts.AsReadOnly(), Keys = keys.AsReadOnly(), Dropped = dropped };
    }

    // The starts that one build keeps: each text once, at most HistoryErrors.MaximumCharacters in all.
    private sealed class TextPool
    {
        private readonly Dictionary<string, string> texts = new(StringComparer.Ordinal);
        private int characters;

        // The kept instance of text, or null when it no longer fits.
        internal string? Keep(string text)
        {
            if (texts.TryGetValue(text, out string? kept)) return kept;
            if (characters + text.Length > HistoryErrors.MaximumCharacters) return null;
            characters += text.Length;
            texts.Add(text, text);
            return text;
        }
    }
}
