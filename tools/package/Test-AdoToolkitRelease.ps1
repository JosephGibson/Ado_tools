#Requires -Version 7.6
#Requires -PSEdition Core
<#
.SYNOPSIS
Checks a prepared AdoToolkit release while the agent writes its prose.

.DESCRIPTION
Runs verify with every stage but documentation, reads the version from MSBuild, packages the
verified build, writes and checks the release assets, and checks what no other script does: the
runtime pin in bundle.json and Install-AdoToolkit.ps1 -WhatIf. Every child runs with
ADOTOOLKIT_RELEASE_BUILD=1. The tree is hashed before the gate and after packaging, apart from the
changelog, the new notes, the archive index and the fragments. Prints one compact JSON document,
also written to artifacts/release/release-check.json, and exits 0 when everything passed, 2 when
a step is incomplete, such as the assets step without the PowerShell runtime for the portable
asset, and 1 otherwise. Complete-AdoToolkitRelease.ps1 accepts an incomplete assets step only.

.EXAMPLE
pwsh -NoProfile -File .\tools\package\Test-AdoToolkitRelease.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'Release.Common.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
try {
    $result = Invoke-AdoReleaseCheck -Root $repository
    $result | ConvertTo-Json -Depth 8 -Compress
    if ($result.Status -eq 'pass') { exit 0 }
    if ($result.Status -eq 'incomplete') { exit 2 }
    exit 1
}
catch {
    [pscustomobject]@{ Status = 'fail'; Error = $_.Exception.Message } | ConvertTo-Json -Compress
    exit 1
}
