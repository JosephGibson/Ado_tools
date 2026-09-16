BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
    $fixtureRoot = Join-Path $PSScriptRoot '../Fixtures/Rest'
    $previousConfig = $env:ADOTOOLKIT_CONFIG_PATH
    $env:ADOTOOLKIT_CONFIG_PATH = Join-Path $TestDrive 'config.json'
    function Get-Fixture {
        param([Parameter(Mandatory = $true)][string] $Name)
        Get-Content -LiteralPath (Join-Path $fixtureRoot $Name) -Raw -Encoding utf8
    }
    # Testplan shapes are [V-04] assumptions; responses are served in request order.
    $plansBody = Get-Fixture 'testplans-first.json'
    $suiteTree = [System.Text.Json.Nodes.JsonNode]::Parse((Get-Fixture 'testsuites-first.json'))
    foreach ($suite in [System.Text.Json.Nodes.JsonNode]::Parse((Get-Fixture 'testsuites-last.json'))['value']) { $suiteTree['value'].Add($suite.DeepClone()) }
    $suitesBody = $suiteTree.ToJsonString()
    function New-MembershipBody {
        param([int[]] $Ids = @())
        $values = [System.Text.Json.Nodes.JsonArray]::new()
        $order = 0
        foreach ($id in $Ids) {
            $order++
            $values.Add([System.Text.Json.Nodes.JsonNode]::Parse('{"workItem":{"id":' + $id + '},"order":' + $order + '}'))
        }
        $root = [System.Text.Json.Nodes.JsonObject]::new()
        $root['value'] = $values
        $root.ToJsonString()
    }
    function New-BatchBody {
        param([int[]] $Ids)
        $template = [System.Text.Json.Nodes.JsonNode]::Parse((Get-Fixture 'testcase-mixed.json'))['value'][2]
        $values = [System.Text.Json.Nodes.JsonArray]::new()
        foreach ($id in $Ids) {
            $item = $template.DeepClone()
            $item['id'] = [System.Text.Json.Nodes.JsonValue]::Create([int] $id)
            $item['fields']['System.Title'] = [System.Text.Json.Nodes.JsonValue]::Create("Synthetic $id")
            $values.Add($item)
        }
        $root = [System.Text.Json.Nodes.JsonObject]::new()
        $root['value'] = $values
        $root.ToJsonString()
    }
    function Get-BatchId {
        param([Parameter(Mandatory = $true)][object] $Server)
        @($Server.Requests.ToArray() | Where-Object { $_.Line.StartsWith('POST ') } | ForEach-Object { , @(($_.Body | ConvertFrom-Json).ids) })
    }
    function Get-BoundParameter {
        param([Parameter(Mandatory = $true)][string] $Path)
        # Tracing is process-wide; keep only pipeline binding for Get-AdoTestCase.
        $lines = @(Get-Content -LiteralPath $Path)
        $start = [array]::FindIndex($lines, [Predicate[string]] { param($line) $line.Contains('BIND PIPELINE object to parameters: [Get-AdoTestCase]') })
        if ($start -lt 0) { throw 'Get-AdoTestCase pipeline binding was not traced.' }
        @($lines[$start..($lines.Count - 1)] | Select-String -Pattern 'BIND arg \[.*\] to param \[(\w+)\] SUCCESSFUL' | ForEach-Object { $_.Matches[0].Groups[1].Value })
    }
}
AfterAll { $env:ADOTOOLKIT_CONFIG_PATH = $previousConfig }

