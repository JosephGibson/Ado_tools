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
    # The eleven responses of one two-run retrieval without history, in request order.
    function Get-TwoRunResponses {
        @(
            @{ Body = Get-TestRunFixture 'runs-two.json' },
            @{ Body = $script:EmptyPage },
            @{ Body = Get-TestRunFixture 'results-run-201.json' },
            @{ Body = $script:EmptyPage },
            @{ Body = Get-TestRunFixture 'results-run-202.json' },
            @{ Body = $script:EmptyPage },
            @{ Body = Get-TestRunFixture 'result-detail-202-11.json' },
            @{ Body = Get-TestRunFixture 'result-detail-201-1.json' },
            @{ Body = Get-TestRunFixture 'attachments-empty.json' },
            @{ Body = Get-TestRunFixture 'attachments-result.json' },
            @{ Body = Get-TestRunFixture 'workitems-testcases.json' }
        )
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
    AfterEach { Disconnect-Ado }

    It 'emits one set with failures, counts, history and Test Case links' {
        $responses = @(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses) + @(
            @{ Body = Get-TestRunFixture 'builds-history.json' },
            @{ Body = Get-TestRunFixture 'runs-history-400.json' },
            @{ Body = $script:EmptyPage },
            @{ Body = Get-TestRunFixture 'results-history-400.json' },
            @{ Body = $script:EmptyPage },
            @{ Body = Get-TestRunFixture 'runs-history-399.json' },
            @{ Body = $script:EmptyPage },
            @{ Body = Get-TestRunFixture 'results-history-399.json' },
            @{ Body = $script:EmptyPage }
        )
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
        Set-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Encoding utf8 -Value '{"schemaVersion":1,"testResults":{"historyScope":"1"}}'
        $responses = @(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses) + @(
            @{ Body = Get-TestRunFixture 'builds-history.json' },
            @{ Body = Get-TestRunFixture 'runs-history-400.json' },
            @{ Body = $script:EmptyPage },
            @{ Body = Get-TestRunFixture 'results-history-400.json' },
            @{ Body = $script:EmptyPage },
            @{ Body = Get-TestRunFixture 'runs-history-399.json' },
            @{ Body = $script:EmptyPage },
            @{ Body = Get-TestRunFixture 'results-history-399.json' },
            @{ Body = $script:EmptyPage }
        )
        $server = Start-FakeAdoServer -Responses $responses
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 3 -WarningAction SilentlyContinue
            $set.History.Count | Should -Be 3
            $window = @($server.Requests.ToArray() | Where-Object { $_.Line -match '/_apis/build/builds\?' })
            $window.Count | Should -Be 1
            $window[0].Line | Should -Match 'branchName='
        }
        finally {
            Stop-FakeAdoServer -Server $server
            Remove-Item -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Force
        }
    }

    It 'emits one set per piped build' {
        $responses = @(@{ Body = Get-TestRunFixture 'builds-history.json' })
        $responses += Get-TwoRunResponses
        $responses += Get-TwoRunResponses
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
            # One build listing plus eleven retrieval requests per set.
            $server.Requests.Count | Should -Be 23
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'warns once and reports Partial when the configured failure maximum is exceeded' {
        Set-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Encoding utf8 -Value '{"schemaVersion":1,"testResults":{"maximumReportedFailures":2}}'
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
        finally {
            Stop-FakeAdoServer -Server $server
            Remove-Item -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Force
        }
    }
}
