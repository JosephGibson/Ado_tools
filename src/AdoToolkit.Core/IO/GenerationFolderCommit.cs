using System.Text;

namespace AdoToolkit.Core.IO;

// §13.4: a report and its attachment folder cannot be replaced atomically together. This order
// keeps the committed report consistent with the folder it links to after a failure or a stop
// at any step; the worst case is an orphaned folder.
internal sealed class GenerationFolderCommit
{
    private static readonly EnumerationOptions DirectChildren = new()
    {
        RecurseSubdirectories = false, AttributesToSkip = 0, IgnoreInaccessible = false,
        ReturnSpecialDirectories = false, MatchType = MatchType.Simple,
    };
    private readonly Action<GenerationCommitStep, string>? fault;

    internal GenerationFolderCommit(Action<GenerationCommitStep, string>? fault = null) => this.fault = fault;

    // Steps 1–2. Nothing is created here.
    internal GenerationFolderPlan Plan(string reportPath, DateTimeOffset generatedAt, bool noClobber, CultureInfo culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        ArgumentNullException.ThrowIfNull(culture);
        string path = Path.GetFullPath(reportPath);
        string baseName = Path.GetFileNameWithoutExtension(path);
        if (baseName.Length == 0)
            throw new AdoFileOutputException(Messages.Get(AdoMessage.TestFailureReportPathInvalid, culture, path));
        string stamp = ReportFileNames.GenerationStamp(generatedAt);
        GenerationFolderPlan plan = new(path, Path.GetDirectoryName(path)!, baseName, stamp, ReportFileNames.AttachmentFolder(baseName, stamp));
        try
        {
            fault?.Invoke(GenerationCommitStep.Stamp, path);
            if (noClobber && Path.Exists(path)) throw FileOutput(path, culture);
            fault?.Invoke(GenerationCommitStep.NoClobber, path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw FileOutput(path, culture, error);
        }
        return plan;
    }

    // Steps 3–8. download fills the temporary folder and returns false when it wrote no file; then
    // no folder is kept and steps 5 (file checks) and 6 are skipped. Step 8 always runs.
    internal async Task<GenerationCommitResult> CommitAsync(GenerationFolderPlan plan, bool noClobber, bool downloadAttachments,
        Func<string, CancellationToken, Task<bool>> download, Action<TextWriter> render, Action<string, string?> validate,
        Action<string> warning, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(download);
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(validate);
        ArgumentNullException.ThrowIfNull(warning);
        ArgumentNullException.ThrowIfNull(culture);
        string random = Guid.NewGuid().ToString("N");
        string temporaryFolder = Path.Combine(plan.Directory, "." + plan.BaseName + ".files." + random + ".tmp");
        string temporaryReport = Path.Combine(plan.Directory, "." + Path.GetFileName(plan.ReportPath) + "." + random + ".tmp");
        bool folderCreated = false, reportCreated = false, renamed = false, hasFolder = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (downloadAttachments)
            {
                Directory.CreateDirectory(temporaryFolder);
                folderCreated = true;
                hasFolder = await download(temporaryFolder, cancellationToken).ConfigureAwait(false);
                fault?.Invoke(GenerationCommitStep.Download, temporaryFolder);
                if (!hasFolder)
                {
                    DeleteTree(temporaryFolder, null, culture);
                    folderCreated = false;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            using (FileStream stream = new(temporaryReport, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                reportCreated = true;
                using (StreamWriter writer = new(stream, new UTF8Encoding(false, true), 4096, leaveOpen: true))
                {
                    writer.NewLine = "\n";
                    render(writer);
                }
                stream.Flush(flushToDisk: true);
            }
            fault?.Invoke(GenerationCommitStep.Render, temporaryReport);
            cancellationToken.ThrowIfCancellationRequested();
            validate(temporaryReport, hasFolder ? temporaryFolder : null);
            fault?.Invoke(GenerationCommitStep.Validate, temporaryReport);
            cancellationToken.ThrowIfCancellationRequested();
            if (hasFolder)
            {
                fault?.Invoke(GenerationCommitStep.Rename, plan.FolderPath);
                // Directory.Move never replaces: an existing name fails the step.
                Directory.Move(temporaryFolder, plan.FolderPath);
                renamed = true;
            }
            fault?.Invoke(GenerationCommitStep.Move, plan.ReportPath);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryReport, plan.ReportPath, overwrite: !noClobber);
            reportCreated = false;
        }
        catch (Exception error)
        {
            // The previous report and its folder were never touched; remove only what this run made.
            if (reportCreated) DeleteQuietly(temporaryReport);
            if (renamed) DeleteTreeQuietly(plan.FolderPath, culture);
            else if (folderCreated) DeleteTreeQuietly(temporaryFolder, culture);
            if (error is IOException or UnauthorizedAccessException or InvalidDataException)
                throw FileOutput(plan.ReportPath, culture, error);
            throw;
        }
        RemovePreviousGenerations(plan, warning, culture);
        return new GenerationCommitResult(new FileInfo(plan.ReportPath), hasFolder ? new DirectoryInfo(plan.FolderPath) : null);
    }

    // Step 8: only direct children named "<base>.files-<stamp>", never through a reparse point.
    // Every failure here is a warning; the new report is already committed.
    private void RemovePreviousGenerations(GenerationFolderPlan plan, Action<string> warning, CultureInfo culture)
    {
        FileSystemInfo[] entries;
        try { entries = new DirectoryInfo(plan.Directory).EnumerateFileSystemInfos("*", DirectChildren).ToArray(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            warning(Messages.Get(AdoMessage.AttachmentFolderNotDeleted, culture, plan.Directory));
            return;
        }
        foreach (FileSystemInfo entry in entries)
        {
            if (!ReportFileNames.IsAttachmentFolder(entry.Name, plan.BaseName)
                || string.Equals(entry.Name, plan.FolderName, StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                warning(Messages.Get(AdoMessage.AttachmentFolderSkipped, culture, entry.FullName));
                continue;
            }
            if (entry is not DirectoryInfo) continue;
            try
            {
                fault?.Invoke(GenerationCommitStep.Cleanup, entry.FullName);
                if (DeleteTree(entry.FullName, warning, culture)) continue;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            warning(Messages.Get(AdoMessage.AttachmentFolderNotDeleted, culture, entry.FullName));
        }
    }

    // Deletes files and plain subfolders. A nested reparse point is left in place with a warning,
    // so its parent stays too and the result is false.
    private static bool DeleteTree(string path, Action<string>? warning, CultureInfo culture)
    {
        bool complete = true;
        foreach (FileSystemInfo entry in new DirectoryInfo(path).EnumerateFileSystemInfos("*", DirectChildren).ToArray())
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                warning?.Invoke(Messages.Get(AdoMessage.AttachmentFolderSkipped, culture, entry.FullName));
                complete = false;
            }
            else if (entry is DirectoryInfo) complete &= DeleteTree(entry.FullName, warning, culture);
            else entry.Delete();
        }
        if (complete) Directory.Delete(path, recursive: false);
        return complete;
    }

    private static void DeleteTreeQuietly(string path, CultureInfo culture)
    {
        try { if (Directory.Exists(path)) DeleteTree(path, null, culture); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    private static void DeleteQuietly(string path)
    {
        try { File.Delete(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    private static AdoFileOutputException FileOutput(string path, CultureInfo culture, Exception? error = null) =>
        new(Messages.Get(AdoMessage.FileOutput, culture, path), error);
}
