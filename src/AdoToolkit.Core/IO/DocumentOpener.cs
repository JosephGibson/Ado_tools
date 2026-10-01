using System.ComponentModel;

namespace AdoToolkit.Core.IO;

// -Open runs after the report is committed. A shell failure (no application for the file type,
// a blocked launch) is a warning, so the caller still receives the written file.
internal static class DocumentOpener
{
    internal static void Open(IDocumentLauncher launcher, string path, CultureInfo culture, Action<string> warning)
    {
        try { launcher.Open(path); }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or IOException
            or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            warning(Messages.Get(AdoMessage.ExportOpenFailed, culture, path, error.Message));
        }
    }
}
