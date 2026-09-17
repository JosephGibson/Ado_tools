BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
    function Get-BuildFixture {
        param([Parameter(Mandatory = $true)][string] $Name)
        Get-Content -LiteralPath (Join-Path $PSScriptRoot "../Fixtures/$Name") -Raw -Encoding utf8
    }
}

Describe 'Build discovery and timeline triage' -Tag 'S4-1', 'S4-2' {
    AfterEach { Disconnect-Ado }

    It 'F04 carries the piped build project and honors an explicit override' -TestCases @(
        @{ ExplicitProject = $false; RouteProject = '%C3%89quipe%20Web' },
        @{ ExplicitProject = $true; RouteProject = 'Override' }
    ) {
        param($ExplicitProject, $RouteProject)
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-BuildFixture 'Rest/builds-first.json' },
            @{ Body = Get-BuildFixture 'Timelines/multi-stage.json' },
            @{ Body = Get-BuildFixture 'Rest/build-logs.json' },
            @{ Body = Get-BuildFixture 'Rest/build-logs.json' },
            @{ Body = 'synthetic log' }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Wrong' -WarningAction SilentlyContinue | Out-Null
            $failures = @(Get-AdoBuild -Project 'Équipe Web' -Definition 42 | Get-AdoBuildFailure)
            $options = if ($ExplicitProject) { @{ Project = 'Override' } } else { @{} }
            $file = $failures[0] | Save-AdoBuildLog -Path $TestDrive @options
            $file | Should -BeOfType ([IO.FileInfo])
            $requests = $server.Requests.ToArray()
            $requests[3].Line | Should -Be "GET /Collection/$RouteProject/_apis/build/builds/401/logs?api-version=6.0 HTTP/1.1"
            $requests[4].Line | Should -Be "GET /Collection/$RouteProject/_apis/build/builds/401/logs/11?api-version=6.0 HTTP/1.1"
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'binds definitions to builds to failures by value and fetches logs once' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-BuildFixture 'Rest/build-definitions-first.json' },
            @{ Body = Get-BuildFixture 'Rest/builds-first.json' },
            @{ Body = Get-BuildFixture 'Timelines/multi-stage.json' },
            @{ Body = Get-BuildFixture 'Rest/build-logs.json' }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Wrong' -WarningAction SilentlyContinue | Out-Null
            $definitions = @(Get-AdoBuildDefinition -Project 'Équipe Web' -Id 42)
            $definitions[0] | Should -BeOfType ([AdoToolkit.Core.Builds.AdoBuildDefinition])
            $builds = @($definitions | Get-AdoBuild -Branch main -Latest -Result Failed)
            $builds[0] | Should -BeOfType ([AdoToolkit.Core.Builds.AdoBuild])
            $builds[0].TeamProject | Should -Be 'Équipe Web'
            $builds[0].WebUrl.AbsoluteUri | Should -Be ($server.Uri + '/%C3%89quipe%20Web/_build/results?buildId=401')
            $failures = @($builds | Get-AdoBuildFailure)
            $failures.Count | Should -Be 2
            $failures[0] | Should -BeOfType ([AdoToolkit.Core.Builds.AdoBuildFailure])
            $failures.Path | Should -Be @('Deploy › Release › Prepare', 'Deploy › Release › Publish')
            $failures.LogLineCount | Should -Be @(1000, 0)
            $requests = $server.Requests.ToArray()
            $requests.Count | Should -Be 4
            $requests[1].Line | Should -Match 'definitions=42&branchName=refs%2Fheads%2Fmain&statusFilter=completed&resultFilter=failed&%24top=1&queryOrder=finishTimeDescending&api-version=6.0'
            $requests[2].Line | Should -Be 'GET /Collection/%C3%89quipe%20Web/_apis/build/builds/401/timeline?api-version=6.0 HTTP/1.1'
            $requests[3].Headers['Accept'] | Should -Be 'application/json'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'binds builds to the timeline and retains all records in tree order' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-BuildFixture 'Rest/builds-first.json' },
            @{ Body = Get-BuildFixture 'Timelines/multi-stage.json' }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $records = @(Get-AdoBuild -Definition 42 | Get-AdoBuildTimeline)
            $records.Name | Should -Be @('Build', 'Compile phase', 'Compile job', 'Compile', 'Deploy', 'Release', 'Prepare', 'Publish')
            $records[0] | Should -BeOfType ([AdoToolkit.Core.Builds.AdoTimelineRecord])
            $records[0].BuildId | Should -Be 401
            $server.Requests.Count | Should -Be 2
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'gets a build by ID through BuildsList before deriving failures' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-BuildFixture 'Rest/builds-first.json' },
            @{ Body = Get-BuildFixture 'Timelines/no-log.json' },
            @{ Body = Get-BuildFixture 'Rest/build-logs.json' }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $failure = Get-AdoBuildFailure -BuildId 401
            $failure.Path | Should -Be 'Build › Agent allocation'
            $failure.LogId | Should -BeNullOrEmpty
            $server.Requests.ToArray()[0].Line | Should -Match 'builds\?buildIds=401&api-version=6.0'
            $server.Requests.Count | Should -Be 3
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'adds the deepest warning only with IncludeWarnings' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-BuildFixture 'Rest/builds-first.json' },
            @{ Body = Get-BuildFixture 'Timelines/warnings.json' },
            @{ Body = Get-BuildFixture 'Rest/build-logs.json' },
            @{ Body = Get-BuildFixture 'Timelines/warnings.json' },
            @{ Body = Get-BuildFixture 'Rest/build-logs.json' }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $run = Get-AdoBuild -Definition 42
            @($run | Get-AdoBuildFailure).Count | Should -Be 0
            $failure = $run | Get-AdoBuildFailure -IncludeWarnings
            $failure.Path | Should -Be 'Build › Compile › Analyze'
            $failure.Result | Should -Be 'succeededWithIssues'
            $failure.WarningCount | Should -Be 1
            $server.Requests.Count | Should -Be 5
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'reports ambiguous exact names with the count and ID guidance' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-BuildFixture 'Rest/build-definitions-first.json'; Headers = @{ 'x-ms-continuationtoken' = 'page2' } },
            @{ Body = Get-BuildFixture 'Rest/build-definitions-last.json' }
        )
        $previous = [System.Threading.Thread]::CurrentThread.CurrentUICulture
        try {
            [System.Threading.Thread]::CurrentThread.CurrentUICulture = [cultureinfo] 'fr-CA'
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $failures = @()
            @(Get-AdoBuild -Definition 'Tâches' -ErrorAction SilentlyContinue -ErrorVariable failures).Count | Should -Be 0
            $failures.Count | Should -Be 1
            $failures[0].FullyQualifiedErrorId | Should -Be 'AdoRequest,AdoToolkit.GetAdoBuildCommand'
            $failures[0].Exception.Message | Should -Match '2 définitions.*-Definition <id>'
            $server.Requests.Count | Should -Be 2
        }
        finally {
            [System.Threading.Thread]::CurrentThread.CurrentUICulture = $previous
            Stop-FakeAdoServer -Server $server
        }
    }

    It 'matches wildcard names using NFC with case ignored and accents preserved' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-BuildFixture 'Rest/build-definitions-last.json' },
            @{ Body = Get-BuildFixture 'Rest/build-definitions-last.json' }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $decomposed = 'TA' + [char] 0x0302 + 'CH*'
            (Get-AdoBuildDefinition -Name $decomposed).Id | Should -Be 44
            (Get-AdoBuildDefinition -Name 'Tach*').Id | Should -Be 43
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'rejects foreign pipeline entities and continues with the next build' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-BuildFixture 'Rest/builds-first.json' },
            @{ Body = Get-BuildFixture 'Timelines/no-log.json' },
            @{ Body = Get-BuildFixture 'Rest/build-logs.json' }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $local = Get-AdoBuild -Definition 42
            $foreign = [AdoToolkit.Core.Builds.AdoBuild]@{ Id = 401; BuildNumber = 'Foreign'; Definition = $local.Definition
                TeamProject = 'Équipe Web'; WebUrl = 'https://other.example.test/ignored'; CollectionUri = 'https://other.example.test/Collection' }
            $definition = [AdoToolkit.Core.Builds.AdoBuildDefinition]@{ Id = 42; Name = 'Foreign'; TeamProject = 'Équipe Web'
                WebUrl = 'https://other.example.test/ignored'; CollectionUri = $foreign.CollectionUri }
            $errors = @()
            @($definition | Get-AdoBuild -ErrorAction SilentlyContinue -ErrorVariable errors).Count | Should -Be 0
            $errors[0].FullyQualifiedErrorId | Should -Be 'AdoConnectionMismatch,AdoToolkit.GetAdoBuildCommand'
            $errors = @()
            @($foreign | Get-AdoBuildTimeline -ErrorAction SilentlyContinue -ErrorVariable errors).Count | Should -Be 0
            $errors[0].FullyQualifiedErrorId | Should -Be 'AdoConnectionMismatch,AdoToolkit.GetAdoBuildTimelineCommand'
            $errors = @()
            @(@($foreign, $local) | Get-AdoBuildFailure -ErrorAction SilentlyContinue -ErrorVariable errors).Count | Should -Be 1
            $errors.Count | Should -Be 1
            $errors[0].FullyQualifiedErrorId | Should -Be 'AdoConnectionMismatch,AdoToolkit.GetAdoBuildFailureCommand'
            $server.Requests.Count | Should -Be 3
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'validates positive IDs and closed enums without requests' {
        $server = Start-FakeAdoServer
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            { Get-AdoBuildDefinition -Id 0 } | Should -Throw
            { Get-AdoBuildTimeline -BuildId 0 } | Should -Throw
            { Get-AdoBuildFailure -BuildId -1 } | Should -Throw
            { Get-AdoBuild -Definition 42 -Top 0 } | Should -Throw
            { Get-AdoBuild -Definition 42 -Result Strange } | Should -Throw
            { Get-AdoBuild -Definition 42 -Status Strange } | Should -Throw
            { Get-AdoBuild -Definition 0 -ErrorAction Stop } | Should -Throw
            $server.Requests.Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}
