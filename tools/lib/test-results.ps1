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
        Summary = 'Core tests: {0} passed, {1} failed, {2} executed, {3} discovered.' -f
            $counts.passed, $counts.failed, $counts.executed, $counts.total
    }
}
