function Start-FakeAdoServer {
    [CmdletBinding()]
    param(
        [object[]] $Responses = @(),
        # Tried in order before the queue; the first match answers. Each route is a hashtable: Line, a regex
        # on the request line; optionally Body, a regex on the request body; and either Response, one
        # response, or Responses, responses keyed by the captures of Line then Body joined with '/'. A key
        # that is not in Responses leaves the request to the next route.
        [object[]] $Routes = @(),
        # Waits before each response, so that concurrent requests overlap.
        [ValidateRange(0, 60000)][int] $LatencyMilliseconds = 0,
        # Connections served at once. One worker answers requests strictly one after another.
        [ValidateRange(1, 64)][int] $Workers = 1
    )

    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $plans = [System.Collections.Concurrent.ConcurrentQueue[object]]::new()
    foreach ($response in $Responses) { $plans.Enqueue($response) }
    $requests = [System.Collections.Concurrent.ConcurrentQueue[object]]::new()
    $cancellation = [System.Threading.CancellationTokenSource]::new()
    $ready = [System.Threading.CountdownEvent]::new($Workers)
    # Requests being answered now, then the highest value reached. PowerShell cannot pass an array element
    # by reference to Interlocked, so both are updated under a lock on the array.
    $load = [int[]]::new(2)
    $worker = {
        $taskListener = $using:listener
        $taskPlans = $using:plans
        $taskRequests = $using:requests
        $taskCancellation = $using:cancellation
        $taskLatency = $using:LatencyMilliseconds
        $taskLoad = $using:load
        Set-StrictMode -Version 2.0
        $ErrorActionPreference = 'Stop'
        $taskRoutes = @(foreach ($route in $using:Routes) {
                @{
                    Line = [regex]::new([string] $route.Line)
                    Body = if ($route.ContainsKey('Body')) { [regex]::new([string] $route.Body) } else { $null }
                    Response = if ($route.ContainsKey('Response')) { $route.Response } else { $null }
                    Responses = if ($route.ContainsKey('Responses')) { $route.Responses } else { $null }
                }
            })
        [void] ($using:ready).Signal()
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
                        [System.Threading.Monitor]::Enter($taskLoad)
                        try {
                            $taskLoad[0]++
                            if ($taskLoad[0] -gt $taskLoad[1]) { $taskLoad[1] = $taskLoad[0] }
                        }
                        finally { [System.Threading.Monitor]::Exit($taskLoad) }
                        try {
                            $response = $null
                            foreach ($route in $taskRoutes) {
                                $lineMatch = $route.Line.Match($line)
                                if (-not $lineMatch.Success) { continue }
                                $bodyMatch = $null
                                if ($null -ne $route.Body) {
                                    if ($null -eq $body) { continue }
                                    $bodyMatch = $route.Body.Match($body)
                                    if (-not $bodyMatch.Success) { continue }
                                }
                                if ($null -eq $route.Responses) { $response = $route.Response; break }
                                $captures = [System.Collections.Generic.List[string]]::new()
                                foreach ($match in $lineMatch, $bodyMatch) {
                                    if ($null -eq $match) { continue }
                                    for ($group = 1; $group -lt $match.Groups.Count; $group++) {
                                        if ($match.Groups[$group].Success) { $captures.Add($match.Groups[$group].Value) }
                                    }
                                }
                                $key = $captures -join '/'
                                if ($route.Responses.ContainsKey($key)) { $response = $route.Responses[$key]; break }
                            }
                            if ($null -eq $response -and -not $taskPlans.TryDequeue([ref] $response)) { $response = @{ Status = 200; Body = '{"value":[]}' } }
                            if ($response.ContainsKey('Block') -and $response.Block) {
                                [System.Threading.Tasks.Task]::Delay(-1, $taskCancellation.Token).GetAwaiter().GetResult()
                            }
                            if ($taskLatency -gt 0) {
                                [System.Threading.Tasks.Task]::Delay($taskLatency, $taskCancellation.Token).GetAwaiter().GetResult()
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
                        finally {
                            [System.Threading.Monitor]::Enter($taskLoad)
                            try { $taskLoad[0]-- }
                            finally { [System.Threading.Monitor]::Exit($taskLoad) }
                        }
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
    # One job answers in request order, as every positional response list expects. More jobs accept on the
    # same listener; the thread-job throttle, 5 by default, must cover them or the extra ones never start.
    if ($Workers -eq 1) { $job = Start-ThreadJob -ScriptBlock $worker }
    else { $job = @(foreach ($index in 1..$Workers) { Start-ThreadJob -ScriptBlock $worker -ThrottleLimit ($Workers + 5) }) }
    # Callers may trace the process; setup commands in the job must finish first.
    if (-not $ready.Wait(10000)) { throw 'Synthetic server did not start.' }
    $ready.Dispose()
    return [pscustomobject]@{
        Uri = 'http://127.0.0.1:' + ([System.Net.IPEndPoint] $listener.LocalEndpoint).Port.ToString([cultureinfo]::InvariantCulture) + '/Collection'
        Requests = $requests; Listener = $listener; Cancellation = $cancellation; Job = $job; Load = $load
    }
}

# The highest number of requests the server was answering at once since it started or since the last reset.
function Get-FakeAdoServerPeak {
    [CmdletBinding()]
    [OutputType([int])]
    param([Parameter(Mandatory = $true)][object] $Server, [switch] $Reset)

    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    [System.Threading.Monitor]::Enter($Server.Load)
    try {
        $peak = $Server.Load[1]
        if ($Reset) { $Server.Load[1] = $Server.Load[0] }
        return $peak
    }
    finally { [System.Threading.Monitor]::Exit($Server.Load) }
}

function Stop-FakeAdoServer {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][object] $Server)

    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    $Server.Cancellation.Cancel()
    $Server.Listener.Stop()
    $jobs = @($Server.Job)
    $stopped = @($jobs | Wait-Job -Timeout 5)
    if ($stopped.Count -lt $jobs.Count) { $jobs | Stop-Job; throw 'Synthetic server did not stop.' }
    try { $jobs | Receive-Job -ErrorAction Stop | Out-Null }
    finally { $jobs | Remove-Job -Force; $Server.Cancellation.Dispose() }
}
