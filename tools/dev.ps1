#requires -Version 7.6
<#
.SYNOPSIS
Compact, machine-oriented repository discovery and validation for coding agents.

.DESCRIPTION
Detects the repository instead of assuming an ecosystem. The default output is
compressed JSON so an agent can collect project state without reading several
configuration files or parsing human-oriented command output.
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('inspect', 'context', 'find', 'plan', 'verify', 'diagnose', 'deps', 'bootstrap', 'init')]
    [string] $Command = 'context',

    [string] $Query,

    [string] $Path,

    [string[]] $Stage,

    [string] $ProjectName,

    [string] $Description,

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
$script:ExcludedDirectoryNames = @('.git', '.vs', '.idea', 'node_modules', '.venv', 'venv', 'bin', 'obj', 'artifacts', 'dist', 'build', 'coverage', 'TestResults', 'target', '__pycache__', '.pytest_cache', '.ruff_cache', '.mypy_cache', '.next', '.nuxt', '.cache', '.nuget')
$script:SensitiveDirectoryNames = @('secret', 'secrets')
$script:SensitiveFileGlobs = @('.env', '.env.*', 'id_rsa*', 'id_ed25519*', '*.pem', '*.pfx', '*.p12', '*.key', '*.log', '*credentials*', '*.local.*', '.npmrc', '.pypirc', '.netrc')
# Sensitive directories must be rejected wherever they occur in a path; sensitive file
# names are rejected at the final path component. This remains a final safety check even
# when the walker or ripgrep globs already excluded the path earlier.
$script:SensitivePathPattern = '(?:^|[\\/])(?:\.env(?:\.[^\\/]*)?|secrets?)(?:[\\/]|$)|(?:^|[\\/])(?:id_(?:rsa|ed25519)[^\\/]*|[^\\/]*credentials[^\\/]*|[^\\/]*\.local\.[^\\/]*|\.(?:npmrc|pypirc|netrc)|[^\\/]*\.(?:pem|pfx|p12|key|log))$'
$script:ConfigurationExtensions = @('.json', '.xml', '.config', '.csproj', '.fsproj', '.vbproj', '.props', '.targets', '.slnx')

foreach ($library in @('discovery', 'dependencies', 'validation', 'setup')) {
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
            'init' { Initialize-Project -ProjectName $ProjectName -Description $Description }
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
