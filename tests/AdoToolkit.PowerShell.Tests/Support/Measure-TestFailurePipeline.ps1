<#
.SYNOPSIS
Measures Get-AdoBuildTestFailure and Export-AdoBuildTestFailure against the synthetic server.

.DESCRIPTION
Generates one synthetic build in memory, serves it from the routed synthetic server with a fixed
latency, and runs the two cmdlets -Iterations times. Prints one JSON document: the parameters, the
average size of a listed result, the history builds available out of those in the set (History),
and per cmdlet the requests and response bytes of one run, the wall time of each run with its
median, the highest number of requests the server answered at once, and the responses it did not
deliver because the client had abandoned the request.

Stages splits the requests and bytes of the last run by what they read:

    Build        the build, by ID
    Runs         the test runs of the build
    Results      the result listings of those runs (pass 1)
    Details      one result each (pass 2)
    Attachments  the attachment lists of results and sub-results
    TestCases    the Test Case batches
    Bugs         the bug batches, the Bug category and the Bug states
    History      the builds window, then the runs and result listings of earlier builds: the
                 requests that testResults.maximumHistoryRequests counts
    Downloads    attachment contents, read by the export

Get-AdoBuildTestFailure -Verbose names the same stages, except Downloads. Bytes are response
bodies as the server sent them, compressed when it compressed them; DecodedBytes are the same
bodies before compression.

With -SkipAttachments, Get-AdoBuildTestFailure reads no attachment list, and each export runs
after Disconnect-Ado: an export that needed a connection would stop the script, so its zero
requests show that it needs none.

Not a test: neither runner collects this file, and it asserts nothing. Run it by hand against a
staged module, for example the one `verify` leaves under artifacts/verify:

    $env:ADOTOOLKIT_MODULE_MANIFEST = '<repository>\artifacts\verify\AdoToolkit\<version>\AdoToolkit.psd1'
    pwsh -NoProfile -File .\tests\AdoToolkit.PowerShell.Tests\Support\Measure-TestFailurePipeline.ps1

Every failing test fails in every run of its group, so it has one result record per run. With
-SubResults above zero each record is a rerun group and each sub-result is an attempt.

The scenarios of the examples are the ones that docs/archive/plans/0.8.0-test-failure-report.md records.
Each also runs with -LatencyMilliseconds 0 and 80, and with -ResultBytes 1000, which gives each
listed result about the size of a real one.

.EXAMPLE
Measure-TestFailurePipeline.ps1

Default: 1 group of 4 runs of 3,000 results, 150 failing tests that each rerun twice, and 10
builds of history, at 20 ms.

.EXAMPLE
Measure-TestFailurePipeline.ps1 -SubResults 0

NoReruns: Default without rerun sub-results, so each failing result is one attempt.

.EXAMPLE
Measure-TestFailurePipeline.ps1 -Groups 4 -RunsPerGroup 2 -ResultsPerRun 5000 -FailingTests 200 -SubResults 0

Wide: 4 groups of 2 runs of 5,000 results and 200 failing tests. Its history needs 451 requests:
more than the budget of 400 that testResults.maximumHistoryRequests had by default before 0.8.0,
within the default of 500.

.EXAMPLE
Measure-TestFailurePipeline.ps1 -HistoryBuilds 30

LongHistory: Default with 30 builds of history, whose 523 history requests exceed the default
budget of 500.

.EXAMPLE
Measure-TestFailurePipeline.ps1 -SkipAttachments

