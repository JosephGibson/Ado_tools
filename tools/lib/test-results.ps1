# Product test result checks; callers enable strict mode and terminating errors.
function Get-AdoTestOutcome {
    param([Parameter(Mandatory = $true)][System.Xml.XmlDocument] $Document)

    $nodes = @($Document.SelectNodes("//*[local-name()='ResultSummary']/*[local-name()='Counters']"))
    if ($nodes.Count -ne 1) {
        return [pscustomobject]@{ ExitCode = 2; Summary = 'Core test counters are missing or ambiguous.' }
    }
    $counts = @{}
    foreach ($name in @('total', 'executed', 'passed', 'failed', 'error', 'timeout', 'aborted')) {
        $count = 0
        if (-not [int]::TryParse($nodes[0].GetAttribute($name), [Globalization.NumberStyles]::None,
            [cultureinfo]::InvariantCulture, [ref] $count)) {
            return [pscustomobject]@{ ExitCode = 2; Summary = 'Core test counters are incomplete or invalid.' }
        }
        $counts[$name] = $count
    }
    $code = if ($counts.failed -gt 0 -or $counts.error -gt 0 -or $counts.timeout -gt 0 -or $counts.aborted -gt 0) { 1 }
        elseif ($counts.total -eq 0 -or $counts.executed -ne $counts.total -or $counts.passed -ne $counts.total) { 2 }
        else { 0 }
    return [pscustomobject]@{
        ExitCode = $code
        Summary = '{0} passed, {1} failed, {2} executed, {3} discovered' -f
            $counts.passed, $counts.failed, $counts.executed, $counts.total
    }
}

# Starts one step of the gate, so that steps that need only the build run side by side. The
# arguments go as an array; the Environment entries apply to the child alone, and a $null value
# removes one. Both pipes are drained as the child writes, so it never waits on a full pipe.
function Start-AdoGateProcess {
    param(
        [Parameter(Mandatory = $true)][string] $FilePath,
        [Parameter(Mandatory = $true)][string[]] $ArgumentList,
        [hashtable] $Environment = @{}
    )

    $info = [System.Diagnostics.ProcessStartInfo]::new($FilePath)
    foreach ($argument in $ArgumentList) { $info.ArgumentList.Add($argument) }
    foreach ($name in $Environment.Keys) {
        if ($null -eq $Environment[$name]) { [void] $info.Environment.Remove($name) }
        else { $info.Environment[$name] = [string] $Environment[$name] }
    }
    $info.WorkingDirectory = (Get-Location).ProviderPath
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    # The encoding PowerShell decodes a native command's output with.
    $info.StandardOutputEncoding = $info.StandardErrorEncoding = [Console]::OutputEncoding
    $process = [System.Diagnostics.Process]::Start($info)
    $process.StandardInput.Close()
    return [pscustomobject]@{ Process = $process; Output = $process.StandardOutput.ReadToEndAsync(); Errors = $process.StandardError.ReadToEndAsync() }
}

# Waits for a step of Start-AdoGateProcess: its exit code and the lines it wrote, standard output
# first.
function Wait-AdoGateProcess {
    param([Parameter(Mandatory = $true)][object] $Handle)

    $Handle.Process.WaitForExit()
    $text = $Handle.Output.GetAwaiter().GetResult() + [Environment]::NewLine + $Handle.Errors.GetAwaiter().GetResult()
    return [pscustomobject]@{ ExitCode = $Handle.Process.ExitCode; Lines = @($text -split "`r?`n" | Where-Object { $_ }) }
}

# Stops a step of Start-AdoGateProcess, with its children, if it still runs.
function Stop-AdoGateProcess {
    param([Parameter(Mandatory = $true)][object] $Handle)

    if (-not $Handle.Process.HasExited) { $Handle.Process.Kill($true) }
    $Handle.Process.Dispose()
}
