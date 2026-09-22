#Requires -Version 7.6
#Requires -PSEdition Core
<#
.SYNOPSIS
Installs an AdoToolkit release zip for the current user.

.DESCRIPTION
Checks the zip against its SHA-256 checksum, checks that it contains exactly the AdoToolkit
module files, and installs them to Documents\PowerShell\Modules\AdoToolkit\<version>. The same
version is replaced only after the new copy is complete. No administrator rights, .NET SDK or
other modules are needed. This script is self-contained so it can ship beside the release zip.

When the release is signed, pass -ExpectedThumbprint to require a valid signature from that
certificate on every script, manifest and toolkit assembly.

.EXAMPLE
Unblock-File .\Install-AdoToolkit.ps1
.\Install-AdoToolkit.ps1 -Path .\AdoToolkit-0.5.0.zip

Uses AdoToolkit-0.5.0.zip.sha256 from the same folder as the zip.

.EXAMPLE
.\Install-AdoToolkit.ps1 -Path .\AdoToolkit-0.5.0.zip -Sha256 <64 hexadecimal characters> -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'ChecksumFile')]
param(
    # The release zip, AdoToolkit-<version>.zip.
    [Parameter(Mandatory = $true, Position = 0)][string] $Path,
    # The checksum file; defaults to <zip>.sha256 beside the zip.
    [Parameter(ParameterSetName = 'ChecksumFile')][string] $ChecksumPath,
    # The expected SHA-256 hash, for example copied from the release page.
    [Parameter(Mandatory = $true, ParameterSetName = 'Checksum')][ValidatePattern('^[A-Fa-f0-9]{64}$')][string] $Sha256,
    # Requires every signable file to carry a valid signature from this certificate.
    [ValidatePattern('^[A-Fa-f0-9]{40}$')][string] $ExpectedThumbprint,
    # The module directory to install into; defaults to Documents\PowerShell\Modules.
    [string] $Destination
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Keep in step with Assert-AdoPackage in Package.Common.ps1; a tooling test compares them.
$layout = @(
    'AdoToolkit.psd1', 'AdoToolkit.Format.ps1xml',
    'AdoToolkit.Core.dll', 'AdoToolkit.PowerShell.dll',
    'fr/AdoToolkit.Core.resources.dll', 'fr/AdoToolkit.PowerShell.resources.dll',
    'en-US/AdoToolkit.PowerShell.dll-Help.xml', 'fr/AdoToolkit.PowerShell.dll-Help.xml'
)
$maximumEntryBytes = 64MB
$separator = [System.IO.Path]::DirectorySeparatorChar

# Symbolic links and junctions are never traversed; cloud-file placeholders are allowed.
function Assert-NoLink {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)
    $current = $LiteralPath
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -and $null -ne $item.LinkTarget) {
                throw "The install path must not pass through a link: $current"
            }
        }
        $current = Split-Path -Parent $current
    }
}

function Assert-NoLinkInside {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)
    if (-not (Test-Path -LiteralPath $LiteralPath)) { return }
    foreach ($item in Get-ChildItem -LiteralPath $LiteralPath -Force -Recurse -Attributes ReparsePoint) {
        if ($null -ne $item.LinkTarget) { throw "The installed module contains a link: $($item.FullName)" }
    }
}

function Remove-Tree {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)
    if (Test-Path -LiteralPath $LiteralPath) {
        Assert-NoLinkInside -LiteralPath $LiteralPath
        Remove-Item -LiteralPath $LiteralPath -Recurse -Force
    }
}

# A replaced copy can still be loaded by another PowerShell process, which keeps its assemblies
# locked; Windows allows the rename but not the delete. Leftovers are retried on the next install.
function Remove-Leftover {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)
    # Any failure, including a link found inside, leaves the folder in place.
    try { Remove-Tree -LiteralPath $LiteralPath; return $true }
    catch { return $false }
}

if (-not $IsWindows) { throw 'AdoToolkit supports Windows only.' }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.ZipFile

$zip = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
if (-not (Test-Path -LiteralPath $zip -PathType Leaf)) { throw "Release zip not found: $zip" }

