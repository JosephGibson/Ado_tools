BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    . (Join-Path $PSScriptRoot 'Support/FakeAdoServer.ps1')
    # Sends one GET by hand and returns the open connection, which the caller closes.
    function Send-RawRequest {
        param([Parameter(Mandatory = $true)][object] $Server, [Parameter(Mandatory = $true)][string] $Path)
        $uri = [uri] $Server.Uri
        $client = [System.Net.Sockets.TcpClient]::new()
        $client.Connect([System.Net.IPAddress]::Loopback, $uri.Port)
        $bytes = [System.Text.Encoding]::ASCII.GetBytes("GET $($uri.AbsolutePath)$Path HTTP/1.1`r`nHost: 127.0.0.1`r`n`r`n")
        $client.GetStream().Write($bytes, 0, $bytes.Length)
        $client
    }
    function Wait-Condition {
        param([Parameter(Mandatory = $true)][scriptblock] $Condition, [int] $Milliseconds = 5000)
        $deadline = [datetime]::UtcNow.AddMilliseconds($Milliseconds)
        while (-not (& $Condition) -and [datetime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 10 }
        [bool] (& $Condition)
    }
    function Get-Text {
        param([Parameter(Mandatory = $true)][System.Net.Http.HttpClient] $Client, [Parameter(Mandatory = $true)][string] $Uri)
        $Client.GetStringAsync($Uri).GetAwaiter().GetResult()
    }
}

