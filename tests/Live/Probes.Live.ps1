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

# Opt-in probes for the next version, apart from the checks of shipped behaviour: each states an
# assumption that version would rely on. PASS means the route is usable, FAIL that it is not, which
# is evidence and not a regression, and INCONCLUSIVE that the build lacked the data. Installed
# module only (signed or unsigned); every response stays in memory and nothing is written. Never
# prints a value, a URL, an identity or an exception message: route names come from the allowlist
# below, and the rest are counts, lengths and fixed words.
$printed = [System.Collections.Generic.List[string]]::new()
function Write-AdoProbeResult {
    param([Parameter(Mandatory = $true)][string] $Text)
    $printed.Add($Text)
    Write-Output $Text
}

# The routes of the test area that the next version could use, each with a fixed name.
function Get-AdoProbeRoute {
    @(
        [pscustomobject]@{ Name = 'RESULT_SUMMARY_BY_BUILD'; ResourceName = 'ResultSummaryByBuild'; PerRun = $null }
        [pscustomobject]@{ Name = 'RESULTS_BY_BUILD'; ResourceName = 'ResultsByBuild'; PerRun = $null }
        [pscustomobject]@{ Name = 'RESULTS_QUERY'; ResourceName = 'Results'; PerRun = $false }
        [pscustomobject]@{ Name = 'TEST_HISTORY'; ResourceName = 'TestHistory'; PerRun = $null }
    )
}

