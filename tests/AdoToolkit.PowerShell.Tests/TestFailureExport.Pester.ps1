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
    function Get-AttachmentBytes {
        param([Parameter(Mandatory = $true)][string] $Name)
        # The comma keeps the byte array whole instead of unrolling it into the pipeline.
        , [IO.File]::ReadAllBytes((Join-Path $PSScriptRoot "../Fixtures/Attachments/$Name"))
    }
    $script:EmptyPage = '{"count":0,"value":[]}'
    # One two-run retrieval without history; result 201-1 lists attachments 5001–5005.
    function Get-TwoRunResponses {
        param([switch] $LatestAttachments)
        @(
            @{ Body = Get-TestRunFixture 'runs-two.json' },
            @{ Body = $script:EmptyPage },
            @{ Body = Get-TestRunFixture 'results-run-201.json' },
            @{ Body = $script:EmptyPage },
            @{ Body = Get-TestRunFixture 'results-run-202.json' },
            @{ Body = $script:EmptyPage },
            @{ Body = Get-TestRunFixture 'result-detail-202-11.json' },
            @{ Body = Get-TestRunFixture 'result-detail-201-1.json' },
            @{ Body = Get-TestRunFixture $(if ($LatestAttachments) { 'attachments-result.json' } else { 'attachments-empty.json' }) },
            @{ Body = Get-TestRunFixture 'attachments-result.json' },
            @{ Body = Get-TestRunFixture 'workitems-testcases.json' }
        )
    }
    # Attachment content in report order: 5001 PNG, 5002 JSON, 5003 HTML, 5004 and 5005 other bytes.
    function Get-ContentResponses {
        @(
            @{ Bytes = Get-AttachmentBytes 'pattern.png'; ContentType = 'application/octet-stream' },
            @{ Bytes = Get-AttachmentBytes 'valid.json'; ContentType = 'application/octet-stream' },
            @{ Bytes = Get-AttachmentBytes 'test-output.html'; ContentType = 'application/octet-stream' },
            @{ Bytes = Get-AttachmentBytes 'other-bytes.txt'; ContentType = 'application/octet-stream' },
            @{ Bytes = Get-AttachmentBytes 'other-bytes.txt'; ContentType = 'application/octet-stream' }
        )
    }
    function Get-OutputEntry {
        param([Parameter(Mandatory = $true)][string] $Path)
        @(Get-ChildItem -LiteralPath $Path -Force -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($Path, $_.FullName).Replace('\', '/') } | Sort-Object)
    }
}

AfterAll { $env:ADOTOOLKIT_CONFIG_PATH = $previousConfig }

Describe 'Failed-test report export' {
    BeforeEach {
        $outputDirectory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        [void][IO.Directory]::CreateDirectory($outputDirectory)
    }
    AfterEach { Disconnect-Ado }

    It 'WhatIf writes nothing and requests no attachment content' -Tag 'S5-7' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses -LatestAttachments) + (Get-ContentResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $server.Requests.Count | Should -Be 12
            @($set | Export-AdoBuildTestFailure -Path $outputDirectory -WhatIf).Count | Should -Be 0
            @($set | Export-AdoBuildTestFailure -Path (Join-Path $outputDirectory 'one.html') -WhatIf).Count | Should -Be 0
            @($set | Export-AdoBuildTestFailure -Path (Join-Path $outputDirectory 'missing') -AllRunAttachments -WhatIf).Count | Should -Be 0
            $server.Requests.Count | Should -Be 12
            Get-OutputEntry -Path $outputDirectory | Should -BeNullOrEmpty
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'NoClobber refuses an existing report before any download' -Tag 'S5-7' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses) + (Get-ContentResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $existing = Join-Path $outputDirectory 'Build-401-TestFailures.html'
            [IO.File]::WriteAllText($existing, 'original')
            $failure = { $set | Export-AdoBuildTestFailure -Path $outputDirectory -NoClobber -ErrorAction Stop } | Should -Throw -PassThru
            $failure.FullyQualifiedErrorId | Should -Match '^AdoFileOutput'
            [IO.File]::ReadAllText($existing) | Should -Be 'original'
            $server.Requests.Count | Should -Be 12
            Get-OutputEntry -Path $outputDirectory | Should -Be @('Build-401-TestFailures.html')
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'downloads attachments beside the report and returns FileInfo with AttachmentDirectory' -Tag 'S5-6', 'S5-7' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses) + (Get-ContentResponses) + (Get-ContentResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $warnings = @()
            $file = $set | Export-AdoBuildTestFailure -Path $outputDirectory -Culture fr-CA -WarningVariable warnings -AllRunAttachments
            $file | Should -BeOfType ([IO.FileInfo])
            $file.FullName | Should -Be (Join-Path $outputDirectory 'Build-401-TestFailures.html')
            @($warnings).Count | Should -Be 0
            $folder = $file.AttachmentDirectory
            $folder | Should -Match '\\Build-401-TestFailures\.files-\d{8}T\d{9}Z$'
            $folderName = Split-Path -Leaf $folder
            $entries = Get-OutputEntry -Path $outputDirectory
            $entries | Should -Be (@('Build-401-TestFailures.html', $folderName,
                "$folderName/r201-1-a5001.png", "$folderName/r201-1-a5002.json", "$folderName/r201-1-a5003.html",
                "$folderName/r201-1-a5004.bin", "$folderName/r201-1-a5005.bin") | Sort-Object)
            [IO.File]::ReadAllBytes((Join-Path $folder 'r201-1-a5001.png')) | Should -Be (Get-AttachmentBytes 'pattern.png')
            $requests = $server.Requests.ToArray()
            $requests.Count | Should -Be 17
            $requests[12].Line | Should -Be 'GET /Collection/%C3%89quipe%20Web/_apis/test/Runs/201/Results/1/attachments/5001?api-version=6.0-preview.1 HTTP/1.1'
            $requests[12].Headers['Accept'] | Should -Be 'application/octet-stream'
            $requests[16].Line | Should -Match '/attachments/5005\?'
            $html = [IO.File]::ReadAllText($file.FullName)
            $html | Should -Match '<html lang="fr-CA"'
            $html | Should -Match ([regex]::Escape('<img data-local-file src="' + $folderName + '/r201-1-a5001.png" loading="lazy"'))
            $html | Should -Match 'Copie locale'
            $html | Should -Match 'lang-json'

            # A second export replaces the report and removes the previous generation folder.
            $second = $set | Export-AdoBuildTestFailure -Path (Join-Path $outputDirectory 'Build-401-TestFailures.html') -AllRunAttachments
            $second.AttachmentDirectory | Should -Not -Be $folder
            Test-Path -LiteralPath $folder | Should -BeFalse
            Test-Path -LiteralPath $second.AttachmentDirectory | Should -BeTrue
            @(Get-ChildItem -LiteralPath $outputDirectory -Force).Name | Should -Be @((Split-Path -Leaf $second.AttachmentDirectory), 'Build-401-TestFailures.html')
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'SkipAttachments downloads nothing and creates no folder' -Tag 'S5-6' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            Disconnect-Ado
            # No connection is needed when nothing is downloaded.
            $file = $set | Export-AdoBuildTestFailure -Path $outputDirectory -SkipAttachments -AllRunAttachments
            $file.PSObject.Properties['AttachmentDirectory'] | Should -BeNullOrEmpty
            Get-OutputEntry -Path $outputDirectory | Should -Be @('Build-401-TestFailures.html')
            $server.Requests.Count | Should -Be 12
            $html = [IO.File]::ReadAllText($file.FullName)
            $html | Should -Not -Match 'data-local-file'
            $html | Should -Match 'screenshot\.PNG <span role="img"'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'downloads the latest run by default and every run with AllRunAttachments=<AllRuns>' -ForEach @(
        @{ AllRuns = $false; Downloads = 5 },
        @{ AllRuns = $true; Downloads = 10 }
    ) {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) +
            (Get-TwoRunResponses -LatestAttachments) + (Get-ContentResponses) + (Get-ContentResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $file = $set | Export-AdoBuildTestFailure -Path $outputDirectory -Culture en-US -AllRunAttachments:$AllRuns
            $server.Requests.Count | Should -Be (12 + $Downloads)
            $contentRequests = @($server.Requests.ToArray() | Select-Object -Skip 12)
            @($contentRequests | Where-Object Line -Match '/Runs/202/').Count | Should -Be 5
            @($contentRequests | Where-Object Line -Match '/Runs/201/').Count | Should -Be ($Downloads - 5)
            @(Get-ChildItem -LiteralPath $file.AttachmentDirectory -File).Count | Should -Be $Downloads
            $html = [IO.File]::ReadAllText($file.FullName)
            $html | Should -Match 'runId=201&amp;resultId=1">screenshot\.PNG'
            $html | Should -Match 'runId=202&amp;resultId=11">screenshot\.PNG'
            $html | Should -Match '<span>2,048 bytes</span>'
            $html | Should -Match 'r202-11-a5001.png'
            $html.Contains('r201-1-a5001.png') | Should -Be $AllRuns
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'lists older attachments without a connection when the latest run has none' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            Disconnect-Ado
            $file = $set | Export-AdoBuildTestFailure -Path $outputDirectory
            $file.PSObject.Properties['AttachmentDirectory'] | Should -BeNullOrEmpty
            $server.Requests.Count | Should -Be 12
            Get-OutputEntry -Path $outputDirectory | Should -Be @('Build-401-TestFailures.html')
            [IO.File]::ReadAllText($file.FullName) | Should -Match 'screenshot\.PNG <span role="img"'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'writes one report per set; a file path accepts only the first set' {
        $responses = @(@{ Body = Get-TestRunFixture 'builds-history.json' })
        $responses += Get-TwoRunResponses
        $responses += Get-TwoRunResponses
        $server = Start-FakeAdoServer -Responses $responses
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $sets = @(Get-AdoBuild -Definition 42 -Top 2 | Get-AdoBuildTestFailure -HistoryCount 1 -WarningAction SilentlyContinue)
            $files = @($sets | Export-AdoBuildTestFailure -Path $outputDirectory -SkipAttachments)
            $files.Name | Should -Be @('Build-401-TestFailures.html', 'Build-400-TestFailures.html')
            $single = Join-Path $outputDirectory 'single.html'
            $errors = @()
            $written = @($sets | Export-AdoBuildTestFailure -Path $single -SkipAttachments -ErrorVariable errors -ErrorAction SilentlyContinue)
            $written.Count | Should -Be 1
            @($errors).Count | Should -Be 1
            $errors[0].FullyQualifiedErrorId | Should -Match '^TestFailureReportSingleFile,'
            $errors[0].CategoryInfo.Category | Should -Be 'InvalidArgument'
            [string] $errors[0] | Should -Match '400'
            [IO.File]::ReadAllText($single) | Should -Match 'buildId=401'
            $server.Requests.Count | Should -Be 23
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'rejects invalid paths and foreign collections before any download' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses))
        try {
            $connection = Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $failure = { $set | Export-AdoBuildTestFailure -Path (Join-Path $outputDirectory 'report.txt') } | Should -Throw -PassThru
            $failure.CategoryInfo.Category | Should -Be 'InvalidArgument'
            { $set | Export-AdoBuildTestFailure -Path 'Env:ADOTOOLKIT_REPORT_TEST' } | Should -Throw
            { $set | Export-AdoBuildTestFailure -Path (Join-Path $outputDirectory 'missing/report.html') -SkipAttachments -ErrorAction Stop } | Should -Throw
            $foreign = [AdoToolkit.Core.Connections.AdoConnection]@{ CollectionUri = [uri] 'https://foreign.example.test/Collection' }
            { $set | Export-AdoBuildTestFailure -Connection $foreign -Path $outputDirectory -AllRunAttachments -ErrorAction Stop } | Should -Throw
            $literal = Join-Path $outputDirectory 'rapport-[été].html'
            $file = $set | Export-AdoBuildTestFailure -Connection $connection -Path $literal -SkipAttachments
            $file.FullName | Should -Be $literal
            $server.Requests.Count | Should -Be 12
            Get-OutputEntry -Path $outputDirectory | Should -Be @('rapport-[été].html')
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}
