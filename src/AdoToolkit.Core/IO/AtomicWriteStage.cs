namespace AdoToolkit.Core.IO;

internal enum AtomicWriteStage
{
    TemporaryCreated,
    Rendered,
    Flushed,
    Validated,
    BeforeCommit,
}
