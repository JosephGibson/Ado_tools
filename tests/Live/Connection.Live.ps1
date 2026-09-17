[CmdletBinding()]
param()
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$WarningPreference = 'SilentlyContinue'
$VerbosePreference = 'SilentlyContinue'
$DebugPreference = 'SilentlyContinue'
$InformationPreference = 'SilentlyContinue'

# Opt-in work-machine check. Never persist responses or print work data.
if ([string]::IsNullOrWhiteSpace($env:ADOTOOLKIT_LIVE_PROFILE)) {
    Write-Output 'INCONCLUSIVE S0-9 PROFILE_REQUIRED'
    exit 2
}
try {
    . (Join-Path $PSScriptRoot '../../tools/package/Package.Common.ps1')
    # Releases ship unsigned with a checksum; a signed release must still verify throughout.
    try { $installed = Import-AdoInstalledModule }
    catch {
        $reason = if ($_.Exception.Message -in @('INSTALLED_MODULE_REQUIRED', 'SIGNATURE_INVALID')) { $_.Exception.Message } else { 'INSTALLED_MODULE_INVALID' }
        Write-Output ('FAIL S0-9 ' + $reason)
        exit 1
    }
    if ($installed.Signed) { Write-Output 'PASS S0-9 SIGNATURE_VALID' }
    else { Write-Output 'PASS S0-9 UNSIGNED_RELEASE_INSTALLED' }
    Connect-Ado -Profile $env:ADOTOOLKIT_LIVE_PROFILE | Out-Null
    $result = Test-AdoConnection -ErrorAction Stop
    if (-not $result.Success) { Write-Output 'FAIL S0-9 CONNECTION_FAILED'; exit 1 }
    Get-AdoProject -ErrorAction Stop | Out-Null
    Write-Output 'PASS S0-9 CONNECT_TEST_PROJECTS'
    Write-Output 'PASS V-08 WINDOWS_INTEGRATED_ACCESS'
    # Successful requests cannot prove server error-body or error-language behavior.
    Write-Output 'INCONCLUSIVE V-07 NO_ERROR_OBSERVED'
    Write-Output 'INCONCLUSIVE V-14 PROJECT_PAGING_REQUIRES_WORK_CONFIRMATION'
    Write-Output 'INCONCLUSIVE V-18 NO_SERVER_ERROR_LANGUAGE_OBSERVED'
    exit 2
}
catch {
    $code = ($_.FullyQualifiedErrorId -split ',')[0]
    if ($code -notmatch '^Ado[A-Za-z]+$') { $code = 'CHECK_FAILED' }
    Write-Output ('FAIL S0-9 ' + $code)
    Write-Output 'INCONCLUSIVE V-07 ERROR_SHAPE_REQUIRES_WORK_CONFIRMATION'
    Write-Output 'INCONCLUSIVE V-08 ACCESS_NOT_CONFIRMED'
    Write-Output 'INCONCLUSIVE V-14 PROJECT_PAGING_NOT_CONFIRMED'
    Write-Output 'INCONCLUSIVE V-18 ERROR_LANGUAGE_REQUIRES_WORK_CONFIRMATION'
    exit 1
}
finally {
    if (Get-Command -Name Disconnect-Ado -ErrorAction SilentlyContinue) { Disconnect-Ado | Out-Null }
}

