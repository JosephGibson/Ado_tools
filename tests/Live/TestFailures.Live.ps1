[CmdletBinding()]
param()
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$WarningPreference = 'SilentlyContinue'
$VerbosePreference = 'SilentlyContinue'
$DebugPreference = 'SilentlyContinue'
$InformationPreference = 'SilentlyContinue'

# Opt-in, signed installed module only. Holds every response and downloaded byte in memory and
# writes nothing. Never prints work values: only PASS|FAIL|INCONCLUSIVE <V-ID> <structural note>.
$checkStates = [System.Collections.Generic.List[string]]::new()
$items = @('V-19', 'V-20', 'V-21', 'V-22', 'V-23', 'V-24', 'V-25')
function Write-AdoLiveResult {
    param([Parameter(Mandatory = $true)][string] $Text)
    $checkStates.Add($Text.Split(' ')[0])
    Write-Output $Text
}

$buildId = 0
$rerunId = 0
$reattemptId = 0
if ([string]::IsNullOrWhiteSpace($env:ADOTOOLKIT_LIVE_PROFILE) -or
    -not [int]::TryParse($env:ADOTOOLKIT_LIVE_TEST_BUILD_ID, [ref] $buildId) -or $buildId -lt 1) {
    foreach ($item in $items) { Write-AdoLiveResult "INCONCLUSIVE $item PROFILE_AND_TEST_BUILD_REQUIRED" }
    exit 2
}
$hasRerun = [int]::TryParse($env:ADOTOOLKIT_LIVE_RERUN_BUILD_ID, [ref] $rerunId) -and $rerunId -gt 0
$hasReattempt = [int]::TryParse($env:ADOTOOLKIT_LIVE_REATTEMPT_BUILD_ID, [ref] $reattemptId) -and $reattemptId -gt 0

function Invoke-AdoTestRequest {
    param([Parameter(Mandatory = $true)][string] $Uri, [string] $Accept = 'application/json')
    $response = Invoke-WebRequest -Uri $Uri -Method Get -UseDefaultCredentials -SkipHttpErrorCheck -MaximumRedirection 0 `
        -Headers @{ 'Accept-Language' = $PSUICulture; Accept = $Accept } -TimeoutSec $connection.RequestTimeoutSeconds
    if ([int] $response.StatusCode -ne 200) { throw 'HTTP_STATUS_DIFFERS' }
    return $response
}

function Test-AdoLiveProperty {
    param([Parameter(Mandatory = $true)][object] $Object, [Parameter(Mandatory = $true)][string] $Name)
    return $null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]
}

function Get-AdoLiveMissingField {
    param([Parameter(Mandatory = $true)][object] $Object, [Parameter(Mandatory = $true)][string[]] $Names)
    return @($Names | Where-Object { -not (Test-AdoLiveProperty -Object $Object -Name $_) })
}

# TopSkip enumeration with the toolkit's own rule: advance by the count returned, stop when empty.
function Get-AdoLiveTopSkip {
    param(
        [Parameter(Mandatory = $true)][string] $BaseUri,
        [Parameter(Mandatory = $true)][string] $Query,
        [Parameter(Mandatory = $true)][int] $Top
    )
    $items = [System.Collections.Generic.List[object]]::new()
    $skip = 0
    $pages = 0
    for ($page = 0; $page -lt 50; $page++) {
        $uri = $BaseUri + '?' + $Query + '&%24top=' + $Top.ToString([cultureinfo]::InvariantCulture) +
            '&%24skip=' + $skip.ToString([cultureinfo]::InvariantCulture)
        $data = (Invoke-AdoTestRequest -Uri $uri).Content | ConvertFrom-Json
        $pages++
        if (-not (Test-AdoLiveProperty -Object $data -Name 'value')) { throw 'INVALID_PAGE' }
        $batch = @($data.value)
        foreach ($item in $batch) { $items.Add($item) }
        if ($batch.Count -eq 0) { return [pscustomobject]@{ Items = $items.ToArray(); Pages = $pages; Complete = $true } }
        $skip += $batch.Count
    }
    return [pscustomobject]@{ Items = $items.ToArray(); Pages = $pages; Complete = $false }
}

function Get-AdoLiveBuildRunList {
    param([Parameter(Mandatory = $true)][string] $ProjectBase, [Parameter(Mandatory = $true)][int] $BuildId)
    $buildUri = [uri]::EscapeDataString('vstfs:///Build/Build/' + $BuildId.ToString([cultureinfo]::InvariantCulture))
    return Get-AdoLiveTopSkip -BaseUri ($ProjectBase + '/_apis/test/runs') `
        -Query ('api-version=6.0&buildUri=' + $buildUri + '&includeRunDetails=true') -Top 100
}

