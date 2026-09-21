#Requires -Version 7.6
#Requires -PSEdition Core
[CmdletBinding()]
param([switch] $SmokeTest)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

try {
    $module = Import-Module -Name (Join-Path $PSScriptRoot 'module/AdoToolkit.psd1') -Global -PassThru -ErrorAction Stop
    Set-Location -LiteralPath $PSScriptRoot
    if ($SmokeTest) {
        if ($module.ExportedCmdlets.Count -ne 22) { throw 'The module did not export all 22 cmdlets.' }
        [pscustomobject]@{
            ModuleVersion = $module.Version.ToString()
            PowerShellVersion = $PSVersionTable.PSVersion.ToString()
            Architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
            ModulePath = $module.ModuleBase
            RuntimePath = $PSHOME
        } | ConvertTo-Json -Compress
    }
    elseif ($PSUICulture -like 'fr*') {
        Write-Host "AdoToolkit $($module.Version) est pret."
        Write-Host 'Commandes : Get-Command -Module AdoToolkit'
        Write-Host 'Aide : Get-Help Connect-Ado -Full'
    }
    else {
        Write-Host "AdoToolkit $($module.Version) is ready."
        Write-Host 'Commands: Get-Command -Module AdoToolkit'
        Write-Host 'Help: Get-Help Connect-Ado -Full'
    }
}
catch {
    Write-Error -Message "AdoToolkit could not start: $($_.Exception.Message)" -ErrorAction Continue
    exit 1
}
