# GitHub for the Update-AdoToolkit tests: a handler that the cmdlet's test seam puts in place of the
# network, so no test reaches GitHub. It answers the latest-release description, redirects downloads
# from github.com to the asset host with a signed query as GitHub does, and fails as a test asks. It
# runs on the cmdlet's worker thread, which has no runspace, so it holds data, not script blocks.
Set-StrictMode -Version 2.0

if (-not ('AdoToolkitTests.FakeGitHub' -as [type])) {
    Add-Type -ReferencedAssemblies System.Net.Http, System.Net.Primitives, System.Collections, System.Runtime, System.Threading -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdoToolkitTests
{
    public sealed class FakeGitHub : HttpMessageHandler
    {
        public const string Latest = "https://api.github.com/repos/JosephGibson/Ado_tools/releases/latest";
        private const string AssetPath = "/github-production-release-asset/1/";
        private readonly object gate = new object();
        private readonly List<string> requests = new List<string>();
        public string Tag = "v0.0.0";
        public string ApiBody = "{}";
        public int ApiStatus = 200;
        public Dictionary<string, string> ApiHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, byte[]> Assets = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        public string AssetHost = "release-assets.githubusercontent.com";
        public int ProxyTunnelStatus;

        public string[] Requests() { lock (gate) return requests.ToArray(); }

        public void ClearRequests() { lock (gate) requests.Clear(); }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri uri = request.RequestUri;
            lock (gate) requests.Add(uri.AbsoluteUri);
            if (ProxyTunnelStatus != 0)
                throw new HttpRequestException(HttpRequestError.ProxyTunnelError, "proxy", null, (HttpStatusCode)ProxyTunnelStatus);
            if (uri.AbsoluteUri == Latest)
            {
                HttpResponseMessage api = new HttpResponseMessage((HttpStatusCode)ApiStatus) { Content = new StringContent(ApiBody, Encoding.UTF8, "application/json") };
                foreach (KeyValuePair<string, string> header in ApiHeaders) api.Headers.TryAddWithoutValidation(header.Key, header.Value);
                return Task.FromResult(api);
            }
            string download = "https://github.com/JosephGibson/Ado_tools/releases/download/" + Tag + "/";
            if (uri.AbsoluteUri.StartsWith(download, StringComparison.Ordinal))
            {
                HttpResponseMessage redirect = new HttpResponseMessage(HttpStatusCode.Found) { Content = new ByteArrayContent(new byte[0]) };
                redirect.Headers.Location = new Uri("https://" + AssetHost + AssetPath + uri.AbsoluteUri.Substring(download.Length) + "?sp=r&sig=signed");
                return Task.FromResult(redirect);
            }
            byte[] bytes;
            if (uri.Host == AssetHost && uri.AbsolutePath.StartsWith(AssetPath, StringComparison.Ordinal)
                && Assets.TryGetValue(uri.AbsolutePath.Substring(AssetPath.Length), out bytes))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("Not Found") });
        }
    }
}
'@
}

function New-FakeGitHub { [AdoToolkitTests.FakeGitHub]::new() }

# Puts the handler in place of the network, or $null back. Reflection, because the seam is internal
# and nothing in the product sets it.
function Set-UpdateTransport {
    param([AllowNull()][System.Net.Http.HttpMessageHandler] $Handler)
    $property = [AdoToolkit.UpdateAdoToolkitCommand].GetProperty('TestTransport', [Reflection.BindingFlags] 'NonPublic, Static')
    $property.SetValue($null, $Handler)
    if (-not [object]::ReferenceEquals($property.GetValue($null), $Handler)) { throw 'The update transport seam was not set.' }
}

function Get-Sha256Hex {
    param([byte[]] $Bytes)
    [Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData($Bytes))
}

