BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
    $previousConfig = $env:ADOTOOLKIT_CONFIG_PATH
    $env:ADOTOOLKIT_CONFIG_PATH = Join-Path $TestDrive 'config.json'
    function Get-TestRunFixture {
        param([Parameter(Mandatory = $true)][string] $Name)
        Get-Content -LiteralPath (Join-Path $PSScriptRoot "../Fixtures/TestRuns/$Name") -Raw -Encoding utf8
    }
    $script:EmptyPage = '{"count":0,"value":[]}'
    # The eleven responses of one two-run retrieval without history, in request order:
    # result 201-1 lists bugs 2001 and 2002, read in one batch with one state list. Bug 2002 is
    # closed, so it is read and then left out of the set. Both runs are completed and list as many
    # results as they report, so neither result listing ends with a request for an empty page.
    # -Piped is a later build of the same invocation: its Test Cases and the bug states come from
    # the invocation cache, so those two requests are not sent.
    function Get-TwoRunResponses {
        param([switch] $Piped)
        @(
            @{ Body = Get-TestRunFixture 'runs-two.json' }
            @{ Body = $script:EmptyPage }
            @{ Body = Get-TestRunFixture 'results-run-201.json' }
            @{ Body = Get-TestRunFixture 'results-run-202.json' }
            @{ Body = Get-TestRunFixture 'result-detail-202-11.json' }
            @{ Body = Get-TestRunFixture 'result-detail-201-1.json' }
            @{ Body = Get-TestRunFixture 'attachments-empty.json' }
            @{ Body = Get-TestRunFixture 'attachments-result.json' }
            if (-not $Piped) { @{ Body = Get-TestRunFixture 'workitems-testcases.json' } }
            @{ Body = Get-TestRunFixture 'workitems-bugs.json' }
            if (-not $Piped) { @{ Body = Get-TestRunFixture 'workitemtype-states-bug.json' } }
        )
    }
    # The history of build 401 with -HistoryCount 3: the window, then builds 400 and 399. Each
    # costs two run pages and one result page, because its run lists as many results as it reports.
    function Get-HistoryResponses {
        @(
            @{ Body = Get-TestRunFixture 'builds-history.json' }
            @{ Body = Get-TestRunFixture 'runs-history-400.json' }
            @{ Body = $script:EmptyPage }
            @{ Body = Get-TestRunFixture 'results-history-400.json' }
            @{ Body = Get-TestRunFixture 'runs-history-399.json' }
            @{ Body = $script:EmptyPage }
            @{ Body = Get-TestRunFixture 'results-history-399.json' }
        )
    }
    # The response lists are positional, so these tests send one request at a time. Extra settings
    # are the body of testResults, for example '"historyScope":"1"'.
    function Set-SequentialConfiguration {
        param([string] $TestResults)
        $settings = '"maximumConcurrentRequests":1' + $(if ($TestResults) { ',' + $TestResults })
        Set-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Encoding utf8 -Value ('{"schemaVersion":1,"testResults":{' + $settings + '}}')
    }
}

AfterAll { $env:ADOTOOLKIT_CONFIG_PATH = $previousConfig }

