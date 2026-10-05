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

    // A spreadsheet reads a cell that starts like a formula as one. The failed-test export writes
    // every text cell of its CSV file formula-safe and opens it without asking CanOpen; an export
    // that asks CanOpen, such as a Test Case document named .csv, is never opened.
    public void Open(string path)
    {
        if (!CanOpen(path) && !string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(null, nameof(path));
        using Process? process = Process.Start(new ProcessStartInfo(Path.GetFullPath(path)) { UseShellExecute = true });
    }
}
