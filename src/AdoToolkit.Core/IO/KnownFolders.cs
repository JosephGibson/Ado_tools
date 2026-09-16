using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AdoToolkit.Core.IO;

public static partial class KnownFolders
{
    [SupportedOSPlatform("windows")]
    public static string Downloads(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        Guid downloads = new("374DE290-123F-4565-9164-39C4925E467B");
        nint path = 0;
        try
        {
            int result = SHGetKnownFolderPath(in downloads, 0, 0, out path);
            if (result < 0) Marshal.ThrowExceptionForHR(result);
            return Marshal.PtrToStringUni(path) ?? throw new AdoFileOutputException(Messages.Get(AdoMessage.DownloadsUnavailable, culture));
        }
        catch (COMException error)
        {
            throw new AdoFileOutputException(Messages.Get(AdoMessage.DownloadsUnavailable, culture), error);
        }
        finally
        {
            if (path != 0) Marshal.FreeCoTaskMem(path);
        }
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHGetKnownFolderPath(in Guid folderId, uint flags, nint token, out nint path);
}