Describe 'Test run listing' -Tag 'S5-1' {
    AfterEach { Disconnect-Ado }

    It 'reads the build then its runs and emits AdoTestRun with reconstructed links' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-TestRunFixture 'build-401.json' },
            @{ Body = Get-TestRunFixture 'runs-two.json' },
            @{ Body = $script:EmptyPage }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $runs = @(Get-AdoTestRun -BuildId 401)
            $runs.Count | Should -Be 2
            $runs[0] | Should -BeOfType ([AdoToolkit.Core.TestRuns.AdoTestRun])
            $runs.Id | Should -Be @(201, 202)
            $runs[0].BuildId | Should -Be 401
            $runs[0].TeamProject | Should -Be 'Équipe Web'
            $runs[0].State | Should -Be 'Completed'
            $runs[0].IsAutomated | Should -BeTrue
            $runs[0].OutcomeCounts['NotExecuted'] | Should -Be 1
            $runs[0].WebUrl.AbsoluteUri | Should -Be ($server.Uri + '/%C3%89quipe%20Web/_testManagement/runs?_a=runCharts&runId=201')
            $requests = $server.Requests.ToArray()
            # BuildGet, then the run listing filtered by the build URI.
            $requests[0].Line | Should -Be 'GET /Collection/%C3%89quipe%20Web/_apis/build/builds/401?api-version=6.0 HTTP/1.1'
            $requests[1].Line | Should -Match 'buildUri=vstfs%3A%2F%2F%2FBuild%2FBuild%2F401'
            $requests[1].Line | Should -Match 'includeRunDetails=true'
            $requests[1].Line | Should -Match '_apis/test/runs'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'binds a piped build by value and skips the build request' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-TestRunFixture 'builds-history.json' },
            @{ Body = Get-TestRunFixture 'runs-two.json' },
            @{ Body = $script:EmptyPage }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $runs = @(Get-AdoBuild -Definition 42 -Latest | Get-AdoTestRun)
            $runs.Count | Should -Be 2
            $server.Requests.Count | Should -Be 3
            $server.Requests.ToArray()[1].Line | Should -Match '_apis/test/runs'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'rejects a build from another collection and validates local parameters' {
        $server = Start-FakeAdoServer
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            { Get-AdoTestRun -BuildId 0 } | Should -Throw
            { Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 0 } | Should -Throw
            { Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 51 } | Should -Throw
            { Get-AdoBuildTestFailure -BuildId 401 -HistoryScope Nowhere } | Should -Throw
            $foreign = [AdoToolkit.Core.Builds.AdoBuild]@{
                Id = 401
                BuildNumber = '20260915.1'
                Definition = [AdoToolkit.Core.Builds.AdoBuildDefinitionRef]@{ Id = 42; Name = 'Tâches' }
                TeamProject = 'Équipe Web'
                WebUrl = [uri] 'https://other.example.test/Collection/x'
                CollectionUri = [uri] 'https://other.example.test/Collection'
            }
            { $foreign | Get-AdoTestRun -ErrorAction Stop } | Should -Throw
            { $foreign | Get-AdoBuildTestFailure -ErrorAction Stop } | Should -Throw
            $server.Requests.Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}

