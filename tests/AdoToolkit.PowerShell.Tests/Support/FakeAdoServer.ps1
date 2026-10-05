function Start-FakeAdoServer {
    [CmdletBinding()]
    param(
        [object[]] $Responses = @(),
        # Tried in order before the queue; the first match answers. Each route is a hashtable: Line, a regex
        # on the request line; optionally Body, a regex on the request body; and either Response, one
        # response, or Responses, responses keyed by the captures of Line then Body joined with '/'. A key
        # that is not in Responses leaves the request to the next route. A response may carry Delay, the
        # milliseconds to wait before it is sent, in place of LatencyMilliseconds.
        [object[]] $Routes = @(),
        # Waits before each response, so that concurrent requests overlap.
        [ValidateRange(0, 60000)][int] $LatencyMilliseconds = 0,
        # Connections served at once. One worker answers requests strictly one after another.
        [ValidateRange(1, 64)][int] $Workers = 1,
        # Gzips every response body for a request that accepts gzip, as a server that compresses every
        # content type would. Without it no body is ever compressed.
        [switch] $Compression,
        # Writes each response at most this fast, in bytes per second. Zero writes it at once.
        [ValidateRange(0, [int]::MaxValue)][int] $BytesPerSecond = 0
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
    # Requests being answered now. PowerShell cannot pass an array element by reference to Interlocked, so
    # the count, and the Started and Ended timestamps of each request, change under a lock on the array.
    $load = [int[]]::new(1)
    $worker = {
        $taskListener = $using:listener
        $taskPlans = $using:plans
        $taskRequests = $using:requests
        $taskCancellation = $using:cancellation
        $taskLatency = $using:LatencyMilliseconds
        $taskLoad = $using:load
        $taskCompression = [bool] $using:Compression
        $taskRate = $using:BytesPerSecond
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
        $probe = [byte[]]::new(1)
        [void] ($using:ready).Signal()
        try {
            while (-not $taskCancellation.IsCancellationRequested) {
                $client = $taskListener.AcceptTcpClientAsync($taskCancellation.Token).AsTask().GetAwaiter().GetResult()
                $record = $null
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
                        # Status, Bytes (the body as sent) and DecodedBytes (the body before compression) are set
                        # before the response is written. Aborted marks a request that the client abandoned.
                        $record = [pscustomobject]@{
                            Line = $line; Headers = $headers; Body = $body; Status = $null; Bytes = 0; DecodedBytes = 0; Aborted = $false
                            Started = $null; Ended = $null
                        }
                        $taskRequests.Enqueue($record)
                        [System.Threading.Monitor]::Enter($taskLoad)
                        try {
                            $taskLoad[0]++
                            $record.Started = [System.Diagnostics.Stopwatch]::GetTimestamp()
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
                            $wait = if ($response.ContainsKey('Block') -and $response.Block) { -1 }
                            elseif ($response.ContainsKey('Delay')) { [int] $response.Delay }
                            else { $taskLatency }
                            if ($wait -ne 0) {
                                # The client sends nothing after its request, so a read completes only when it
                                # closes the connection: the request is then abandoned and gets no response.
                                $watch = [System.Threading.CancellationTokenSource]::CreateLinkedTokenSource($taskCancellation.Token)
                                try {
                                    $closed = $stream.ReadAsync($probe, 0, 1, $watch.Token)
                                    $delay = [System.Threading.Tasks.Task]::Delay($wait, $taskCancellation.Token)
                                    [void] [System.Threading.Tasks.Task]::WhenAny([System.Threading.Tasks.Task[]] @($delay, $closed)).GetAwaiter().GetResult()
                                    $record.Aborted = $closed.IsCompleted
                                    $watch.Cancel()
                                    # Cancelled when the response is due; a reset connection fails it instead.
                                    try { [void] $closed.GetAwaiter().GetResult() }
                                    catch { $record.Aborted = $record.Aborted -and -not $taskCancellation.IsCancellationRequested }
                                }
                                finally { $watch.Dispose() }
                                $taskCancellation.Token.ThrowIfCancellationRequested()
                                if ($record.Aborted) { continue }
                            }
                            $status = if ($response.ContainsKey('Status')) { $response.Status } else { 200 }
                            $text = if ($response.ContainsKey('Body')) { [string] $response.Body } else { '{"value":[]}' }
                            $media = if ($response.ContainsKey('ContentType')) { $response.ContentType } else { 'application/json' }
                            # Bytes serves binary content unchanged, for example attachment files. Assign in
                            # each branch: an if expression would unroll the array (an empty body becomes $null).
                            if ($response.ContainsKey('Bytes')) { $bytes = [byte[]] $response.Bytes }
                            else { $bytes = [System.Text.Encoding]::UTF8.GetBytes($text) }
                            $record.DecodedBytes = $bytes.Length
                            $head = "HTTP/1.1 $status Synthetic`r`nContent-Type: $media`r`n"
                            $accepted = if ($headers.ContainsKey('Accept-Encoding')) { [string] $headers['Accept-Encoding'] } else { '' }
                            if ($taskCompression -and @($accepted.Split(',') | Where-Object { $_.Trim() -match '^gzip\s*(;|$)' -and $_ -notmatch 'q\s*=\s*0(\.0*)?\s*$' }).Count -gt 0) {
                                $packed = [System.IO.MemoryStream]::new()
                                $zip = [System.IO.Compression.GZipStream]::new($packed, [System.IO.Compression.CompressionLevel]::Fastest, $true)
                                try { $zip.Write($bytes, 0, $bytes.Length) }
                                finally { $zip.Dispose() }
                                $bytes = $packed.ToArray()
                                $head += "Content-Encoding: gzip`r`n"
                            }
                            $head += "Content-Length: $($bytes.Length)`r`nConnection: close`r`n"
                            if ($response.ContainsKey('Headers')) {
                                foreach ($key in $response.Headers.Keys) {
                                    $value = [string] $response.Headers[$key]
                                    if ($key -match '[\r\n]' -or $value -match '[\r\n]') { throw 'Invalid synthetic response header.' }
                                    $head += "${key}: $value`r`n"
                                }
                            }
                            $headBytes = [System.Text.Encoding]::ASCII.GetBytes($head + "`r`n")
                            $record.Status = $status
                            $record.Bytes = $bytes.Length
                            try {
                                if ($taskRate -eq 0) {
                                    $stream.Write($headBytes, 0, $headBytes.Length)
                                    $stream.Write($bytes, 0, $bytes.Length)
                                }
                                else {
                                    # Chunks of about a fiftieth of a second, each sent once the rate allows it. Nothing
                                    # waits after the last one: the response ends with its last byte.
                                    $whole = [byte[]]::new($headBytes.Length + $bytes.Length)
                                    [System.Buffer]::BlockCopy($headBytes, 0, $whole, 0, $headBytes.Length)
                                    [System.Buffer]::BlockCopy($bytes, 0, $whole, $headBytes.Length, $bytes.Length)
                                    $chunk = [Math]::Max(1024, [int] [Math]::Floor($taskRate / 50))
                                    $paced = [System.Diagnostics.Stopwatch]::StartNew()
                                    for ($offset = 0; $offset -lt $whole.Length; $offset += $chunk) {
                                        $due = [long] [Math]::Floor($offset * 1000.0 / $taskRate) - $paced.ElapsedMilliseconds
                                        if ($due -gt 0) { [System.Threading.Tasks.Task]::Delay([int] $due, $taskCancellation.Token).GetAwaiter().GetResult() }
                                        $stream.Write($whole, $offset, [Math]::Min($chunk, $whole.Length - $offset))
                                    }
                                }
                                $stream.Flush()
                            }
                            catch [System.IO.IOException] {
                                # Only the connection raises this: the client reset it during the response, which
                                # abandons the request. That is not a server error.
                                $record.Aborted = $true
                                $record.Bytes = 0
                                $record.DecodedBytes = 0
                            }
                        }
                        finally {
                            [System.Threading.Monitor]::Enter($taskLoad)
                            try {
                                $taskLoad[0]--
                                $record.Ended = [System.Diagnostics.Stopwatch]::GetTimestamp()
                            }
                            finally { [System.Threading.Monitor]::Exit($taskLoad) }
                        }
                    }
                    finally { $reader.Dispose() }
                }
                catch [System.IO.IOException] {
                    # The client reset the connection before its request was complete: there is no request.
                    continue
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
        Requests = $requests; Listener = $listener; Cancellation = $cancellation; Job = $job; Load = $load; Since = [long[]]::new(1)
    }
}

# The highest number of requests the server was answering at once since it started or since the last reset.
# A request counts from its receipt until its response is written. One whose client closed the connection
# before the response does not count at all: the client stopped waiting for it, and the server sees that only
# afterwards. A client that leaves while the response is being written cannot be told from one that read it.
function Get-FakeAdoServerPeak {
    [CmdletBinding()]
    [OutputType([int])]
    param([Parameter(Mandatory = $true)][object] $Server, [switch] $Reset)

    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    [System.Threading.Monitor]::Enter($Server.Load)
    try {
        $now = [System.Diagnostics.Stopwatch]::GetTimestamp()
        $since = $Server.Since[0]
        # Each answered request adds one at its start and removes one at its end. A key is twice the time, plus
        # one for a start, so that at the same instant an end comes first.
        $keys = [System.Collections.Generic.List[long]]::new()
        foreach ($request in $Server.Requests.ToArray()) {
            if ($null -eq $request.Started -or $request.Aborted) { continue }
            $end = if ($null -eq $request.Ended) { $now } else { [long] $request.Ended }
            if ($end -le $since) { continue }
            $keys.Add(2 * [Math]::Max([long] $request.Started, $since) + 1)
            $keys.Add(2 * $end)
        }
        $keys.Sort()
        $current = 0
        $peak = 0
        foreach ($key in $keys) {
            if ($key % 2 -eq 1) { $current++ } else { $current-- }
            if ($current -gt $peak) { $peak = $current }
        }
        if ($Reset) { $Server.Since[0] = $now }
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