Any scenario can add -SkipAttachments: Default without its 1,800 attachment lists, and its
export without a connection.
#>
[CmdletBinding()]
param(
    # Pipeline groups (stages). Each group has its own tests.
    [ValidateRange(1, 8)][int] $Groups = 1,
    # Job attempts per group; each attempt is one test run.
    [ValidateRange(1, 8)][int] $RunsPerGroup = 4,
    [ValidateRange(1, 20000)][int] $ResultsPerRun = 3000,
    # Failing tests of the whole build, shared evenly between the groups.
    [ValidateRange(1, 2000)][int] $FailingTests = 150,
    # Rerun sub-results per failing result record.
    [ValidateRange(0, 9)][int] $SubResults = 2,
    # Attachments of each attempt, alternately a text file and a JSON file.
    [ValidateRange(0, 9)][int] $AttachmentsPerAttempt = 1,
    # Builds in the history, the measured build included.
    [ValidateRange(1, 50)][int] $HistoryBuilds = 10,
    # Size in bytes that each listed result is padded to with the synthetic references a real one
    # carries: project, run, build and area, two identities, dates and links. Zero lists the fields
    # that pass 1 reads, about 150 bytes.
    [ValidateRange(0, 16384)][int] $ResultBytes = 0,
    [ValidateRange(0, 5000)][int] $LatencyMilliseconds = 20,
    [ValidateRange(1, 64)][int] $Workers = 16,
    [ValidateRange(1, 20)][int] $Iterations = 3,
    # Written as testResults.maximumConcurrentRequests. Zero writes nothing.
    [ValidateRange(0, 16)][int] $MaximumConcurrentRequests = 0,
    # Written as testResults.maximumHistoryRequests. Zero writes nothing.
    [ValidateRange(0, 100000)][int] $MaximumHistoryRequests = 0,
    # The server gzips a body when the request accepts gzip.
    [switch] $Compression,
    # The server writes each response at most this fast. Zero writes it at once.
    [ValidateRange(0, [int]::MaxValue)][int] $BytesPerSecond = 0,
    [switch] $AllRunAttachments,
    # Passed to Get-AdoBuildTestFailure, not to the export, which then runs without a connection.
    [switch] $SkipAttachments
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:ADOTOOLKIT_MODULE_MANIFEST)) { throw 'Set ADOTOOLKIT_MODULE_MANIFEST to the manifest of a staged module.' }
if ($FailingTests -gt $ResultsPerRun * $Groups) { throw 'FailingTests exceeds the results of the build.' }
Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
. (Join-Path $PSScriptRoot 'FakeAdoServer.ps1')

$invariant = [cultureinfo]::InvariantCulture
$project = 'Measure'
$buildId = 9000
$pageSize = 1000
$started = [DateTimeOffset]::UtcNow.AddHours(-2)
function Format-Instant {
    param([Parameter(Mandatory = $true)][DateTimeOffset] $Value)
    $Value.ToString('yyyy-MM-ddTHH:mm:ssZ', $invariant)
}
function ConvertTo-Page {
    param([string[]] $Items = @())
    '{"count":' + $Items.Count.ToString($invariant) + ',"value":[' + ($Items -join ',') + ']}'
}

# Failing tests per group: the first groups take the remainder.
$failingOf = @(foreach ($group in 1..$Groups) {
        [int] [Math]::Floor($FailingTests / $Groups) + $(if ($group -le $FailingTests % $Groups) { 1 } else { 0 })
    })
# Already JSON text: the backslashes of the paths are escaped and the frames are joined by an escaped line feed.
$stack = @(
    'at Contoso.Measure.Tests.Checkout.Verify(Int32 expected, Int32 actual) in C:\\src\\Checkout.cs:line 118',
    'at Contoso.Measure.Tests.Checkout.Totals() in C:\\src\\Checkout.cs:line 64',
    'at Contoso.Measure.Tests.Harness.Run(Action body) in C:\\src\\Harness.cs:line 31',
    'at Contoso.Measure.Tests.Harness.Execute() in C:\\src\\Harness.cs:line 12'
) -join '\n'
$textBytes = [System.Text.Encoding]::UTF8.GetBytes(('2026-09-30T10:00:00Z step completed without a response from the service' + "`n") * 20)
$jsonBytes = [System.Text.Encoding]::UTF8.GetBytes('{"request":"checkout","status":504,"attempts":[' + ((1..40 | ForEach-Object { '{"n":' + $_ + ',"ms":1200}' }) -join ',') + ']}')

