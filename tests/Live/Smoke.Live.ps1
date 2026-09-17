[CmdletBinding()]
param()
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$WarningPreference = 'SilentlyContinue'
$VerbosePreference = 'SilentlyContinue'
$DebugPreference = 'SilentlyContinue'
$InformationPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'Live.Common.ps1')

# Opt-in, installed module only (signed or unsigned). Runs the real cmdlets end to end, the way a
# user does, so typed parsing and rendering meet real responses. Prints only
# PASS|FAIL|INCONCLUSIVE <SMOKE-n> <structural note>: error codes, operation names and JSON paths,
# never work values. Reports are rendered into a temporary folder that is deleted before exit.
#   ADOTOOLKIT_LIVE_PROFILE        profile with a default project (required)
#   ADOTOOLKIT_LIVE_TEST_BUILD_ID  build with automated test runs (or ADOTOOLKIT_LIVE_DEFINITION)
#   ADOTOOLKIT_LIVE_DEFINITION     definition ID or name; its latest completed build is used
#   ADOTOOLKIT_LIVE_PLAN_ID, ADOTOOLKIT_LIVE_SUITE_ID  optional Test Case report check
$checkStates = [System.Collections.Generic.List[string]]::new()
function Write-AdoLiveResult {
    param([Parameter(Mandatory = $true)][string] $Text)
    $checkStates.Add($Text.Split(' ')[0])
    Write-Output $Text
}

# The error code, operation and JSON path of a failure; anything else is dropped.
function Get-AdoLiveFailureNote {
    param([Parameter(Mandatory = $true)][System.Management.Automation.ErrorRecord] $Record)
    $code = ($Record.FullyQualifiedErrorId -split ',')[0]
    if ($code -notmatch '^[A-Za-z]+$') { $code = 'CHECK_FAILED' }
    $note = $code
    $exception = $Record.Exception
    if ($null -ne $exception.PSObject.Properties['Operation'] -and "$($exception.Operation)" -match '^[A-Za-z]+$') {
        $note += ' OP=' + $exception.Operation
    }
    $inner = $exception.InnerException
    if ($inner -is [System.Text.Json.JsonException]) {
        $path = "$($inner.Path)"
        $note += ' PATH=' + (Get-AdoSafeJsonPath $path)
    }
    return $note
}

# Writes the step's result line; $script:stepPassed tells the caller whether later steps can run.
function Invoke-AdoLiveStep {
    param([Parameter(Mandatory = $true)][string] $Id, [Parameter(Mandatory = $true)][scriptblock] $Action)
    $script:stepPassed = $false
    try {
        $note = & $Action
        $script:stepPassed = $true
        Write-AdoLiveResult ("PASS $Id " + $note)
    }
    catch {
        $note = if ($_.Exception.Message -match '^[A-Z_]+$') { $_.Exception.Message } else { Get-AdoLiveFailureNote -Record $_ }
        Write-AdoLiveResult "FAIL $Id $note"
    }
}

if ([string]::IsNullOrWhiteSpace($env:ADOTOOLKIT_LIVE_PROFILE)) {
    Write-AdoLiveResult 'INCONCLUSIVE SMOKE-1 PROFILE_REQUIRED'
    exit 2
}
$buildId = 0
$hasBuild = [int]::TryParse($env:ADOTOOLKIT_LIVE_TEST_BUILD_ID, [ref] $buildId) -and $buildId -gt 0
$hasDefinition = -not [string]::IsNullOrWhiteSpace($env:ADOTOOLKIT_LIVE_DEFINITION)
$planId = 0
$suiteId = 0
$hasSuite = [int]::TryParse($env:ADOTOOLKIT_LIVE_PLAN_ID, [ref] $planId) -and $planId -gt 0 -and
    [int]::TryParse($env:ADOTOOLKIT_LIVE_SUITE_ID, [ref] $suiteId) -and $suiteId -gt 0