Describe 'Failed test retrieval' -Tag 'S5-1', 'S5-3' {
    BeforeEach { Set-SequentialConfiguration }
    AfterEach {
        Disconnect-Ado
        Remove-Item -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Force -ErrorAction SilentlyContinue
    }

    It 'prints the bugs of a failure as a table, one row per bug' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $table = @((($set.Failures[1].Bugs | Out-String -Width 200) -split "`r?`n") | Where-Object { $_.Trim() })
            $table[0] | Should -Match '^\s*Id\s+IsOpen\s+State\s+WorkItemType\s+Title\s*$'
            # The header, its underline and the open bug; the closed bug 2002 is not in the set.
            $table.Count | Should -Be 3
            $table[2] | Should -Match '^\s*2001\s+True\s+Active\s+Bug\s+Le panier perd un article\s*$'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'emits one set with failures, counts, history and Test Case links' {
        $responses = @(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses) + (Get-HistoryResponses)
        $server = Start-FakeAdoServer -Responses $responses
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 3 -WarningAction SilentlyContinue
            $set | Should -BeOfType ([AdoToolkit.Core.TestRuns.AdoBuildTestFailureSet])
            $set.Build.Id | Should -Be 401
            $set.Runs.Count | Should -Be 2
            $set.Failures.Count | Should -Be 2
            $set.Failures[0].Ordinal | Should -Be 1
            $set.Failures.ShortName | Should -Be @('Totals', 'AddsItem')
            $set.Failures[0] | Should -BeOfType ([AdoToolkit.Core.TestRuns.AdoTestFailure])
            $set.Failures[1].TestCase.Title | Should -Be 'Vérifier le panier'
            # Bug 2002 is closed: the attempt still names it, and the set leaves it out.
            $set.Failures[1].Attempts[0].AssociatedBugIds | Should -Be @(2001, 2002)
            $set.Failures[1].Bugs.Count | Should -Be 1
            $set.Failures[1].Bugs[0] | Should -BeOfType ([AdoToolkit.Core.TestRuns.AdoTestBug])
            $set.Failures[1].Bugs[0].Id | Should -Be 2001
            $set.Failures[1].Bugs[0].Title | Should -Be 'Le panier perd un article'
            $set.Failures[1].Bugs[0].State | Should -Be 'Active'
            $set.Failures[1].Bugs[0].StateCategory | Should -Be 'InProgress'
            $set.Failures[1].Bugs[0].IsOpen | Should -BeTrue
            $set.Failures[1].Bugs[0].IsAssociatedWithResult | Should -BeTrue
            $set.Failures[1].HasOpenBug | Should -BeTrue
            $set.Failures[0].HasOpenBug | Should -BeFalse
            $set.Failures[1].Attempts[0].Attachments.Count | Should -Be 5
            $set.Failures[1].Attempts[0].Attachments[0].Kind | Should -Be 'Png'
            $set.Failures[1].Attempts[0].Attachments[0].DownloadStatus | Should -Be 'NotRequested'
            $set.FailedCount | Should -Be 2
            $set.FlakyCount | Should -Be 0
            $set.Status | Should -Be 'Complete'
            $set.Summary.Passed | Should -Be 1
            $set.Summary.Other | Should -Be 1
            # Oldest first, current last, one cell per entry.
            $set.History.Count | Should -Be 3
            $set.History.BuildId | Should -Be @(399, 400, 401)
            $set.History[-1].IsCurrent | Should -BeTrue
            $set.Failures[1].History.Count | Should -Be 3
            $set.Failures[1].History[-1].Outcome | Should -Be 'Failed'
            $set.Diagnostics.Code | Should -Contain 'UnresolvedTestCase'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'falls back to SameBranch for a configured scope that is not a scope name' {
        Set-SequentialConfiguration -TestResults '"historyScope":"1"'
        $responses = @(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses) + (Get-HistoryResponses)
        $server = Start-FakeAdoServer -Responses $responses
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 3 -WarningAction SilentlyContinue
            $set.History.Count | Should -Be 3
            $window = @($server.Requests.ToArray() | Where-Object { $_.Line -match '/_apis/build/builds\?' })
            $window.Count | Should -Be 1
            $window[0].Line | Should -Match 'branchName='
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'emits one set per piped build' {
        $responses = @(@{ Body = Get-TestRunFixture 'builds-history.json' })
        $responses += Get-TwoRunResponses
        $responses += Get-TwoRunResponses -Piped
        $server = Start-FakeAdoServer -Responses $responses
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $sets = @(Get-AdoBuild -Definition 42 -Top 2 | Get-AdoBuildTestFailure -HistoryCount 1 -WarningAction SilentlyContinue)
            $sets.Count | Should -Be 2
            $sets.Build.Id | Should -Be @(401, 400)
            foreach ($set in $sets) {
                $set.Failures.Count | Should -Be 2
                $set.History.Count | Should -Be 1
                $set.History[0].IsCurrent | Should -BeTrue
            }
            # One build listing, eleven retrieval requests for the first set and nine for the second,
            # which takes its Test Cases and the bug states from the invocation cache.
            $server.Requests.Count | Should -Be 21
            # The second set still warns about the Test Case that the first one could not resolve.
            foreach ($set in $sets) { $set.Diagnostics.Code | Should -Contain 'UnresolvedTestCase' }
            @($server.Requests.ToArray() | Where-Object { $_.Body -match '\$expand' }).Count | Should -Be 1
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'warns once and reports Partial when the configured failure maximum is exceeded' {
        Set-SequentialConfiguration -TestResults '"maximumReportedFailures":2'
        # Run 201 lists five results and reports three, so its listing still ends on an empty page;
        # the listing of run 202 is empty at once.
        $responses = @(@{ Body = Get-TestRunFixture 'build-401.json' }, @{ Body = Get-TestRunFixture 'runs-two.json' },
            @{ Body = $script:EmptyPage }, @{ Body = Get-TestRunFixture 'results-outcomes.json' },
            @{ Body = $script:EmptyPage }, @{ Body = $script:EmptyPage })
        foreach ($id in 93, 91) {
            $responses += @{ Body = ('{"id":' + $id + ',"outcome":"Error","automatedTestStorage":"Contoso.Web.Tests.dll","automatedTestName":"Contoso.Web.Tests.OutcomeTests.X' + $id + '"}') }
        }
        $responses += @{ Body = Get-TestRunFixture 'attachments-empty.json' }
        $responses += @{ Body = Get-TestRunFixture 'attachments-empty.json' }
        $server = Start-FakeAdoServer -Responses $responses
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $warnings = @()
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningVariable warnings
            $set.Status | Should -Be 'Partial'
            $set.Failures.Count | Should -Be 2
            @($warnings).Count | Should -Be 1
            [string] $warnings[0] | Should -Match '401'
            $set.Diagnostics.Code | Should -Contain 'FailureLimitExceeded'
            # The identities beyond the maximum are still counted.
            $set.Summary.Failed | Should -Be 3
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}

# The same two-run build, answered by route instead of by position, so requests may arrive in any
# order. A small latency makes the requests of one stage overlap; no wall time is asserted.
Describe 'Concurrent failed test retrieval and download' {
    BeforeAll {
        function Get-TwoRunRoutes {
            @(
                @{ Line = '/_apis/build/builds/401\?'; Response = @{ Body = Get-TestRunFixture 'build-401.json' } }
                @{ Line = '/_apis/test/runs\?.*%24skip=0&'; Response = @{ Body = Get-TestRunFixture 'runs-two.json' } }
                @{ Line = '/Runs/201/results\?.*%24skip=0&'; Response = @{ Body = Get-TestRunFixture 'results-run-201.json' } }
                @{ Line = '/Runs/202/results\?.*%24skip=0&'; Response = @{ Body = Get-TestRunFixture 'results-run-202.json' } }
                @{ Line = '/Runs/(\d+)/results/(\d+)\?'; Responses = @{
                        '201/1' = @{ Body = Get-TestRunFixture 'result-detail-201-1.json' }
                        '202/11' = @{ Body = Get-TestRunFixture 'result-detail-202-11.json' }
                    }
                }
                @{ Line = '/Runs/(201/Results/1|202/Results/11)/attachments\?'; Response = @{ Body = Get-TestRunFixture 'attachments-result.json' } }
                @{ Line = '/attachments/5002\?'; Response = @{
                        Bytes = [IO.File]::ReadAllBytes((Join-Path $PSScriptRoot '../Fixtures/Attachments/valid.json')); ContentType = 'application/octet-stream'
                    }
                }
                @{ Line = '/_apis/wit/workitemsbatch\?'; Body = '"\$expand":"relations"'; Response = @{ Body = Get-TestRunFixture 'workitems-testcases.json' } }
                @{ Line = '/_apis/wit/workitemsbatch\?'; Body = '"fields":'; Response = @{ Body = Get-TestRunFixture 'workitems-bugs.json' } }
                @{ Line = '/_apis/wit/workitemtypes/Bug/states\?'; Response = @{ Body = Get-TestRunFixture 'workitemtype-states-bug.json' } }
            )
        }
    }
    AfterEach {
        Disconnect-Ado
        Remove-Item -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Force -ErrorAction SilentlyContinue
    }

    It 'sends the same requests with <Bound> at a time and never more (peak <Peak>)' -ForEach @(
        @{ Bound = 6; Peak = 2 },
        @{ Bound = 1; Peak = 1 }
    ) {
        # Six is the default, so that case writes no configuration.
        if ($Bound -ne 6) { Set-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Encoding utf8 -Value ('{"schemaVersion":1,"testResults":{"maximumConcurrentRequests":' + $Bound + '}}') }
        $server = Start-FakeAdoServer -Routes (Get-TwoRunRoutes) -LatencyMilliseconds 100 -Workers 8
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $set.Failures.ShortName | Should -Be @('Totals', 'AddsItem')
            $set.Failures[1].Bugs.Id | Should -Be @(2001)
            $set.Diagnostics.Code | Should -Be @('UnresolvedTestCase')
            # The build, two run pages, two result pages, two details, two attachment lists, the Test
            # Cases, the bugs and one state list. Each stage of this build has two requests at most.
            $server.Requests.Count | Should -Be 12
            Get-FakeAdoServerPeak -Server $server -Reset | Should -Be $Peak
            $lines = @($server.Requests.ToArray().Line)
            @($lines | Sort-Object -Unique).Count | Should -Be 11
            @($lines | Where-Object { $_ -match '/_apis/wit/workitemsbatch\?' }).Count | Should -Be 2

            # The export reads the small JSON file of each run, both at once unless the bound is one.
            # The fixture runs started in September 2026, so the attachment window is a year.
            $file = $set | Export-AdoBuildTestFailure -Path (Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))) -AttachmentWindowDays 365 6> $null
            $server.Requests.Count | Should -Be 14
            Get-FakeAdoServerPeak -Server $server | Should -Be $Peak
            @(Get-ChildItem -LiteralPath $file.AttachmentDirectory -File).Name | Sort-Object | Should -Be @('r201-1-a5002.json', 'r202-11-a5002.json')
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}
