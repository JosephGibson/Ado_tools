using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AdoToolkit.Core.Tests.Http;
using AdoToolkit.Core.Update;

namespace AdoToolkit.Core.Tests.Update;

// Synthetic releases for the updater tests: module and portable zips whose DLLs are assemblies emitted
// at the wanted name and version, as tools/tests/Package.Tests.ps1 emits its fixtures, the release
// description GitHub would return, and installs laid out in a test directory.
internal static partial class UpdateFixture
{
    internal static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, byte[]> Emitted = new(StringComparer.Ordinal);

    internal sealed record ZipItem(string Name, byte[]? Data = null, int? Attributes = null, long? Size = null);

    internal static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    internal static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal static string Manifest(Version version, string rootModule = "AdoToolkit.PowerShell.dll", string powerShell = "7.6") =>
        "@{\n    RootModule = '" + rootModule + "'\n    ModuleVersion = '" + version.ToString(3) + "'\n    PowerShellVersion = '" + powerShell + "'\n}\n";

    // A stand-in for the PowerShell side's reader: string values of a data file, or null when the text
    // is not a hashtable.
    internal static ModuleManifestFacts? ReadManifest(string path)
    {
        string text = File.ReadAllText(path).Trim();
        if (!text.StartsWith("@{", StringComparison.Ordinal) || !text.EndsWith('}')) return null;
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ManifestValue().Matches(text)) values[match.Groups[1].Value] = match.Groups[2].Value;
        return new ModuleManifestFacts
        {
            ModuleVersion = values.GetValueOrDefault("ModuleVersion"),
            RootModule = values.GetValueOrDefault("RootModule"),
            PowerShellVersion = values.GetValueOrDefault("PowerShellVersion"),
        };
    }

    [GeneratedRegex(@"(?m)^\s*(\w+)\s*=\s*'([^']*)'\s*$")]
    private static partial Regex ManifestValue();

    // An empty assembly with the given identity, cached per identity.
    internal static byte[] Assembly(string name, Version version)
    {
        string key = name + "/" + version;
        lock (Gate)
        {
            if (Emitted.TryGetValue(key, out byte[]? cached)) return cached;
            PersistedAssemblyBuilder builder = new(new AssemblyName(name) { Version = version }, typeof(object).Assembly);
            builder.DefineDynamicModule(name).DefineType("SyntheticFixture").CreateType();
            using MemoryStream stream = new();
            builder.Save(stream);
            return Emitted[key] = stream.ToArray();
        }
    }

    // The seven module files, relative to the version folder.
    internal static List<(string Name, byte[] Data)> ModuleFiles(Version version, string? manifest = null)
    {
        Version assembly = new(version.Major, version.Minor, version.Build, 0);
        return
        [
            ("AdoToolkit.psd1", Utf8(manifest ?? Manifest(version))),
            ("AdoToolkit.Format.ps1xml", Utf8("<Configuration><ViewDefinitions /></Configuration>")),
            ("AdoToolkit.Core.dll", Assembly("AdoToolkit.Core", assembly)),
            ("AdoToolkit.PowerShell.dll", Assembly("AdoToolkit.PowerShell", assembly)),
            ("fr/AdoToolkit.Core.resources.dll", Assembly("AdoToolkit.Core.resources", assembly)),
            ("en-US/AdoToolkit.PowerShell.dll-Help.xml", Utf8("<helpItems />")),
            ("fr/AdoToolkit.PowerShell.dll-Help.xml", Utf8("<helpItems />")),
        ];
    }

    internal static List<ZipItem> ModuleItems(Version version, string? manifest = null) =>
        [.. ModuleFiles(version, manifest).Select(file => new ZipItem("AdoToolkit/" + version.ToString(3) + "/" + file.Name, file.Data))];

    internal static string Bundle(Version version, string architecture = "win-x64") =>
        "{\"ModuleVersion\":\"" + version.ToString(3) + "\",\"PowerShellVersion\":\"7.6.6\",\"Architecture\":\"" + architecture
        + "\",\"PowerShellSha256\":\"" + new string('0', 64) + "\"}";

    internal static List<ZipItem> PortableItems(Version version, string? bundle = null)
    {
        List<ZipItem> items =
        [
            new("Start-AdoToolkit.cmd", Utf8("@echo off\r\n")), new("Start-AdoToolkit.ps1", Utf8("# launcher\n")),
            new("README.txt", Utf8("AdoToolkit for Windows x64\n")), new("bundle.json", Utf8(bundle ?? Bundle(version))),
        ];
        items.AddRange(ModuleFiles(version).Select(file => new ZipItem("module/" + file.Name, file.Data)));
        items.AddRange(PortableArchive.RuntimeFiles.Select(name => new ZipItem("runtime/" + name, Utf8(name))));
        items.Add(new ZipItem("runtime/Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1", Utf8("@{}")));
        return items;
    }

    internal static byte[] Zip(IEnumerable<ZipItem> items)
    {
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
            foreach (ZipItem item in items)
            {
                ZipArchiveEntry entry = zip.CreateEntry(item.Name, CompressionLevel.Fastest);
                if (item.Attributes is int attributes) entry.ExternalAttributes = attributes;
                if (item.Data is null && item.Size is null) continue;
                using Stream output = entry.Open();
                if (item.Size is long size)
                {
                    byte[] zeros = new byte[1024 * 1024];
                    for (long left = size; left > 0; left -= zeros.Length) output.Write(zeros, 0, (int)Math.Min(left, zeros.Length));
                }
                else output.Write(item.Data);
            }
        return stream.ToArray();
    }

    internal static ZipArchive Open(byte[] zip) => new(new MemoryStream(zip), ZipArchiveMode.Read);

    // Rewrites one entry's uncompressed size in the central directory: a header that lies.
    internal static byte[] WithCentralSize(byte[] zip, string name, uint size)
    {
        byte[] bytes = (byte[])zip.Clone();
        byte[] wanted = Utf8(name);
        for (int index = 0; index < bytes.Length - 46; index++)
        {
            if (bytes[index] != 0x50 || bytes[index + 1] != 0x4B || bytes[index + 2] != 0x01 || bytes[index + 3] != 0x02) continue;
            int length = BitConverter.ToUInt16(bytes, index + 28);
            if (!bytes.AsSpan(index + 46, length).SequenceEqual(wanted)) continue;
            BitConverter.GetBytes(size).CopyTo(bytes, index + 24);
            return bytes;
        }
        throw new InvalidOperationException(name);
    }

    // A directory junction; it needs no elevation.
    internal static void CreateJunction(string link, string target)
    {
        System.Diagnostics.ProcessStartInfo start = new("cmd.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string argument in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("cmd.exe did not start");
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0 && Directory.Exists(link), "mklink /J failed: " + output);
    }

    // <root>\Modules\AdoToolkit\<version> with the seven files; PSModulePath names <root>\Modules
    // unless onModulePath is false.
    internal static ToolkitInstallContext ModuleInstall(TestDirectory directory, Version version, bool onModulePath = true)
    {
        string modules = Path.Combine(directory.Root, "Modules");
        string folder = Path.Combine(modules, "AdoToolkit", version.ToString(3));
        WriteFiles(folder, ModuleFiles(version));
        return new ToolkitInstallContext
        {
            ModuleBase = folder, ModuleVersion = version, PowerShellVersion = new Version(7, 6, 6),
            ModulePaths = onModulePath ? [Path.Combine(directory.Root, "Elsewhere"), modules] : [Path.Combine(directory.Root, "Elsewhere")],
        };
    }

    // <root>\Portable\AdoToolkit-<version>-win-x64 with its module, launcher and runtime\pwsh.exe.
    internal static ToolkitInstallContext PortableInstall(TestDirectory directory, Version version)
    {
        string folder = Path.Combine(directory.Root, "Portable", "AdoToolkit-" + version.ToString(3) + "-win-x64");
        WriteFiles(Path.Combine(folder, "module"), ModuleFiles(version));
        WriteFiles(folder, [("Start-AdoToolkit.cmd", Utf8("@echo off\r\n")), ("runtime/pwsh.exe", Utf8("pwsh"))]);
        return new ToolkitInstallContext
        {
            ModuleBase = Path.Combine(folder, "module"), ModuleVersion = version, PowerShellVersion = new Version(7, 6, 6), ModulePaths = [],
        };
    }

    internal static void WriteFiles(string folder, IEnumerable<(string Name, byte[] Data)> files)
    {
        foreach ((string name, byte[] data) in files)
        {
            string path = Path.Combine(folder, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, data);
        }
    }

    // Relative path → hash of every file and "<dir>" for every folder, hidden items included.
    internal static Dictionary<string, string> Snapshot(string root) =>
        Directory.EnumerateFileSystemEntries(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 })
            .ToDictionary(path => Path.GetRelativePath(root, path), path => Directory.Exists(path) ? "<dir>" : Sha256(File.ReadAllBytes(path)),
                StringComparer.Ordinal);

    internal static UpdateHttp.Limits Limits() => new() { CommitRetryDelay = TimeSpan.Zero };

    internal static HttpResponseMessage Bytes(byte[] body, int status = 200) =>
        new((HttpStatusCode)status) { Content = new ByteArrayContent(body) };

    internal static HttpResponseMessage Redirect(string location, int status = 302)
    {
        HttpResponseMessage response = new((HttpStatusCode)status) { Content = new ByteArrayContent([]) };
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }
}

