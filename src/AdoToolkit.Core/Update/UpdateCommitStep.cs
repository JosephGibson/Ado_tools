namespace AdoToolkit.Core.Update;

// The steps of UpdateFolderCommit, in order. A test's fault hook runs before each one; Commit, the
// move onto the target, is the boundary between rolling back and keeping the new version.
internal enum UpdateCommitStep
{
    Lock,
    Staging,
    Build,
    Backup,
    Commit,
    Rollback,
    Cleanup,
}
