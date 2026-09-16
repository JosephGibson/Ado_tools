using System.Diagnostics;

namespace AdoToolkit.Core.IO;

public sealed class ShellDocumentLauncher : IDocumentLauncher
{
    public void Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using Process? process = Process.Start(new ProcessStartInfo(Path.GetFullPath(path)) { UseShellExecute = true });
    }
}