# The server that the Pester suites and the Measure script share. A client that gives up on a
# request closes its connection: the request is abandoned, which is not a server error.
Describe 'Synthetic server' {
    It 'stops counting a request whose client closed its connection, and keeps serving' {
        # The first request waits until its client leaves; the second is answered at once.
        $server = Start-FakeAdoServer -Routes @(
            @{ Line = '/slow\?'; Response = @{ Block = $true } }
            @{ Line = '/fast\?'; Response = @{ Body = '{"value":[]}' } }
        )
        $stopped = $false
        $client = [System.Net.Http.HttpClient]::new()
        $client.Timeout = [timespan]::FromSeconds(10)
        try {
            $raw = Send-RawRequest -Server $server -Path '/slow?first=1'
            Wait-Condition { $server.Requests.Count -eq 1 } | Should -BeTrue
            $raw.Dispose()
            # Soon after its client leaves, the request no longer counts as in flight.
            Wait-Condition { $server.Load[0] -eq 0 } -Milliseconds 700 | Should -BeTrue
            # The one worker is free again and answers the next request.
            Get-Text -Client $client -Uri ($server.Uri + '/fast?second=1') | Should -Be '{"value":[]}'
            $requests = $server.Requests.ToArray()
            $requests.Aborted | Should -Be @($true, $false)
            $requests[1].Status | Should -Be 200
            $stopped = $true
            { Stop-FakeAdoServer -Server $server } | Should -Not -Throw
        }
        finally {
            $client.Dispose()
            if (-not $stopped) { Stop-FakeAdoServer -Server $server }
        }
    }

    # The client stopped waiting for a request it abandoned and may already have sent the next one, so
    # the peak, which checks the client's bound, leaves such a request out.
    It 'leaves out of its peak a request whose client left' {
        $server = Start-FakeAdoServer -Routes @(@{ Line = '/slow\?'; Response = @{ Body = '{"value":[]}' } }) -LatencyMilliseconds 1000 -Workers 2
        $client = [System.Net.Http.HttpClient]::new()
        $client.Timeout = [timespan]::FromSeconds(10)
        try {
            $raw = Send-RawRequest -Server $server -Path '/slow?left=1'
            Wait-Condition { $server.Requests.Count -eq 1 } | Should -BeTrue
            $answered = $client.GetStringAsync($server.Uri + '/slow?answered=1')
            Wait-Condition { $server.Load[0] -eq 2 } | Should -BeTrue
            $raw.Dispose()
            $answered.GetAwaiter().GetResult() | Should -Be '{"value":[]}'
            Get-FakeAdoServerPeak -Server $server | Should -Be 1
        }
        finally { $client.Dispose(); Stop-FakeAdoServer -Server $server }
    }

    # The Measure script ended with exit code 1 this way: a request that the client had cancelled
    # was answered once its latency ended, and the failed write stopped the worker.
    It 'is not failed by a client that resets its connection before the response' {
        $server = Start-FakeAdoServer -Routes @(@{ Line = '/slow\?'; Response = @{ Body = '{"value":[]}' } }) -LatencyMilliseconds 300
        $stopped = $false
        try {
            $raw = Send-RawRequest -Server $server -Path '/slow?a=1'
            Wait-Condition { $server.Requests.Count -eq 1 } | Should -BeTrue
            $raw.Client.LingerState = [System.Net.Sockets.LingerOption]::new($true, 0)
            $raw.Dispose()
            Wait-Condition { $server.Load[0] -eq 0 } | Should -BeTrue
            $stopped = $true
            { Stop-FakeAdoServer -Server $server } | Should -Not -Throw
            $server.Requests.ToArray()[0].Aborted | Should -BeTrue
        }
        finally { if (-not $stopped) { Stop-FakeAdoServer -Server $server } }
    }

    It 'is not failed by a client that leaves during the response' {
        $text = '"' + ('x' * 100000) + '"'
        $server = Start-FakeAdoServer -Routes @(@{ Line = '/paced\?'; Response = @{ Body = $text } }) -BytesPerSecond 20000
        $stopped = $false
        try {
            $raw = Send-RawRequest -Server $server -Path '/paced?a=1'
            # The start of the response arrives, then the client resets the connection.
            $buffer = [byte[]]::new(256)
            [void] $raw.GetStream().Read($buffer, 0, $buffer.Length)
            $raw.Client.LingerState = [System.Net.Sockets.LingerOption]::new($true, 0)
            $raw.Dispose()
            Wait-Condition { $server.Load[0] -eq 0 } | Should -BeTrue
            $stopped = $true
            { Stop-FakeAdoServer -Server $server } | Should -Not -Throw
            $record = $server.Requests.ToArray()[0]
            $record.Aborted | Should -BeTrue
            $record.Bytes | Should -Be 0
        }
        finally { if (-not $stopped) { Stop-FakeAdoServer -Server $server } }
    }

    It 'records the status and the bytes of each response it sends' {
        $server = Start-FakeAdoServer -Routes @(@{ Line = '/missing\?'; Response = @{ Status = 404; Body = '{"message":"Synthétique"}' } })
        $client = [System.Net.Http.HttpClient]::new()
        try {
            $response = $client.GetAsync($server.Uri + '/missing?a=1').GetAwaiter().GetResult()
            [int] $response.StatusCode | Should -Be 404
            $record = $server.Requests.ToArray()[0]
            $record.Status | Should -Be 404
            # UTF-8: the accented letter takes two bytes.
            $record.Bytes | Should -Be 26
            $record.DecodedBytes | Should -Be 26
            $record.Aborted | Should -BeFalse
        }
        finally { $client.Dispose(); Stop-FakeAdoServer -Server $server }
    }

    It 'gzips a body only with -Compression and only for a request that accepts gzip' {
        $text = '{"count":200,"value":[' + ((1..200 | ForEach-Object { '{"id":' + $_ + ',"outcome":"Passed","automatedTestName":"Synthetic.Compression.Test"}' }) -join ',') + ']}'
        $routes = @(@{ Line = '/page\?'; Response = @{ Body = $text } })
        $handler = [System.Net.Http.SocketsHttpHandler]::new()
        $handler.AutomaticDecompression = [System.Net.DecompressionMethods]::GZip
        $decoding = [System.Net.Http.HttpClient]::new($handler)
        $plain = [System.Net.Http.HttpClient]::new()
        $compressing = Start-FakeAdoServer -Routes $routes -Compression
        $default = Start-FakeAdoServer -Routes $routes
        try {
            foreach ($server in $compressing, $default) {
                Get-Text -Client $decoding -Uri ($server.Uri + '/page?a=1') | Should -Be $text
                Get-Text -Client $plain -Uri ($server.Uri + '/page?a=1') | Should -Be $text
            }
            $length = [System.Text.Encoding]::UTF8.GetByteCount($text)
            $records = @($compressing.Requests.ToArray()) + @($default.Requests.ToArray())
            $records.DecodedBytes | Should -Be @($length, $length, $length, $length)
            $records[0].Headers['Accept-Encoding'] | Should -Match 'gzip'
            $records[0].Bytes | Should -BeLessThan ($length / 4)
            @($records[1..3].Bytes) | Should -Be @($length, $length, $length)
        }
        finally {
            $decoding.Dispose(); $plain.Dispose()
            Stop-FakeAdoServer -Server $compressing
            Stop-FakeAdoServer -Server $default
        }
    }

    # A paced response ends with its last byte: the client already has it and may send its next request.
    It 'stops counting a paced response once its last byte is sent' {
        # About 3,000 bytes with the head, in chunks of 1,024: the last chunk is nearly full, so it takes
        # most of a twentieth of a second to send, which is how long the server could count it too late.
        # The next request follows at once, so two requests show it.
        $text = '"' + ('x' * 2898) + '"'
        $server = Start-FakeAdoServer -Routes @(@{ Line = '/paced\?'; Response = @{ Body = $text } }) -BytesPerSecond 20480 -Workers 2
        $client = [System.Net.Http.HttpClient]::new()
        try {
            foreach ($index in 1..2) { Get-Text -Client $client -Uri ($server.Uri + "/paced?i=$index") | Should -Be $text }
            Get-FakeAdoServerPeak -Server $server | Should -Be 1
        }
        finally { $client.Dispose(); Stop-FakeAdoServer -Server $server }
    }

    It 'writes a response no faster than -BytesPerSecond' {
        # 3,999 bytes with the 99-byte head, in chunks of 1,024: the fourth and last chunk is due 300 ms
        # after the first, and the client cannot have the whole response before it.
        $text = '"' + ('x' * 3898) + '"'
        $server = Start-FakeAdoServer -Routes @(@{ Line = '/paced\?'; Response = @{ Body = $text } }) -BytesPerSecond 10240
        $client = [System.Net.Http.HttpClient]::new()
        try {
            $watch = [System.Diagnostics.Stopwatch]::StartNew()
            Get-Text -Client $client -Uri ($server.Uri + '/paced?a=1') | Should -Be $text
            $watch.ElapsedMilliseconds | Should -BeGreaterThan 250
        }
        finally { $client.Dispose(); Stop-FakeAdoServer -Server $server }
    }
}

