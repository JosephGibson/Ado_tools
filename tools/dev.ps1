#requires -Version 7.6
<#
.SYNOPSIS
Compact, machine-oriented discovery and validation of the AdoToolkit repository for coding agents.

.DESCRIPTION
Each command writes one compressed JSON document, so an agent collects project state
without reading several configuration files or parsing human-oriented command output.
verify runs the full gate; its exit code is 0 for pass, 1 for failure and 2 for incomplete.
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('inspect', 'context', 'find', 'plan', 'verify', 'diagnose', 'deps', 'bootstrap')]
    [string] $Command = 'context',

    [string] $Query,

    [string] $Path,

    [string[]] $Stage,

    [ValidateRange(1, 100)]
    [int] $Limit = 20,

    [ValidateSet('Json', 'Object')]
    [string] $Format = 'Json',

    [switch] $Install,

    [switch] $SkipTests
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion -lt [version] '7.6.5') {
    throw 'tools/dev.ps1 requires PowerShell 7.6.5 or later.'
}

$script:RepositoryRoot = Split-Path -Parent $PSScriptRoot

# One exclusion list drives both discovery paths. The ripgrep globs below are derived
# from these names rather than written out again, so the file set an agent sees cannot
# change depending on whether ripgrep happens to be installed.
$script:ExcludedDirectoryNames = @('.git', '.vs', '.idea', 'bin', 'obj', 'artifacts', 'dist', 'build', 'coverage', 'TestResults', 'target', '.cache', '.nuget')
# Directories excluded by their path from the repository root rather than by name: each
# subdirectory of one is a Git worktree that Claude Code created, a checkout of its own.
$script:WorktreeDirectoryPaths = @('.claude/worktrees')
$script:SensitiveDirectoryNames = @('secret', 'secrets')
$script:SensitiveFileGlobs = @('.env*', 'id_rsa*', 'id_ed25519*', '*.pem', '*.pfx', '*.p12', '*.key', '*.log', '*credentials*', '*.local.*', '.npmrc', '.pypirc', '.netrc')
# A sensitive name is rejected wherever it occurs in a path: ripgrep applies a file glob to
# directory names too, so a directory named like a sensitive file hides what it holds. The
# pattern is built from the two lists above and remains a final safety check even when the
# walker or the ripgrep globs already excluded the path earlier.
$script:SensitivePathPattern = '(?:^|[\\/])(?:' + ((@($script:SensitiveDirectoryNames) + @($script:SensitiveFileGlobs) |
        ForEach-Object { [regex]::Escape($_).Replace('\*', '[^\\/]*') }) -join '|') + ')(?:[\\/]|$)'
$script:ConfigurationExtensions = @('.json', '.xml', '.config', '.csproj', '.props', '.targets', '.slnx', '.resx', '.ps1xml')
# Markdown that the documentation stage does not check: archived history, apart from its two
# index files, and test fixtures, apart from their catalog. In a dated file, the changelog or
# the notes of a release, every link must resolve, but a path may name a file that has since
# moved or gone.
$script:FrozenDocumentationPattern = '^docs/archive/(?!README\.md$|plans/README\.md$)|^tests/Fixtures/(?!README\.md$)'
$script:DatedDocumentationPattern = '^(?:CHANGELOG\.md|docs/release-[^/]+\.md)$'
# A code span that starts with one of these directories names a repository path.
$script:RepositoryPathRoots = @('src', 'tests', 'tools', 'docs', '.claude', '.agents', '.github')

foreach ($library in @('discovery', 'dependencies', 'processes', 'validation', 'setup')) {
    . (Join-Path $PSScriptRoot "lib/$library.ps1")
}
function Write-DevResult {
    param([Parameter(Mandatory = $true)][object] $Result)

    if ($Format -eq 'Object') { Write-Output $Result }
    else { $Result | ConvertTo-Json -Depth 12 -Compress }
}

if ($MyInvocation.InvocationName -ne '.') {
    try {
        $result = switch ($Command) {
            'inspect' { Get-ProjectInspection }
            'context' { Get-ProjectContext -Path $Path }
            'find' { Find-ProjectSource -Query $Query -Limit $Limit }
            'plan' { Get-ProjectValidationPlan -SkipTests:$SkipTests }
            'verify' { Invoke-ProjectVerification -SkipTests:$SkipTests -Stage $Stage }
            'diagnose' { Get-ProjectDiagnostics }
            'deps' { Get-DependencyInventory -Limit $Limit }
            'bootstrap' { Invoke-ToolBootstrap -Install:$Install }
        }
        Write-DevResult -Result $result
        if ($result.Status -eq 'pass' -or $result.Status -eq 'ok') { exit 0 }
        if ($result.Status -eq 'incomplete' -or $result.Status -eq 'unavailable') { exit 2 }
        exit 1
    }
    catch {
        Write-DevResult -Result ([pscustomobject]@{ Status = 'fail'; Error = $_.Exception.Message })
        exit 1
    }
}
