using System.Text;

namespace AdoToolkit.Core.IO;

public sealed class AtomicFileWriter
{
    private readonly Action<AtomicWriteStage, string>? fault;

    public AtomicFileWriter() { }

    internal AtomicFileWriter(Action<AtomicWriteStage, string> fault) => this.fault = fault;

    // The caller owns source. Source read failures must reach the HTTP retry policy unchanged;
    // only destination I/O failures become AdoFileOutputException.
    public async Task<FileInfo> WriteAsync(string destination, Stream source, Action<string> validate,
        CultureInfo culture, bool noClobber = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(validate);
        ArgumentNullException.ThrowIfNull(culture);
        string path = Path.GetFullPath(destination);
        string temporary = Path.Combine(Path.GetDirectoryName(path)!,
            "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        bool created = false;
        Exception? readError = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (noClobber && File.Exists(path))
                throw new AdoFileOutputException(Messages.Get(AdoMessage.FileOutput, culture, path));
            using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                created = true;
                fault?.Invoke(AtomicWriteStage.TemporaryCreated, temporary);
                byte[] buffer = new byte[81920];
                while (true)
                {
                    int count;
                    try { count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false); }
                    catch (Exception error) { readError = error; throw; }
                    if (count == 0) break;
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                }
                fault?.Invoke(AtomicWriteStage.Rendered, temporary);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
                fault?.Invoke(AtomicWriteStage.Flushed, temporary);
            }
            cancellationToken.ThrowIfCancellationRequested();
            validate(temporary);
            fault?.Invoke(AtomicWriteStage.Validated, temporary);
            fault?.Invoke(AtomicWriteStage.BeforeCommit, temporary);
            cancellationToken.ThrowIfCancellationRequested();
            AtomicFileReplace.Commit(temporary, path, noClobber);
            return new FileInfo(path);
        }
        catch (Exception error) when (!ReferenceEquals(error, readError)
            && error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new AdoFileOutputException(Messages.Get(AdoMessage.FileOutput, culture, path), error);
        }
        finally
        {
            if (created) DeleteTemporary(temporary, path, culture);
        }
    }

    private static void DeleteTemporary(string temporary, string destination, CultureInfo culture)
    {
        try { File.Delete(temporary); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new AdoFileOutputException(Messages.Get(AdoMessage.FileOutput, culture, destination), error);
        }
    }

    public FileInfo Write(string destination, Action<TextWriter> render, Action<string> validate,
        CultureInfo culture, bool noClobber = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(render);
        ArgumentNullException.ThrowIfNull(validate);
        ArgumentNullException.ThrowIfNull(culture);
        string path = Path.GetFullPath(destination);
        string temporary = Path.Combine(Path.GetDirectoryName(path)!,
            "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        bool created = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (noClobber && File.Exists(path))
                throw new AdoFileOutputException(Messages.Get(AdoMessage.FileOutput, culture, path));
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                fault?.Invoke(AtomicWriteStage.TemporaryCreated, temporary);
                using (StreamWriter writer = new(stream, new UTF8Encoding(false, true), 4096, leaveOpen: true))
                {
                    writer.NewLine = "\n";
                    render(writer);
                    fault?.Invoke(AtomicWriteStage.Rendered, temporary);
                    writer.Flush();
                }
                stream.Flush(flushToDisk: true);
                fault?.Invoke(AtomicWriteStage.Flushed, temporary);
            }
            cancellationToken.ThrowIfCancellationRequested();
            validate(temporary);
            fault?.Invoke(AtomicWriteStage.Validated, temporary);
            fault?.Invoke(AtomicWriteStage.BeforeCommit, temporary);
            cancellationToken.ThrowIfCancellationRequested();
            AtomicFileReplace.Commit(temporary, path, noClobber);
            return new FileInfo(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new AdoFileOutputException(Messages.Get(AdoMessage.FileOutput, culture, path), error);
        }
        finally
        {
            if (created && File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
