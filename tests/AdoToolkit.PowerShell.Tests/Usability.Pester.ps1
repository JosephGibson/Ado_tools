BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
    $previousConfig = $env:ADOTOOLKIT_CONFIG_PATH
    function Get-TestRunFixture {
        param([Parameter(Mandatory = $true)][string] $Name)
        Get-Content -LiteralPath (Join-Path $PSScriptRoot "../Fixtures/TestRuns/$Name") -Raw -Encoding utf8
    }
    $script:EmptyPage = '{"count":0,"value":[]}'
    # One two-run retrieval without history, after the build has been selected.
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

Describe 'Implicit default-profile connection' {
    BeforeEach {
        $env:ADOTOOLKIT_CONFIG_PATH = Join-Path $TestDrive ([guid]::NewGuid().ToString('N') + '/config.json')
        Disconnect-Ado
    }
    AfterEach { Disconnect-Ado }

    It 'connects with the default profile when no connection exists' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-TestRunFixture 'build-401.json' },
            @{ Body = Get-TestRunFixture 'runs-server2020.json' },
            @{ Body = $script:EmptyPage }
        )
        try {
            Set-AdoProfile -Name sample -CollectionUrl $server.Uri -DefaultProject 'Équipe Web' -DefaultProfile -WarningAction SilentlyContinue | Out-Null
            Get-AdoConnection | Should -BeNullOrEmpty
            $output = @(Get-AdoTestRun -BuildId 401 -Verbose 4>&1)
            $verbose = @($output | Where-Object { $_ -is [System.Management.Automation.VerboseRecord] })
            ($verbose.Message -join "`n") | Should -Match 'sample'
            $runs = @($output | Where-Object { $_ -is [AdoToolkit.Core.TestRuns.AdoTestRun] })
            $runs.Count | Should -Be 2
            $runs[0].BuildId | Should -Be 401
            $runs[0].PassedTests | Should -Be 2
            $runs[0].UnanalyzedTests | Should -Be 2
            $runs[0].OutcomeCounts.Count | Should -Be 0
            (Get-AdoConnection).CollectionUri.AbsoluteUri | Should -Be $server.Uri
            (Get-AdoConnection).DefaultProject | Should -Be 'Équipe Web'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'still fails when no default profile is configured' {
        { Get-AdoTestRun -BuildId 401 -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoTestRunCommand'
        Get-AdoConnection | Should -BeNullOrEmpty
    }
}

Describe 'Connection diagnostics' {
    AfterEach { Disconnect-Ado }

    It 'suggests the collection and project when the URL ends with a project' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Status = 404; Body = '{"message":"Synthetic resource not found.","typeKey":"NotFound"}' },
            @{ Body = '{"value":[{"id":"11111111-1111-1111-1111-111111111111","name":"Équipe O''Web"}]}' },
            @{ Body = '{"value":[]}' }
        )
        try {
            Connect-Ado -CollectionUrl ($server.Uri + '/%C3%89quipe%20O%27Web') -WarningAction SilentlyContinue | Out-Null
            $result = Test-AdoConnection -ErrorAction SilentlyContinue -ErrorVariable failures
            $result.Success | Should -BeFalse
            @($failures).Count | Should -Be 1
            # The suggestion is a pasteable command: single quotes inside values are doubled.
            $result.Hint | Should -BeLike ("*-CollectionUrl '" + $server.Uri + "' -Project 'Équipe O''Web'")
            $requests = $server.Requests.ToArray()
            $requests.Count | Should -Be 3
            $requests[0].Line | Should -Match '^GET /Collection/%C3%89quipe%20O(%27|'')Web/_apis/projects\?'
            $requests[1].Line | Should -Match '^GET /Collection/_apis/projects\?'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'keeps the general hint when the parent has no such project' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Status = 404; Body = '{"message":"Synthetic resource not found.","typeKey":"NotFound"}' },
            @{ Body = '{"value":[]}' }
        )
        try {
            Connect-Ado -CollectionUrl ($server.Uri + '/Mobile') -WarningAction SilentlyContinue | Out-Null
            $result = Test-AdoConnection -ErrorAction SilentlyContinue
            $result.Success | Should -BeFalse
            $result.Hint | Should -Not -Match 'Connect-Ado'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'names the operation and JSON path of a response format error' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-TestRunFixture 'build-401.json' },
            @{ Body = '{"count":1,"value":[{"id":201,"name":"Web tests","build":{"id":"not-a-number"}}]}' }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $failure = { Get-AdoTestRun -BuildId 401 -ErrorAction Stop } | Should -Throw -PassThru
            $failure.FullyQualifiedErrorId | Should -Match '^AdoResponseFormat,'
            $failure.ErrorDetails.Message | Should -Match 'TestRunsList'
            $failure.ErrorDetails.Message | Should -Match ([regex]::Escape('$.value[0].build.id'))
            $failure.ErrorDetails.Message | Should -Not -Match 'not-a-number'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}