# An identity reference as the server writes one, with an invented account and a .test host.
function Get-IdentityJson {
    param([Parameter(Mandatory = $true)][string] $Name, [Parameter(Mandatory = $true)][string] $Id)
    '{"displayName":"' + $Name + '","url":"https://ado.example.test/Collection/_apis/Identities/' + $Id + '","id":"' + $Id +
    '","uniqueName":"Build\\' + $Id + '","imageUrl":"https://ado.example.test/Collection/_api/_common/identityImage?id=' + $Id + '"}'
}
$listedAt = Format-Instant $started
$runBy = Get-IdentityJson -Name 'Measure Build Service (Collection)' -Id '2b3c4d5e-0000-4000-8000-000000000b01'
$updatedBy = Get-IdentityJson -Name 'Project Collection Build Service (Measure)' -Id '3c4d5e6f-0000-4000-8000-000000000b02'
# The references of a listed result, in the order the server writes them. Pass 1 reads none of them.
function Get-ReferenceFields {
    param([Parameter(Mandatory = $true)][int] $Id, [Parameter(Mandatory = $true)][int] $Group, [Parameter(Mandatory = $true)][string] $Name)
    $run = (100 + ($Group - 1) * 10 + 1).ToString($invariant)
    @(
        '"project":{"id":"6f1d2c3a-0000-4000-8000-00000000c0de","name":"' + $project + '","url":"https://ado.example.test/Collection/_apis/projects/6f1d2c3a-0000-4000-8000-00000000c0de"}'
        '"startedDate":"' + $listedAt + '","completedDate":"' + $listedAt + '","durationInMs":1333.0'
        '"revision":1,"state":"Completed"'
        '"runBy":' + $runBy
        '"testCase":{"name":"' + $Name.Substring($Name.LastIndexOf('.') + 1) + '"}'
        '"testRun":{"id":"' + $run + '","name":"Tests ' + $Group + '","url":"https://ado.example.test/Collection/' + $project + '/_apis/test/Runs/' + $run + '"}'
        '"lastUpdatedDate":"' + $listedAt + '","lastUpdatedBy":' + $updatedBy
        '"priority":0,"computerName":"AGENT-0' + $Group + '"'
        '"build":{"id":"' + $buildId + '","name":"' + $buildId + '.1","url":"https://ado.example.test/Collection/_apis/build/Builds/' + $buildId + '"}'
        '"createdDate":"' + $listedAt + '","url":"https://ado.example.test/Collection/' + $project + '/_apis/test/Runs/' + $run + '/Results/' + $Id + '"'
        '"failureType":"None","automatedTestType":"UnitTest","automatedTestTypeId":"13cdc9d9-ddb5-4fa4-a97d-d965ccfc6d4b","automatedTestId":"' + $Id.ToString('x40', $invariant) + '"'
        '"area":{"id":"37528","name":"' + $project + '","url":"vstfs:///Classification/Node/0d1e2f3a-0000-4000-8000-0000000a4ea0"}'
    )
}
# A listed result: the fields pass 1 reads, then the references that fit within -ResultBytes, then
# a comment that takes up what is left.
function Get-ListedResult {
    param([Parameter(Mandatory = $true)][int] $Id, [Parameter(Mandatory = $true)][int] $Group, [Parameter(Mandatory = $true)][int] $Failing)
    $name = 'Contoso.Measure.Group' + $Group + '.Tests.Suite' + [int] [Math]::Floor(($Id - 1) / 50) + '.Test' + $Id.ToString('D5', $invariant)
    $item = '{"id":' + $Id + ',"outcome":"' + $(if ($Id -le $Failing) { 'Failed' } else { 'Passed' }) + '","automatedTestName":"' + $name +
    '","automatedTestStorage":"Contoso.Measure.Group' + $Group + '.Tests.dll"' + $(if ($Id -le $Failing -and $SubResults -gt 0) { ',"resultGroupType":"Rerun"' } else { '' })
    if ($ResultBytes -gt 0) {
        foreach ($field in Get-ReferenceFields -Id $Id -Group $Group -Name $name) {
            if ($item.Length + $field.Length + 2 -le $ResultBytes) { $item += ',' + $field }
        }
        $room = $ResultBytes - $item.Length - ',"comment":""}'.Length
        if ($room -gt 0) { $item += ',"comment":"' + ('x' * $room) + '"' }
    }
    $item + '}'
}

