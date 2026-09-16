BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
    $fixtureRoot = Join-Path $PSScriptRoot '../Fixtures/Rest'
}

Describe 'Test case command' -Tag 'S1-2' {
    AfterEach { Disconnect-Ado }

    It 'binds <Binding>, preserves order, reports per-input errors and one partial warning' -TestCases @(
        @{ Binding = 'value' }, @{ Binding = 'property' }, @{ Binding = 'explicit' }
    ) {
        param($Binding)
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-Content -LiteralPath (Join-Path $fixtureRoot 'testcase-mixed.json') -Raw },
            @{ Body = Get-Content -LiteralPath (Join-Path $fixtureRoot 'test-category.json') -Raw },
            @{ Body = Get-Content -LiteralPath (Join-Path $fixtureRoot 'shared-category.json') -Raw },
            @{ Body = '{"value":[]}' }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -WarningAction SilentlyContinue | Out-Null
            $failures = @()
            $warnings = @()
            $items = @(switch ($Binding) {
                'value' { 3, 1, 2, 4, 1 | Get-AdoTestCase -ErrorAction SilentlyContinue -ErrorVariable failures -WarningAction SilentlyContinue -WarningVariable warnings }
                'property' { 3, 1, 2, 4, 1 | ForEach-Object { [pscustomobject]@{ Id = $_ } } | Get-AdoTestCase -ErrorAction SilentlyContinue -ErrorVariable failures -WarningAction SilentlyContinue -WarningVariable warnings }
                'explicit' { Get-AdoTestCase -Id 3, 1, 2, 4, 1 -ErrorAction SilentlyContinue -ErrorVariable failures -WarningAction SilentlyContinue -WarningVariable warnings }
            })
            $items.Id | Should -Be @(3, 1)
            $items[1].Status | Should -Be 'Partial'
            $warnings.Count | Should -Be 1
            $warnings[0].Message | Should -Match '1'
            $failures.Count | Should -Be 2
            $failures[0].FullyQualifiedErrorId | Should -Be 'NotAStepContainer,AdoToolkit.GetAdoTestCaseCommand'
            $failures[0].TargetObject | Should -Be 2
            $failures[1].CategoryInfo.Category | Should -Be 'ObjectNotFound'
            $failures[1].TargetObject | Should -Be 4
            $server.Requests.Count | Should -Be 4
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'Strict emits a diagnostic-bearing error for each partial case and keeps complete objects' {
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = Get-Content -LiteralPath (Join-Path $fixtureRoot 'testcase-mixed.json') -Raw },
            @{ Body = Get-Content -LiteralPath (Join-Path $fixtureRoot 'test-category.json') -Raw },
            @{ Body = Get-Content -LiteralPath (Join-Path $fixtureRoot 'shared-category.json') -Raw },
            @{ Body = '{"value":[]}' }
        )
        try {
            Connect-Ado -CollectionUrl $server.Uri -WarningAction SilentlyContinue | Out-Null
            $failures = @()
            $warnings = @()
            $items = @(Get-AdoTestCase -Id 1, 2, 3 -Strict -ErrorAction SilentlyContinue -ErrorVariable failures -WarningVariable warnings)
            $items.Count | Should -Be 1
            $items[0].Id | Should -Be 3
            $warnings.Count | Should -Be 0
            $failures.Count | Should -Be 2
            $failures[0].FullyQualifiedErrorId | Should -Be 'PartialTestCase,AdoToolkit.GetAdoTestCaseCommand'
            $failures[0].TargetObject.Id | Should -Be 1
            $failures[0].Exception.Data['Diagnostics'][0].Code | Should -Be 'UnresolvedSharedStep'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'validates inputs and collection provenance before any request' {
        $server = Start-FakeAdoServer
        try {
            $connection = Connect-Ado -CollectionUrl $server.Uri -WarningAction SilentlyContinue
            { Get-AdoTestCase -Id 0 } | Should -Throw
            { Get-AdoTestCase -Id 1 -MaximumSharedStepDepth 0 } | Should -Throw
            { Get-AdoTestCase -Id 1 -MaximumExpandedSteps 0 } | Should -Throw
            { Get-AdoTestCase -Id 1 -Project 'Sample' } | Should -Throw
            $foreign = [AdoToolkit.Core.TestManagement.AdoTestCase]@{ Id = 1; CollectionUri = [uri]'https://other.example.test/Collection' }
            $failures = @()
            @($foreign | Get-AdoTestCase -Connection $connection -ErrorAction SilentlyContinue -ErrorVariable failures).Count | Should -Be 0
            $failures[0].FullyQualifiedErrorId | Should -Be 'AdoConnectionMismatch,AdoToolkit.GetAdoTestCaseCommand'
            $server.Requests.Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'uses French messages under fr-CA while retaining diagnostic codes' {
        $previous = [System.Threading.Thread]::CurrentThread.CurrentUICulture
        $body = @{ value = @(@{ id = 1; rev = 1; fields = @{
            'System.Title' = 'Exemple'; 'System.WorkItemType' = 'Cas'; 'System.TeamProject' = 'Équipe'
            'System.State' = 'Prêt'; 'System.ChangedDate' = '2026-09-15T12:00:00Z'; 'Microsoft.VSTS.TCM.Steps' = '<broken'
        } }) } | ConvertTo-Json -Depth 6 -Compress
        $server = Start-FakeAdoServer -Responses @(@{ Body = $body })
        try {
            [System.Threading.Thread]::CurrentThread.CurrentUICulture = [cultureinfo]'fr-CA'
            Connect-Ado -CollectionUrl $server.Uri -WarningAction SilentlyContinue | Out-Null
            $warnings = @()
            $item = Get-AdoTestCase -Id 1 -WarningVariable warnings -WarningAction SilentlyContinue
            $warnings.Count | Should -Be 1
            $warnings[0].Message | Should -Be "Le cas de test 1 est partiel : 1 erreur(s), 0 avertissement(s), 0 diagnostic(s) d’information."
            $item.Diagnostics[0].Code | Should -Be 'MalformedStepsXml'
            $item.Diagnostics[0].Message | Should -Match 'XML'
        }
        finally {
            [System.Threading.Thread]::CurrentThread.CurrentUICulture = $previous
            Stop-FakeAdoServer -Server $server
        }
    }
}
