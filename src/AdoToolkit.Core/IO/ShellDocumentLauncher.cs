using System.Diagnostics;

namespace AdoToolkit.Core.IO;

public sealed class ShellDocumentLauncher : IDocumentLauncher
{
    // The shell runs rather than shows many file types (.cmd, .js, .hta and others), and report
    // text comes from the server, so only these document types are ever handed to it.
    private static readonly string[] DocumentExtensions = [".html", ".htm", ".md", ".markdown", ".json", ".txt"];

    public static bool CanOpen(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return DocumentExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    public void Open(string path)
    {
        if (!CanOpen(path)) throw new ArgumentException(null, nameof(path));
        using Process? process = Process.Start(new ProcessStartInfo(Path.GetFullPath(path)) { UseShellExecute = true });
    }
}