# 1. Checksum.
if ($PSCmdlet.ParameterSetName -eq 'Checksum') { $expectedHash = $Sha256 }
else {
    $checksumFile = if ($ChecksumPath) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ChecksumPath) } else { $zip + '.sha256' }
    if (-not (Test-Path -LiteralPath $checksumFile -PathType Leaf)) {
        throw "Checksum file not found: $checksumFile. Download it with the zip, or pass -Sha256."
    }
    $line = @([System.IO.File]::ReadAllLines($checksumFile) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) | Select-Object -First 1
    $parts = @("$line".Trim() -split '\s+', 2)
    if ($parts[0] -notmatch '^[A-Fa-f0-9]{64}$') { throw "The checksum file has no SHA-256 hash: $checksumFile" }
    if ($parts.Count -eq 2 -and $parts[1].TrimStart('*') -ne [System.IO.Path]::GetFileName($zip)) {
        throw "The checksum file describes $($parts[1]), not $([System.IO.Path]::GetFileName($zip))."
    }
    $expectedHash = $parts[0]
}
$actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
if (-not [string]::Equals($actual, $expectedHash, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The release zip does not match its SHA-256 checksum. Download it again.'
}

# 2. Contents: exactly one version folder holding exactly the module files.
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $files = [System.Collections.Generic.List[object]]::new()
    $version = $null
    foreach ($entry in $archive.Entries) {
        $name = $entry.FullName
        if ($name.EndsWith('/', [System.StringComparison]::Ordinal) -and $entry.Length -eq 0) { continue }
        if ($name.Contains('\') -or $name.Contains(':') -or $name.StartsWith('/', [System.StringComparison]::Ordinal)) {
            throw "The release zip contains an unsafe entry: $name"
        }
        $segments = $name.Split('/')
        if ($segments.Count -lt 3 -or $segments[0] -cne 'AdoToolkit' -or $segments[1] -notmatch '^\d+\.\d+\.\d+$' -or
            @($segments | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0) {
            throw "The release zip contains an unexpected entry: $name"
        }
        if ($null -eq $version) { $version = $segments[1] }
        elseif ($segments[1] -cne $version) { throw 'The release zip contains more than one version.' }
        if ($entry.Length -gt $maximumEntryBytes) { throw "The release zip entry is too large: $name" }
        $files.Add([pscustomobject]@{ Entry = $entry; Relative = ($segments[2..($segments.Count - 1)] -join '/') })
    }
    if ($null -eq $version -or @(Compare-Object -ReferenceObject $layout -DifferenceObject @($files.Relative) -CaseSensitive).Count -ne 0) {
        throw 'The release zip does not contain the AdoToolkit module layout.'
    }

    $modules = if ($Destination) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destination) }
    else {
        $documents = [Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)
        if ([string]::IsNullOrWhiteSpace($documents)) { throw 'The Documents folder is unavailable; pass -Destination.' }
        Join-Path $documents 'PowerShell\Modules'
    }
    $modules = [System.IO.Path]::GetFullPath($modules)
    $root = Join-Path $modules 'AdoToolkit'
    $target = Join-Path $root $version
    Assert-NoLink -LiteralPath $root
    $inUse = @([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object {
            -not $_.IsDynamic -and $_.Location -and
            $_.Location.StartsWith($target + $separator, [System.StringComparison]::OrdinalIgnoreCase)
        })
    if ($inUse.Count -gt 0) {
        throw "AdoToolkit $version is loaded in this PowerShell session. Run the installer from a new PowerShell window."
    }
    if (-not $PSCmdlet.ShouldProcess($target, "Install AdoToolkit $version")) { return }

    # 3. Extract beside the target, validate, then replace atomically.
    [void] [System.IO.Directory]::CreateDirectory($root)
    foreach ($leftover in Get-ChildItem -LiteralPath $root -Directory -Force) {
        if ($leftover.Name -cmatch '^(\.staging-[0-9a-f]{32}|\d+\.\d+\.\d+\.previous-[0-9a-f]{32})$') {
            [void] (Remove-Leftover -LiteralPath $leftover.FullName)
        }
    }
    $staging = Join-Path $root ('.staging-' + [guid]::NewGuid().ToString('N'))
    $backup = $null
    try {
        [void] [System.IO.Directory]::CreateDirectory($staging)
        foreach ($file in $files) {
            $output = [System.IO.Path]::GetFullPath((Join-Path $staging $file.Relative))
            if (-not $output.StartsWith($staging + $separator, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "The release zip entry escapes the module folder: $($file.Entry.FullName)"
            }
            [void] [System.IO.Directory]::CreateDirectory((Split-Path -Parent $output))
            $source = $file.Entry.Open()
            try {
                $stream = [System.IO.File]::Open($output, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write)
                try { $source.CopyTo($stream) }
                finally { $stream.Dispose() }
            }
            finally { $source.Dispose() }
            if ((Get-Item -LiteralPath $output).Length -ne $file.Entry.Length) { throw "The release zip entry is incomplete: $($file.Entry.FullName)" }
        }
        $manifest = Import-PowerShellDataFile -LiteralPath (Join-Path $staging 'AdoToolkit.psd1')
        if ($manifest.ModuleVersion -cne $version -or $manifest.RootModule -cne 'AdoToolkit.PowerShell.dll') {
            throw 'The module manifest does not match the release version.'
        }
        if ($ExpectedThumbprint) {
            $signable = @(Get-ChildItem -LiteralPath $staging -File -Recurse | Where-Object {
                    $_.Extension -in @('.psd1', '.ps1xml', '.ps1') -or $_.Name -match '^AdoToolkit\.(Core|PowerShell)(\.resources)?\.dll$'
                })
            foreach ($item in $signable) {
                $signature = Get-AuthenticodeSignature -LiteralPath $item.FullName
                if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate -or
                    -not [string]::Equals($signature.SignerCertificate.Thumbprint, $ExpectedThumbprint, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "The release is not signed by the expected certificate: $($item.Name)"
                }
            }
        }
        # Files written here carry no download mark; removing any inherited one is harmless.
        Get-ChildItem -LiteralPath $staging -File -Recurse | Unblock-File
        if (Test-Path -LiteralPath $target) {
            Assert-NoLink -LiteralPath $target
            Assert-NoLinkInside -LiteralPath $target
            $backup = $target + '.previous-' + [guid]::NewGuid().ToString('N')
            # Loaded assemblies stay locked until their PowerShell process ends.
            try { [System.IO.Directory]::Move($target, $backup) }
            catch [System.IO.IOException], [System.UnauthorizedAccessException] {
                $backup = $null
                throw "AdoToolkit $version is in use. Close every PowerShell window that loaded it, then run the installer again."
            }
        }
        try { [System.IO.Directory]::Move($staging, $target) }
        catch {
            if ($backup) { [System.IO.Directory]::Move($backup, $target); $backup = $null }
            throw
        }
    }
    finally {
        Remove-Tree -LiteralPath $staging
        if ($backup -and -not (Remove-Leftover -LiteralPath $backup)) {
            Write-Warning "The previous copy is still in use by another PowerShell window and was left at $backup. The next install removes it."
        }
    }
}
finally { $archive.Dispose() }

$searched = @($env:PSModulePath -split [System.IO.Path]::PathSeparator | Where-Object { $_ } |
        ForEach-Object { [System.IO.Path]::GetFullPath($_).TrimEnd($separator) })
if ($modules.TrimEnd($separator) -notin $searched) {
    Write-Warning "$modules is not in PSModulePath; import the module by path: Import-Module '$target\AdoToolkit.psd1'"
}
$loaded = @([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'AdoToolkit.PowerShell' })
if ($loaded.Count -gt 0) {
    Write-Warning 'AdoToolkit is already loaded in this PowerShell session. Open a new PowerShell window to use the installed version.'
}
[pscustomobject]@{
    Name = 'AdoToolkit'
    Version = $version
    Path = $target
    Signed = [bool] $ExpectedThumbprint
    Next = 'Import-Module AdoToolkit'
}
