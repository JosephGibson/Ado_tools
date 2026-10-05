#Requires -Version 7.6
#Requires -PSEdition Core
<#
.SYNOPSIS
Finishes a checked AdoToolkit release and writes the developer's handoff.

.DESCRIPTION
Refuses while a release:todo marker remains, when the release check did not pass, or when the tree
changed after the check started. Then writes the Validation section of the notes from
artifacts/release/release-check.json, runs the documentation stage and the changelog and README
tests, deletes the change fragments the release assembled, and writes the handoff commands to
artifacts/release/handoff.md. Prints one compact JSON document and exits 0 when the handoff is
ready, 1 otherwise. A second run after an interruption completes it. Reads Git only, and never
commits, tags or pushes.

.EXAMPLE
pwsh -NoProfile -File .\tools\package\Complete-AdoToolkitRelease.ps1 -Summary 'faster releases'
#>
[CmdletBinding()]
param(
    # The summary of the commit and pull-request title "AdoToolkit <version>: <summary>": one line,
    # with no quote.
    [Parameter(Mandatory = $true)][AllowEmptyString()][string] $Summary
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'Release.Common.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
try {
    Invoke-AdoReleaseComplete -Root $repository -Summary $Summary | ConvertTo-Json -Depth 8 -Compress
    exit 0
}
catch {
    [pscustomobject]@{ Status = 'fail'; Error = $_.Exception.Message } | ConvertTo-Json -Compress
    exit 1
}
