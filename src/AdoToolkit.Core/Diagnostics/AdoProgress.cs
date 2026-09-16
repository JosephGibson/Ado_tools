namespace AdoToolkit.Core.Diagnostics;

// Counts only; the shell localizes activity text in its own culture.
public sealed class AdoProgress
{
    public AdoProgressPhase Phase { get; init; }
    public int Completed { get; init; }
    // Null while the total is still unknown (for example, Shared Steps levels).
    public int? Total { get; init; }
}