Describe 'Bulk test case retrieval' -Tag 'S3-2' {
    AfterEach { Disconnect-Ado }

    It 'reads a recursive suite, fetches each case once and emits every membership with Suite' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = $plansBody }, @{ Body = $suitesBody },
            @{ Body = New-MembershipBody -Ids @(1201, 1203) }, @{ Body = New-MembershipBody -Ids @(1201, 1202) }, @{ Body = New-MembershipBody },
            @{ Body = New-BatchBody -Ids @(1201, 1203, 1202) }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $items = @(Get-AdoTestCase -PlanId 812 -SuiteId 814 -Recurse)
            $items.Id | Should -Be @(1201, 1203, 1201, 1202)
            @($items | ForEach-Object { $_.Suite.SuiteId }) | Should -Be @(814, 814, 816, 816)
            $items[0] | Should -BeOfType ([AdoToolkit.Core.TestManagement.AdoTestCase])
            @($items[2].Suite.SuitePath) | Should -Be @('Tâches de régression', 'Connexion', "Écran d’accueil")
            $items[2].Suite.PlanName | Should -Be 'Tâches de régression'
            [object]::ReferenceEquals($items[0].Steps, $items[2].Steps) | Should -BeTrue
            $lines = @($server.Requests.ToArray().Line)
            $lines.Count | Should -Be 6
            $lines[2] | Should -Be 'GET /Collection/%C3%89quipe%20Web/_apis/testplan/Plans/812/Suites/814/TestCase?api-version=6.0-preview.2 HTTP/1.1'
            $lines[4] | Should -Match '/Suites/818/TestCase\?'
            $batches = @(Get-BatchId -Server $server)
            $batches.Count | Should -Be 1
            $batches[0] | Should -Be @(1201, 1203, 1202)
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'binds a piped <Kind> to its own parameter set and never uses a suite ID as a test case ID' -TestCases @(
        @{ Kind = 'AdoTestSuite'; Parameter = 'Suite'; Expected = @(1203); Batch = @(1203) },
        @{ Kind = 'AdoWorkItem'; Parameter = 'WorkItem'; Expected = @(1202); Batch = @(1202) },
        @{ Kind = 'AdoWiqlResult'; Parameter = 'WiqlResult'; Expected = @(1203, 1201); Batch = @(1203, 1201) },
        @{ Kind = 'PSCustomObject'; Parameter = 'Id'; Expected = @(1201); Batch = @(1201) }
    ) {
        param($Kind, $Parameter, $Expected, $Batch)
        $responses = if ($Kind -eq 'AdoTestSuite') { @(@{ Body = $plansBody }, @{ Body = New-MembershipBody -Ids @(1203) }) } else { @() }
        $server = Start-FakeAdoServer -Responses (@($responses) + @(@{ Body = New-BatchBody -Ids $Batch }))
        try {
            $connection = Connect-Ado -CollectionUrl $server.Uri -Project 'Autre projet' -WarningAction SilentlyContinue
            $uri = $connection.CollectionUri
            # The suite ID equals a real test case ID, so a wrong binding would return case 1201.
            $object = switch ($Kind) {
                'AdoTestSuite' {
                    [AdoToolkit.Core.TestManagement.AdoTestSuite]@{ Id = 1201; PlanId = 812; Name = 'Connexion'
                        SuitePath = [string[]] @('Tâches de régression', 'Connexion'); TeamProject = 'Équipe Web'; CollectionUri = $uri }
                }
                'AdoWorkItem' {
                    [AdoToolkit.Core.WorkItems.AdoWorkItem]@{ Id = 1202; Rev = 1; WorkItemType = 'Cas'; Title = 'T'; State = 'Ready'; TeamProject = 'Équipe Web'
                        AreaPath = 'A'; IterationPath = 'I'; Fields = [System.Collections.Generic.Dictionary[string, object]]::new(); WebUrl = $uri; CollectionUri = $uri }
                }
                'AdoWiqlResult' {
                    [AdoToolkit.Core.WorkItems.AdoWiqlResult]@{ Ids = [int[]] @(1203, 1201); TeamProject = 'Équipe Web'; CollectionUri = $uri }
                }
                default { [pscustomobject]@{ Id = 1201; CollectionUri = $uri } }
            }
            $trace = Join-Path $TestDrive ([guid]::NewGuid().ToString('N') + '.txt')
            $items = @(Trace-Command -Name ParameterBinding -FilePath $trace -Expression { $object | Get-AdoTestCase })
            $bound = Get-BoundParameter -Path $trace
            $bound | Should -Contain $Parameter
            if ($Kind -ne 'PSCustomObject') { $bound | Should -Not -Contain 'Id' }
            $items.Id | Should -Be $Expected
            $batches = @(Get-BatchId -Server $server)
            $batches.Count | Should -Be 1
            $batches[0] | Should -Be $Batch
            if ($Kind -eq 'AdoTestSuite') {
                $items[0].Suite.SuiteId | Should -Be 1201
                $items[0].Suite.TeamProject | Should -Be 'Équipe Web'
                $server.Requests.ToArray()[1].Line | Should -Match '^GET /Collection/%C3%89quipe%20Web/_apis/testplan/Plans/812/Suites/1201/TestCase\?'
            }
            else { $items | ForEach-Object { $_.Suite | Should -BeNullOrEmpty } }
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'rejects untyped suite and plan shapes before any request' {
        $server = Start-FakeAdoServer
        try {
            $connection = Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue
            $suite = [AdoToolkit.Core.TestManagement.AdoTestSuite]@{ Id = 1201; PlanId = 812; Name = 'S'; SuitePath = [string[]] @('S')
                TeamProject = 'Équipe Web'; CollectionUri = $connection.CollectionUri }
            $plan = [AdoToolkit.Core.TestManagement.AdoTestPlan]@{ Id = 812; Name = 'P'; RootSuiteId = 813; TeamProject = 'Équipe Web'; CollectionUri = $connection.CollectionUri }
            foreach ($candidate in @(($suite | Select-Object -Property *), $plan)) {
                $failures = @()
                @($candidate | Get-AdoTestCase -ErrorAction SilentlyContinue -ErrorVariable failures).Count | Should -Be 0
                $failures.Count | Should -Be 1
                $failures[0].FullyQualifiedErrorId | Should -Be 'InputNotTestCase,AdoToolkit.GetAdoTestCaseCommand'
                $failures[0].CategoryInfo.Category | Should -Be 'InvalidType'
            }
            $foreign = [AdoToolkit.Core.TestManagement.AdoTestSuite]@{ Id = 814; PlanId = 812; Name = 'S'; TeamProject = 'Équipe Web'
                CollectionUri = [uri] 'https://other.example.test/Collection' }
            $failures = @()
            @($foreign | Get-AdoTestCase -ErrorAction SilentlyContinue -ErrorVariable failures).Count | Should -Be 0
            $failures[0].FullyQualifiedErrorId | Should -Be 'AdoConnectionMismatch,AdoToolkit.GetAdoTestCaseCommand'
            { Get-AdoTestCase -PlanId 0 -SuiteId 1 } | Should -Throw -ErrorId 'ParameterArgumentValidationError,AdoToolkit.GetAdoTestCaseCommand'
            $server.Requests.Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'keeps mixed pipeline order and reports a missing suite without stopping other input' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = $plansBody }, @{ Body = New-MembershipBody -Ids @(1203) }, @{ Body = New-MembershipBody -Ids @(1203) },
            @{ Body = New-BatchBody -Ids @(1203, 1202) }, @{ Body = $plansBody }, @{ Body = $suitesBody }
        )
        try {
            $connection = Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue
            $suite = [AdoToolkit.Core.TestManagement.AdoTestSuite]@{ Id = 814; PlanId = 812; Name = 'Connexion'; SuitePath = [string[]] @('Connexion')
                TeamProject = 'Équipe Web'; CollectionUri = $connection.CollectionUri }
            $missing = [AdoToolkit.Core.TestManagement.AdoTestSuite]@{ Id = 999; PlanId = 812; Name = 'Absente'; TeamProject = 'Autre'
                CollectionUri = $connection.CollectionUri }
            $failures = @()
            $items = @($suite, 1202, $suite | Get-AdoTestCase -ErrorAction SilentlyContinue -ErrorVariable failures)
            $items.Id | Should -Be @(1203, 1202)
            $failures.Count | Should -Be 0
            $items = @($missing | Get-AdoTestCase -Recurse -ErrorAction SilentlyContinue -ErrorVariable failures)
            $items.Count | Should -Be 0
            $failures.Count | Should -Be 1
            $failures[0].FullyQualifiedErrorId | Should -Be 'AdoNotFound,AdoToolkit.GetAdoTestCaseCommand'
            $server.Requests.Count | Should -Be 6
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'writes localized progress records for enumeration, fetch and expansion with a completed record' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = $plansBody }, @{ Body = $suitesBody },
            @{ Body = New-MembershipBody -Ids @(1201, 1203) }, @{ Body = New-MembershipBody -Ids @(1201, 1202) }, @{ Body = New-MembershipBody },
            @{ Body = New-BatchBody -Ids @(1201, 1203, 1202) }
        )
        $shell = [powershell]::Create()
        try {
            [void] $shell.AddScript({
                param($Manifest, $Uri, $Culture)
                [System.Threading.Thread]::CurrentThread.CurrentUICulture = [cultureinfo]::GetCultureInfo($Culture)
                $ProgressPreference = 'Continue'
                Import-Module -Name $Manifest
                Connect-Ado -CollectionUrl $Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
                @(Get-AdoTestCase -PlanId 812 -SuiteId 814 -Recurse).Count
            }).AddArgument($env:ADOTOOLKIT_MODULE_MANIFEST).AddArgument($server.Uri).AddArgument('fr-CA')
            $shell.Invoke()[0] | Should -Be 4
            $shell.HadErrors | Should -BeFalse
            $records = @($shell.Streams.Progress)
            $fr = [cultureinfo]::GetCultureInfo('fr-CA')
            $message = { param($Key, [object[]] $Arguments) [AdoToolkit.Core.Resources.Messages]::Get($Key, $fr, $Arguments) }
            $records.Count | Should -BeLessThan 20
            $records | ForEach-Object { $_.ActivityId | Should -Be 1; $_.Activity | Should -Be (& $message 'ProgressActivity' @()) }
            $status = @($records.StatusDescription)
            $status | Should -Contain (& $message 'ProgressEnumeration' @(3, 3))
            $status | Should -Contain (& $message 'ProgressFetch' @(1))
            $status | Should -Contain (& $message 'ProgressExpansion' @(3, 3))
            $records[-1].RecordType | Should -Be 'Completed'
            $records[-1].StatusDescription | Should -Be (& $message 'ProgressCompleted' @(4))
            ($records | Where-Object StatusDescription -EQ (& $message 'ProgressExpansion' @(3, 3))).PercentComplete | Should -Be 100
        }
        finally { $shell.Dispose(); Stop-FakeAdoServer -Server $server }
    }
}