Describe 'Failed-test report shortcuts' {
    BeforeEach {
        $env:ADOTOOLKIT_CONFIG_PATH = Join-Path $TestDrive 'config.json'
        $outputDirectory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        [void][IO.Directory]::CreateDirectory($outputDirectory)
    }
    AfterEach { Disconnect-Ado }

    It 'selects the latest completed build of a definition' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'builds-history.json' }) + (Get-TwoRunResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -Definition 42 -Branch main -Result Failed -HistoryCount 1 -WarningAction SilentlyContinue
            $set.Build.Id | Should -Be 401
            $set.FailedCount | Should -Be 2
            $first = $server.Requests.ToArray()[0].Line
            $first | Should -Match '_apis/build/builds\?'
            $first | Should -Match 'definitions=42'
            $first | Should -Match 'branchName=refs%2Fheads%2Fmain'
            $first | Should -Match 'resultFilter=failed'
            $first | Should -Match 'queryOrder=finishTimeDescending'
            $first | Should -Match '%24top=1'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'reports a definition without a completed build as not found' {
        $server = Start-FakeAdoServer -Responses @(@{ Body = '{"count":0,"value":[]}' })
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $failure = { Get-AdoBuildTestFailure -Definition 42 -ErrorAction Stop } | Should -Throw -PassThru
            $failure.FullyQualifiedErrorId | Should -Match '^AdoNotFound,'
            $failure.CategoryInfo.Category | Should -Be 'ObjectNotFound'
            [string] $failure | Should -Match '42'
            $server.Requests.Count | Should -Be 1
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'rejects an invalid definition before any request' {
        $server = Start-FakeAdoServer
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            { Get-AdoBuildTestFailure -Definition 0 -ErrorAction Stop } | Should -Throw -ErrorId 'AdoRequest,AdoToolkit.GetAdoBuildTestFailureCommand'
            $server.Requests.Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'creates a missing report directory only when the export runs' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $destination = Join-Path (Join-Path $outputDirectory 'reports') 'nightly'
            @($set | Export-AdoBuildTestFailure -Path $destination -SkipAttachments -WhatIf).Count | Should -Be 0
            Test-Path -LiteralPath (Join-Path $outputDirectory 'reports') | Should -BeFalse
            $file = $set | Export-AdoBuildTestFailure -Path $destination -SkipAttachments
            $file.FullName | Should -Be (Join-Path $destination 'Build-401-TestFailures.html')
            Test-Path -LiteralPath $file.FullName -PathType Leaf | Should -BeTrue
            $forced = Join-Path $outputDirectory ('release.v2' + [IO.Path]::DirectorySeparatorChar)
            $second = $set | Export-AdoBuildTestFailure -Path $forced -SkipAttachments
            $second.DirectoryName | Should -Be $forced.TrimEnd([IO.Path]::DirectorySeparatorChar)
            $invalid = { $set | Export-AdoBuildTestFailure -Path (Join-Path $outputDirectory 'report.txt') } | Should -Throw -PassThru
            $invalid.FullyQualifiedErrorId | Should -Match '^TestFailureReportPathInvalid,'
            Test-Path -LiteralPath (Join-Path $outputDirectory 'report.txt') | Should -BeFalse
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}