# A read as the toolkit sends it, or with another method. The answer stays in memory.
function Invoke-AdoProbeRequest {
    param([Parameter(Mandatory = $true)][string] $Uri, [string] $Method = 'Get')
    $response = Invoke-WebRequest -Uri $Uri -Method $Method -UseDefaultCredentials -SkipHttpErrorCheck -MaximumRedirection 0 `
        -Headers @{ 'Accept-Language' = $PSUICulture; Accept = 'application/json' } -TimeoutSec $connection.RequestTimeoutSeconds
    if ([int] $response.StatusCode -ne 200) { throw 'HTTP_STATUS_DIFFERS' }
    return $response
}

# One body exactly as it came over the wire: gzip accepted, nothing decoded on the way.
function Invoke-AdoProbeRawRequest {
    param([Parameter(Mandatory = $true)][string] $Uri)
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.UseDefaultCredentials = $true
    $handler.AllowAutoRedirect = $false
    $handler.AutomaticDecompression = [System.Net.DecompressionMethods]::None
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $client.Timeout = [TimeSpan]::FromSeconds($connection.RequestTimeoutSeconds)
        $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $Uri)
        $request.Headers.Accept.ParseAdd('application/json')
        $request.Headers.AcceptEncoding.ParseAdd('gzip')
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        if ([int] $response.StatusCode -ne 200) { throw 'HTTP_STATUS_DIFFERS' }
        return [pscustomobject]@{
            Bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
            ContentEncoding = @($response.Content.Headers.ContentEncoding) -join ','
        }
    }
    finally { $client.Dispose() }
}

function Get-AdoProbeJson {
    param([Parameter(Mandatory = $true)][string] $Uri, [string] $Method = 'Get')
    return (Invoke-AdoProbeRequest -Uri $Uri -Method $Method).Content | ConvertFrom-Json
}

# The test runs of a build: one page of up to 100, enough for a probe.
function Get-AdoProbeRun {
    param([Parameter(Mandatory = $true)][int] $BuildId)
    $buildUri = [uri]::EscapeDataString('vstfs:///Build/Build/' + $BuildId.ToString([cultureinfo]::InvariantCulture))
    return @((Get-AdoProbeJson -Uri ($projectBase + '/_apis/test/runs?api-version=6.0&buildUri=' + $buildUri + '&includeRunDetails=true&%24top=100')).value)
}

function Get-AdoProbeResultBase {
    param([Parameter(Mandatory = $true)][object] $Run)
    return $projectBase + '/_apis/test/Runs/' + ([int] $Run.id).ToString([cultureinfo]::InvariantCulture) + '/results'
}

function Test-AdoProbeFailed {
    param([AllowNull()][object] $Result)
    return ([string] (Get-AdoLivePropertyValue $Result 'outcome')) -in @('Failed', 'Error', 'Timeout', 'Aborted')
}

$items = @('V-28', 'V-34', 'V-35', 'V-36', 'V-39')
$buildId = 0
$rerunId = 0
if ([string]::IsNullOrWhiteSpace($env:ADOTOOLKIT_LIVE_PROFILE)) {
    foreach ($item in $items) { Write-AdoProbeResult "INCONCLUSIVE $item PROFILE_REQUIRED" }
    exit 2
}
$hasBuild = [int]::TryParse($env:ADOTOOLKIT_LIVE_TEST_BUILD_ID, [ref] $buildId) -and $buildId -gt 0
$hasRerun = [int]::TryParse($env:ADOTOOLKIT_LIVE_RERUN_BUILD_ID, [ref] $rerunId) -and $rerunId -gt 0
$details = [uri]::EscapeDataString('Iterations,WorkItems,SubResults')

try {
    . (Join-Path $PSScriptRoot '../../tools/package/Package.Common.ps1')
    # The newest installed release; a signed release must verify throughout, an unsigned one is accepted.
    $null = Import-AdoInstalledModule
    $connection = Connect-Ado -Profile $env:ADOTOOLKIT_LIVE_PROFILE
    if ([string]::IsNullOrWhiteSpace($connection.DefaultProject)) {
        foreach ($item in $items) { Write-AdoProbeResult "INCONCLUSIVE $item PROFILE_DEFAULT_PROJECT_REQUIRED" }
        exit 2
    }
    $collectionBase = $connection.CollectionUri.AbsoluteUri.TrimEnd('/')
    $projectBase = $collectionBase + '/' + [uri]::EscapeDataString($connection.DefaultProject)
    $runs = if ($hasBuild) { @(Get-AdoProbeRun -BuildId $buildId) } else { @() }

    # V-28: the resource locations of the test area, matched against the allowlist.
    try {
        $locations = @((Get-AdoProbeJson -Uri ($collectionBase + '/_apis/test') -Method Options).value)
        $routes = @(Get-AdoLiveRouteEvidence -Location $locations -Allowlist (Get-AdoProbeRoute))
        foreach ($route in $routes) { Write-AdoProbeResult ('NOTE V-28 ' + $route.Name + ' ' + $route.State + ' RELEASED=' + $route.Released + ' MAX=' + $route.Maximum) }
        $absent = @($routes | Where-Object { $_.State -eq 'ABSENT' }).Count
        $note = 'LOCATIONS=' + $locations.Count.ToString([cultureinfo]::InvariantCulture) + ' ABSENT=' + $absent.ToString([cultureinfo]::InvariantCulture)
        if ($locations.Count -eq 0) { Write-AdoProbeResult "INCONCLUSIVE V-28 NO_LOCATIONS_LISTED $note" }
        elseif ($absent -eq 0) { Write-AdoProbeResult "PASS V-28 ROUTES_PRESENT $note" }
        else { Write-AdoProbeResult "FAIL V-28 ROUTES_ABSENT $note" }
    }
    catch { Write-AdoProbeResult 'FAIL V-28 CHECK_FAILED' }

    # V-34: one listing page with details from a run with failures, then the result read alone for
    # up to five of its failed results.
    try {
        $run = Select-AdoLiveFailingRun -Run $runs
        if (-not $hasBuild) { Write-AdoProbeResult 'INCONCLUSIVE V-34 TEST_BUILD_REQUIRED' }
        elseif ($null -eq $run) { Write-AdoProbeResult 'INCONCLUSIVE V-34 NO_RUNS_FOR_BUILD' }
        else {
            $base = Get-AdoProbeResultBase -Run $run
            $listed = @((Get-AdoProbeJson -Uri ($base + '?api-version=6.0&detailsToInclude=' + $details + '&%24top=200')).value)
            $failed = @($listed | Where-Object { Test-AdoProbeFailed $_ } | Select-Object -First 5)
            $counts = [ordered]@{ LISTED = $listed.Count; COMPARED = $failed.Count; FIELDS = 0; SUB_RESULTS = 0; ITERATIONS = 0; TEXT = 0
                MESSAGES_AT_4000 = @($listed | Where-Object { ([string] (Get-AdoLivePropertyValue $_ 'errorMessage')).Length -eq 4000 }).Count }
            foreach ($result in $failed) {
                $detail = Get-AdoProbeJson -Uri ($base + '/' + ([int] $result.id).ToString([cultureinfo]::InvariantCulture) + '?api-version=6.0&detailsToInclude=' + $details)
                $agreement = Get-AdoLiveListedDetailAgreement -Listed $result -Detail $detail
                if ($agreement.Fields) { $counts.FIELDS++ }
                if ($agreement.SubResults) { $counts.SUB_RESULTS++ }
                if ($agreement.Iterations) { $counts.ITERATIONS++ }
                if ($agreement.Text) { $counts.TEXT++ }
            }
            $note = Format-AdoLiveCounts $counts
            $agreed = $counts.FIELDS -eq $failed.Count -and $counts.SUB_RESULTS -eq $failed.Count -and $counts.ITERATIONS -eq $failed.Count -and $counts.TEXT -eq $failed.Count
            if ($failed.Count -eq 0) { Write-AdoProbeResult "INCONCLUSIVE V-34 NO_FAILED_RESULT_LISTED $note" }
            elseif ($agreed) { Write-AdoProbeResult "PASS V-34 LISTED_DETAILS_AGREE $note" }
            else { Write-AdoProbeResult "FAIL V-34 LISTED_DETAILS_DIFFER $note" }
        }
    }
    catch { Write-AdoProbeResult 'FAIL V-34 CHECK_FAILED' }

    # V-39: one result page listed without details, as pass 1 and the history read list it, from a
    # run with failures, then the result read alone for up to five of its failed results: whether
    # the listing carries the start of each error message.
    try {
        $run = Select-AdoLiveFailingRun -Run $runs
        if (-not $hasBuild) { Write-AdoProbeResult 'INCONCLUSIVE V-39 TEST_BUILD_REQUIRED' }
        elseif ($null -eq $run) { Write-AdoProbeResult 'INCONCLUSIVE V-39 NO_RUNS_FOR_BUILD' }
        else {
            $base = Get-AdoProbeResultBase -Run $run
            $listed = @((Get-AdoProbeJson -Uri ($base + '?api-version=6.0&detailsToInclude=None&%24top=1000')).value)
            $failed = @($listed | Where-Object { Test-AdoProbeFailed $_ })
            $messages = @($failed | ForEach-Object { [string] (Get-AdoLivePropertyValue $_ 'errorMessage') } | Where-Object { $_.Length -gt 0 })
            $counts = [ordered]@{ LISTED = $listed.Count; FAILED = $failed.Count; WITH_MESSAGE = $messages.Count; COMPARED = 0; AGREE = 0; CUT = 0; SHORT = 0
                MESSAGES_AT_4000 = @($messages | Where-Object { $_.Length -eq 4000 }).Count }
            foreach ($result in @($failed | Select-Object -First 5)) {
                $detail = Get-AdoProbeJson -Uri ($base + '/' + ([int] $result.id).ToString([cultureinfo]::InvariantCulture) + '?api-version=6.0&detailsToInclude=' + $details)
                $agreement = Get-AdoLiveListedMessageAgreement -Listed $result -Detail $detail
                if (-not $agreement.HasMessage) { continue }
                $counts.COMPARED++
                if ($agreement.Agree) { $counts.AGREE++ }
                if ($agreement.Cut) { $counts.CUT++ }
                if ($agreement.Short) { $counts.SHORT++ }
            }
            $note = Format-AdoLiveCounts $counts
            # A message cut before 4,000 characters agrees, but the report would read it as whole.
            if ($failed.Count -eq 0) { Write-AdoProbeResult "INCONCLUSIVE V-39 NO_FAILED_RESULT_LISTED $note" }
            elseif ($counts.COMPARED -eq 0) { Write-AdoProbeResult "INCONCLUSIVE V-39 NO_MESSAGE_TO_COMPARE $note" }
            elseif ($messages.Count -eq 0) { Write-AdoProbeResult "FAIL V-39 NO_MESSAGE_LISTED $note" }
            elseif ($counts.AGREE -lt $counts.COMPARED) { Write-AdoProbeResult "FAIL V-39 LISTED_MESSAGES_DIFFER $note" }
            elseif ($counts.SHORT -gt 0) { Write-AdoProbeResult "FAIL V-39 LISTED_MESSAGES_CUT_SHORT $note" }
            else { Write-AdoProbeResult "PASS V-39 LISTED_MESSAGES_AGREE $note" }
        }
    }
    catch { Write-AdoProbeResult 'FAIL V-39 CHECK_FAILED' }

    # V-35: one result page asked for with gzip, read as it came over the wire.
    try {
        $run = Select-AdoLiveFailingRun -Run $runs
        if (-not $hasBuild) { Write-AdoProbeResult 'INCONCLUSIVE V-35 TEST_BUILD_REQUIRED' }
        elseif ($null -eq $run) { Write-AdoProbeResult 'INCONCLUSIVE V-35 NO_RUNS_FOR_BUILD' }
        else {
            $body = Invoke-AdoProbeRawRequest -Uri ((Get-AdoProbeResultBase -Run $run) + '?api-version=6.0&detailsToInclude=None&%24top=1000&%24skip=0')
            $decoded = Get-AdoLiveDecodedLength -Bytes $body.Bytes -ContentEncoding $body.ContentEncoding
            $evidence = Get-AdoLiveCompressionEvidence -ContentEncoding $body.ContentEncoding -WireBytes $body.Bytes.Length -DecodedBytes $decoded
            $note = 'PERCENT=' + $evidence.Percent.ToString([cultureinfo]::InvariantCulture) + ' DECODED_BYTES=' + $decoded.ToString([cultureinfo]::InvariantCulture)
            # A server may leave a small body as it is, so only a large one settles the question.
            if ($evidence.State -eq 'COMPRESSED') { Write-AdoProbeResult "PASS V-35 COMPRESSED $note" }
            elseif ($decoded -lt 4096) { Write-AdoProbeResult "INCONCLUSIVE V-35 PAGE_TOO_SMALL $note" }
            else { Write-AdoProbeResult "FAIL V-35 UNCOMPRESSED $note" }
        }
    }
    catch { Write-AdoProbeResult 'FAIL V-35 CHECK_FAILED' }

    # V-36: the first rerun result whose sub-results have attachments: its own list against theirs.
    try {
        if (-not $hasRerun) { Write-AdoProbeResult 'INCONCLUSIVE V-36 RERUN_BUILD_REQUIRED' }
        else {
            $overlap = $null
            $examined = 0
            foreach ($run in @(Get-AdoProbeRun -BuildId $rerunId)) {
                $base = Get-AdoProbeResultBase -Run $run
                $reruns = @(@((Get-AdoProbeJson -Uri ($base + '?api-version=6.0&detailsToInclude=None&%24top=1000')).value) |
                        Where-Object { ([string] (Get-AdoLivePropertyValue $_ 'resultGroupType')) -eq 'Rerun' } | Select-Object -First 5)
                foreach ($result in $reruns) {
                    $examined++
                    $id = ([int] $result.id).ToString([cultureinfo]::InvariantCulture)
                    $detail = Get-AdoProbeJson -Uri ($base + '/' + $id + '?api-version=6.0&detailsToInclude=SubResults')
                    $attachments = $base.Replace('/results', '/Results') + '/' + $id + '/attachments?api-version=6.0-preview.1'
                    $sub = @(foreach ($child in @(Get-AdoLivePropertyValue $detail 'subResults')) {
                            $subId = ([int] (Get-AdoLivePropertyValue $child 'id')).ToString([cultureinfo]::InvariantCulture)
                            @((Get-AdoProbeJson -Uri ($attachments + '&testSubResultId=' + $subId)).value) | ForEach-Object { [int] $_.id }
                        })
                    if ($sub.Count -eq 0) { continue }
                    $own = @(@((Get-AdoProbeJson -Uri $attachments).value) | ForEach-Object { [int] $_.id })
                    $overlap = Get-AdoLiveAttachmentOverlap -Result $own -SubResult $sub
                    break
                }
                if ($null -ne $overlap) { break }
            }
            if ($null -eq $overlap) { Write-AdoProbeResult ('INCONCLUSIVE V-36 NO_SUB_RESULT_ATTACHMENTS RERUNS=' + $examined.ToString([cultureinfo]::InvariantCulture)) }
            else {
                $note = $overlap.State + ' ' + (Format-AdoLiveCounts $overlap.Counts)
                if ($overlap.Complete) { Write-AdoProbeResult "PASS V-36 $note" }
                else { Write-AdoProbeResult "FAIL V-36 $note" }
            }
        }
    }
    catch { Write-AdoProbeResult 'FAIL V-36 CHECK_FAILED' }

    if (@($printed | Where-Object { $_.StartsWith('FAIL ', [StringComparison]::Ordinal) }).Count -gt 0) { exit 1 }
    if (@($printed | Where-Object { $_.StartsWith('INCONCLUSIVE ', [StringComparison]::Ordinal) }).Count -gt 0) { exit 2 }
    exit 0
}
catch {
    # One verdict per check: only a check that has none yet fails here.
    foreach ($item in (Get-AdoLivePendingCheck -Id $items -Line $printed.ToArray())) { Write-AdoProbeResult "FAIL $item CHECK_FAILED" }
    exit 1
}
finally {
    if (Get-Command -Name Disconnect-Ado -ErrorAction SilentlyContinue) { Disconnect-Ado | Out-Null }
}