$runPages = @{}
$resultPages = @{}
$details = @{}
$attachmentLists = @{}
$testCaseIds = [System.Collections.Generic.List[int]]::new()
$testCases = @{}
$bugIds = [System.Collections.Generic.SortedSet[int]]::new()
$listedBytes = 0L
$listedCount = 0

# The run objects of one build; history builds repeat the measured build under other run IDs.
function Get-RunJson {
    param([Parameter(Mandatory = $true)][int] $Build, [Parameter(Mandatory = $true)][int] $Offset)
    foreach ($group in 1..$Groups) {
        foreach ($attempt in 1..$RunsPerGroup) {
            $runId = $Offset + 100 + ($group - 1) * 10 + $attempt
            $name = "Tests $group" + $(if ($attempt -gt 1) { " (attempt $attempt)" } else { '' })
            $begin = $started.AddMinutes(($group - 1) * 60 + $attempt * 6)
            '{"id":' + $runId + ',"name":"' + $name + '","state":"Completed","isAutomated":true,"startedDate":"' + (Format-Instant $begin) +
            '","completedDate":"' + (Format-Instant $begin.AddMinutes(5)) + '","pipelineReference":{"pipelineId":' + $Build +
            ',"stageReference":{"attempt":1,"stageName":"Stage ' + $group + '"},"phaseReference":{"attempt":1,"phaseName":"Tests"},"jobReference":{"attempt":' +
            $attempt + ',"jobName":"__default"}},"totalTests":' + $ResultsPerRun + ',"runStatistics":[{"state":"Completed","outcome":"Failed","count":' +
            $failingOf[$group - 1] + '},{"state":"Completed","outcome":"Passed","count":' + ($ResultsPerRun - $failingOf[$group - 1]) +
            '}],"build":{"id":"' + $Build + '","name":"' + $Build + '.1"}}'
        }
    }
}

