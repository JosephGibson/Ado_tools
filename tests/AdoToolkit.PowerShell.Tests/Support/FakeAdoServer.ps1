function Start-FakeAdoServer {
    [CmdletBinding()]
    param([object[]] $Responses = @())

    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $plans = [System.Collections.Concurrent.ConcurrentQueue[object]]::new()
    foreach ($response in $Responses) { $plans.Enqueue($response) }
    $requests = [System.Collections.Concurrent.ConcurrentQueue[object]]::new()
    $cancellation = [System.Threading.CancellationTokenSource]::new()
    $ready = [System.Threading.ManualResetEventSlim]::new($false)
    $job = Start-ThreadJob -ScriptBlock {
        $taskListener = $using:listener
        $taskPlans = $using:plans
        $taskRequests = $using:requests
        $taskCancellation = $using:cancellation
        Set-StrictMode -Version 2.0
        $ErrorActionPreference = 'Stop'
        ($using:ready).Set()
        try {
            while (-not $taskCancellation.IsCancellationRequested) {
                $client = $taskListener.AcceptTcpClientAsync($taskCancellation.Token).AsTask().GetAwaiter().GetResult()
                try {
                    $stream = $client.GetStream()
                    $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::ASCII, $false, 4096, $true)
                    try {
                        $line = $reader.ReadLineAsync($taskCancellation.Token).AsTask().GetAwaiter().GetResult()
                        if ([string]::IsNullOrEmpty($line)) { continue }
                        $headers = @{}
                        while ($true) {
                            $header = $reader.ReadLineAsync($taskCancellation.Token).AsTask().GetAwaiter().GetResult()
                            if ([string]::IsNullOrEmpty($header)) { break }
                            $parts = $header.Split(':', 2)
                            $headers[$parts[0]] = $parts[1].Trim()
                        }
                        # Bodies are read for assertions; the ASCII reader keeps one character per byte.
                        $body = $null
                        if ($headers.ContainsKey('Content-Length')) {
                            $length = [int] $headers['Content-Length']
                            $buffer = [char[]]::new($length)
                            $read = 0
                            while ($read -lt $length) {
                                $count = $reader.ReadAsync([Memory[char]]::new($buffer, $read, $length - $read), $taskCancellation.Token).AsTask().GetAwaiter().GetResult()
                                if ($count -eq 0) { break }
                                $read += $count
                            }
                            $body = [string]::new($buffer, 0, $read)
                        }
                        $taskRequests.Enqueue([pscustomobject]@{ Line = $line; Headers = $headers; Body = $body })
                        $response = $null
                        if (-not $taskPlans.TryDequeue([ref] $response)) { $response = @{ Status = 200; Body = '{"value":[]}' } }
                        if ($response.ContainsKey('Block') -and $response.Block) {
                            [System.Threading.Tasks.Task]::Delay(-1, $taskCancellation.Token).GetAwaiter().GetResult()
                        }
                        $status = if ($response.ContainsKey('Status')) { $response.Status } else { 200 }
                        $body = if ($response.ContainsKey('Body')) { [string] $response.Body } else { '{"value":[]}' }
                        $media = if ($response.ContainsKey('ContentType')) { $response.ContentType } else { 'application/json' }
                        # Bytes serves binary content unchanged, for example attachment files. Assign in
                        # each branch: an if expression would unroll the array (an empty body becomes $null).
                        if ($response.ContainsKey('Bytes')) { $bytes = [byte[]] $response.Bytes }
                        else { $bytes = [System.Text.Encoding]::UTF8.GetBytes($body) }
                        $head = "HTTP/1.1 $status Synthetic`r`nContent-Type: $media`r`nContent-Length: $($bytes.Length)`r`nConnection: close`r`n"
                        if ($response.ContainsKey('Headers')) {
                            foreach ($key in $response.Headers.Keys) {
                                $value = [string] $response.Headers[$key]
                                if ($key -match '[\r\n]' -or $value -match '[\r\n]') { throw 'Invalid synthetic response header.' }
                                $head += "${key}: $value`r`n"
                            }
                        }
                        $headBytes = [System.Text.Encoding]::ASCII.GetBytes($head + "`r`n")
                        $stream.Write($headBytes, 0, $headBytes.Length)
                        $stream.Write($bytes, 0, $bytes.Length)
                        $stream.Flush()
                    }
                    finally { $reader.Dispose() }
                }
                finally { $client.Dispose() }
            }
        }
        catch {
            if (-not $taskCancellation.IsCancellationRequested) { throw }
        }
    }
    # Callers may trace the process; setup commands in the job must finish first.
    if (-not $ready.Wait(10000)) { throw 'Synthetic server did not start.' }
    $ready.Dispose()
    return [pscustomobject]@{
        Uri = 'http://127.0.0.1:' + ([System.Net.IPEndPoint] $listener.LocalEndpoint).Port.ToString([cultureinfo]::InvariantCulture) + '/Collection'
        Requests = $requests; Listener = $listener; Cancellation = $cancellation; Job = $job
    }
}

function Stop-FakeAdoServer {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][object] $Server)

    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    $Server.Cancellation.Cancel()
    $Server.Listener.Stop()
    $job = $Server.Job | Wait-Job -Timeout 5
    if ($null -eq $job) { $Server.Job | Stop-Job; throw 'Synthetic server did not stop.' }
    try { $Server.Job | Receive-Job -ErrorAction Stop | Out-Null }
    finally { $Server.Job | Remove-Job -Force; $Server.Cancellation.Dispose() }
}