# Serves one release: its zip and checksum file, described by the API as GitHub describes them.
function Publish-FakeRelease {
    param(
        [Parameter(Mandatory = $true)] [AdoToolkitTests.FakeGitHub] $GitHub,
        [Parameter(Mandatory = $true)] [string] $Version,
        [Parameter(Mandatory = $true)] [byte[]] $Archive,
        [ValidateSet('Module', 'Portable')] [string] $Mode = 'Module',
        [byte[]] $Checksum,
        [string] $ArchiveDigest
    )
    $name = if ($Mode -eq 'Portable') { "AdoToolkit-$Version-win-x64.zip" } else { "AdoToolkit-$Version.zip" }
    if (-not $Checksum) { $Checksum = [Text.Encoding]::UTF8.GetBytes((Get-Sha256Hex $Archive) + "  $name`n") }
    if (-not $ArchiveDigest) { $ArchiveDigest = 'sha256:' + (Get-Sha256Hex $Archive) }
    $GitHub.Tag = "v$Version"
    $GitHub.Assets.Clear()
    $GitHub.Assets[$name] = $Archive
    $GitHub.Assets["$name.sha256"] = $Checksum
    $assets = @(
        [ordered]@{ name = $name; size = $Archive.Length; digest = $ArchiveDigest; browser_download_url = "https://evil.example.test/$name" },
        [ordered]@{ name = "$name.sha256"; size = $Checksum.Length; digest = 'sha256:' + (Get-Sha256Hex $Checksum) }
    )
    $GitHub.ApiBody = [ordered]@{ tag_name = "v$Version"; draft = $false; prerelease = $false; assets = $assets } | ConvertTo-Json -Depth 4 -Compress
    $GitHub.ApiStatus = 200
    $GitHub.ApiHeaders.Clear()
    $GitHub.ProxyTunnelStatus = 0
    $GitHub.AssetHost = 'release-assets.githubusercontent.com'
    $GitHub.ClearRequests()
}

# An empty assembly with this identity, as tools/tests/Package.Tests.ps1 emits its fixtures.
function New-SyntheticAssembly {
    param([string] $Name, [version] $Version)
    $identity = [Reflection.AssemblyName]::new($Name)
    $identity.Version = $Version
    $assembly = [Reflection.Emit.PersistedAssemblyBuilder]::new($identity, [object].Assembly)
    $null = $assembly.DefineDynamicModule($Name).DefineType('SyntheticFixture').CreateType()
    $stream = [IO.MemoryStream]::new()
    try {
        $assembly.Save($stream)
        return , $stream.ToArray()
    }
    finally { $stream.Dispose() }
}

function New-ModuleManifestText {
    param([string] $Version, [string] $RootModule = 'AdoToolkit.PowerShell.dll', [string] $PowerShellVersion = '7.6')
    "@{`n    RootModule = '$RootModule'`n    ModuleVersion = '$Version'`n    PowerShellVersion = '$PowerShellVersion'`n}`n"
}

# The seven module files of a release, relative to its version folder, with assemblies of
# AssemblyVersion, which is the module version plus .0 unless a test wants another.
function New-ModuleFiles {
    param([string] $Version, [version] $AssemblyVersion = ([version] "$Version.0"), [string] $Manifest)
    if (-not $Manifest) { $Manifest = New-ModuleManifestText -Version $Version }
    [ordered]@{
        'AdoToolkit.psd1' = $Manifest
        'AdoToolkit.Format.ps1xml' = '<Configuration><ViewDefinitions /></Configuration>'
        'AdoToolkit.Core.dll' = New-SyntheticAssembly -Name 'AdoToolkit.Core' -Version $AssemblyVersion
        'AdoToolkit.PowerShell.dll' = New-SyntheticAssembly -Name 'AdoToolkit.PowerShell' -Version $AssemblyVersion
        'fr/AdoToolkit.Core.resources.dll' = New-SyntheticAssembly -Name 'AdoToolkit.Core.resources' -Version $AssemblyVersion
        'en-US/AdoToolkit.PowerShell.dll-Help.xml' = '<helpItems />'
        'fr/AdoToolkit.PowerShell.dll-Help.xml' = '<helpItems />'
    }
}

# A zip from entries, each a hashtable with Name and optionally Data (bytes or text), Size (that many
# zero bytes) and Attributes. An entry with neither Data nor Size is empty, as a folder entry is.
function New-ZipBytes {
    param([object[]] $Entries)
    $stream = [IO.MemoryStream]::new()
    try {
        $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($item in $Entries) {
                $entry = $zip.CreateEntry($item.Name, [IO.Compression.CompressionLevel]::Fastest)
                if ($item.Contains('Attributes')) { $entry.ExternalAttributes = $item.Attributes }
                if (-not $item.Contains('Data') -and -not $item.Contains('Size')) { continue }
                $target = $entry.Open()
                try {
                    if ($item.Contains('Size')) {
                        $buffer = [byte[]]::new(1MB)
                        for ($left = [long] $item.Size; $left -gt 0; $left -= $buffer.Length) { $target.Write($buffer, 0, [int][Math]::Min($left, $buffer.Length)) }
                    }
                    else {
                        # Typed, because PowerShell unrolls an array that a statement outputs.
                        [byte[]] $bytes = if ($item.Data -is [byte[]]) { $item.Data } else { [Text.Encoding]::UTF8.GetBytes([string] $item.Data) }
                        $target.Write($bytes, 0, $bytes.Length)
                    }
                }
                finally { $target.Dispose() }
            }
        }
        finally { $zip.Dispose() }
        return , $stream.ToArray()
    }
    finally { $stream.Dispose() }
}

