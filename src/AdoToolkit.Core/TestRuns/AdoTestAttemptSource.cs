using System.Diagnostics.CodeAnalysis;

namespace AdoToolkit.Core.TestRuns;

// The member names are the public contract in spec §15.11 and cannot be renamed.
[SuppressMessage("Naming", "CA1720:Identifier contains type name",
    Justification = "Single is the attempt source named by the specification (§15.11).")]
public enum AdoTestAttemptSource { Single, Rerun, RunAttempt }
