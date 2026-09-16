namespace AdoToolkit.Core.IO;

public static class AtomicFileReplace
{
    internal static void Commit(string temporary, string destination, bool noClobber = false) =>
        File.Move(temporary, destination, overwrite: !noClobber);

    public static void Write(string destination, ReadOnlySpan<byte> contents, CultureInfo culture, Action<string>? validate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(culture);
        string path = Path.GetFullPath(destination);
        string directory = Path.GetDirectoryName(path)!;
        string temporary = Path.Combine(directory, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Directory.CreateDirectory(directory);
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(contents);
                stream.Flush(flushToDisk: true);
            }
            validate?.Invoke(temporary);
            Commit(temporary, path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new AdoFileOutputException(Messages.Get(AdoMessage.FileOutput, culture, path), error);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
