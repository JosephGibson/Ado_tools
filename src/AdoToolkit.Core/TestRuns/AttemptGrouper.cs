namespace AdoToolkit.Core.TestRuns;

internal static class AttemptGrouper
{
    // Parent retries reset child attempt counters; compare stage, phase, then job attempts.
    // Missing references retain the date/ID fallback (§15.10).
    internal static IReadOnlyList<AdoTestRun> OrderRuns(IEnumerable<AdoTestRun> runs) => runs
        .OrderBy(static run => run.StageAttempt ?? 0)
        .ThenBy(static run => run.PhaseAttempt ?? 0)
        .ThenBy(static run => run.PipelineAttempt ?? 0)
        .ThenBy(static run => run.StartedDate ?? DateTimeOffset.MinValue)
        .ThenBy(static run => run.Id)
        .ToArray();

    // Groups pass-1 records by identity. Records keep run order, then result ID within a run.
    // Diagnostics are collected for the reported build only; history builds pass null.
    internal static IReadOnlyList<TestIdentityGroup> Group(IEnumerable<TestResultRecord> records,
        CultureInfo culture, List<AdoDiagnostic>? diagnostics, CancellationToken cancellationToken)
    {
        Dictionary<TestIdentity, List<TestResultRecord>> groups = [];
        List<TestIdentity> order = [];
        foreach (TestResultRecord record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TestIdentity identity;
            if (string.IsNullOrEmpty(record.AutomatedTestName))
            {
                identity = TestIdentity.ForUngrouped(record.RunId, record.ResultId);
                diagnostics?.Add(DiagnosticMessageRenderer.Create(DiagnosticCodes.UngroupedTestResult, culture,
                    arguments: [record.ResultId.ToString(CultureInfo.InvariantCulture), record.RunId.ToString(CultureInfo.InvariantCulture)]));
            }
            else identity = TestIdentity.ForAutomated(record.AutomatedTestStorage, record.AutomatedTestName);
            if (!groups.TryGetValue(identity, out List<TestResultRecord>? bucket))
            {
                bucket = [];
                groups.Add(identity, bucket);
                order.Add(identity);
            }
            bucket.Add(record);
        }
        List<TestIdentityGroup> result = [];
        foreach (TestIdentity identity in order)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(new TestIdentityGroup
            {
                Identity = identity,
                Records = groups[identity]
                    .OrderBy(static record => record.RunOrder)
                    .ThenBy(static record => record.ResultId)
                    .ToArray(),
            });
        }
        return result.AsReadOnly();
    }

    // Report order: Failed before Flaky, then storage, automated name, result ID (§15.10).
    internal static IReadOnlyList<TestIdentityGroup> ReportOrder(IEnumerable<TestIdentityGroup> groups) => groups
        .OrderBy(static group => group.ProvisionalClassification == AdoTestFailureClassification.Failed ? 0 : 1)
        .ThenBy(static group => group.Storage ?? "", StringComparer.Ordinal)
        .ThenBy(static group => group.Name ?? "", StringComparer.Ordinal)
        .ThenBy(static group => group.FirstResultId)
        .ToArray();

    // The last name segment with its arguments: a dot inside the argument list does not split, so
    // Tests.Login("a.b") is Login("a.b").
    internal static string ShortName(string? automatedName, string? title)
    {
        string source = automatedName ?? title ?? "";
        int separator = LastSeparator(source, Arguments(source));
        // Keep the whole text when the last segment would be empty.
        return separator >= 0 && separator < source.Length - 1 ? source[(separator + 1)..] : source;
    }

    // The segment before the short name (the class of a method), or null when there is none.
    internal static string? ClassName(string? automatedName)
    {
        if (automatedName is null) return null;
        int end = LastSeparator(automatedName, Arguments(automatedName));
        int start = LastSeparator(automatedName, end) + 1;
        return end > start ? automatedName[start..end] : null;
    }

    private static int Arguments(string name) => name.IndexOf('(', StringComparison.Ordinal) is int open and >= 0 ? open : name.Length;

    private static int LastSeparator(string name, int end) => end <= 0 ? -1 : name.LastIndexOfAny(['.', '+'], end - 1);
}