// GitHub as the updater sees it: the latest-release description, and asset downloads that redirect
// from github.com to the asset host with a signed query, as GitHub does. Every value is read when a
// request arrives, so a test changes what is served by setting a property.
internal sealed class ReleaseServer
{
    private const string Api = "https://api.github.com/repos/JosephGibson/Ado_tools/releases/latest";

    internal ReleaseServer(Version version, AdoToolkitInstallMode mode, byte[] archive)
    {
        Tag = "v" + version.ToString(3);
        ArchiveName = "AdoToolkit-" + version.ToString(3) + (mode == AdoToolkitInstallMode.Portable ? "-win-x64.zip" : ".zip");
        Archive = archive;
        Handler.Fallback = (request, token) => Task.FromResult(Respond(request.RequestUri!));
    }

    internal FakeHttpMessageHandler Handler { get; } = new();
    internal string Tag { get; set; }
    internal string ArchiveName { get; }
    internal byte[] Archive { get; set; }
    // What the API reports; null takes the value of the bytes served.
    internal long? ArchiveSize { get; set; }
    internal string? ArchiveDigest { get; set; }
    internal bool OmitArchiveDigest { get; set; }
    internal byte[]? Checksum { get; set; }
    internal bool IncludeChecksum { get; set; } = true;
    internal string? ReleaseBody { get; set; }
    internal Func<Uri, HttpResponseMessage?>? Override { get; set; }

