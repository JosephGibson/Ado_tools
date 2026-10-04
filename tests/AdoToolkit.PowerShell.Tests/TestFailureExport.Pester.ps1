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
    # The attachment window is measured from now, so the two runs start $AgeHours ago, ten minutes apart.
    function Get-RecentRunsFixture {
        param([int] $AgeHours = 2)
        $start = [DateTimeOffset]::UtcNow.AddHours(-$AgeHours)
        $format = 'yyyy-MM-ddTHH:mm:ssZ'
        (Get-TestRunFixture 'runs-two.json').
            Replace('2026-09-14T10:00:00Z', $start.ToString($format, [cultureinfo]::InvariantCulture)).
            Replace('2026-09-14T10:10:00Z', $start.AddMinutes(10).ToString($format, [cultureinfo]::InvariantCulture))
    }
    # One two-run retrieval without history, eleven requests; result 201-1 lists attachments 5001–5005
    # and bugs 2001–2002. Both runs list as many results as they report, so neither listing ends with
    # a request for an empty page. -Piped is a later build of the same invocation: its Test Cases and
    # the bug states come from the invocation cache, so those two requests are not sent.
    # -SkipAttachments leaves out the two attachment lists.
    function Get-TwoRunResponses {
        param([switch] $LatestAttachments, [int] $AgeHours = 2, [switch] $Piped, [switch] $SkipAttachments)
        @(
            @{ Body = Get-RecentRunsFixture -AgeHours $AgeHours }
            @{ Body = $script:EmptyPage }
            @{ Body = Get-TestRunFixture 'results-run-201.json' }
            @{ Body = Get-TestRunFixture 'results-run-202.json' }
            @{ Body = Get-TestRunFixture 'result-detail-202-11.json' }
            @{ Body = Get-TestRunFixture 'result-detail-201-1.json' }
            if (-not $SkipAttachments) {
                @{ Body = Get-TestRunFixture $(if ($LatestAttachments) { 'attachments-result.json' } else { 'attachments-empty.json' }) }
                @{ Body = Get-TestRunFixture 'attachments-result.json' }
            }
            if (-not $Piped) { @{ Body = Get-TestRunFixture 'workitems-testcases.json' } }
            @{ Body = Get-TestRunFixture 'workitems-bugs.json' }
            if (-not $Piped) { @{ Body = Get-TestRunFixture 'workitemtype-states-bug.json' } }
        )
    }
    # The response lists are positional, so retrieval and downloads send one request at a time.
    # Extra settings are the body of testResults.
    function Set-SequentialConfiguration {
        param([string] $TestResults)
        $settings = '"maximumConcurrentRequests":1' + $(if ($TestResults) { ',' + $TestResults })
        Set-Content -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Encoding utf8 -Value ('{"schemaVersion":1,"testResults":{' + $settings + '}}')
    }
    # Of 5001 PNG, 5002 JSON, 5003 HTML, 5004 and 5005 other bytes, only the JSON is ever requested.
    function Get-ContentResponses {
        @(
            @{ Bytes = Get-AttachmentBytes 'valid.json'; ContentType = 'application/octet-stream' }
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
        Set-SequentialConfiguration
    }
    AfterEach {
        Disconnect-Ado
        Remove-Item -LiteralPath $env:ADOTOOLKIT_CONFIG_PATH -Force -ErrorAction SilentlyContinue
    }

    It 'WhatIf writes nothing and requests no attachment content' -Tag 'S5-7' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses -LatestAttachments) + (Get-ContentResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $server.Requests.Count | Should -Be 12
            @($set | Export-AdoBuildTestFailure -Path $outputDirectory -WhatIf -InformationVariable reportInformation).Count | Should -Be 0
            ($reportInformation | Out-String) | Should -Not -Match 'file:///'
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
            $failure = $null
            try { $set | Export-AdoBuildTestFailure -Path $outputDirectory -NoClobber -InformationVariable reportInformation -ErrorAction Stop | Out-Null }
            catch { $failure = $_ }
            $failure | Should -Not -BeNullOrEmpty
            ($reportInformation | Out-String) | Should -Not -Match 'file:///'
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
            $entries | Should -Be (@('Build-401-TestFailures.html', $folderName, "$folderName/r201-1-a5002.json") | Sort-Object)
            [IO.File]::ReadAllBytes((Join-Path $folder 'r201-1-a5002.json')) | Should -Be (Get-AttachmentBytes 'valid.json')
            $requests = $server.Requests.ToArray()
            $requests.Count | Should -Be 13
            $requests[12].Line | Should -Be 'GET /Collection/%C3%89quipe%20Web/_apis/test/Runs/201/Results/1/attachments/5002?api-version=6.0-preview.1 HTTP/1.1'
            $requests[12].Headers['Accept'] | Should -Be 'application/octet-stream'
            $html = [IO.File]::ReadAllText($file.FullName)
            $html | Should -Match '<html lang="fr-CA"'
            $html | Should -Not -Match '<img'
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

    # -Verbose gives each step of the export its own line, then ends with a summary of the export.
    It 'ends -Verbose output with a summary that counts the downloaded files and their requests' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses) + (Get-ContentResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $records = @($set | Export-AdoBuildTestFailure -Path $outputDirectory -AllRunAttachments -Verbose 4>&1 6> $null)
            $verbose = @($records | Where-Object { $_ -is [System.Management.Automation.VerboseRecord] })
            $culture = [cultureinfo]::GetCultureInfo($PSUICulture)
            # A catalog message with its milliseconds and bytes left open.
            $pattern = {
                param([string] $Key, [object[]] $Arguments)
                '^' + [regex]::Escape([AdoToolkit.Core.Resources.Messages]::Get($Key, $culture, $Arguments)).Replace('NUMBER', '[0-9]+') + '$'
            }
            $server.Requests.Count | Should -Be 13
            @($verbose.Message -match (& $pattern 'ExportStageDownloads' @(1, 1, 'NUMBER'))).Count | Should -Be 1
            @($verbose.Message -match (& $pattern 'ExportStageRender' @('NUMBER', 'NUMBER'))).Count | Should -Be 1
            $verbose[-1].Message | Should -Match (& $pattern 'ExportSummary' @(401, 1, 1, 'NUMBER'))
            @($records | Where-Object { $_ -is [IO.FileInfo] }).Count | Should -Be 1
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    # A name with %41 and %20 must stay literal: Uri(path) read %41 as an escaped A and pointed to xAy.
    It 'prints the report URI on the information stream and pipes only FileInfo (<Culture>, <Name>)' -ForEach @(
        @{ Culture = 'en-US'; Name = 'rapport [été] #1.html'; Escapes = @('%20', '%23', '%C3%A9', '%5B') },
        @{ Culture = 'fr-CA'; Name = 'rapport [été] #1.html'; Escapes = @('%20', '%23', '%C3%A9', '%5B') },
        @{ Culture = 'en-US'; Name = 'x%41y %20 100%.html'; Escapes = @('x%2541y', '%2520', '100%25') }
    ) {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $destination = Join-Path $outputDirectory $Name
            $InformationPreference = 'SilentlyContinue'
            $files = @($set | Export-AdoBuildTestFailure -Path $destination -SkipAttachments -Culture $Culture -InformationVariable reportInformation)
            $files.Count | Should -Be 1
            $files[0] | Should -BeOfType ([IO.FileInfo])
            $files[0].FullName | Should -Be $destination
            @($reportInformation).Count | Should -Be 1
            $reportInformation[0] | Should -BeOfType ([System.Management.Automation.InformationRecord])
            # PSHOST records show even with the default SilentlyContinue preference.
            $reportInformation[0].Tags | Should -Contain 'PSHOST'
            $message = $reportInformation[0].MessageData.Message
            $message | Should -Match '^file:///[A-Za-z]:/'
            $message | Should -Not -Match '[\s\[\]#]'
            foreach ($escape in $Escapes) { $message.Contains($escape) | Should -BeTrue -Because $escape }
            [Uri]::new($message).LocalPath | Should -Be $destination
            Test-Path -LiteralPath ([Uri]::new($message).LocalPath) | Should -BeTrue
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    # A compressed attachment counts its decoded bytes against maximumAttachmentBytes: this body is
    # far under the limit on the wire and far over it once decoded, so the file is not kept.
    It 'counts the decoded bytes of a compressed attachment against the per-file limit' {
        Set-SequentialConfiguration -TestResults '"maximumAttachmentBytes":2048'
        $content = @{ Bytes = [Text.Encoding]::UTF8.GetBytes('{"padding":"' + ('a' * 10000) + '"}'); ContentType = 'application/octet-stream' }
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses) + @($content)) -Compression
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $warnings = @()
            $file = $set | Export-AdoBuildTestFailure -Path $outputDirectory -WarningVariable warnings 6> $null
            $download = $server.Requests.ToArray()[-1]
            $download.Line | Should -Match '/attachments/5002\?'
            $download.Bytes | Should -BeLessThan 2048
            $download.DecodedBytes | Should -BeGreaterThan 2048
            @($warnings | Where-Object { [string] $_ -match '5002' -and [string] $_ -match '2048' }).Count | Should -Be 1
            @(Get-ChildItem -LiteralPath $outputDirectory -Recurse -File -Filter '*a5002*').Count | Should -Be 0
            $file | Should -BeOfType ([IO.FileInfo])
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
            $html | Should -Match '>screenshot\.PNG</a>'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    # A set gathered with -SkipAttachments has no attachment to download, whatever the export's own
    # switches, so it is exported without a connection and without a request.
    It 'exports a set gathered with -SkipAttachments without a connection' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses -SkipAttachments))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -SkipAttachments -WarningAction SilentlyContinue
            $server.Requests.Count | Should -Be 10
            Disconnect-Ado
            $warnings = @()
            $file = $set | Export-AdoBuildTestFailure -Path $outputDirectory -Culture en-US -AllRunAttachments -WarningVariable warnings 6> $null
            $file | Should -BeOfType ([IO.FileInfo])
            $file.PSObject.Properties['AttachmentDirectory'] | Should -BeNullOrEmpty
            @($warnings).Count | Should -Be 0
            $server.Requests.Count | Should -Be 10
            Get-OutputEntry -Path $outputDirectory | Should -Be @('Build-401-TestFailures.html')
            $html = [IO.File]::ReadAllText($file.FullName)
            $html | Should -Match '<span class="count-label">Attachments</span><span class="count-note">not listed</span>'
            $html | Should -Not -Match 'data-local-file'
            $html | Should -Not -Match 'screenshot\.PNG'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    # payload.json declares 128 bytes. A small-file limit of 100 makes it a large file, so the rule
    # for large files decides: the latest run only, or every run with -AllRunAttachments.
    It 'downloads large files of the latest run by default and of every run with AllRunAttachments=<AllRuns>' -ForEach @(
        @{ AllRuns = $false; Downloads = 1 },
        @{ AllRuns = $true; Downloads = 2 }
    ) {
        Set-SequentialConfiguration -TestResults '"maximumInlineJsonBytes":100'
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) +
            (Get-TwoRunResponses -LatestAttachments) + (Get-ContentResponses) + (Get-ContentResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $file = $set | Export-AdoBuildTestFailure -Path $outputDirectory -Culture en-US -AllRunAttachments:$AllRuns
            $server.Requests.Count | Should -Be (12 + $Downloads)
            $contentRequests = @($server.Requests.ToArray() | Select-Object -Skip 12)
            # The latest run is downloaded first.
            $contentRequests[0].Line | Should -Match '/Runs/202/'
            @($contentRequests | Where-Object Line -Match '/Runs/202/').Count | Should -Be 1
            @($contentRequests | Where-Object Line -Match '/Runs/201/').Count | Should -Be ($Downloads - 1)
            # PNG, HTML and other kinds are never requested, whatever the switches.
            @($contentRequests | Where-Object Line -NotMatch '/attachments/5002\?').Count | Should -Be 0
            @(Get-ChildItem -LiteralPath $file.AttachmentDirectory -File).Count | Should -Be $Downloads
            $html = [IO.File]::ReadAllText($file.FullName)
            $html | Should -Match '/_apis/test/Runs/201/Results/1/attachments/5001\?api-version=6\.0-preview\.1">screenshot\.PNG'
            $html | Should -Match '/_apis/test/Runs/202/Results/11/attachments/5001\?api-version=6\.0-preview\.1">screenshot\.PNG'
            $html | Should -Match '<span class="attachment-size" title="2,048 bytes">2\.0 KB</span>'
            $html | Should -Match 'r202-11-a5002.json'
            $html.Contains('r201-1-a5002.json') | Should -Be $AllRuns
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    # With the default limit payload.json is a small file, so it is downloaded from every run inside
    # the window, the latest run first, without any switch.
    It 'downloads small files of every run by default, the latest run first' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) +
            (Get-TwoRunResponses -LatestAttachments) + (Get-ContentResponses) + (Get-ContentResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $warnings = @()
            $file = $set | Export-AdoBuildTestFailure -Path $outputDirectory -Culture en-US -WarningVariable warnings
            @($warnings).Count | Should -Be 0
            $contentRequests = @($server.Requests.ToArray() | Select-Object -Skip 12)
            $contentRequests.Count | Should -Be 2
            $contentRequests[0].Line | Should -Match '/Runs/202/Results/11/attachments/5002\?'
            $contentRequests[1].Line | Should -Match '/Runs/201/Results/1/attachments/5002\?'
            @(Get-ChildItem -LiteralPath $file.AttachmentDirectory -File).Name | Sort-Object | Should -Be @('r201-1-a5002.json', 'r202-11-a5002.json')
            $html = [IO.File]::ReadAllText($file.FullName)
            # Both copies are previewed, so their content is searchable in the report.
            ([regex]::Matches($html, '<details class="attachment-preview">')).Count | Should -Be 2
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    # The latest run has no attachment; the older run has one small JSON file. The export downloads
    # it, so it needs a connection, and without one it stops before it writes anything.
    It 'downloads a small file of an older run when the latest run has none, which needs a connection' {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses) + (Get-ContentResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            Disconnect-Ado
            { $set | Export-AdoBuildTestFailure -Path $outputDirectory -ErrorAction Stop } | Should -Throw
            $server.Requests.Count | Should -Be 12
            Get-OutputEntry -Path $outputDirectory | Should -BeNullOrEmpty
            # -SkipAttachments still needs no connection.
            $listed = $set | Export-AdoBuildTestFailure -Path $outputDirectory -SkipAttachments
            $listed.PSObject.Properties['AttachmentDirectory'] | Should -BeNullOrEmpty

            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $file = $set | Export-AdoBuildTestFailure -Path $outputDirectory
            $server.Requests.Count | Should -Be 13
            $server.Requests.ToArray()[12].Line | Should -Match '/Runs/201/Results/1/attachments/5002\?'
            @(Get-ChildItem -LiteralPath $file.AttachmentDirectory -File).Name | Should -Be @('r201-1-a5002.json')
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'lists large older attachments without a connection when the latest run has none' {
        Set-SequentialConfiguration -TestResults '"maximumInlineJsonBytes":100'
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            Disconnect-Ado
            $file = $set | Export-AdoBuildTestFailure -Path $outputDirectory
            $file.PSObject.Properties['AttachmentDirectory'] | Should -BeNullOrEmpty
            $server.Requests.Count | Should -Be 12
            Get-OutputEntry -Path $outputDirectory | Should -Be @('Build-401-TestFailures.html')
            [IO.File]::ReadAllText($file.FullName) | Should -Match '>screenshot\.PNG</a>'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'leaves out attachments of runs older than AttachmentWindowDays=<Days>' -ForEach @(
        @{ Days = 7; Listed = $true },
        @{ Days = 2; Listed = $false }
    ) {
        $server = Start-FakeAdoServer -Responses (@(@{ Body = Get-TestRunFixture 'build-401.json' }) + (Get-TwoRunResponses -AgeHours 72))
        try {
            Connect-Ado -CollectionUrl $server.Uri -Project 'Équipe Web' -WarningAction SilentlyContinue | Out-Null
            $set = Get-AdoBuildTestFailure -BuildId 401 -HistoryCount 1 -WarningAction SilentlyContinue
            $file = $set | Export-AdoBuildTestFailure -Path $outputDirectory -SkipAttachments -AttachmentWindowDays $Days -IncludeFlaky
            $html = [IO.File]::ReadAllText($file.FullName)
            $html.Contains('>screenshot.PNG</a>') | Should -Be $Listed
            $html | Should -Match 'data-attempt-count="1"'
            { $set | Export-AdoBuildTestFailure -Path $outputDirectory -AttachmentWindowDays 0 } | Should -Throw -ErrorId 'ParameterArgumentValidationError,AdoToolkit.ExportAdoBuildTestFailureCommand'
        }
        finally { Stop-FakeAdoServer -Server $server }
    }

    It 'writes one report per set; a file path accepts only the first set' {
        $responses = @(@{ Body = Get-TestRunFixture 'builds-history.json' })
        $responses += Get-TwoRunResponses
        $responses += Get-TwoRunResponses -Piped
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
            $server.Requests.Count | Should -Be 21
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
            { $set | Export-AdoBuildTestFailure -Connection $foreign -Path $outputDirectory -AllRunAttachments -ErrorAction Stop } |
                Should -Throw -ErrorId 'AdoConnectionMismatch,AdoToolkit.ExportAdoBuildTestFailureCommand'
            $literal = Join-Path $outputDirectory 'rapport-[été].html'
            $file = $set | Export-AdoBuildTestFailure -Connection $connection -Path $literal -SkipAttachments
            $file.FullName | Should -Be $literal
            $server.Requests.Count | Should -Be 12
            Get-OutputEntry -Path $outputDirectory | Should -Be @('rapport-[été].html')
        }
        finally { Stop-FakeAdoServer -Server $server }
    }
}
