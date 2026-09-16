BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
    $fixtureRoot = Join-Path $PSScriptRoot '../Fixtures/Rest'
}

Describe 'Work item command' -Tag 'S1-1' {
    AfterEach { Disconnect-Ado }

    It 'buffers <Binding> input, deduplicates, orders output and writes one error per omitted ID (<Fixture>)' -TestCases @(
        @{ Binding = 'value'; Fixture = 'workitems-null.json' },
        @{ Binding = 'property'; Fixture = 'workitems-compact.json' },
        @{ Binding = 'explicit'; Fixture = 'workitems-compact.json' }
    ) {
        param($Binding, $Fixture)
        $server = Start-FakeAdoServer -Responses @(@{ Body = Get-Content -LiteralPath (Join-Path $fixtureRoot $Fixture) -Raw })
        try {
            Connect-Ado -CollectionUrl $server.Uri -WarningAction SilentlyContinue | Out-Null
            $missing = @()
            $items = switch ($Binding) {
                'value' { 3, 2, 1, 4, 2, 3 | Get-AdoWorkItem -ErrorAction SilentlyContinue -ErrorVariable missing }
                'property' { 3, 2, 1, 4, 2, 3 | ForEach-Object { [pscustomobject]@{ Id = $_ } } | Get-AdoWorkItem -ErrorAction SilentlyContinue -ErrorVariable missing }
                'explicit' { Get-AdoWorkItem -Id 3, 2, 1, 4, 2, 3 -ErrorAction SilentlyContinue -ErrorVariable missing }
            }
            @($items).Id | Should -Be @(3, 1)
            $missing.Count | Should -Be 2
            $missing.TargetObject | Should -Be @(2, 4)
            foreach ($record in $missing) {
                $record.CategoryInfo.Category | Should -Be 'ObjectNotFound'
                $record.FullyQualifiedErrorId | Should -Be 'AdoNotFound,AdoToolkit.GetAdoWorkItemCommand'
                $record.Exception | Should -BeOfType ([AdoToolkit.Core.Diagnostics.AdoNotFoundException])
            }
            $server.Requests.Count | Should -Be 1
            $server.Requests.ToArray()[0].Line | Should -Be 'POST /Collection/_apis/wit/workitemsbatch?api-version=6.0 HTTP/1.1'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'rejects Field with IncludeRelations and invalid IDs before a request' {
        $server = Start-FakeAdoServer
        try {
            Connect-Ado -CollectionUrl $server.Uri -WarningAction SilentlyContinue | Out-Null
            { Get-AdoWorkItem -Id 1 -Field System.Title -IncludeRelations -ErrorAction Stop } | Should -Throw -ErrorId 'AmbiguousParameterSet,AdoToolkit.GetAdoWorkItemCommand'
            { Get-AdoWorkItem -Id 0 -ErrorAction Stop } | Should -Throw
            { Get-AdoWorkItem -Id -1 -ErrorAction Stop } | Should -Throw
            $server.Requests.Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'rejects a typed object from another collection without sending any request' {
        $source = Start-FakeAdoServer -Responses @(@{ Body = Get-Content -LiteralPath (Join-Path $fixtureRoot 'workitem-fields.json') -Raw })
        $destination = Start-FakeAdoServer
        try {
            $sourceConnection = Connect-Ado -CollectionUrl $source.Uri -WarningAction SilentlyContinue
            $item = Get-AdoWorkItem -Id 1 -Connection $sourceConnection
            $item | Should -BeOfType ([AdoToolkit.Core.WorkItems.AdoWorkItem])
            $destinationConnection = Connect-Ado -CollectionUrl $destination.Uri -WarningAction SilentlyContinue
            $mismatches = @()
            $output = @($item | Get-AdoWorkItem -Connection $destinationConnection -ErrorAction SilentlyContinue -ErrorVariable mismatches)
            $output.Count | Should -Be 0
            $mismatches.Count | Should -Be 1
            $mismatches[0].FullyQualifiedErrorId | Should -Be 'AdoConnectionMismatch,AdoToolkit.GetAdoWorkItemCommand'
            $mismatches[0].CategoryInfo.Category | Should -Be 'InvalidArgument'
            $destination.Requests.Count | Should -Be 0
        }
        finally { Stop-FakeAdoServer -Server $source; Stop-FakeAdoServer -Server $destination }
    }

    It 'accepts same-collection typed objects and does not carry provenance into later raw IDs' {
        $body = Get-Content -LiteralPath (Join-Path $fixtureRoot 'workitem-fields.json') -Raw
        $server = Start-FakeAdoServer -Responses @(@{ Body = $body }, @{ Body = $body })
        try {
            $connection = Connect-Ado -CollectionUrl $server.Uri -WarningAction SilentlyContinue
            $item = Get-AdoWorkItem -Id 1 -IncludeRelations
            @($item.Relations).Count | Should -Be 1
            $bad = [pscustomobject]@{ Id = 2; CollectionUri = [uri]'https://other.example.test/Collection' }
            $mismatches = @()
            $output = @(@($bad, 1, $item) | Get-AdoWorkItem -Connection $connection -ErrorAction SilentlyContinue -ErrorVariable mismatches)
            $output.Count | Should -Be 1
            $output[0].Id | Should -Be 1
            $mismatches.Count | Should -Be 1
            $server.Requests.Count | Should -Be 2
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}
