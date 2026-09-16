namespace AdoToolkit.Core.TestRuns;

// Storage is compared ordinally ignoring case; the automated name is case-sensitive because C#
// names are (§15.10). A result without an automated name is its own identity, keyed by run and
// result ID, and never matches a result in another build.
internal sealed class TestIdentity : IEquatable<TestIdentity>
{
    private TestIdentity(string? storage, string? name, int runId, int resultId)
    {
        Storage = storage;
        Name = name;
        RunId = runId;
        ResultId = resultId;
    }

    internal string? Storage { get; }
    internal string? Name { get; }
    internal int RunId { get; }
    internal int ResultId { get; }
    internal bool IsGrouped => !string.IsNullOrEmpty(Name);

    internal static TestIdentity ForAutomated(string? storage, string name) => new(storage, name, 0, 0);

    internal static TestIdentity ForUngrouped(int runId, int resultId) => new(null, null, runId, resultId);

    public bool Equals(TestIdentity? other)
    {
        if (other is null) return false;
        if (IsGrouped != other.IsGrouped) return false;
        if (!IsGrouped) return RunId == other.RunId && ResultId == other.ResultId;
        return string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(Storage ?? "", other.Storage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => Equals(obj as TestIdentity);

    public override int GetHashCode() => IsGrouped
        ? HashCode.Combine(StringComparer.Ordinal.GetHashCode(Name!), StringComparer.OrdinalIgnoreCase.GetHashCode(Storage ?? ""))
        : HashCode.Combine(RunId, ResultId);
}