try {
    . (Join-Path $PSScriptRoot '../../tools/package/Package.Common.ps1')
    $installedRoot = Resolve-AdoPackagePath -Path (Get-AdoModuleRoot)
    $candidate = Get-Module -ListAvailable -Name AdoToolkit | Select-Object -First 1
    if ($null -eq $candidate) { throw 'INSTALLED_MODULE_REQUIRED' }
    $package = Resolve-AdoPackagePath -Path $candidate.ModuleBase -Root $installedRoot
    $signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $package 'AdoToolkit.psd1')
    if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate) { throw 'SIGNATURE_INVALID' }
    Assert-AdoPackageSignature -PackagePath $package -ExpectedThumbprint $signature.SignerCertificate.Thumbprint
    Import-Module -Name (Join-Path $package 'AdoToolkit.psd1') -Force
    $connection = Connect-Ado -Profile $env:ADOTOOLKIT_LIVE_PROFILE
    if ([string]::IsNullOrWhiteSpace($connection.DefaultProject)) {
        foreach ($item in $items) { Write-AdoLiveResult "INCONCLUSIVE $item PROFILE_DEFAULT_PROJECT_REQUIRED" }
        exit 2
    }
    $projectBase = $connection.CollectionUri.AbsoluteUri.TrimEnd('/') + '/' + [uri]::EscapeDataString($connection.DefaultProject)
    $buildText = $buildId.ToString([cultureinfo]::InvariantCulture)

    # V-25: BuildGet fields and the BuildsList history parameters.
    $build = $null
    try {
        $build = (Invoke-AdoTestRequest -Uri ($projectBase + '/_apis/build/builds/' + $buildText + '?api-version=6.0')).Content | ConvertFrom-Json
        $missing = Get-AdoLiveMissingField -Object $build -Names @('uri', 'definition', 'buildNumber', 'sourceBranch',
            'sourceVersion', 'repository', 'result', 'status', 'finishTime', 'queueTime')
        $composed = 'vstfs:///Build/Build/' + $buildText
        $uriAgrees = (Test-AdoLiveProperty -Object $build -Name 'uri') -and ([string] $build.uri) -eq $composed
        $maxTime = if ((Test-AdoLiveProperty -Object $build -Name 'finishTime') -and $null -ne $build.finishTime) {
            ([datetimeoffset] $build.finishTime).ToUniversalTime().ToString('o', [cultureinfo]::InvariantCulture)
        }
        else { ([datetimeoffset] $build.queueTime).ToUniversalTime().ToString('o', [cultureinfo]::InvariantCulture) }
        $window = $projectBase + '/_apis/build/builds?api-version=6.0&definitions=' +
            ([int] $build.definition.id).ToString([cultureinfo]::InvariantCulture) +
            '&statusFilter=completed&queryOrder=finishTimeDescending&maxTime=' + [uri]::EscapeDataString($maxTime) +
            '&branchName=' + [uri]::EscapeDataString([string] $build.sourceBranch)
        $history = @(((Invoke-AdoTestRequest -Uri $window).Content | ConvertFrom-Json).value)
        $ordered = $true
        $previous = $null
        foreach ($entry in $history) {
            if (-not (Test-AdoLiveProperty -Object $entry -Name 'finishTime') -or $null -eq $entry.finishTime) { continue }
            $current = [datetimeoffset] $entry.finishTime
            if ($null -ne $previous -and $current -gt $previous) { $ordered = $false }
            $previous = $current
        }
        if ($missing.Count -gt 0) { Write-AdoLiveResult "FAIL V-25 BUILD_FIELDS_MISSING $($missing -join ',')" }
        elseif ($history.Count -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-25 BUILD_FIELDS_PRESENT_HISTORY_WINDOW_EMPTY' }
        elseif (-not $ordered) { Write-AdoLiveResult 'FAIL V-25 HISTORY_ORDER_DIFFERS' }
        elseif (-not $uriAgrees) { Write-AdoLiveResult 'PASS V-25 FIELDS_AND_WINDOW_AGREE_URI_FORM_DIFFERS' }
        else { Write-AdoLiveResult 'PASS V-25 FIELDS_WINDOW_AND_URI_FORM_AGREE' }
    }
    catch { Write-AdoLiveResult 'FAIL V-25 CHECK_FAILED' }

    # V-19: TestRunsList route, version, buildUri filter, includeRunDetails and TopSkip paging.
    $runs = @()
    $hasAttempt = $false
    try {
        $listing = Get-AdoLiveBuildRunList -ProjectBase $projectBase -BuildId $buildId
        $runs = @($listing.Items)
        $missing = @()
        foreach ($run in $runs) {
            $missing += Get-AdoLiveMissingField -Object $run -Names @('id', 'name', 'state', 'isAutomated',
                'startedDate', 'completedDate', 'totalTests', 'runStatistics')
            if (Test-AdoLiveProperty -Object $run -Name 'pipelineReference') {
                if (Test-AdoLiveProperty -Object $run.pipelineReference -Name 'pipelineAttempt') { $hasAttempt = $true }
            }
        }
        $missing = @($missing | Sort-Object -Unique)
        if ($runs.Count -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-19 NO_RUNS_FOR_BUILD' }
        elseif ($missing.Count -gt 0) { Write-AdoLiveResult "FAIL V-19 RUN_FIELDS_MISSING $($missing -join ',')" }
        elseif (-not $listing.Complete) { Write-AdoLiveResult 'INCONCLUSIVE V-19 PAGE_LIMIT_REACHED_FIELDS_AGREE' }
        elseif ($hasAttempt) { Write-AdoLiveResult 'PASS V-19 ROUTE_FIELDS_PAGING_AND_PIPELINE_ATTEMPT_AGREE' }
        else { Write-AdoLiveResult 'PASS V-19 ROUTE_FIELDS_AND_PAGING_AGREE_NO_PIPELINE_ATTEMPT' }
    }
    catch { Write-AdoLiveResult 'FAIL V-19 CHECK_FAILED' }

    # V-20: TestResultsList $top maximum, $skip paging, listed fields, outcome spellings, and
    # whether the server outcomes filter hides rerun groups (filtered versus unfiltered counts).
    $results = @()
    try {
        if ($runs.Count -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-20 NO_RUNS_FOR_BUILD' }
        else {
            $runBase = $projectBase + '/_apis/test/Runs/' + ([int] $runs[0].id).ToString([cultureinfo]::InvariantCulture) + '/results'
            $unfiltered = Get-AdoLiveTopSkip -BaseUri $runBase -Query 'api-version=6.0&detailsToInclude=None' -Top 1000
            $results = @($unfiltered.Items)
            $missing = @()
            $outcomes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            $groups = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach ($result in $results) {
                $missing += Get-AdoLiveMissingField -Object $result -Names @('id', 'outcome', 'automatedTestName',
                    'automatedTestStorage', 'testCaseTitle', 'startedDate')
                if (Test-AdoLiveProperty -Object $result -Name 'outcome') { [void] $outcomes.Add([string] $result.outcome) }
                if (Test-AdoLiveProperty -Object $result -Name 'resultGroupType') { [void] $groups.Add([string] $result.resultGroupType) }
            }
            $missing = @($missing | Sort-Object -Unique)
            # Outcome names are server enumeration values, not work data.
            $known = @('Passed', 'Failed', 'Error', 'Timeout', 'Aborted', 'Inconclusive', 'NotExecuted', 'Blocked',
                'Warning', 'NotApplicable', 'NotImpacted', 'None', 'InProgress', 'Paused')
            $unknownOutcomes = @($outcomes | Where-Object { $known -cnotcontains $_ })
            $filteredCount = -1
            if ($results.Count -gt 0) {
                $filtered = Get-AdoLiveTopSkip -BaseUri $runBase `
                    -Query 'api-version=6.0&detailsToInclude=None&outcomes=Failed' -Top 1000
                $filteredCount = @($filtered.Items).Count
            }
            $rerunParents = @($results | Where-Object {
                (Test-AdoLiveProperty -Object $_ -Name 'resultGroupType') -and ([string] $_.resultGroupType) -ceq 'Rerun' })
            $hiddenReruns = @($rerunParents | Where-Object { ([string] $_.outcome) -cne 'Failed' }).Count
            if ($results.Count -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-20 RUN_HAS_NO_RESULTS' }
            elseif ($missing.Count -gt 0) { Write-AdoLiveResult "FAIL V-20 RESULT_FIELDS_MISSING $($missing -join ',')" }
            elseif ($unknownOutcomes.Count -gt 0) { Write-AdoLiveResult "INCONCLUSIVE V-20 UNKNOWN_OUTCOME_SPELLINGS $($unknownOutcomes -join ',')" }
            elseif (-not $unfiltered.Complete) { Write-AdoLiveResult 'INCONCLUSIVE V-20 PAGE_LIMIT_REACHED_FIELDS_AGREE' }
            elseif ($hiddenReruns -gt 0 -and $filteredCount -ge 0) {
                Write-AdoLiveResult "PASS V-20 FIELDS_AND_PAGING_AGREE_OUTCOMES_FILTER_HIDES_RERUN_GROUPS UNFILTERED=$($results.Count) FILTERED=$filteredCount"
            }
            elseif ($groups.Count -eq 0) { Write-AdoLiveResult "PASS V-20 FIELDS_AND_PAGING_AGREE_NO_GROUP_TYPES UNFILTERED=$($results.Count) FILTERED=$filteredCount" }
            else { Write-AdoLiveResult "PASS V-20 FIELDS_PAGING_AND_GROUP_TYPES_AGREE UNFILTERED=$($results.Count) FILTERED=$filteredCount" }
        }
    }
    catch { Write-AdoLiveResult 'FAIL V-20 CHECK_FAILED' }

    # V-21: TestResultGet detail field names and the testCase.id type.
    $detail = $null
    try {
        $failing = @($results | Where-Object { @('Failed', 'Error', 'Timeout', 'Aborted') -ccontains ([string] $_.outcome) })
        if ($failing.Count -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-21 NO_FAILING_RESULT_IN_FIRST_RUN' }
        else {
            $detailUri = $projectBase + '/_apis/test/Runs/' + ([int] $runs[0].id).ToString([cultureinfo]::InvariantCulture) +
                '/results/' + ([int] $failing[0].id).ToString([cultureinfo]::InvariantCulture) +
                '?api-version=6.0&detailsToInclude=' + [uri]::EscapeDataString('Iterations,WorkItems,SubResults')
            $detail = (Invoke-AdoTestRequest -Uri $detailUri).Content | ConvertFrom-Json
            $expected = @('errorMessage', 'stackTrace', 'failureType', 'resolutionState', 'comment', 'computerName',
                'durationInMs', 'owner', 'runBy', 'priority', 'associatedBugs', 'failingSince', 'customFields')
            $absent = Get-AdoLiveMissingField -Object $detail -Names $expected
            $caseType = 'absent'
            if ((Test-AdoLiveProperty -Object $detail -Name 'testCase') -and $null -ne $detail.testCase) {
                if (Test-AdoLiveProperty -Object $detail.testCase -Name 'id') {
                    $caseType = if ($detail.testCase.id -is [string]) { 'string' } else { $detail.testCase.id.GetType().Name }
                }
            }
            if ($absent.Count -eq $expected.Count) { Write-AdoLiveResult 'FAIL V-21 NO_DETAIL_FIELDS_PRESENT' }
            elseif ($caseType -eq 'string') { Write-AdoLiveResult "PASS V-21 DETAIL_FIELDS_AGREE_TESTCASE_ID_STRING ABSENT=$($absent -join ',')" }
            else { Write-AdoLiveResult "FAIL V-21 TESTCASE_ID_TYPE_DIFFERS $caseType" }
        }
    }
    catch { Write-AdoLiveResult 'FAIL V-21 CHECK_FAILED' }

    # V-22: how in-task reruns and job or stage re-attempts are recorded, and any flaky metadata.
    try {
        $notes = [System.Collections.Generic.List[string]]::new()
        if ($null -ne $detail) {
            if ((Test-AdoLiveProperty -Object $detail -Name 'subResults') -and $null -ne $detail.subResults) {
                $subGroups = @(@($detail.subResults) | ForEach-Object {
                    if (Test-AdoLiveProperty -Object $_ -Name 'resultGroupType') { [string] $_.resultGroupType } else { 'none' } })
                $notes.Add('SUBRESULTS=' + (@($subGroups | Sort-Object -Unique) -join '/'))
                $sequenced = @(@($detail.subResults) | Where-Object { Test-AdoLiveProperty -Object $_ -Name 'sequenceId' }).Count
                $notes.Add('SEQUENCEIDS=' + $sequenced.ToString([cultureinfo]::InvariantCulture))
            }
            $flaky = @($detail.PSObject.Properties.Name | Where-Object { $_ -match 'flaky' })
            $flakyNote = if ($flaky.Count -gt 0) { @($flaky) -join '/' } else { 'none' }
            $notes.Add('FLAKYFIELDS=' + $flakyNote)
        }
        if ($hasRerun) {
            $rerunRuns = @((Get-AdoLiveBuildRunList -ProjectBase $projectBase -BuildId $rerunId).Items)
            $parents = 0
            foreach ($run in $rerunRuns) {
                $page = Get-AdoLiveTopSkip -BaseUri ($projectBase + '/_apis/test/Runs/' +
                    ([int] $run.id).ToString([cultureinfo]::InvariantCulture) + '/results') `
                    -Query 'api-version=6.0&detailsToInclude=None' -Top 1000
                $parents += @(@($page.Items) | Where-Object {
                    (Test-AdoLiveProperty -Object $_ -Name 'resultGroupType') -and ([string] $_.resultGroupType) -ceq 'Rerun' }).Count
            }
            $notes.Add('RERUNBUILD_RUNS=' + $rerunRuns.Count.ToString([cultureinfo]::InvariantCulture) +
                ' RERUN_PARENTS=' + $parents.ToString([cultureinfo]::InvariantCulture))
        }
        if ($hasReattempt) {
            $reRuns = @((Get-AdoLiveBuildRunList -ProjectBase $projectBase -BuildId $reattemptId).Items)
            $attempts = @($reRuns | Where-Object {
                (Test-AdoLiveProperty -Object $_ -Name 'pipelineReference') -and
                (Test-AdoLiveProperty -Object $_.pipelineReference -Name 'pipelineAttempt') }).Count
            $notes.Add('REATTEMPTBUILD_RUNS=' + $reRuns.Count.ToString([cultureinfo]::InvariantCulture) +
                ' WITH_ATTEMPT=' + $attempts.ToString([cultureinfo]::InvariantCulture))
        }
        $note = if ($notes.Count -gt 0) { $notes -join ' ' } else { 'NO_DETAIL_OBSERVED' }
        if (-not $hasRerun -and -not $hasReattempt) { Write-AdoLiveResult "INCONCLUSIVE V-22 RETRY_BUILDS_REQUIRED $note" }
        elseif ($hasRerun -and $hasReattempt) { Write-AdoLiveResult "PASS V-22 BOTH_RETRY_KINDS_OBSERVED $note" }
        else { Write-AdoLiveResult "INCONCLUSIVE V-22 ONE_RETRY_KIND_OBSERVED $note" }
    }
    catch { Write-AdoLiveResult 'FAIL V-22 CHECK_FAILED' }

    # V-23: attachment list routes and version, then one content download held in memory.
    try {
        if ($null -eq $detail) { Write-AdoLiveResult 'INCONCLUSIVE V-23 NO_DETAILED_RESULT' }
        else {
            $attachmentBase = $projectBase + '/_apis/test/Runs/' + ([int] $runs[0].id).ToString([cultureinfo]::InvariantCulture) +
                '/Results/' + ([int] $detail.id).ToString([cultureinfo]::InvariantCulture) + '/attachments'
            $list = @(((Invoke-AdoTestRequest -Uri ($attachmentBase + '?api-version=6.0-preview.1')).Content | ConvertFrom-Json).value)
            # Content is held in memory only; the length is compared with the listed size.
            function Test-AdoLiveContent {
                param([Parameter(Mandatory = $true)][object] $Item, [Parameter(Mandatory = $true)][string] $Query)
                $content = Invoke-AdoTestRequest -Accept 'application/octet-stream' -Uri ($attachmentBase + '/' +
                    ([int] $Item.id).ToString([cultureinfo]::InvariantCulture) + '?api-version=6.0-preview.1' + $Query)
                # The raw stream holds the received bytes whatever the declared media type.
                $length = [long] $content.RawContentStream.Length
                $declared = if (Test-AdoLiveProperty -Object $Item -Name 'size') { [long] $Item.size } else { -1 }
                return ($declared -lt 0 -or $length -eq $declared)
            }
            $subOk = 'none'
            if ((Test-AdoLiveProperty -Object $detail -Name 'subResults') -and $null -ne $detail.subResults -and
                @($detail.subResults).Count -gt 0) {
                $subId = ([int] @($detail.subResults)[0].id).ToString([cultureinfo]::InvariantCulture)
                $subList = @(((Invoke-AdoTestRequest -Uri ($attachmentBase + '?api-version=6.0-preview.1&testSubResultId=' + $subId)).Content |
                    ConvertFrom-Json).value)
                $subOk = 'ok/' + $subList.Count.ToString([cultureinfo]::InvariantCulture)
                # The exporter downloads sub-result content with the same testSubResultId parameter (§6.2).
                if ($subList.Count -gt 0) {
                    $subOk += if (Test-AdoLiveContent -Item $subList[0] -Query ('&testSubResultId=' + $subId)) { '/CONTENT_AGREES' } else { '/CONTENT_DIFFERS' }
                }
            }
            if ($list.Count -eq 0) { Write-AdoLiveResult "INCONCLUSIVE V-23 ROUTES_OK_NO_ATTACHMENTS SUBRESULT=$subOk" }
            else {
                $missing = Get-AdoLiveMissingField -Object $list[0] -Names @('id', 'fileName', 'size', 'comment', 'attachmentType')
                $agrees = Test-AdoLiveContent -Item $list[0] -Query ''
                if ($missing.Count -gt 0) { Write-AdoLiveResult "FAIL V-23 ATTACHMENT_FIELDS_MISSING $($missing -join ',')" }
                elseif (-not $agrees) { Write-AdoLiveResult 'FAIL V-23 CONTENT_LENGTH_DIFFERS_FROM_SIZE' }
                elseif ($subOk.EndsWith('/CONTENT_DIFFERS')) { Write-AdoLiveResult "FAIL V-23 SUBRESULT_CONTENT_DIFFERS SUBRESULT=$subOk" }
                else { Write-AdoLiveResult "PASS V-23 ROUTES_FIELDS_AND_CONTENT_LENGTH_AGREE SUBRESULT=$subOk" }
            }
        }
    }
    catch { Write-AdoLiveResult 'FAIL V-23 CHECK_FAILED' }

    # V-24: storage plus automated name identify the same test across two builds of one definition.
    try {
        if ($null -eq $build -or $results.Count -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-24 CURRENT_BUILD_RESULTS_REQUIRED' }
        else {
            $maxTime = ([datetimeoffset] $build.queueTime).ToUniversalTime().ToString('o', [cultureinfo]::InvariantCulture)
            $window = $projectBase + '/_apis/build/builds?api-version=6.0&definitions=' +
                ([int] $build.definition.id).ToString([cultureinfo]::InvariantCulture) +
                '&statusFilter=completed&queryOrder=finishTimeDescending&%24top=5&maxTime=' + [uri]::EscapeDataString($maxTime)
            $earlier = @(((Invoke-AdoTestRequest -Uri $window).Content | ConvertFrom-Json).value |
                Where-Object { [int] $_.id -ne $buildId } | Select-Object -First 1)
            if ($earlier.Count -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-24 SECOND_BUILD_REQUIRED' }
            else {
                $current = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                foreach ($result in $results) {
                    if ([string]::IsNullOrEmpty([string] $result.automatedTestName)) { continue }
                    [void] $current.Add((([string] $result.automatedTestStorage).ToLowerInvariant() + '|' + [string] $result.automatedTestName))
                }
                $matched = 0
                $total = 0
                foreach ($run in @((Get-AdoLiveBuildRunList -ProjectBase $projectBase -BuildId ([int] $earlier[0].id)).Items)) {
                    $page = Get-AdoLiveTopSkip -BaseUri ($projectBase + '/_apis/test/Runs/' +
                        ([int] $run.id).ToString([cultureinfo]::InvariantCulture) + '/results') `
                        -Query 'api-version=6.0&detailsToInclude=None' -Top 1000
                    foreach ($result in @($page.Items)) {
                        if ([string]::IsNullOrEmpty([string] $result.automatedTestName)) { continue }
                        $total++
                        if ($current.Contains((([string] $result.automatedTestStorage).ToLowerInvariant() + '|' + [string] $result.automatedTestName))) { $matched++ }
                    }
                }
                if ($total -eq 0) { Write-AdoLiveResult 'INCONCLUSIVE V-24 EARLIER_BUILD_HAS_NO_AUTOMATED_RESULTS' }
                elseif ($matched -eq 0) { Write-AdoLiveResult 'FAIL V-24 NO_IDENTITY_MATCHED_ACROSS_BUILDS' }
                else { Write-AdoLiveResult "PASS V-24 IDENTITIES_MATCH_ACROSS_BUILDS MATCHED=$matched OF=$total" }
            }
        }
    }
    catch { Write-AdoLiveResult 'FAIL V-24 CHECK_FAILED' }

    # S5-10 also requires the manual acceptance run in plan section 8.
    if ($checkStates.Contains('FAIL')) { exit 1 }
    if ($checkStates.Contains('INCONCLUSIVE')) { exit 2 }
    exit 0
}
catch {
    foreach ($item in $items) { Write-AdoLiveResult "FAIL $item CHECK_FAILED" }
    exit 1
}
finally {
    if (Get-Command -Name Disconnect-Ado -ErrorAction SilentlyContinue) { Disconnect-Ado | Out-Null }
}
