<#
.SYNOPSIS
Measures Get-AdoBuildTestFailure and Export-AdoBuildTestFailure against the synthetic server.

.DESCRIPTION
Generates one synthetic build in memory, serves it from the routed synthetic server with a fixed
latency, and runs the two cmdlets -Iterations times. Prints one JSON document: the parameters
and, per cmdlet, the requests of one run, the wall time of each run with its median, and the
highest number of requests the server answered at once.

Not a test: neither runner collects this file, and it asserts nothing. Run it by hand against a
staged module, for example the one `verify -Stage project-check` leaves under artifacts/verify:

    $env:ADOTOOLKIT_MODULE_MANIFEST = '<repository>\artifacts\verify\AdoToolkit\<version>\AdoToolkit.psd1'
    pwsh -NoProfile -File .\tests\AdoToolkit.PowerShell.Tests\Support\Measure-TestFailurePipeline.ps1

Every failing test fails in every run of its group, so it has one result record per run. With
-SubResults above zero each record is a rerun group and each sub-result is an attempt.
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
    [ValidateRange(0, 5000)][int] $LatencyMilliseconds = 20,
    [ValidateRange(1, 64)][int] $Workers = 16,
    [ValidateRange(1, 20)][int] $Iterations = 3,
    # Written as testResults.maximumConcurrentRequests. Zero writes no configuration.
    [ValidateRange(0, 16)][int] $MaximumConcurrentRequests = 0,
    [switch] $AllRunAttachments
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

$runPages = @{}
$resultPages = @{}
$details = @{}
$attachmentLists = @{}
$testCaseIds = [System.Collections.Generic.List[int]]::new()
$testCases = @{}
$bugIds = [System.Collections.Generic.SortedSet[int]]::new()

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
                '{"id":' + $id + ',"outcome":"' + $(if ($id -le $failing) { 'Failed' } else { 'Passed' }) +
                '","automatedTestName":"Contoso.Measure.Group' + $group + '.Tests.Suite' + [int] [Math]::Floor(($id - 1) / 50) + '.Test' + $id.ToString('D5', $invariant) +
                '","automatedTestStorage":"' + $storage + '"' + $(if ($id -le $failing -and $SubResults -gt 0) { ',"resultGroupType":"Rerun"' } else { '' }) + '}'
            }
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

$previousConfig = $env:ADOTOOLKIT_CONFIG_PATH
$previousProgress = $ProgressPreference
$work = Join-Path ([IO.Path]::GetTempPath()) ('AdoToolkit-measure-' + [guid]::NewGuid().ToString('N'))
[void] [IO.Directory]::CreateDirectory($work)
$env:ADOTOOLKIT_CONFIG_PATH = Join-Path $work 'config.json'
if ($MaximumConcurrentRequests -gt 0) {
    Set-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Encoding utf8 -Value ('{"schemaVersion":1,"testResults":{"maximumConcurrentRequests":' + $MaximumConcurrentRequests + '}}')
}
$ProgressPreference = 'SilentlyContinue'
$server = Start-FakeAdoServer -Routes $routes -LatencyMilliseconds $LatencyMilliseconds -Workers $Workers
try {
    Connect-Ado -CollectionUrl $server.Uri -Project $project -WarningAction SilentlyContinue | Out-Null
    $samples = foreach ($iteration in 1..$Iterations) {
        $output = Join-Path $work "run-$iteration"
        [void] [IO.Directory]::CreateDirectory($output)
        $first = $server.Requests.Count
        [void] (Get-FakeAdoServerPeak -Server $server -Reset)
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        $set = Get-AdoBuildTestFailure -BuildId $buildId -HistoryCount $HistoryBuilds -WarningAction SilentlyContinue
        $watch.Stop()
        $second = $server.Requests.Count
        [pscustomobject]@{
            Cmdlet = 'Get-AdoBuildTestFailure'; Requests = $second - $first; Milliseconds = $watch.ElapsedMilliseconds
            Peak = Get-FakeAdoServerPeak -Server $server -Reset; Failures = $set.Failures.Count
            Attempts = @($set.Failures | ForEach-Object Attempts).Count
            Diagnostics = @($set.Diagnostics | ForEach-Object Code | Sort-Object -Unique) -join ','; ReportBytes = $null
        }
        $watch.Restart()
        # The report URI goes to the information stream, which the host prints unless it is redirected.
        $file = $set | Export-AdoBuildTestFailure -Path $output -AllRunAttachments:$AllRunAttachments -WarningAction SilentlyContinue 6> $null
        $watch.Stop()
        [pscustomobject]@{
            Cmdlet = 'Export-AdoBuildTestFailure'; Requests = $server.Requests.Count - $second; Milliseconds = $watch.ElapsedMilliseconds
            Peak = Get-FakeAdoServerPeak -Server $server -Reset; Failures = 0; Attempts = 0; Diagnostics = ''; ReportBytes = $file.Length
        }
    }
    $results = foreach ($cmdlet in 'Get-AdoBuildTestFailure', 'Export-AdoBuildTestFailure') {
        $own = @($samples | Where-Object Cmdlet -EQ $cmdlet)
        $sorted = @($own.Milliseconds | Sort-Object)
        [ordered]@{
            Cmdlet = $cmdlet
            Requests = $own[-1].Requests
            MedianMilliseconds = $sorted[[Math]::Floor(($sorted.Count - 1) / 2)]
            Milliseconds = @($own.Milliseconds)
            PeakConcurrency = [int] ($own.Peak | Measure-Object -Maximum).Maximum
        }
    }
    [ordered]@{
        Module = (Get-Module AdoToolkit).Version.ToString()
        Parameters = [ordered]@{
            Groups = $Groups; RunsPerGroup = $RunsPerGroup; ResultsPerRun = $ResultsPerRun; FailingTests = $FailingTests
            SubResults = $SubResults; AttachmentsPerAttempt = $AttachmentsPerAttempt; HistoryBuilds = $HistoryBuilds
            LatencyMilliseconds = $LatencyMilliseconds; Workers = $Workers; Iterations = $Iterations
            MaximumConcurrentRequests = $MaximumConcurrentRequests; AllRunAttachments = [bool] $AllRunAttachments
        }
        Failures = $samples[0].Failures
        Attempts = $samples[0].Attempts
        Diagnostics = $samples[0].Diagnostics
        ReportBytes = $samples[1].ReportBytes
        Results = @($results)
    } | ConvertTo-Json -Depth 5
}
finally {
    Disconnect-Ado
    Stop-FakeAdoServer -Server $server
    $ProgressPreference = $previousProgress
    $env:ADOTOOLKIT_CONFIG_PATH = $previousConfig
    Remove-Item -LiteralPath $work -Recurse -Force
}
