namespace AdoToolkit.Core.IO;

// Fault-injection points, one per §13.4 step. Stamp, NoClobber, Download, Render and Validate run
// after the step's work; Rename, Move and Cleanup run immediately before the step's operation.
internal enum GenerationCommitStep
{
    Stamp = 1,
    NoClobber = 2,
    Download = 3,
    Render = 4,
    Validate = 5,
    Rename = 6,
    Move = 7,
    Cleanup = 8,
}