$ordinal = 0
foreach ($group in 1..$Groups) {
    $failing = $failingOf[$group - 1]
    $storage = "Contoso.Measure.Group$group.Tests.dll"
    # Pass 1: the same pages serve every run of the group, in the measured build and in its history.
    $pages = @(for ($skip = 0; $skip -lt $ResultsPerRun; $skip += $pageSize) {
            $items = for ($id = $skip + 1; $id -le [Math]::Min($skip + $pageSize, $ResultsPerRun); $id++) {
                Get-ListedResult -Id $id -Group $group -Failing $failing
            }
            foreach ($item in $items) { $listedBytes += [System.Text.Encoding]::UTF8.GetByteCount($item); $listedCount++ }
            @{ Body = ConvertTo-Page -Items $items }
        })
    foreach ($build in 0..($HistoryBuilds - 1)) {
        foreach ($attempt in 1..$RunsPerGroup) {
            $runId = $build * 1000 + 100 + ($group - 1) * 10 + $attempt
            for ($page = 0; $page -lt $pages.Count; $page++) { $resultPages["$runId/$($page * $pageSize)"] = $pages[$page] }
        }
    }
    for ($id = 1; $id -le $failing; $id++) {
        $ordinal++
        $testCaseId = 5000 + $ordinal
        $testCaseIds.Add($testCaseId)
        # Every fifth Test Case links one work item; every third test has one associated bug.
        $relations = if ($ordinal % 5 -eq 0) {
            $linked = 7100 + $ordinal % 7
            [void] $bugIds.Add($linked)
            ',"relations":[{"rel":"Microsoft.VSTS.Common.TestedBy-Reverse","url":"https://ado.example.test/Collection/_apis/wit/workItems/' + $linked + '","attributes":{"name":"Tests"}}]'
        }
        else { '' }
        $testCases[$testCaseId] = '{"id":' + $testCaseId + ',"rev":1,"fields":{"System.Id":' + $testCaseId + ',"System.Title":"Measured case ' + $ordinal +
        '","System.State":"Ready","System.WorkItemType":"Test Case","System.TeamProject":"' + $project + '"}' + $relations + '}'
        $associated = if ($ordinal % 3 -eq 0) {
            $bug = 7000 + $ordinal % 10
            [void] $bugIds.Add($bug)
            ',"associatedBugs":[{"id":"' + $bug + '"}]'
        }
        else { '' }
        $name = 'Contoso.Measure.Group' + $group + '.Tests.Suite' + [int] [Math]::Floor(($id - 1) / 50) + '.Test' + $id.ToString('D5', $invariant)
        foreach ($attempt in 1..$RunsPerGroup) {
            $runId = 100 + ($group - 1) * 10 + $attempt
            $message = "Assert.AreEqual failed. Expected:<$id>. Actual:<0>. Checkout total differs in attempt $attempt."
            $nested = if ($SubResults -gt 0) {
                ',"resultGroupType":"Rerun","subResults":[' + (@(foreach ($sub in 1..$SubResults) {
                            '{"id":' + $sub + ',"sequenceId":' + $sub + ',"displayName":"Attempt ' + $sub + '","outcome":"Failed","errorMessage":"' + $message +
                            '","stackTrace":"' + $stack + '","durationInMs":1200.5}'
                        }) -join ',') + ']'
            }
            else { '' }
            $details["$runId/$id"] = @{
                Body = '{"id":' + $id + ',"outcome":"Failed","automatedTestName":"' + $name + '","automatedTestStorage":"' + $storage +
                '","startedDate":"' + (Format-Instant $started) + '","completedDate":"' + (Format-Instant $started.AddSeconds(3)) +
                '","durationInMs":2400.5,"computerName":"AGENT-0' + $group + '","errorMessage":"' + $message + '","stackTrace":"' + $stack +
                '","testCase":{"id":"' + $testCaseId + '"}' + $associated + $nested + '}'
            }
            # With sub-results the attachments belong to them and the result's own list is empty.
            $owners = if ($SubResults -gt 0) { 1..$SubResults } else { @(0) }
            if ($SubResults -gt 0) { $attachmentLists["$runId/$id"] = @{ Body = ConvertTo-Page } }
            foreach ($owner in $owners) {
                $items = @(for ($index = 1; $index -le $AttachmentsPerAttempt; $index++) {
                        $json = $index % 2 -eq 0
                        '{"id":' + ($owner * 100 + $index) + ',"fileName":"' + $(if ($json) { "payload-$index.json" } else { "output-$index.log" }) +
                        '","size":' + $(if ($json) { $jsonBytes.Length } else { $textBytes.Length }) + ',"attachmentType":"GeneralAttachment"}'
                    })
                $attachmentLists[$(if ($owner -eq 0) { "$runId/$id" } else { "$runId/$id/$owner" })] = @{ Body = ConvertTo-Page -Items $items }
            }
        }
    }
}
foreach ($build in 0..($HistoryBuilds - 1)) {
    $runPages["$($buildId - $build)/0"] = @{ Body = ConvertTo-Page -Items @(Get-RunJson -Build ($buildId - $build) -Offset ($build * 1000)) }
}

# WorkItemsBatch answers only the IDs it was asked for, so each chunk of 200 sorted IDs is keyed by its first ID.
function Get-BatchResponse {
    param([int[]] $Ids = @(), [Parameter(Mandatory = $true)][scriptblock] $Item)
    $responses = @{}
    for ($start = 0; $start -lt $Ids.Count; $start += 200) {
        $chunk = $Ids[$start..([Math]::Min($start + 200, $Ids.Count) - 1)]
        $responses[$chunk[0].ToString($invariant)] = @{ Body = ConvertTo-Page -Items @($chunk | ForEach-Object $Item) }
    }
    $responses
}
$testCaseBatches = Get-BatchResponse -Ids ($testCaseIds | Sort-Object) -Item { $testCases[$_] }
$bugBatches = Get-BatchResponse -Ids @($bugIds) -Item {
    '{"id":' + $_ + ',"rev":1,"fields":{"System.Id":' + $_ + ',"System.Title":"Measured bug ' + $_ + '","System.State":"' +
    $(if ($_ % 2 -eq 0) { 'Active' } else { 'Closed' }) + '","System.WorkItemType":"Bug","System.TeamProject":"' + $project + '"}}'
}
function Get-BuildJson {
    param([Parameter(Mandatory = $true)][int] $Id, [Parameter(Mandatory = $true)][int] $Age)
    $finished = Format-Instant $started.AddDays(-$Age).AddHours(1)
    '{"id":' + $Id + ',"buildNumber":"' + $Id + '.1","definition":{"id":42,"name":"Measure"},"sourceBranch":"refs/heads/main","status":"completed","result":"failed","finishTime":"' +
    $finished + '","queueTime":"' + $finished + '","uri":"vstfs:///Build/Build/' + $Id + '"}'
}

