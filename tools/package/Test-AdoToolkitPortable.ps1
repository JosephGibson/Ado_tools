#Requires -Version 7.6
#Requires -PSEdition Core
# Offline check of the actual release ZIP and launcher before publication.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $ArchivePath,
    [Parameter(Mandatory = $true)][string] $ModuleVersion,
    [Parameter(Mandatory = $true)][string] $PowerShellVersion
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Package.Common.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$archive = Resolve-AdoPackagePath -Path $ArchivePath -Root $repository
$root = Split-Path -Parent $archive
$staging = Resolve-AdoPackagePath -Path (Join-Path $root ('.portable smoke [test] & ' + [guid]::NewGuid().ToString('N'))) -Root $root
try {
    [IO.Compression.ZipFile]::ExtractToDirectory($archive, $staging)
    if ((Assert-AdoPackage -PackagePath (Join-Path $staging 'module')) -ne $ModuleVersion) { throw 'Unexpected portable module version.' }
    $metadata = Get-Content -LiteralPath (Join-Path $staging 'bundle.json') -Raw | ConvertFrom-Json
    if ($metadata.ModuleVersion -ne $ModuleVersion -or $metadata.PowerShellVersion -ne $PowerShellVersion -or $metadata.Architecture -ne 'win-x64') {
        throw 'Unexpected portable bundle metadata.'
    }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = Join-Path ([Environment]::GetFolderPath('System')) 'cmd.exe'
    $start.Arguments = '/d /v:off /s /c ""%ADOTOOLKIT_PORTABLE_LAUNCHER%" --smoke-test"'
    $start.Environment['ADOTOOLKIT_PORTABLE_LAUNCHER'] = Join-Path $staging 'Start-AdoToolkit.cmd'
    $start.Environment['PATH'] = ''
    $start.Environment['PSModulePath'] = ''
    $start.Environment['POWERSHELL_UPDATECHECK'] = 'Off'
    $start.Environment['POWERSHELL_TELEMETRY_OPTOUT'] = '1'
    $start.WorkingDirectory = $root
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        [void] $process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(60000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw 'Portable launcher timed out.'
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "Portable launcher failed: $errorOutput" }
        $probe = $output | ConvertFrom-Json
        if ($probe.ModuleVersion -ne $ModuleVersion -or $probe.PowerShellVersion -ne $PowerShellVersion -or
            $probe.Architecture -ne 'X64' -or $probe.ModulePath -ne (Join-Path $staging 'module') -or
            $probe.RuntimePath -ne (Join-Path $staging 'runtime')) { throw 'Portable launcher used an unexpected module or runtime.' }
    }
    finally { $process.Dispose() }
    Write-Output "Portable launcher passed: AdoToolkit $ModuleVersion, PowerShell $PowerShellVersion, Windows x64."
}
finally { Remove-AdoPackageDirectory -Path $staging -Root $root }