# The Measure script on a build of a few tests, in a child process like a run by hand, with its
# temporary folder under $TestDrive. An export with nothing to download sends no request, and the
# script summed the bytes of a run with Measure-Object, which gives no Sum for no input.
Describe 'Measure script' {
    It 'reports an export that sends no request (<Case>)' -ForEach @(
        @{ Case = 'no attachment'; Arguments = @('-AttachmentsPerAttempt', '0'); AttachmentLists = $true },
        @{ Case = '-SkipAttachments'; Arguments = @('-SkipAttachments'); AttachmentLists = $false }
    ) {
        $script = Join-Path $PSScriptRoot 'Support/Measure-TestFailurePipeline.ps1'
        $previousTemp = $env:TMP
        $env:TMP = $TestDrive
        try {
            $json = & ([Environment]::ProcessPath) -NoProfile -File $script -ResultsPerRun 20 -FailingTests 2 -HistoryBuilds 2 -Iterations 1 `
                -LatencyMilliseconds 0 @Arguments 2>&1
            $exit = $LASTEXITCODE
        }
        finally { $env:TMP = $previousTemp }
        $exit | Should -Be 0 -Because ($json -join "`n")
        $measured = ($json -join "`n") | ConvertFrom-Json
        $get = $measured.Results | Where-Object Cmdlet -EQ 'Get-AdoBuildTestFailure'
        $export = $measured.Results | Where-Object Cmdlet -EQ 'Export-AdoBuildTestFailure'
        $measured.Failures | Should -Be 2
        $get.Requests | Should -BeGreaterThan 0
        $get.Stages.PSObject.Properties.Name -contains 'Attachments' | Should -Be $AttachmentLists
        $export.Requests | Should -Be 0
        $export.Bytes | Should -Be 0
        $export.DecodedBytes | Should -Be 0
        @($export.Stages.PSObject.Properties).Count | Should -Be 0
        $measured.ReportBytes | Should -BeGreaterThan 0
        @(Get-ChildItem -LiteralPath $TestDrive -Filter 'AdoToolkit-measure-*').Count | Should -Be 0
    }
}