    internal byte[] ChecksumBytes => Checksum ?? UpdateFixture.Utf8(UpdateFixture.Sha256(Archive) + "  " + ArchiveName + "\n");

    internal int ApiRequests => Handler.Requests.Count(request => request.Uri.Host == "api.github.com");
    internal int AssetRequests => Handler.Requests.Count(request => request.Uri.Host == "release-assets.githubusercontent.com");

    internal string Json()
    {
        JsonArray assets =
        [
            Asset(ArchiveName, ArchiveSize ?? Archive.Length, OmitArchiveDigest ? null : ArchiveDigest ?? "sha256:" + UpdateFixture.Sha256(Archive)),
            Asset("Install-AdoToolkit.ps1", 100, "sha256:" + new string('a', 64)),
        ];
        if (IncludeChecksum) assets.Add(Asset(ArchiveName + ".sha256", ChecksumBytes.Length, "sha256:" + UpdateFixture.Sha256(ChecksumBytes)));
        return new JsonObject
        {
            ["tag_name"] = Tag, ["name"] = "AdoToolkit " + Tag, ["draft"] = false, ["prerelease"] = false,
            ["body"] = "## Changes", ["assets"] = assets,
        }.ToJsonString();
    }

    private static JsonObject Asset(string name, long size, string? digest)
    {
        JsonObject asset = new() { ["name"] = name, ["size"] = size, ["browser_download_url"] = "https://evil.example.test/" + name };
        if (digest is not null) asset["digest"] = digest;
        return asset;
    }

    private HttpResponseMessage Respond(Uri uri)
    {
        if (Override?.Invoke(uri) is { } replaced) return replaced;
        if (uri.AbsoluteUri == Api) return FakeHttpMessageHandler.Response(ReleaseBody ?? Json());
        string download = "https://github.com/JosephGibson/Ado_tools/releases/download/" + Tag + "/";
        if (uri.AbsoluteUri.StartsWith(download, StringComparison.Ordinal))
            return UpdateFixture.Redirect("https://release-assets.githubusercontent.com/github-production-release-asset/1/"
                + uri.AbsoluteUri[download.Length..] + "?sp=r&sig=signed-secret");
        if (uri.Host == "release-assets.githubusercontent.com")
            return uri.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal) ? UpdateFixture.Bytes(ChecksumBytes) : UpdateFixture.Bytes(Archive);
        return FakeHttpMessageHandler.Response("{}", 404);
    }
}