$routes = @(
    @{ Line = '/_apis/test/Runs/(\d+)/Results/(\d+)/attachments\?(?:testSubResultId=(\d+)&)?api-version'; Responses = $attachmentLists },
    @{ Line = '/_apis/test/Runs/(\d+)/results/(\d+)\?'; Responses = $details },
    @{ Line = '/_apis/test/Runs/(\d+)/results\?.*%24skip=(\d+)&'; Responses = $resultPages },
    @{ Line = '/_apis/test/runs\?buildUri=vstfs%3A%2F%2F%2FBuild%2FBuild%2F(\d+)&.*%24skip=(\d+)&'; Responses = $runPages },
    @{ Line = '/attachments/\d*[13579]\?'; Response = @{ Bytes = $textBytes; ContentType = 'application/octet-stream' } },
    @{ Line = '/attachments/\d*[02468]\?'; Response = @{ Bytes = $jsonBytes; ContentType = 'application/octet-stream' } },
    @{ Line = '/_apis/wit/workitemsbatch\?'; Body = '^\{"ids":\[(\d+)[,\]].*"\$expand":"relations"'; Responses = $testCaseBatches },
    @{ Line = '/_apis/wit/workitemsbatch\?'; Body = '^\{"ids":\[(\d+)[,\]]'; Responses = $bugBatches },
    @{ Line = '/_apis/wit/workitemtypecategories/'; Response = @{ Body = '{"name":"Bug Category","referenceName":"Microsoft.BugCategory","workItemTypes":[{"name":"Bug"}]}' } },
    @{ Line = '/_apis/wit/workitemtypes/Bug/states\?'; Response = @{ Body = '{"count":2,"value":[{"name":"Active","category":"InProgress"},{"name":"Closed","category":"Completed"}]}' } },
    @{ Line = "/_apis/build/builds/$buildId\?"; Response = @{ Body = Get-BuildJson -Id $buildId -Age 0 } },
    @{ Line = '/_apis/build/builds\?'; Response = @{ Body = ConvertTo-Page -Items @(foreach ($build in 0..($HistoryBuilds - 1)) { Get-BuildJson -Id ($buildId - $build) -Age $build }) } }
)

