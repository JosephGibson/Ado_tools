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

# Runs a PowerShell child that writes its result lines to the file named by
# ADOTOOLKIT_GATE_REPORT, and returns only those lines. A cmdlet under test prints its
# "What if:" text straight to the host, where no redirection inside the child reaches it; kept
# out of the gate's output, such text can no longer be mistaken for a warning.
function Invoke-AdoReportingChild {
    param(
        [Parameter(Mandatory = $true)][string] $Script,
        [Parameter(Mandatory = $true)][string] $ReportPath
    )

    $previous = $env:ADOTOOLKIT_GATE_REPORT
    try {
        $env:ADOTOOLKIT_GATE_REPORT = $ReportPath
        $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($Script))
        # Text output: with the default for an encoded command, PowerShell rebuilds the child's
        # warning records in this process and writes them to its host, past any redirection.
        $childArguments = @('-NoProfile', '-OutputFormat', 'Text', '-EncodedCommand', $encoded)
        $hostText = @(& (Join-Path $PSHOME 'pwsh.exe') @childArguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally { $env:ADOTOOLKIT_GATE_REPORT = $previous }

    if (Test-Path -LiteralPath $ReportPath -PathType Leaf) {
        return [pscustomobject]@{ ExitCode = $exitCode; Lines = @([System.IO.File]::ReadAllLines($ReportPath)) }
    }
    # Without a report the child stopped early; its last output is the only explanation.
    return [pscustomobject]@{
        ExitCode = if ($exitCode -eq 0) { 1 } else { $exitCode }
        Lines = @('The child process wrote no report.') + @($hostText | Select-Object -Last 20 | ForEach-Object { [string] $_ })
    }
}
