using System.IO;

namespace AdoToolkit.Commands.Infrastructure;

// A file URI that names exactly the given path. Uri(path) reads "%41" in a name as an escaped "A"
// and leaves '[' and ']' raw, so every segment after the drive or server is escaped here instead.
internal static class FileUris
{
    internal static string FromPath(string path)
    {
        string full = Path.GetFullPath(path);
        // Device paths (\\?\ and \\.\) keep the framework conversion.
        if (full.StartsWith(@"\\?\", StringComparison.Ordinal) || full.StartsWith(@"\\.\", StringComparison.Ordinal))
            return new Uri(full).AbsoluteUri;
        bool unc = full.StartsWith(@"\\", StringComparison.Ordinal);
        string[] segments = (unc ? full[2..] : full).Split(Path.DirectorySeparatorChar);
        return new Uri((unc ? "file://" : "file:///") + segments[0] + "/"
            + string.Join('/', segments.Skip(1).Select(Uri.EscapeDataString))).AbsoluteUri;
    }
}
