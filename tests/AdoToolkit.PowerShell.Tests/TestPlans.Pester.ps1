BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
    $fixtureRoot = Join-Path $PSScriptRoot '../Fixtures/Rest'
    function Get-Fixture {
        param([Parameter(Mandatory = $true)][string] $Name)
        Get-Content -LiteralPath (Join-Path $fixtureRoot $Name) -Raw -Encoding utf8
    }
    # Testplan shapes and versions are [V-04] assumptions; the WIQL cap shape is [V-06].
    function Get-PagedResponse {
        param([Parameter(Mandatory = $true)][string] $First, [Parameter(Mandatory = $true)][string] $Last)
        @(
            @{ Body = Get-Fixture $First; Headers = @{ 'x-ms-continuationtoken' = 'page 2+/=' } },
            @{ Body = Get-Fixture 'testplan-empty-page.json'; Headers = @{ 'x-ms-continuationtoken' = 'page3' } },
            @{ Body = Get-Fixture $Last }
        )
    }
}

Describe 'Test plan and suite commands' -Tag 'S3-1' {
    AfterEach { Disconnect-Ado }

    It 'pages plans through short and empty token pages in order' {
        $server = Start-FakeAdoServer -Responses (Get-PagedResponse 'testplans-first.json' 'testplans-last.json')
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Autre projet' -WarningAction SilentlyContinue | Out-Null
            $plans = @(Get-AdoTestPlan -Project 'Équipe Web')
            $plans.Id | Should -Be @(812, 820, 830)
            $plans[0] | Should -BeOfType ([AdoToolkit.Core.TestManagement.AdoTestPlan])
            $plans[0].TeamProject | Should -Be 'Équipe Web'
            $plans[0].WebUrl.AbsoluteUri | Should -Be ($server.Uri + '/%C3%89quipe%20Web/_testPlans/define?planId=812')
            $requests = $server.Requests.ToArray()
            $requests.Count | Should -Be 3
            $requests[0].Line | Should -Be 'GET /Collection/%C3%89quipe%20Web/_apis/testplan/plans?api-version=6.0-preview.1 HTTP/1.1'
            $requests[1].Line | Should -Match 'continuationToken=page%202%2B%2F%3D&api-version=6\.0-preview\.1 '
            $requests[2].Line | Should -Match 'continuationToken=page3&'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'matches names case-insensitively with significant accents and composed or decomposed input' {
        $decomposed = 'Ta' + [char] 0x0302 + 'ches'
        $listing = [System.Text.Json.Nodes.JsonNode]::Parse((Get-Fixture 'testplans-first.json'))
        $extra = [System.Text.Json.Nodes.JsonNode]::Parse('{"id":840,"name":' + (ConvertTo-Json -InputObject "$decomposed décomposées") + ',"rootSuite":{"id":841}}')
        $listing['value'].Add($extra)
        $body = $listing.ToJsonString()
        $cases = @(
            @{ Name = 'Tâches*'; Expected = @(812, 840) },
            @{ Name = "$decomposed*"; Expected = @(812, 840) },
            @{ Name = 'TÂCHES DE*'; Expected = @(812) },
            @{ Name = 'Tache*'; Expected = @(820) },
            @{ Name = 'Tach?s*'; Expected = @() }
        )
        $server = Start-FakeAdoServer -Responses @(@($cases) + @($null) | ForEach-Object { @{ Body = $body } })
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            foreach ($case in $cases) {
                $matched = @(Get-AdoTestPlan -Name $case.Name)
                @($matched | ForEach-Object Id) | Should -Be $case.Expected -Because $case.Name
            }
            (Get-AdoTestPlan -Name "$decomposed*" | Select-Object -Last 1).Name | Should -Be "$decomposed décomposées"
            $server.Requests.Count | Should -Be ($cases.Count + 1)
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'filters by Id over the listing and reports a missing plan' {
        $server = Start-FakeAdoServer -Responses @(@{ Body = Get-Fixture 'testplans-first.json' }, @{ Body = Get-Fixture 'testplans-first.json' })
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            (Get-AdoTestPlan -Id 820).Id | Should -Be 820
            $failures = @()
            $output = @(Get-AdoTestPlan -Id 999 -ErrorAction SilentlyContinue -ErrorVariable failures)
            $output.Count | Should -Be 0
            $failures.Count | Should -Be 1
            $failures[0].FullyQualifiedErrorId | Should -Be 'AdoNotFound,AdoToolkit.GetAdoTestPlanCommand'
            $failures[0].TargetObject | Should -Be 999
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'requires a project before any request' {
        $server = Start-FakeAdoServer
        try {
            Connect-Ado -CollectionUrl $server.Uri -WarningAction SilentlyContinue | Out-Null
            { Get-AdoTestPlan -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoTestPlanCommand'
            { Get-AdoTestSuite -PlanId 812 -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.GetAdoTestSuiteCommand'
            { Invoke-AdoWiql -Query 'SELECT [System.Id] FROM WorkItems' -ErrorAction Stop } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.InvokeAdoWiqlCommand'
            { Get-AdoTestSuite -PlanId 0 -Project 'P' -ErrorAction Stop } | Should -Throw
            $server.Requests.Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'returns a suite subtree depth-first across pages with suite paths' {
        $server = Start-FakeAdoServer -Responses (Get-PagedResponse 'testsuites-first.json' 'testsuites-last.json')
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $suites = @(Get-AdoTestSuite -PlanId 812 -SuiteId 814 -Recurse)
            $suites.Id | Should -Be @(814, 816, 818)
            $suites[1] | Should -BeOfType ([AdoToolkit.Core.TestManagement.AdoTestSuite])
            @($suites[1].SuitePath) | Should -Be @('Tâches de régression', 'Connexion', "Écran d’accueil")
            $suites[1].PlanId | Should -Be 812
            $suites[1].ParentSuiteId | Should -Be 814
            $requests = $server.Requests.ToArray()
            $requests.Count | Should -Be 3
            $requests[0].Line | Should -Be 'GET /Collection/%C3%89quipe%20Web/_apis/testplan/Plans/812/suites?api-version=6.0-preview.1 HTTP/1.1'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'binds only typed plans from the pipeline and uses their project' {
        $suitesBody = Get-Fixture 'testsuites-first.json'
        $server = Start-FakeAdoServer -Responses @(@{ Body = Get-Fixture 'testplans-first.json' }, @{ Body = $suitesBody }, @{ Body = $suitesBody })
        $other = Start-FakeAdoServer
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $plan = Get-AdoTestPlan -Id 812
            Connect-Ado -CollectionUrl $server.Uri -Project 'Autre projet' -WarningAction SilentlyContinue | Out-Null
            $roots = @($plan | Get-AdoTestSuite)
            $roots.Id | Should -Be @(813)
            @($plan | Get-AdoTestSuite -Recurse).Id | Should -Be @(813, 814, 815)
            $server.Requests.ToArray()[1].Line | Should -Match '^GET /Collection/%C3%89quipe%20Web/_apis/testplan/Plans/812/suites\?'
            $before = $server.Requests.Count
            { [pscustomobject]@{ Id = 812; PlanId = 812; TeamProject = 'Équipe Web' } | Get-AdoTestSuite -ErrorAction Stop } | Should -Throw -ErrorId 'InputObjectNotBound,AdoToolkit.GetAdoTestSuiteCommand'
            { 812 | Get-AdoTestSuite -ErrorAction Stop } | Should -Throw -ErrorId 'InputObjectNotBound,AdoToolkit.GetAdoTestSuiteCommand'
            $server.Requests.Count | Should -Be $before
            $otherConnection = Connect-Ado -CollectionUrl $other.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue
            $mismatches = @()
            $output = @($plan | Get-AdoTestSuite -Connection $otherConnection -ErrorAction SilentlyContinue -ErrorVariable mismatches)
            $output.Count | Should -Be 0
            $mismatches.Count | Should -Be 1
            $mismatches[0].FullyQualifiedErrorId | Should -Be 'AdoConnectionMismatch,AdoToolkit.GetAdoTestSuiteCommand'
            $other.Requests.Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $server; Stop-FakeAdoServer -Server $other }
    }

    It 'reports a missing suite as a non-terminating error' {
        $server = Start-FakeAdoServer -Responses @(@{ Body = Get-Fixture 'testsuites-first.json' })
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $failures = @()
            @(Get-AdoTestSuite -PlanId 812 -SuiteId 999 -ErrorAction SilentlyContinue -ErrorVariable failures).Count | Should -Be 0
            $failures[0].FullyQualifiedErrorId | Should -Be 'AdoNotFound,AdoToolkit.GetAdoTestSuiteCommand'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}

Describe 'WIQL command' -Tag 'S3-3' {
    BeforeAll {
        $query = "SELECT [System.Id] FROM WorkItems WHERE [System.Title] = 'marqueur-confidentiel-7Q'"
        function New-BatchBody {
            param([int[]] $Ids)
            $template = [System.Text.Json.Nodes.JsonNode]::Parse((Get-Fixture 'workitem-fields.json'))['value'][0]
            $values = [System.Text.Json.Nodes.JsonArray]::new()
            foreach ($id in $Ids) {
                $item = $template.DeepClone()
                $item['id'] = [System.Text.Json.Nodes.JsonValue]::Create([int] $id)
                $values.Add($item)
            }
            $root = [System.Text.Json.Nodes.JsonObject]::new()
            $root['value'] = $values
            $root.ToJsonString()
        }
    }
    AfterEach { Disconnect-Ado }

    It 'returns a flat result and hydrates in query order without logging the query' {
        $flat = Get-Fixture 'wiql-flat.json'
        $server = Start-FakeAdoServer -Responses @(@{ Body = $flat }, @{ Body = $flat }, @{ Body = New-BatchBody -Ids @(1202, 1203) })
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $records = @(Invoke-AdoWiql -Query $query -Top 2 -Verbose -Debug 4>&1 5>&1)
            $result = @($records | Where-Object { $_ -is [AdoToolkit.Core.WorkItems.AdoWiqlResult] })
            $result.Count | Should -Be 1
            @($result[0].Ids) | Should -Be @(1203, 1201)
            $result[0].LimitApplied | Should -Be 2
            $result[0].TeamProject | Should -Be 'Équipe Web'
            $messages = @($records | Where-Object { $_ -isnot [AdoToolkit.Core.WorkItems.AdoWiqlResult] } | ForEach-Object { [string] $_ })
            $messages.Count | Should -BeGreaterThan 0
            $messages | Where-Object { $_ -match 'marqueur|SELECT' } | Should -BeNullOrEmpty
            $failures = @()
            $items = @(Invoke-AdoWiql -Query $query -Hydrate -ErrorAction SilentlyContinue -ErrorVariable failures)
            $items.Id | Should -Be @(1203, 1202)
            $items[0] | Should -BeOfType ([AdoToolkit.Core.WorkItems.AdoWorkItem])
            $failures.Count | Should -Be 1
            $failures[0].FullyQualifiedErrorId | Should -Be 'AdoNotFound,AdoToolkit.InvokeAdoWiqlCommand'
            $failures[0].TargetObject | Should -Be 1201
            $requests = $server.Requests.ToArray()
            $requests.Count | Should -Be 3
            $requests[0].Line | Should -Be 'POST /Collection/%C3%89quipe%20Web/_apis/wit/wiql?%24top=2&api-version=6.0 HTTP/1.1'
            $requests[1].Line | Should -Be 'POST /Collection/%C3%89quipe%20Web/_apis/wit/wiql?api-version=6.0 HTTP/1.1'
            $requests[2].Line | Should -Be 'POST /Collection/_apis/wit/workitemsbatch?api-version=6.0 HTTP/1.1'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'writes NotSupported for link queries and a localized cap error with no output' {
        $previous = [System.Threading.Thread]::CurrentThread.CurrentUICulture
        $ids = [System.Text.Json.Nodes.JsonArray]::new()
        foreach ($id in 1..20000) {
            $entry = [System.Text.Json.Nodes.JsonObject]::new()
            $entry['id'] = [System.Text.Json.Nodes.JsonValue]::Create([int] $id)
            $ids.Add($entry)
        }
        $capped = [System.Text.Json.Nodes.JsonNode]::Parse((Get-Fixture 'wiql-flat.json'))
        $capped['workItems'] = $ids
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-Fixture 'wiql-tree.json' },
            @{ Body = $capped.ToJsonString() },
            @{ Status = 400; Body = Get-Fixture 'wiql-limit-error.json' }
        )
        try {
            [System.Threading.Thread]::CurrentThread.CurrentUICulture = [cultureinfo] 'fr-CA'
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            foreach ($expected in @('NotSupported', 'AdoRequest', 'AdoRequest')) {
                $failures = @()
                $output = @(Invoke-AdoWiql -Query $query -Hydrate -ErrorAction SilentlyContinue -ErrorVariable failures)
                $output.Count | Should -Be 0
                $failures.Count | Should -Be 1
                $failures[0].FullyQualifiedErrorId | Should -Be "$expected,AdoToolkit.InvokeAdoWiqlCommand"
                if ($expected -eq 'NotSupported') {
                    $failures[0].CategoryInfo.Category | Should -Be 'NotImplemented'
                    $failures[0].Exception.Message | Should -Match 'requêtes plates'
                }
                else { $failures[0].Exception.Message | Should -Match 'Restreignez la requête' }
            }
            $server.Requests.Count | Should -Be 3
        }
        finally {
            [System.Threading.Thread]::CurrentThread.CurrentUICulture = $previous
            Stop-FakeAdoServer -Server $server
        }
    }

    It 'validates Top locally' {
        $server = Start-FakeAdoServer
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            { Invoke-AdoWiql -Query $query -Top 0 -ErrorAction Stop } | Should -Throw -ErrorId 'ParameterArgumentValidationError,AdoToolkit.InvokeAdoWiqlCommand'
            { Invoke-AdoWiql -Query $query -Top 20001 -ErrorAction Stop } | Should -Throw -ErrorId 'ParameterArgumentValidationError,AdoToolkit.InvokeAdoWiqlCommand'
            { Invoke-AdoWiql -Query '' -ErrorAction Stop } | Should -Throw
            $server.Requests.Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}
