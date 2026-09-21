#Requires -Version 7.6
#Requires -PSEdition Core
# Turns a staged package into the release assets: AdoToolkit-<version>.zip, its .sha256 checksum
# file, and the standalone Install-AdoToolkit.ps1. Supply the pinned PowerShell archive,
# published checksums and version to also build a portable win-x64 ZIP and checksum.
# Run Publish-AdoToolkitPackage.ps1 first. This script never downloads dependencies.
[CmdletBinding()]
param([string] $PackagePath, [string] $OutputRoot,
    [string] $PowerShellArchivePath, [string] $PowerShellChecksumPath, [string] $PowerShellVersion)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'Package.Common.ps1')
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $PackagePath) {
    $properties = Read-AdoPackageXml -Path (Join-Path $repository 'Directory.Build.props')
    $version = $properties.SelectSingleNode('/Project/PropertyGroup/VersionPrefix').InnerText
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VersionPrefix must be a three-part module version.' }
    $PackagePath = Join-Path $repository "artifacts/AdoToolkit/$version"
}
if (-not $OutputRoot) { $OutputRoot = Join-Path $repository 'artifacts/release' }
$package = Resolve-AdoPackagePath -Path $PackagePath -Root $repository
$output = Resolve-AdoPackagePath -Path $OutputRoot -Root $repository
if (-not (Test-Path -LiteralPath $package -PathType Container)) {
    throw 'No staged package was found. Run tools/package/Publish-AdoToolkitPackage.ps1 first.'
}
$release = New-AdoReleaseArchive -PackagePath $package -OutputRoot $output -InstallerPath (Join-Path $PSScriptRoot 'Install-AdoToolkit.ps1') `
    -PowerShellArchivePath $PowerShellArchivePath -PowerShellChecksumPath $PowerShellChecksumPath -PowerShellVersion $PowerShellVersion
Write-Output "Release ready: $($release.Archive)"
Write-Output "SHA256: $($release.Sha256)"
if ($release.PortableArchive) { Write-Output "Portable release ready: $($release.PortableArchive)" }