# Rewrites one entry's uncompressed size in the central directory: a header that lies.
function Set-ZipCentralSize {
    param([byte[]] $Zip, [string] $Name, [uint32] $Size)
    $bytes = [byte[]] $Zip.Clone()
    $wanted = [Text.Encoding]::UTF8.GetBytes($Name)
    for ($index = 0; $index -lt $bytes.Length - 46; $index++) {
        if ($bytes[$index] -ne 0x50 -or $bytes[$index + 1] -ne 0x4B -or $bytes[$index + 2] -ne 0x01 -or $bytes[$index + 3] -ne 0x02) { continue }
        $length = [BitConverter]::ToUInt16($bytes, $index + 28)
        if ([Text.Encoding]::UTF8.GetString($bytes, $index + 46, $length) -cne $Name -or $length -ne $wanted.Length) { continue }
        [BitConverter]::GetBytes($Size).CopyTo($bytes, $index + 24)
        return , $bytes
    }
    throw "Entry not found: $Name"
}

# A manifest in Destination that names another version and loads the module files of Source by
# their full paths. The module then runs from Destination, at that version, while its assemblies
# load from Source, so that nothing in the test drive stays locked when Pester deletes it.
function New-RelabeledModule {
    param([string] $Source, [string] $Destination, [string] $Version)
    [void] [IO.Directory]::CreateDirectory($Destination)
    $text = [IO.File]::ReadAllText((Join-Path $Source 'AdoToolkit.psd1'))
    $relabeled = [regex]::Replace($text, "(?m)^(\s*ModuleVersion\s*=\s*)'[^']*'", "`${1}'$Version'")
    foreach ($name in @('AdoToolkit.PowerShell.dll', 'AdoToolkit.Core.dll', 'AdoToolkit.Format.ps1xml')) {
        $relabeled = $relabeled.Replace("'$name'", "'" + (Join-Path $Source $name).Replace("'", "''") + "'")
    }
    if ($relabeled -cnotmatch "ModuleVersion = '$([regex]::Escape($Version))'" -or $relabeled -cmatch "'AdoToolkit\.(PowerShell\.dll|Core\.dll|Format\.ps1xml)'") {
        throw 'The staged manifest does not have the expected lines.'
    }
    [IO.File]::WriteAllText((Join-Path $Destination 'AdoToolkit.psd1'), $relabeled, [Text.UTF8Encoding]::new($false))
}

# The PowerShell archive that New-AdoReleaseArchive bundles, as tools/tests/Portable.Tests.ps1 builds
# it: the ten files that tools/package/Portable.Common.ps1 requires, and one published checksum line.
function New-SyntheticRuntime {
    param([string] $Folder, [string] $Version = '7.6.6')
    [void] [IO.Directory]::CreateDirectory($Folder)
    $path = Join-Path $Folder "PowerShell-$Version-win-x64.zip"
    $names = @('pwsh.exe', 'pwsh.dll', 'pwsh.runtimeconfig.json', 'System.Management.Automation.dll', 'coreclr.dll', 'hostfxr.dll',
        'hostpolicy.dll', 'System.Private.CoreLib.dll', 'LICENSE.txt', 'ThirdPartyNotices.txt', 'Modules/Example/Example.psd1')
    [IO.File]::WriteAllBytes($path, (New-ZipBytes -Entries @($names | ForEach-Object { @{ Name = $_; Data = "synthetic $_" } })))
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    [IO.File]::WriteAllText(($path + '.sha256'), "$hash *$([IO.Path]::GetFileName($path))`r`n", [Text.Encoding]::Unicode)
    [pscustomobject]@{ Archive = $path; Checksum = $path + '.sha256'; Version = $Version }
}
