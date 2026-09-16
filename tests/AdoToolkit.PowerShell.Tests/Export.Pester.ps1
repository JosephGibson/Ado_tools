BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
    $fixtureRoot = Join-Path $PSScriptRoot '../Fixtures/Rest'
    $previousConfig = $env:ADOTOOLKIT_CONFIG_PATH
    $env:ADOTOOLKIT_CONFIG_PATH = Join-Path $TestDrive 'config.json'
    $batch = Get-Content -LiteralPath (Join-Path $fixtureRoot 'testcase-mixed.json') -Raw | ConvertFrom-Json
    $singleCaseBody = @{ value = @($batch.value | Where-Object id -EQ 3) } | ConvertTo-Json -Depth 8 -Compress
}
AfterAll { $env:ADOTOOLKIT_CONFIG_PATH = $previousConfig }

Describe 'Single case report export' -Tag 'S2-4' {
    BeforeEach {
        $requestCount = $null
        $server = Start-FakeAdoServer -Responses @(
            @{ Body = $singleCaseBody },
            @{ Body = Get-Content -LiteralPath (Join-Path $fixtureRoot 'test-category.json') -Raw },
            @{ Body = Get-Content -LiteralPath (Join-Path $fixtureRoot 'shared-category.json') -Raw }
        )
        $connection = Connect-Ado -CollectionUrl $server.Uri -WarningAction SilentlyContinue
        $item = Get-AdoTestCase -Id 3
        $requestCount = $server.Requests.Count
        $outputDirectory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        [void][IO.Directory]::CreateDirectory($outputDirectory)
        $destination = Join-Path $outputDirectory 'report.html'
    }
    AfterEach {
        try { if ($null -ne $requestCount) { $server.Requests.Count | Should -Be $requestCount } }
        finally {
            Disconnect-Ado
            Stop-FakeAdoServer -Server $server
        }
    }

    It 'WhatIf names the target and writes nothing' {
        @($item | Export-AdoTestCase -Path $destination -WhatIf).Count | Should -Be 0
        Test-Path -LiteralPath $destination | Should -BeFalse
        @(Get-ChildItem -LiteralPath $outputDirectory -Force -Filter '.*.tmp').Count | Should -Be 0
    }

    It 'returns FileInfo and resolves a directory to the default name' {
        $file = $item | Export-AdoTestCase -Path $outputDirectory
        $file | Should -BeOfType ([IO.FileInfo])
        $file.Name | Should -Be 'TestCase-3-Steps.html'
        $file.DirectoryName | Should -Be $outputDirectory
        [IO.File]::ReadAllText($file.FullName) | Should -Match 'data-case-count="1"'
    }

    It 'NoClobber preserves existing bytes and leaves no temporary file' {
        [IO.File]::WriteAllText($destination, 'original')
        { $item | Export-AdoTestCase -Path $destination -NoClobber -ErrorAction Stop } | Should -Throw
        [IO.File]::ReadAllText($destination) | Should -Be 'original'
        @(Get-ChildItem -LiteralPath $outputDirectory -Force -Filter '.*.tmp').Count | Should -Be 0
    }

    It 'renders French labels and falls back to English with one warning' {
        $file = $item | Export-AdoTestCase -Path $destination -Culture fr-CA
        $text = [IO.File]::ReadAllText($file.FullName)
        $text | Should -Match '<html lang="fr-CA"'
        $text | Should -Match 'Résultat attendu'
        $warnings = @()
        $file = $item | Export-AdoTestCase -Path $destination -Culture de-DE -WarningVariable warnings -WarningAction SilentlyContinue
        $warnings.Count | Should -Be 1
        [IO.File]::ReadAllText($file.FullName) | Should -Match '<html lang="en"'
    }

    It 'exports Markdown and JSON with optional sources' {
        $file = $item | Export-AdoTestCase -Path (Join-Path $TestDrive 'case.md') -Format Markdown
        [IO.File]::ReadAllText($file.FullName) | Should -Match '^# Azure DevOps Test Case'
        $file = $item | Export-AdoTestCase -Path (Join-Path $TestDrive 'case.json') -Format Json -IncludeSource
        $json = [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json
        $json.schemaVersion | Should -Be 1
        $json.cases[0].rows[0].PSObject.Properties.Name | Should -Contain 'actionSource'
    }

    It 'rejects source options, providers and missing parents before creating a file' {
        $failure = { $item | Export-AdoTestCase -Path $destination -IncludeSource } | Should -Throw -PassThru
        $failure.CategoryInfo.Category | Should -Be 'InvalidArgument'
        { $item | Export-AdoTestCase -Path 'Env:ADOTOOLKIT_REPORT_TEST' } | Should -Throw
        { $item | Export-AdoTestCase -Path (Join-Path $TestDrive 'missing/report.html') } | Should -Throw
        Test-Path -LiteralPath $destination | Should -BeFalse
    }

    It 'writes the same case twice into one document with unique anchors' -Tag 'S3-5' {
        $file = $item, $item | Export-AdoTestCase -Path $destination
        @($file).Count | Should -Be 1
        $text = [IO.File]::ReadAllText($file.FullName)
        $text | Should -Match 'data-case-count="2"'
        $text | Should -Match '<article class="test-case" id="tc-3">'
        $text | Should -Match '<article class="test-case" id="tc-3-2">'
    }

    It 'checks provenance and treats wildcard characters in paths literally' {
        $foreign = [AdoToolkit.Core.Connections.AdoConnection]@{ CollectionUri = [uri]'https://foreign.example.test/Collection' }
        { $item | Export-AdoTestCase -Connection $foreign -Path $destination } | Should -Throw
        $literal = Join-Path $TestDrive 'rapport-[été].html'
        $file = $item | Export-AdoTestCase -Connection $connection -Path $literal
        $file.FullName | Should -Be $literal
        Test-Path -LiteralPath $literal | Should -BeTrue
    }
}
