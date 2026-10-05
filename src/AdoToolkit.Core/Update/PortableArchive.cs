using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AdoToolkit.Core.Update;

// The portable zip as New-AdoPortableArchive writes it: the launchers, README.txt and bundle.json at
// the top, the seven module files under module/, and PowerShell under runtime/. Entry names follow the
// rules that tools/package/Portable.Common.ps1 applies to the PowerShell archive. Top-level and module
// paths come from constants; runtime paths come from the checked entry names, the one place where
// remote text becomes a path.
internal static partial class PortableArchive
{
    internal const string Launcher = "Start-AdoToolkit.cmd";
    internal const string ModuleFolder = "module";
    internal const string RuntimeFolder = "runtime";
    private const string BundleName = "bundle.json";
    private const string Architecture = "win-x64";
    private static readonly string[] TopFiles = [Launcher, "Start-AdoToolkit.ps1", "README.txt", BundleName];

    // The required runtime files of New-AdoPortableArchive in tools/package/Portable.Common.ps1.
    internal static readonly string[] RuntimeFiles =
    [
        "pwsh.exe", "pwsh.dll", "pwsh.runtimeconfig.json", "System.Management.Automation.dll",
        "coreclr.dll", "hostfxr.dll", "hostpolicy.dll", "System.Private.CoreLib.dll", "LICENSE.txt", "ThirdPartyNotices.txt",
    ];

    [GeneratedRegex(@"[\\:\x00-\x1f<>""|?*]", RegexOptions.CultureInvariant, 100)]
    private static partial Regex UnsafeCharacter();

    [GeneratedRegex(@"^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", RegexOptions.CultureInvariant, 100)]
    private static partial Regex ReservedName();

