#Requires -Version 7.6
#Requires -PSEdition Core
<#
.SYNOPSIS
Prepares an AdoToolkit release: the version bump, the archive of the older notes, and the changelog
section and release notes assembled from the change fragments in docs/unreleased/.

.DESCRIPTION
Prints one compact JSON document: the versions, the base commit, the files changed, the fragments,
the release:todo markers left for the agent and every other place that still names the old version.
Exits 0 when the preparation is done and 1 when it failed. A run after a failure resumes from
artifacts/release/release-prep.json and skips each step that is already done. Reads Git only, and
never commits, tags or pushes.

.EXAMPLE
pwsh -NoProfile -File .\tools\package\Start-AdoToolkitRelease.ps1 -Version 0.11.5 -WhatIf
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $Version,
    # The date of the changelog section and the notes, yyyy-MM-dd; today by default.
    [string] $Date,
    # Lists the edits and writes nothing. A plain switch keeps the output one JSON document, where
    # ShouldProcess would print lines of its own.
    [switch] $WhatIf
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'Release.Common.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
try {
    Invoke-AdoReleaseStart -Root $repository -Version $Version -Date $Date -DryRun:$WhatIf | ConvertTo-Json -Depth 8 -Compress
    exit 0
}
catch {
    [pscustomobject]@{ Status = 'fail'; Error = $_.Exception.Message } | ConvertTo-Json -Compress
    exit 1
}