# What a request read, as Stages names it. The measured build's runs are the ones below 1000.
function Get-RequestStage {
    param([Parameter(Mandatory = $true)][object] $Request)
    $line = $Request.Line
    if ($line -match '/_apis/test/Runs/\d+/Results/\d+/attachments/\d+\?') { return 'Downloads' }
    if ($line -match '/_apis/test/Runs/\d+/Results/\d+/attachments\?') { return 'Attachments' }
    if ($line -match '/_apis/test/Runs/\d+/results/\d+\?') { return 'Details' }
    if ($line -match '/_apis/test/Runs/(\d+)/results\?') { return $(if ([int] $Matches[1] -lt 1000) { 'Results' } else { 'History' }) }
    if ($line -match '/_apis/test/runs\?buildUri=vstfs%3A%2F%2F%2FBuild%2FBuild%2F(\d+)&') { return $(if ([int] $Matches[1] -eq $buildId) { 'Runs' } else { 'History' }) }
    if ($line -match "/_apis/build/builds/$buildId\?") { return 'Build' }
    if ($line -match '/_apis/build/builds\?') { return 'History' }
    if ($line -match '/_apis/wit/workitemsbatch\?') { return $(if ($Request.Body -match '"\$expand":"relations"') { 'TestCases' } else { 'Bugs' }) }
    if ($line -match '/_apis/wit/workitemtype') { return 'Bugs' }
    'Other'
}
# The synthetic server closes every connection after one response, and Windows keeps the client's port of
# a closed connection for about two minutes; one iteration of the Default scenario uses about 3,800. Each
# run waits, outside its timing, until enough of the 16,384 dynamic ports are free, or runs started one
# after another fail with a transport error once the ports run out.
function Wait-FreePort {
    $deadline = [datetime]::UtcNow.AddMinutes(5)
    while ([datetime]::UtcNow -lt $deadline) {
        $waiting = @([System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpConnections().Where({ $_.State -eq 'TimeWait' })).Count
        if ($waiting -le 8000) { return }
        Start-Sleep -Seconds 5
    }
}
# Requests and response bytes per stage, in stage order, for the requests of one cmdlet run.
function Get-StageTotal {
    param([object[]] $Requests = @())
    $totals = @{}
    foreach ($request in $Requests) {
        $stage = Get-RequestStage -Request $request
        if (-not $totals.ContainsKey($stage)) { $totals[$stage] = [long[]]::new(3) }
        $totals[$stage][0]++
        $totals[$stage][1] += $request.Bytes
        $totals[$stage][2] += $request.DecodedBytes
    }
    $stages = [ordered]@{}
    foreach ($stage in 'Build', 'Runs', 'Results', 'Details', 'Attachments', 'TestCases', 'Bugs', 'History', 'Downloads', 'Other') {
        if ($totals.ContainsKey($stage)) { $stages[$stage] = [ordered]@{ Requests = $totals[$stage][0]; Bytes = $totals[$stage][1]; DecodedBytes = $totals[$stage][2] } }
    }
    $stages
}

$previousConfig = $env:ADOTOOLKIT_CONFIG_PATH
$previousProgress = $ProgressPreference
$work = Join-Path ([IO.Path]::GetTempPath()) ('AdoToolkit-measure-' + [guid]::NewGuid().ToString('N'))
[void] [IO.Directory]::CreateDirectory($work)
$env:ADOTOOLKIT_CONFIG_PATH = Join-Path $work 'config.json'
$settings = @(
    if ($MaximumConcurrentRequests -gt 0) { '"maximumConcurrentRequests":' + $MaximumConcurrentRequests }
    if ($MaximumHistoryRequests -gt 0) { '"maximumHistoryRequests":' + $MaximumHistoryRequests }
)
if ($settings.Count -gt 0) {
    Set-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Encoding utf8 -Value ('{"schemaVersion":1,"testResults":{' + ($settings -join ',') + '}}')
}
$ProgressPreference = 'SilentlyContinue'
$server = Start-FakeAdoServer -Routes $routes -LatencyMilliseconds $LatencyMilliseconds -Workers $Workers -Compression:$Compression -BytesPerSecond $BytesPerSecond
try {
    Connect-Ado -CollectionUrl $server.Uri -Project $project -WarningAction SilentlyContinue | Out-Null
    $samples = foreach ($iteration in 1..$Iterations) {
        $output = Join-Path $work "run-$iteration"
        [void] [IO.Directory]::CreateDirectory($output)
        Wait-FreePort
        $first = $server.Requests.Count
        [void] (Get-FakeAdoServerPeak -Server $server -Reset)
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        $set = Get-AdoBuildTestFailure -BuildId $buildId -HistoryCount $HistoryBuilds -SkipAttachments:$SkipAttachments -WarningAction SilentlyContinue
        $watch.Stop()
        $second = $server.Requests.Count
        [pscustomobject]@{
            Cmdlet = 'Get-AdoBuildTestFailure'; From = $first; To = $second; Milliseconds = $watch.ElapsedMilliseconds
            Peak = Get-FakeAdoServerPeak -Server $server -Reset; Failures = $set.Failures.Count
            Attempts = @($set.Failures | ForEach-Object Attempts).Count
            Diagnostics = @($set.Diagnostics | ForEach-Object Code | Sort-Object -Unique) -join ','; ReportBytes = $null
            History = '{0} of {1}' -f @($set.History | Where-Object IsAvailable).Count, $set.History.Count
        }
        # Outside the timing, as connecting sends no request.
        if ($SkipAttachments) { Disconnect-Ado }
        $watch.Restart()
        # The report URI goes to the information stream, which the host prints unless it is redirected.
        $file = $set | Export-AdoBuildTestFailure -Path $output -AllRunAttachments:$AllRunAttachments -WarningAction SilentlyContinue 6> $null
        $watch.Stop()
        if ($SkipAttachments) { Connect-Ado -CollectionUrl $server.Uri -Project $project -WarningAction SilentlyContinue | Out-Null }
        [pscustomobject]@{
            Cmdlet = 'Export-AdoBuildTestFailure'; From = $second; To = $server.Requests.Count; Milliseconds = $watch.ElapsedMilliseconds
            Peak = Get-FakeAdoServerPeak -Server $server -Reset; Failures = 0; Attempts = 0; Diagnostics = ''; ReportBytes = $file.Length
            History = ''
        }
    }
    $requests = $server.Requests.ToArray()
    $results = foreach ($cmdlet in 'Get-AdoBuildTestFailure', 'Export-AdoBuildTestFailure') {
        $own = @($samples | Where-Object Cmdlet -EQ $cmdlet)
        $sorted = @($own.Milliseconds | Sort-Object)
        $slices = @(foreach ($sample in $own) { , @(if ($sample.To -gt $sample.From) { $requests[$sample.From..($sample.To - 1)] }) })
        $last = $slices[-1]
        $sent = @($last | Where-Object { -not $_.Aborted })
        # Measure-Object gives no total at all for a run that sent no request, so these sums start at zero.
        [long] $bytes = 0
        [long] $decoded = 0
        foreach ($request in $sent) { $bytes += $request.Bytes; $decoded += $request.DecodedBytes }
        [ordered]@{
            Cmdlet = $cmdlet
            Requests = $last.Count
            Bytes = $bytes
            DecodedBytes = $decoded
            MedianMilliseconds = $sorted[[Math]::Floor(($sorted.Count - 1) / 2)]
            Milliseconds = @($own.Milliseconds)
            PeakConcurrency = [int] ($own.Peak | Measure-Object -Maximum).Maximum
            AbortedResponses = @($slices | ForEach-Object { $_ } | Where-Object Aborted).Count
            Stages = Get-StageTotal -Requests $last
        }
    }
    [ordered]@{
        Module = (Get-Module AdoToolkit).Version.ToString()
        Parameters = [ordered]@{
            Groups = $Groups; RunsPerGroup = $RunsPerGroup; ResultsPerRun = $ResultsPerRun; FailingTests = $FailingTests
            SubResults = $SubResults; AttachmentsPerAttempt = $AttachmentsPerAttempt; HistoryBuilds = $HistoryBuilds; ResultBytes = $ResultBytes
            LatencyMilliseconds = $LatencyMilliseconds; Workers = $Workers; Iterations = $Iterations
            MaximumConcurrentRequests = $MaximumConcurrentRequests; MaximumHistoryRequests = $MaximumHistoryRequests
            Compression = [bool] $Compression; BytesPerSecond = $BytesPerSecond; AllRunAttachments = [bool] $AllRunAttachments
            SkipAttachments = [bool] $SkipAttachments
        }
        ListedResultBytes = [int] [Math]::Round($listedBytes / $listedCount)
        Failures = $samples[0].Failures
        Attempts = $samples[0].Attempts
        Diagnostics = $samples[0].Diagnostics
        History = $samples[0].History
        ReportBytes = $samples[1].ReportBytes
        Results = @($results)
    } | ConvertTo-Json -Depth 6
}
finally {
    Disconnect-Ado
    Stop-FakeAdoServer -Server $server
    $ProgressPreference = $previousProgress
    $env:ADOTOOLKIT_CONFIG_PATH = $previousConfig
    Remove-Item -LiteralPath $work -Recurse -Force
}