Describe 'Bulk export' -Tag 'S3-5' {
    AfterEach { Disconnect-Ado }

    It 'exports many piped cases as one FileInfo named by <Name>' -TestCases @(
        @{ Name = 'suite'; Recurse = $false; Format = 'Html'; Pattern = '^TestSuite-814-Steps\.html$'; Count = 2 },
        @{ Name = 'timestamp'; Recurse = $true; Format = 'Markdown'; Pattern = '^TestCases-\d{8}-\d{6}\.md$'; Count = 4 }
    ) {
        param($Name, $Recurse, $Format, $Pattern, $Count)
        $responses = if ($Recurse) {
            @(@{ Body = $plansBody }, @{ Body = $suitesBody }, @{ Body = New-MembershipBody -Ids @(1201, 1203) },
                @{ Body = New-MembershipBody -Ids @(1201, 1202) }, @{ Body = New-MembershipBody }, @{ Body = New-BatchBody -Ids @(1201, 1203, 1202) })
        }
        else { @(@{ Body = $plansBody }, @{ Body = $suitesBody }, @{ Body = New-MembershipBody -Ids @(1201, 1203) }, @{ Body = New-BatchBody -Ids @(1201, 1203) }) }
        $server = Start-FakeAdoServer -Responses $responses
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $directory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
            [void] [IO.Directory]::CreateDirectory($directory)
            $files = @(Get-AdoTestCase -PlanId 812 -SuiteId 814 -Recurse:$Recurse | Export-AdoTestCase -Path $directory -Format $Format -Culture fr-CA)
            $files.Count | Should -Be 1
            $files[0] | Should -BeOfType ([IO.FileInfo])
            $files[0].Name | Should -Match $Pattern
            @(Get-ChildItem -LiteralPath $directory -Force).Count | Should -Be 1
            $text = [IO.File]::ReadAllText($files[0].FullName)
            if ($Format -eq 'Html') {
                $text | Should -Match ('data-case-count="' + $Count + '"')
                ([regex]::Matches($text, '<article class="test-case" id="tc-')).Count | Should -Be $Count
            }
            else {
                ([regex]::Matches($text, '(?m)^<a id="tc-[0-9-]+"></a>$')).Count | Should -Be $Count
                $text | Should -Match '\(#tc-1201-2\)'
            }
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'warns and writes nothing when no test case arrives' {
        $server = Start-FakeAdoServer
        try {
            Connect-Ado -CollectionUrl $server.Uri -WarningAction SilentlyContinue | Out-Null
            $directory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
            [void] [IO.Directory]::CreateDirectory($directory)
            $warnings = @()
            @(@() | Export-AdoTestCase -Path $directory -WarningVariable warnings -WarningAction SilentlyContinue).Count | Should -Be 0
            $warnings.Count | Should -Be 1
            @(Get-ChildItem -LiteralPath $directory -Force).Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}
