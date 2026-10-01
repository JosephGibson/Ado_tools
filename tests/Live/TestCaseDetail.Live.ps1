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

$checkStates = [System.Collections.Generic.List[string]]::new()
function Write-AdoLiveResult {
    param([Parameter(Mandatory = $true)][string] $Text)
    $checkStates.Add($Text.Split(' ')[0])
    Write-Output $Text
}

# A read sent as POST, as the toolkit sends it. The answer stays in memory.
function Invoke-AdoLivePost {
    param([Parameter(Mandatory = $true)][string] $Uri, [Parameter(Mandatory = $true)][string] $Body)
    $request = @{
        Uri = $Uri; Method = 'Post'; Body = $Body; ContentType = 'application/json'; UseDefaultCredentials = $true
        Headers = @{ 'Accept-Language' = $PSUICulture }; MaximumRedirection = 0; TimeoutSec = $connection.RequestTimeoutSeconds
    }
    Invoke-RestMethod @request
}

# Opt-in, installed module only (signed or unsigned); no work data leaves this process.
# Never persist responses or print values, URLs, identities, or exception messages.
# V-31: the fields and relation attributes that Export-AdoTestCase -IncludeDetail reads.
# V-32: the test points query. Choose a Test Case that has a description, links to another work
# item and is in a suite with two or more test points (two configurations, or two suites).
$items = @('V-31', 'V-32')
$caseId = 0
if ([string]::IsNullOrWhiteSpace($env:ADOTOOLKIT_LIVE_PROFILE) -or
    -not [int]::TryParse($env:ADOTOOLKIT_LIVE_TESTCASE_ID, [ref] $caseId) -or $caseId -lt 1) {
    foreach ($item in $items) { Write-AdoLiveResult "INCONCLUSIVE $item PROFILE_AND_CASE_REQUIRED" }
    exit 2
}
$folder = $null
try {
    . (Join-Path $PSScriptRoot '../../tools/package/Package.Common.ps1')
    # The newest installed release; a signed release must verify throughout, an unsigned one is accepted.
    $null = Import-AdoInstalledModule
    $connection = Connect-Ado -Profile $env:ADOTOOLKIT_LIVE_PROFILE
    $collectionBase = $connection.CollectionUri.AbsoluteUri.TrimEnd('/')
    $case = Get-AdoTestCase -Id $caseId
    $pointsBase = $collectionBase + '/' + [uri]::EscapeDataString($case.TeamProject) + '/_apis/test/points?api-version=6.0-preview.2'
    $caseText = $caseId.ToString([cultureinfo]::InvariantCulture)

    # The installed module's own export: its report is counted, never printed, and deleted below.
    $folder = Join-Path ([System.IO.Path]::GetTempPath()) ('AdoToolkitLive-' + [guid]::NewGuid().ToString('N'))
    [void][System.IO.Directory]::CreateDirectory($folder)
    $report = $case | Export-AdoTestCase -IncludeDetail -Path $folder
    $html = [System.IO.File]::ReadAllText($report.FullName)
    $rendered = [pscustomobject]@{
        Links = [regex]::Matches($html, '<tr data-link="[0-9]+">').Count
        Points = [regex]::Matches($html, '<tr data-point="[0-9]+" ').Count
        Codes = @([regex]::Matches($html, ' data-diagnostic="([A-Za-z]+)"') | ForEach-Object { $_.Groups[1].Value })
    }

    try {
        $batch = Invoke-AdoLivePost -Uri ($collectionBase + '/_apis/wit/workitemsbatch?api-version=6.0') -Body ('{"ids":[' + $caseText + '],"errorPolicy":"omit","$expand":"relations"}')
        $read = @($batch.value | Where-Object { $null -ne $_ })
        $evidence = Get-AdoLiveTestCaseDetailEvidence -Item ($read | Select-Object -First 1) -TestCaseId $caseId
        $degraded = @($rendered.Codes | Where-Object { $_ -in @('TestCaseDetailUnavailable', 'UnresolvedTestCaseDetail', 'LinkedWorkItemsUnavailable') }).Count
        $note = (Format-AdoLiveCounts $evidence.Counts) + ' RENDERED_LINKS=' + $rendered.Links.ToString([cultureinfo]::InvariantCulture)
        if ($evidence.Problems.Count -gt 0) { Write-AdoLiveResult ('FAIL V-31 ' + $evidence.Problems[0] + ' ' + $note) }
        elseif ($degraded -gt 0) { Write-AdoLiveResult "FAIL V-31 MODULE_LOOKUP_DEGRADED $note" }
        elseif ($rendered.Links -ne $evidence.Counts.LINKS) { Write-AdoLiveResult "FAIL V-31 MODULE_LINK_COUNT_DIFFERS $note" }
        elseif ($evidence.Counts.LINKS_WITH_NAME -eq 0) { Write-AdoLiveResult "INCONCLUSIVE V-31 NO_NAMED_WORKITEM_LINK $note" }
        elseif ($evidence.Counts.DESCRIPTION -eq 0) { Write-AdoLiveResult "INCONCLUSIVE V-31 NO_DESCRIPTION $note" }
        else { Write-AdoLiveResult "PASS V-31 FIELDS_AND_RELATIONS_AS_ASSUMED $note" }
    }
    catch { Write-AdoLiveResult 'FAIL V-31 CHECK_FAILED' }

    try {
        $body = '{"pointsFilter":{"testcaseIds":[' + $caseText + ']}}'
        $status = 0
        $response = $null
        try { $response = Invoke-AdoLivePost -Uri ($pointsBase + '&%24top=1000&%24skip=0') -Body $body }
        catch {
            $failed = $_.Exception.PSObject.Properties['Response']
            if ($null -eq $failed -or $null -eq $failed.Value) { throw }
            $status = [int] $failed.Value.StatusCode
        }
        if ($status -ne 0) { Write-AdoLiveResult ('FAIL V-32 QUERY_REJECTED STATUS=' + $status.ToString([cultureinfo]::InvariantCulture)) }
        else {
            $evidence = Get-AdoLiveTestPointEvidence -Response $response -TestCaseId $caseId
            $paging = 'UNOBSERVED'
            if ($evidence.Problems.Count -eq 0 -and $evidence.Counts.POINTS -ge 2) {
                $pages = @(foreach ($skip in 0, 1) {
                        , @((Get-AdoLiveTestPointEvidence -Response (Invoke-AdoLivePost -Uri ($pointsBase + '&%24top=1&%24skip=' + $skip) -Body $body) -TestCaseId $caseId).Ids)
                    })
                $paging = Get-AdoLivePagingEvidence -First $pages[0] -Second $pages[1]
            }
            $unavailable = @($rendered.Codes | Where-Object { $_ -eq 'TestPointsUnavailable' }).Count
            $note = (Format-AdoLiveCounts $evidence.Counts) + ' RENDERED_POINTS=' + $rendered.Points.ToString([cultureinfo]::InvariantCulture) + ' PAGING=' + $paging
            if ($evidence.Problems.Count -gt 0) { Write-AdoLiveResult ('FAIL V-32 ' + $evidence.Problems[0] + ' ' + $note) }
            elseif ($unavailable -gt 0) { Write-AdoLiveResult "FAIL V-32 MODULE_LOOKUP_DEGRADED $note" }
            elseif ($rendered.Points -ne $evidence.Counts.POINTS) { Write-AdoLiveResult "FAIL V-32 MODULE_POINT_COUNT_DIFFERS $note" }
            elseif ($evidence.Counts.POINTS -eq 0) { Write-AdoLiveResult "INCONCLUSIVE V-32 NO_POINTS_FOR_CASE $note" }
            elseif ($paging -eq 'UNOBSERVED') { Write-AdoLiveResult "INCONCLUSIVE V-32 TWO_POINTS_REQUIRED_FOR_PAGING $note" }
            elseif ($paging -ne 'HONORED') { Write-AdoLiveResult "FAIL V-32 PAGING_$paging $note" }
            else { Write-AdoLiveResult "PASS V-32 POINTS_QUERY_AS_ASSUMED $note" }
        }
    }
    catch { Write-AdoLiveResult 'FAIL V-32 CHECK_FAILED' }

    # The rendered report also needs the developer's visual comparison with the ADO web UI.
    if ($checkStates.Contains('FAIL')) { exit 1 }
    if ($checkStates.Contains('INCONCLUSIVE')) { exit 2 }
    exit 0
}
catch {
    # Each check prints one line, in order; a check that printed none failed before it could.
    foreach ($item in @($items | Select-Object -Skip $checkStates.Count)) { Write-AdoLiveResult "FAIL $item CHECK_FAILED" }
    exit 1
}
finally {
    if ($null -ne $folder -and (Test-Path -LiteralPath $folder)) { Remove-Item -LiteralPath $folder -Recurse -Force }
    if (Get-Command -Name Disconnect-Ado -ErrorAction SilentlyContinue) { Disconnect-Ado | Out-Null }
}
