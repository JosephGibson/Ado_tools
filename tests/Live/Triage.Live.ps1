[CmdletBinding()]
param()
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$WarningPreference = 'SilentlyContinue'
$VerbosePreference = 'SilentlyContinue'
$DebugPreference = 'SilentlyContinue'
$InformationPreference = 'SilentlyContinue'

# Opt-in, installed module only (signed or unsigned). Keep response bodies and ranges in memory.
# Never print work values or exception messages and never invoke Save-AdoBuildLog here.
$checkStates = [System.Collections.Generic.List[string]]::new()
function Write-AdoLiveResult {
    param([Parameter(Mandatory = $true)][string] $Text)
    $checkStates.Add($Text.Split(' ')[0])
    Write-Output $Text
}

$buildId = 0
$retriedId = 0
if ([string]::IsNullOrWhiteSpace($env:ADOTOOLKIT_LIVE_PROFILE) -or
    [string]::IsNullOrWhiteSpace($env:ADOTOOLKIT_LIVE_DEFINITION) -or
    -not [int]::TryParse($env:ADOTOOLKIT_LIVE_BUILD_ID, [ref] $buildId) -or $buildId -lt 1) {
    foreach ($item in @('V-11', 'V-14')) { Write-AdoLiveResult "INCONCLUSIVE $item PROFILE_DEFINITION_AND_BUILD_REQUIRED" }
    exit 2
}
$hasRetried = [int]::TryParse($env:ADOTOOLKIT_LIVE_RETRIED_BUILD_ID, [ref] $retriedId) -and $retriedId -gt 0

