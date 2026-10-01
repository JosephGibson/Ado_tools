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

Describe 'Test case detail export' {
    BeforeAll {
        $detailBody = Get-Content -LiteralPath (Join-Path $fixtureRoot 'testcase-detail.json') -Raw
        $linkedBody = Get-Content -LiteralPath (Join-Path $fixtureRoot 'testcase-detail-linked.json') -Raw
        $pointsBody = Get-Content -LiteralPath (Join-Path $fixtureRoot 'testpoints.json') -Raw
        function Start-DetailServer {
            param([object[]] $Detail)
            $script:server = Start-FakeAdoServer -Responses (@(@{ Body = $singleCaseBody }) + $Detail)
            $script:connection = Connect-Ado -CollectionUrl $script:server.Uri -WarningAction SilentlyContinue
            $script:item = Get-AdoTestCase -Id 3
            $script:destination = Join-Path $TestDrive ([guid]::NewGuid().ToString('N') + '.html')
        }
    }
    AfterEach {
        Disconnect-Ado
        Stop-FakeAdoServer -Server $script:server
    }

    It 'reads the fields, links and test points in three requests and shows them in the HTML report' {
        Start-DetailServer -Detail @(@{ Body = $detailBody }, @{ Body = $linkedBody }, @{ Body = $pointsBody })
        $warnings = @()
        $file = $script:item | Export-AdoTestCase -Path $script:destination -IncludeDetail -WarningVariable warnings -WarningAction SilentlyContinue
        $file | Should -BeOfType ([IO.FileInfo])
        $requests = @($script:server.Requests.ToArray())
        $requests.Count | Should -Be 4
        $requests[1].Line | Should -Be 'POST /Collection/_apis/wit/workitemsbatch?api-version=6.0 HTTP/1.1'
        $requests[1].Body | Should -Be '{"ids":[3],"errorPolicy":"omit","$expand":"relations"}'
        $requests[2].Line | Should -Be 'POST /Collection/_apis/wit/workitemsbatch?api-version=6.0 HTTP/1.1'
        @(($requests[2].Body | ConvertFrom-Json).ids) | Should -Be @(3001, 3050, 3099)
        $requests[3].Line | Should -Be 'POST /Collection/%C3%89quipe%20%2F%20Web/_apis/test/points?%24top=1000&%24skip=0&api-version=6.0-preview.2 HTTP/1.1'
        $requests[3].Body | Should -Be '{"pointsFilter":{"testcaseIds":[3]}}'
        # The linked work item that the batch omits is the one warning; the report is complete.
        $warnings.Count | Should -Be 1
        [string] $warnings[0] | Should -Match '3099'
        $text = [IO.File]::ReadAllText($file.FullName)
        $text | Should -Match 'id="report-1-description"'
        $text | Should -Match '<code data-copy-value>Synthetic\.Orders\.OrderTests\.Creates</code>'
        $text | Should -Match '<tr data-link="3050"><td>Tests</td>'
        $text | Should -Match 'data-point="2" data-outcome="failed"'
        $text | Should -Match 'data-diagnostic="UnresolvedLinkedWorkItem"'
        $text | Should -Not -Match 'untrusted\.example\.test'
        $text | Should -Not -Match 'href="javascript:'
    }

    It 'sends no request for -WhatIf and none without the switch' {
        Start-DetailServer -Detail @()
        @($script:item | Export-AdoTestCase -Path $script:destination -IncludeDetail -WhatIf).Count | Should -Be 0
        $script:item | Export-AdoTestCase -Path $script:destination | Should -BeOfType ([IO.FileInfo])
        $script:server.Requests.Count | Should -Be 1
        [IO.File]::ReadAllText($script:destination) | Should -Not -Match 'id="report-1-points"'
    }

    It 'requires the HTML format before any request' {
        Start-DetailServer -Detail @()
        foreach ($format in 'Markdown', 'Json') {
            $failure = { $script:item | Export-AdoTestCase -Path $script:destination -Format $format -IncludeDetail } | Should -Throw -PassThru
            $failure.FullyQualifiedErrorId | Should -Match '^IncludeDetailHtmlOnly'
            $failure.CategoryInfo.Category | Should -Be 'InvalidArgument'
        }
        $script:server.Requests.Count | Should -Be 1
        Test-Path -LiteralPath $script:destination | Should -BeFalse
    }

    It 'turns a failed lookup into a warning and still writes the report' {
        # The detail batch is refused, and the points answer has another shape.
        Start-DetailServer -Detail @(@{ Status = 400; Body = '{"message":"synthetic"}' }, @{ Body = '{"value":[]}' })
        $warnings = @()
        $file = $script:item | Export-AdoTestCase -Path $script:destination -IncludeDetail -Culture fr-CA -WarningVariable warnings -WarningAction SilentlyContinue
        $warnings.Count | Should -Be 2
        $text = [IO.File]::ReadAllText($file.FullName)
        $text | Should -Match 'data-diagnostic="TestCaseDetailUnavailable"'
        $text | Should -Match 'data-diagnostic="TestPointsUnavailable"'
        $text | Should -Match 'data-status="complete"'
        $text | Should -Not -Match 'id="report-1-points"'
        # The report states the problem in its own culture.
        $text | Should -Match 'Avertissement · TestPointsUnavailable'
    }

    It 'stops on an authorization failure and writes no file' {
        Start-DetailServer -Detail @(@{ Status = 403; Body = '{"message":"synthetic"}' })
        { $script:item | Export-AdoTestCase -Path $script:destination -IncludeDetail -ErrorAction Stop } | Should -Throw
        Test-Path -LiteralPath $script:destination | Should -BeFalse
        @(Get-ChildItem -LiteralPath $TestDrive -Force -Filter '.*.tmp').Count | Should -Be 0
    }
}
