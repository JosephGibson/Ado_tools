using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AdoToolkit.Core.IO;

public static partial class KnownFolders
{
    internal delegate int KnownFolderPathReader(in Guid folderId, uint flags, nint token, out nint path);

    [SupportedOSPlatform("windows")]
    public static string Downloads(CultureInfo culture) => Downloads(culture, SHGetKnownFolderPath);

    // A redirected folder on an absent drive or share fails with a Win32 code, which .NET maps to
    // FileNotFoundException and other non-COM types. Every failing result is the same output error.
    internal static string Downloads(CultureInfo culture, KnownFolderPathReader read)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(read);
        Guid downloads = new("374DE290-123F-4565-9164-39C4925E467B");
        nint path = 0;
        try
        {
            int result = read(in downloads, 0, 0, out path);
            if (result < 0)
                throw new AdoFileOutputException(Messages.Get(AdoMessage.DownloadsUnavailable, culture), Marshal.GetExceptionForHR(result));
            return Marshal.PtrToStringUni(path) ?? throw new AdoFileOutputException(Messages.Get(AdoMessage.DownloadsUnavailable, culture));
        }
        finally
        {
            if (path != 0) Marshal.FreeCoTaskMem(path);
        }
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHGetKnownFolderPath(in Guid folderId, uint flags, nint token, out nint path);
}