$scratch = Join-Path ([System.IO.Path]::GetTempPath()) ('adotoolkit-smoke-' + [guid]::NewGuid().ToString('N'))
try {
    . (Join-Path $PSScriptRoot '../../tools/package/Package.Common.ps1')
    $installed = Import-AdoInstalledModule
    Write-AdoLiveResult ('PASS SMOKE-1 MODULE_LOADED SIGNED=' + $installed.Signed)
    Invoke-AdoLiveStep 'SMOKE-2' {
        $connection = Connect-Ado -Profile $env:ADOTOOLKIT_LIVE_PROFILE
        if ([string]::IsNullOrWhiteSpace($connection.DefaultProject)) { throw 'PROFILE_DEFAULT_PROJECT_REQUIRED' }
        $result = Test-AdoConnection -ErrorAction Stop
        if (-not $result.Success) { throw 'CONNECTION_FAILED' }
        $null = Get-AdoProject -ErrorAction Stop
        'CONNECT_TEST_PROJECTS'
    }
    if (-not $script:stepPassed) { exit 1 }

    $script:build = $null
    if (-not $hasBuild -and -not $hasDefinition) {
        foreach ($item in @('SMOKE-3', 'SMOKE-4', 'SMOKE-5')) { Write-AdoLiveResult "INCONCLUSIVE $item TEST_BUILD_OR_DEFINITION_REQUIRED" }
    }
    else {
        Invoke-AdoLiveStep 'SMOKE-3' {
            $runs = if ($hasBuild) { @(Get-AdoTestRun -BuildId $buildId -ErrorAction Stop) }
            else {
                $definitionId = 0
                $query = if ([int]::TryParse($env:ADOTOOLKIT_LIVE_DEFINITION, [ref] $definitionId)) { $definitionId } else { $env:ADOTOOLKIT_LIVE_DEFINITION }
                $latest = @(Get-AdoBuild -Definition $query -Latest -ErrorAction Stop)
                if ($latest.Count -eq 0) { throw 'DEFINITION_HAS_NO_COMPLETED_BUILD' }
                $script:build = $latest[0]
                @($latest[0] | Get-AdoTestRun -ErrorAction Stop)
            }
            if ($runs.Count -eq 0) { throw 'NO_TEST_RUNS' }
            $kinds = @()
            if (@($runs | Where-Object { $_.OutcomeCounts.Count -gt 0 }).Count -gt 0) { $kinds += 'STATISTICS' }
            if (@($runs | Where-Object { $null -ne $_.PassedTests }).Count -gt 0) { $kinds += 'AGGREGATES' }
            'RUNS_READ COUNTS=' + ($(if ($kinds.Count -gt 0) { $kinds -join '+' } else { 'NONE' }))
        }
        $runsOk = $script:stepPassed
        $script:set = $null
        $failuresOk = $false
        if ($runsOk) {
            Invoke-AdoLiveStep 'SMOKE-4' {
                $script:set = if ($null -ne $script:build) { $script:build | Get-AdoBuildTestFailure -ErrorAction Stop }
                else { Get-AdoBuildTestFailure -BuildId $buildId -ErrorAction Stop }
                $codes = @($script:set.Diagnostics | ForEach-Object { [string] $_.Code } | Where-Object { $_ -match '^[A-Za-z]+$' } | Sort-Object -Unique)
                'STATUS=' + $script:set.Status + ' DIAGNOSTICS=' + ($(if ($codes.Count -gt 0) { $codes -join ',' } else { 'NONE' }))
            }
            $failuresOk = $script:stepPassed
        }
        else { Write-AdoLiveResult 'INCONCLUSIVE SMOKE-4 RUNS_REQUIRED' }
        if ($failuresOk) {
            Invoke-AdoLiveStep 'SMOKE-5' {
                $file = $script:set | Export-AdoBuildTestFailure -Path (Join-Path $scratch 'failures') -SkipAttachments -ErrorAction Stop
                if (-not (Test-Path -LiteralPath $file.FullName -PathType Leaf) -or $file.Length -eq 0) { throw 'REPORT_MISSING' }
                'REPORT_RENDERED'
            }
        }
        else { Write-AdoLiveResult 'INCONCLUSIVE SMOKE-5 FAILURE_SET_REQUIRED' }
    }

    if (-not $hasSuite) { Write-AdoLiveResult 'INCONCLUSIVE SMOKE-6 PLAN_AND_SUITE_REQUIRED' }
    else {
        Invoke-AdoLiveStep 'SMOKE-6' {
            $null = Get-AdoTestPlan -Id $planId -ErrorAction Stop
            $null = Get-AdoTestSuite -PlanId $planId -ErrorAction Stop
            $cases = @(Get-AdoTestCase -PlanId $planId -SuiteId $suiteId -ErrorAction Stop)
            if ($cases.Count -eq 0) { throw 'SUITE_HAS_NO_TEST_CASES' }
            [void] [System.IO.Directory]::CreateDirectory((Join-Path $scratch 'cases'))
            $file = $cases | Export-AdoTestCase -Path (Join-Path $scratch 'cases') -ErrorAction Stop
            if (-not (Test-Path -LiteralPath $file.FullName -PathType Leaf)) { throw 'REPORT_MISSING' }
            'PLAN_SUITE_CASES_REPORT'
        }
    }

    if ($checkStates.Contains('FAIL')) { exit 1 }
    if ($checkStates.Contains('INCONCLUSIVE')) { exit 2 }
    exit 0
}
catch {
    $note = if ($_.Exception.Message -match '^[A-Z_]+$') { $_.Exception.Message } else { Get-AdoLiveFailureNote -Record $_ }
    Write-AdoLiveResult "FAIL SMOKE-1 $note"
    exit 1
}
finally {
    if (Get-Command -Name Disconnect-Ado -ErrorAction SilentlyContinue) { Disconnect-Ado | Out-Null }
    if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue }
}
