using System.IO.Compression;
using System.Reflection;

namespace AdoToolkit.Core.Update;

// The module-only zip, checked as Install-AdoToolkit.ps1 checks it (steps 2 and 3): one version
// folder under AdoToolkit/ holding exactly the seven module files. The updater adds that the folder
// is the tag's version, an entry count cap, a size cap on each copy, and the identity of each DLL.
// Update.Pester.ps1 runs one table of zips through the installer and the cmdlet, so the rules
// cannot drift apart.
internal static class ModuleArchive
{
    internal const string ManifestName = "AdoToolkit.psd1";
    internal const string RootModule = "AdoToolkit.PowerShell.dll";
    private const string RootFolder = "AdoToolkit";

    // $layout in tools/package/Install-AdoToolkit.ps1 and Assert-AdoPackage in
    // tools/package/Package.Common.ps1.
    internal static readonly string[] Layout =
    [
        "AdoToolkit.psd1", "AdoToolkit.Format.ps1xml",
        "AdoToolkit.Core.dll", "AdoToolkit.PowerShell.dll", "fr/AdoToolkit.Core.resources.dll",
        "en-US/AdoToolkit.PowerShell.dll-Help.xml", "fr/AdoToolkit.PowerShell.dll-Help.xml",
    ];

    // Assert-AdoPackage reads the same identity, without loading the assembly.
    internal static readonly string[] Assemblies = ["AdoToolkit.Core.dll", "AdoToolkit.PowerShell.dll", "fr/AdoToolkit.Core.resources.dll"];

    internal enum FolderState
    {
        Missing,
        Valid,
        Incomplete,
    }