function Invoke-AdoTriageRequest {
    param([Parameter(Mandatory = $true)][string] $Uri, [string] $Accept = 'application/json')
    $response = Invoke-WebRequest -Uri $Uri -Method Get -UseDefaultCredentials -SkipHttpErrorCheck -MaximumRedirection 0 `
        -Headers @{ 'Accept-Language' = $PSUICulture; Accept = $Accept } -TimeoutSec $connection.RequestTimeoutSeconds
    if ([int] $response.StatusCode -ne 200) { throw 'HTTP_STATUS_DIFFERS' }
    return $response
}

function Get-AdoTriagePageSummary {
    param([Parameter(Mandatory = $true)][string] $BaseUri)
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $items = [System.Collections.Generic.List[object]]::new()
    $token = $null
    for ($page = 0; $page -lt 50; $page++) {
        $query = 'api-version=6.0&%24top=1'
        if ($null -ne $token) { $query += '&continuationToken=' + [uri]::EscapeDataString($token) }
        $response = Invoke-AdoTriageRequest -Uri ($BaseUri + '?' + $query)
        $data = $response.Content | ConvertFrom-Json
        if ($null -eq $data -or $null -eq $data.PSObject.Properties['value']) { throw 'INVALID_PAGE' }
        foreach ($item in @($data.value)) { $items.Add($item) }
        $values = @($response.Headers['x-ms-continuationtoken'])
        $token = if ($values.Count -gt 0 -and -not [string]::IsNullOrEmpty([string] $values[0])) { [string] $values[0] } else { $null }
        if ($null -eq $token) { return [pscustomobject]@{ Continued = $seen.Count -gt 0; Complete = $true; Items = $items.ToArray() } }
        if (-not $seen.Add($token)) { throw 'REPEATED_TOKEN' }
    }
    return [pscustomobject]@{ Continued = $seen.Count -gt 0; Complete = $false; Items = $items.ToArray() }
}

function Get-AdoTriageLine {
    param([AllowEmptyString()][string] $Text)
    # StringReader handles CRLF/LF and a final unterminated line without inventing a trailing line.
    $reader = [IO.StringReader]::new($Text)
    try {
        while ($null -ne ($line = $reader.ReadLine())) { Write-Output -InputObject $line -NoEnumerate }
    }
    finally { $reader.Dispose() }
}

try {
    . (Join-Path $PSScriptRoot '../../tools/package/Package.Common.ps1')
    # The newest installed release; a signed release must verify throughout, an unsigned one is accepted.
    $null = Import-AdoInstalledModule
    $connection = Connect-Ado -Profile $env:ADOTOOLKIT_LIVE_PROFILE
    if ([string]::IsNullOrWhiteSpace($connection.DefaultProject)) {
        foreach ($item in @('V-11', 'V-14')) { Write-AdoLiveResult "INCONCLUSIVE $item PROFILE_DEFAULT_PROJECT_REQUIRED" }
        exit 2
    }
    $projectBase = $connection.CollectionUri.AbsoluteUri.TrimEnd('/') + '/' + [uri]::EscapeDataString($connection.DefaultProject)
    $buildBase = $projectBase + '/_apis/build/builds/' + $buildId.ToString([cultureinfo]::InvariantCulture)

    try {
        $timeline = (Invoke-AdoTriageRequest -Uri ($buildBase + '/timeline?api-version=6.0')).Content | ConvertFrom-Json
        $records = @($timeline.records)
        $knownTypes = @('Stage', 'Phase', 'Job', 'Task')
        $unknown = @($records | Where-Object { $knownTypes -cnotcontains $_.type })
        if ($records.Count -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-11 TIMELINE_EMPTY' }
        elseif ($unknown.Count -gt 0) { Write-AdoLiveResult 'FAIL V-11 RECORD_TYPES_DIFFER' }
        elseif (-not $hasRetried) { Write-AdoLiveResult 'INCONCLUSIVE V-11 TYPES_AGREE_RETRIED_BUILD_REQUIRED' }
        else {
            $retryBase = $projectBase + '/_apis/build/builds/' + $retriedId.ToString([cultureinfo]::InvariantCulture)
            $retryTimeline = (Invoke-AdoTriageRequest -Uri ($retryBase + '/timeline?api-version=6.0')).Content | ConvertFrom-Json
            $attempts = @($retryTimeline.records | Where-Object {
                $null -ne $_.PSObject.Properties['previousAttempts'] -and $null -ne $_.previousAttempts -and @($_.previousAttempts).Count -gt 0
            })
            if ($attempts.Count -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-11 NO_PREVIOUS_ATTEMPTS_OBSERVED' }
            else {
                $agrees = $true
                foreach ($record in $attempts) {
                    if ([int] $record.attempt -lt 2 -or [string]::IsNullOrEmpty([string] $record.identifier)) { $agrees = $false }
                    foreach ($previous in @($record.previousAttempts)) {
                        if ([int] $previous.attempt -lt 1 -or [int] $previous.attempt -ge [int] $record.attempt -or
                            [string]::IsNullOrEmpty([string] $previous.timelineId) -or [string]::IsNullOrEmpty([string] $previous.recordId)) { $agrees = $false }
                    }
                }
                if ($agrees) { Write-AdoLiveResult 'PASS V-11 TYPES_AND_RETRY_SHAPES_AGREE' }
                else { Write-AdoLiveResult 'FAIL V-11 RETRY_SHAPES_DIFFER' }
            }
        }
    }
    catch { Write-AdoLiveResult 'FAIL V-11 CHECK_FAILED' }

    try {
        $definitions = Get-AdoTriagePageSummary -BaseUri ($projectBase + '/_apis/build/definitions')
        $builds = Get-AdoTriagePageSummary -BaseUri ($projectBase + '/_apis/build/builds')
        $definitionId = 0
        $query = if ([int]::TryParse($env:ADOTOOLKIT_LIVE_DEFINITION, [ref] $definitionId)) { $definitionId } else { $env:ADOTOOLKIT_LIVE_DEFINITION }
        $resolved = @(Get-AdoBuild -Definition $query -Latest)
        if ($resolved.Count -eq 0) { throw 'DEFINITION_HAS_NO_COMPLETED_BUILD' }
        $response = Invoke-AdoTriageRequest -Uri ($buildBase + '/logs?api-version=6.0')
        if ([string] $response.Headers['Content-Type'] -notmatch 'application/json') { throw 'LOG_LIST_NOT_JSON' }
        $logs = @((($response.Content | ConvertFrom-Json).value))
        $usable = @($logs | Where-Object { $null -ne $_.PSObject.Properties['lineCount'] -and [long] $_.lineCount -ge 3 })
        if ($usable.Count -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-14 NONEMPTY_LOG_WITH_LINECOUNT_REQUIRED' }
        else {
            $log = $usable[0]
            $count = [long] $log.lineCount
            $rangeBase = $buildBase + '/logs/' + ([int] $log.id).ToString([cultureinfo]::InvariantCulture)
            # Independent zero-based inclusive probe: first line, then the final two and final one.
            $first = @(Get-AdoTriageLine -Text (Invoke-AdoTriageRequest -Uri ($rangeBase + '?api-version=6.0&startLine=0&endLine=0') -Accept 'text/plain').Content)
            $end = ($count - 1).ToString([cultureinfo]::InvariantCulture)
            $start = ($count - 2).ToString([cultureinfo]::InvariantCulture)
            $lastTwo = @(Get-AdoTriageLine -Text (Invoke-AdoTriageRequest -Uri ($rangeBase + '?api-version=6.0&startLine=' + $start + '&endLine=' + $end) -Accept 'text/plain').Content)
            $lastOne = @(Get-AdoTriageLine -Text (Invoke-AdoTriageRequest -Uri ($rangeBase + '?api-version=6.0&startLine=' + $end + '&endLine=' + $end) -Accept 'text/plain').Content)
            if ($first.Count -ne 1 -or $lastTwo.Count -ne 2 -or $lastOne.Count -ne 1 -or $lastTwo[1] -cne $lastOne[0]) {
                Write-AdoLiveResult 'FAIL V-14 ZERO_BASED_INCLUSIVE_RANGES_DIFFER'
            }
            elseif (-not $definitions.Complete -or -not $builds.Complete) { Write-AdoLiveResult 'INCONCLUSIVE V-14 PAGE_LIMIT_REACHED_RANGES_AGREE' }
            elseif (-not $definitions.Continued -or -not $builds.Continued) { Write-AdoLiveResult 'INCONCLUSIVE V-14 RANGES_AGREE_BOTH_CONTINUATION_HEADERS_REQUIRED' }
            else { Write-AdoLiveResult 'PASS V-14 BOTH_CONTINUATIONS_JSON_LINECOUNT_AND_RANGES_AGREE' }
        }
    }
    catch { Write-AdoLiveResult 'FAIL V-14 CHECK_FAILED' }

    # S4-4 also requires the manual pipeline and UI comparison in plan section 8.
    if ($checkStates.Contains('FAIL')) { exit 1 }
    if ($checkStates.Contains('INCONCLUSIVE')) { exit 2 }
    exit 0
}
catch {
    foreach ($item in @('V-11', 'V-14')) { Write-AdoLiveResult "FAIL $item CHECK_FAILED" }
    exit 1
}
finally {
    if (Get-Command -Name Disconnect-Ado -ErrorAction SilentlyContinue) { Disconnect-Ado | Out-Null }
}