    internal static void Extract(ZipArchive archive, Version version, string folder, UpdateHttp.Limits limits, string archiveName,
        Func<string, ModuleManifestFacts?> readManifest, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(limits);
        if (archive.Entries.Count > limits.PortableEntries) throw ModuleArchive.ArchiveLayout(archiveName, culture);
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        List<(ZipArchiveEntry Entry, string[] Segments)> files = [];
        List<string[]> directories = [];
        long total = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string name = entry.FullName;
            bool directory = name.EndsWith('/');
            string[] segments = name.TrimEnd('/').Split('/');
            if (name.StartsWith('/') || UnsafeCharacter().IsMatch(name) || segments.Any(IsUnsafeSegment)
                || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || !names.Add(name.TrimEnd('/')))
                throw ModuleArchive.ArchiveEntry(archiveName, name, culture);
            if (directory)
            {
                if (entry.Length != 0) throw ModuleArchive.ArchiveEntry(archiveName, name, culture);
                directories.Add(segments);
                continue;
            }
            if (entry.Length > limits.EntryBytes)
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.UpdateArchiveEntryTooLarge, culture, archiveName,
                    UpdateGuards.Printable(name, 260))) { Operation = UpdateHttp.ArchiveOperation };
            total += entry.Length;
            if (total > limits.PortableBytes) throw ModuleArchive.ArchiveLayout(archiveName, culture);
            files.Add((entry, segments));
        }
        CheckLayout(files, directories, names, archiveName, culture);
        CheckBundle(files, version, limits, archiveName, culture);
        UpdateGuards.EnsureFreeSpace(folder, total, limits.FreeSpaceMargin, version, culture);
        Directory.CreateDirectory(folder);
        string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
        // Module folders come from the constant layout with their files; only runtime folders, which
        // may be empty, are created from their entries.
        foreach (string[] segments in directories)
            if (string.Equals(segments[0], RuntimeFolder, StringComparison.Ordinal))
                Directory.CreateDirectory(Contained(prefix, Path.Combine([folder, .. segments]), segments, archiveName, culture));
        foreach ((ZipArchiveEntry entry, string[] segments) in files)
        {
            string output = Contained(prefix, FilePath(folder, segments), segments, archiveName, culture);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            ModuleArchive.WriteEntry(entry, output, archiveName, culture);
        }
        ModuleArchive.Check(Path.Combine(folder, ModuleFolder), version, runningPowerShell: null, readManifest, archiveName, culture);
    }

    // Portable.Common.ps1: no '', '.' or '..' segment, no trailing dot or space, no reserved device name.
    private static bool IsUnsafeSegment(string segment) =>
        segment is "" or "." or ".." || segment.EndsWith('.') || segment.EndsWith(' ') || ReservedName().IsMatch(segment);

    private static void CheckLayout(List<(ZipArchiveEntry Entry, string[] Segments)> files, List<string[]> directories, HashSet<string> names,
        string archiveName, CultureInfo culture)
    {
        HashSet<string> fileNames = new(files.Select(static file => string.Join('/', file.Segments)), StringComparer.OrdinalIgnoreCase);
        // A file must not also be a folder of another entry.
        foreach (string name in names)
            for (int slash = name.LastIndexOf('/'); slash > 0; slash = name.LastIndexOf('/', slash - 1))
                if (fileNames.Contains(name[..slash])) throw ModuleArchive.ArchiveEntry(archiveName, name, culture);
        List<string> top = [], module = [];
        HashSet<string> runtime = new(StringComparer.OrdinalIgnoreCase);
        foreach ((ZipArchiveEntry entry, string[] segments) in files)
        {
            if (segments.Length == 1 && TopFiles.Contains(segments[0], StringComparer.Ordinal)) top.Add(segments[0]);
            else if (segments.Length > 1 && string.Equals(segments[0], ModuleFolder, StringComparison.Ordinal)) module.Add(string.Join('/', segments[1..]));
            else if (segments.Length > 1 && string.Equals(segments[0], RuntimeFolder, StringComparison.Ordinal)) runtime.Add(string.Join('/', segments[1..]));
            else throw ModuleArchive.ArchiveEntry(archiveName, entry.FullName, culture);
        }
        foreach (string[] segments in directories)
            if (!string.Equals(segments[0], ModuleFolder, StringComparison.Ordinal) && !string.Equals(segments[0], RuntimeFolder, StringComparison.Ordinal))
                throw ModuleArchive.ArchiveEntry(archiveName, string.Join('/', segments) + "/", culture);
        if (top.Count != TopFiles.Length
            || !module.Order(StringComparer.Ordinal).SequenceEqual(ModuleArchive.Layout.Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || !RuntimeFiles.All(runtime.Contains))
            throw ModuleArchive.ArchiveLayout(archiveName, culture);
    }

    // bundle.json names the module version and the architecture; both must be this release's.
    private static void CheckBundle(List<(ZipArchiveEntry Entry, string[] Segments)> files, Version version, UpdateHttp.Limits limits,
        string archiveName, CultureInfo culture)
    {
        ZipArchiveEntry bundle = files.First(static file => file.Segments.Length == 1 && string.Equals(file.Segments[0], BundleName, StringComparison.Ordinal)).Entry;
        if (bundle.Length > limits.BundleBytes) throw ModuleArchive.ArchiveLayout(archiveName, culture);
        byte[] content = new byte[bundle.Length];
        try
        {
            using Stream stream = bundle.Open();
            stream.ReadExactly(content);
            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("ModuleVersion", out JsonElement moduleVersion) && moduleVersion.ValueKind == JsonValueKind.String
                && string.Equals(moduleVersion.GetString(), UpdateHttp.VersionText(version), StringComparison.Ordinal)
                && root.TryGetProperty("Architecture", out JsonElement architecture) && architecture.ValueKind == JsonValueKind.String
                && string.Equals(architecture.GetString(), Architecture, StringComparison.Ordinal)) return;
        }
        // A string that cannot be read, such as a lone surrogate escape, is an InvalidOperationException.
        catch (Exception error) when (error is JsonException or InvalidDataException or EndOfStreamException or InvalidOperationException)
        {
            throw ModuleArchive.Mismatch(archiveName, version, culture);
        }
        throw ModuleArchive.Mismatch(archiveName, version, culture);
    }

    // CheckLayout has placed every file: a top file, a module file of the layout, or a runtime file.
    private static string FilePath(string folder, string[] segments)
    {
        if (segments.Length == 1) return Path.Combine(folder, Array.Find(TopFiles, item => string.Equals(item, segments[0], StringComparison.Ordinal))!);
        if (!string.Equals(segments[0], ModuleFolder, StringComparison.Ordinal)) return Path.Combine([folder, RuntimeFolder, .. segments[1..]]);
        string relative = string.Join('/', segments[1..]);
        return Path.Combine(folder, ModuleFolder, Array.Find(ModuleArchive.Layout, item => string.Equals(item, relative, StringComparison.Ordinal))!);
    }

    private static string Contained(string prefix, string path, string[] segments, string archiveName, CultureInfo culture)
    {
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw ModuleArchive.ArchiveEntry(archiveName, string.Join('/', segments), culture);
        return full;
    }
}