    // Checks every entry, then writes the seven files into folder, which must not exist. Each output
    // path is built from Layout, never from the entry's text.
    internal static void Extract(ZipArchive archive, Version version, string folder, UpdateHttp.Limits limits, string archiveName, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(limits);
        if (archive.Entries.Count > limits.ModuleEntries) throw ArchiveLayout(archiveName, culture);
        List<(ZipArchiveEntry Entry, string Name)> files = [];
        string? found = null;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string name = entry.FullName;
            if (name.EndsWith('/') && entry.Length == 0) continue;
            if (name.Contains('\\', StringComparison.Ordinal) || name.Contains(':', StringComparison.Ordinal) || name.StartsWith('/'))
                throw ArchiveEntry(archiveName, name, culture);
            string[] segments = name.Split('/');
            if (segments.Length < 3 || !string.Equals(segments[0], RootFolder, StringComparison.Ordinal) || !IsVersionFolder(segments[1])
                || segments.Any(static segment => segment is "" or "." or ".."))
                throw ArchiveEntry(archiveName, name, culture);
            if (found is null) found = segments[1];
            else if (!string.Equals(segments[1], found, StringComparison.Ordinal)) throw ArchiveLayout(archiveName, culture);
            if (entry.Length > limits.EntryBytes)
                throw new AdoResponseFormatException(Messages.Get(AdoMessage.UpdateArchiveEntryTooLarge, culture, archiveName,
                    UpdateGuards.Printable(name, 260))) { Operation = UpdateHttp.ArchiveOperation };
            files.Add((entry, string.Join('/', segments, 2, segments.Length - 2)));
        }
        if (found is null || !files.Select(static file => file.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(Layout.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw ArchiveLayout(archiveName, culture);
        if (!string.Equals(found, UpdateHttp.VersionText(version), StringComparison.Ordinal)) throw Mismatch(archiveName, version, culture);
        UpdateGuards.EnsureFreeSpace(folder, files.Sum(static file => file.Entry.Length), limits.FreeSpaceMargin, version, culture);
        Directory.CreateDirectory(folder);
        string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
        foreach ((ZipArchiveEntry entry, string name) in files)
        {
            string constant = Array.Find(Layout, item => string.Equals(item, name, StringComparison.Ordinal))!;
            string output = Path.GetFullPath(Path.Combine(folder, constant));
            if (!output.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw ArchiveEntry(archiveName, entry.FullName, culture);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            WriteEntry(entry, output, archiveName, culture);
        }
    }

    // The staged folder before the commit: the manifest's version and root module, the PowerShell it
    // requires when runningPowerShell is given (module-only mode), and each DLL's identity.
    internal static void Check(string folder, Version version, Version? runningPowerShell, Func<string, ModuleManifestFacts?> readManifest,
        string archiveName, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(readManifest);
        ModuleManifestFacts? facts = readManifest(Path.Combine(folder, ManifestName));
        if (!Matches(facts, version)) throw Mismatch(archiveName, version, culture);
        if (runningPowerShell is not null && facts!.PowerShellVersion is { } required)
        {
            if (!Version.TryParse(required, out Version? requiredVersion)) throw Mismatch(archiveName, version, culture);
            if (requiredVersion > runningPowerShell)
                throw new AdoConfigurationException(Messages.Get(AdoMessage.UpdatePowerShellTooOld, culture, UpdateHttp.VersionText(version),
                    requiredVersion.ToString(), runningPowerShell.ToString()));
        }
        if (FindAssemblyMismatch(folder, version) is { } assembly)
            throw new AdoResponseFormatException(Messages.Get(AdoMessage.UpdateArchiveAssembly, culture, archiveName, UpdateHttp.VersionText(version), assembly))
            { Operation = UpdateHttp.ArchiveOperation };
    }

    // An existing version folder counts as installed only when it would pass the checks of a new
    // install: the seven files, no link, the manifest and each DLL's identity. Its bytes cannot be
    // compared without a download. A link inside is an error; anything else missing is Incomplete.
    internal static FolderState Inspect(string folder, Version version, Func<string, ModuleManifestFacts?> readManifest, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(readManifest);
        if (File.Exists(folder)) throw new AdoFileOutputException(Messages.Get(AdoMessage.UpdateTargetExists, culture, folder));
        if (!Directory.Exists(folder)) return FolderState.Missing;
        List<string> files;
        try
        {
            if ((UpdateGuards.FindLink(folder) ?? UpdateGuards.FindLinkInside(folder)) is { } link)
                throw new AdoConfigurationException(Messages.Get(AdoMessage.UpdateLinkedPath, culture, link));
            files = UpdateGuards.RelativeFiles(folder);
        }
        // A folder that cannot be read cannot be replaced either.
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new AdoFileOutputException(Messages.Get(AdoMessage.UpdateFolderUnreadable, culture, folder), error);
        }
        if (!files.Order(StringComparer.Ordinal).SequenceEqual(Layout.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            return FolderState.Incomplete;
        ModuleManifestFacts? facts;
        try { facts = readManifest(Path.Combine(folder, ManifestName)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return FolderState.Incomplete; }
        return Matches(facts, version) && FindAssemblyMismatch(folder, version) is null ? FolderState.Valid : FolderState.Incomplete;
    }

    // The first DLL whose name differs from its file name or whose version is not the module
    // version plus .0, or that is no assembly at all, such as a truncated copy; null when all match.
    internal static string? FindAssemblyMismatch(string folder, Version version)
    {
        Version expected = new(version.Major, version.Minor, version.Build, 0);
        foreach (string name in Assemblies)
        {
            AssemblyName identity;
            try { identity = AssemblyName.GetAssemblyName(Path.Combine(folder, name)); }
            catch (Exception error) when (error is BadImageFormatException or FileLoadException or IOException or ArgumentException
                or UnauthorizedAccessException or System.Security.SecurityException) { return name; }
            if (!string.Equals(identity.Name, Path.GetFileNameWithoutExtension(name), StringComparison.Ordinal) || identity.Version != expected)
                return name;
        }
        return null;
    }

    // Copies one entry, never more than its declared length, and checks the length written (the
    // installer's "incomplete entry"). A corrupt deflate stream is incomplete too.
    internal static void WriteEntry(ZipArchiveEntry entry, string output, string archiveName, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(entry);
        long written = 0;
        try
        {
            using Stream source = entry.Open();
            using FileStream target = new(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[81920];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                written += read;
                if (written > entry.Length) break;
                target.Write(buffer, 0, read);
            }
        }
        catch (InvalidDataException error)
        {
            throw Incomplete(archiveName, entry.FullName, culture, error);
        }
        if (written != entry.Length) throw Incomplete(archiveName, entry.FullName, culture);
    }

    private static bool Matches(ModuleManifestFacts? facts, Version version) =>
        facts is not null && string.Equals(facts.ModuleVersion, UpdateHttp.VersionText(version), StringComparison.Ordinal)
        && string.Equals(facts.RootModule, RootModule, StringComparison.Ordinal);

    // The installer's '^\d+\.\d+\.\d+$' with ASCII digits: the folder must also equal the tag's
    // version, so another script's digits or a leading zero is refused there.
    private static bool IsVersionFolder(string segment)
    {
        string[] parts = segment.Split('.');
        return parts.Length == 3 && parts.All(static part => part.Length > 0 && part.All(char.IsAsciiDigit));
    }

    internal static AdoResponseFormatException ArchiveEntry(string archiveName, string entry, CultureInfo culture) =>
        new(Messages.Get(AdoMessage.UpdateArchiveEntry, culture, archiveName, UpdateGuards.Printable(entry, 260))) { Operation = UpdateHttp.ArchiveOperation };

    internal static AdoResponseFormatException ArchiveLayout(string archiveName, CultureInfo culture, Exception? error = null) =>
        new(Messages.Get(AdoMessage.UpdateArchiveLayout, culture, archiveName), error) { Operation = UpdateHttp.ArchiveOperation };

    internal static AdoResponseFormatException Mismatch(string archiveName, Version version, CultureInfo culture) =>
        new(Messages.Get(AdoMessage.UpdateManifestMismatch, culture, archiveName, UpdateHttp.VersionText(version))) { Operation = UpdateHttp.ArchiveOperation };

    private static AdoResponseFormatException Incomplete(string archiveName, string entry, CultureInfo culture, Exception? error = null) =>
        new(Messages.Get(AdoMessage.UpdateArchiveIncomplete, culture, archiveName, UpdateGuards.Printable(entry, 260)), error)
        { Operation = UpdateHttp.ArchiveOperation };
}
