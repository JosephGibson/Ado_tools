# PowerShell work that runs in child processes, side by side: the Pester runs of the powershell-test
# stage and of the product gate. Callers enable strict mode and terminating errors.

# Runs each program in a pwsh process of its own, at most ThrottleLimit at once, started in the
# order given, and returns one result per program in that order: ExitCode, TimedOut and the text of
# its error stream. Environment entries apply to the processes alone, and a $null value removes one.
# What a process prints, "What if:" text included, never reaches the caller: a program reports
# through a file of its own.
function Invoke-PowerShellProcess {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]] $Program,
        [hashtable] $Environment = @{},
        [ValidateRange(1, 64)][int] $ThrottleLimit = [Math]::Min(64, [Environment]::ProcessorCount),
        [ValidateRange(1, 3600)][int] $TimeoutSeconds = 600
    )

    $results = New-Object 'object[]' $Program.Count
    $next = 0
    $running = [System.Collections.Generic.List[object]]::new()
    try {
        while ($next -lt $Program.Count -or $running.Count -gt 0) {
            while ($next -lt $Program.Count -and $running.Count -lt $ThrottleLimit) {
                $info = [System.Diagnostics.ProcessStartInfo]::new((Join-Path $PSHOME 'pwsh.exe'))
                foreach ($argument in @('-NoProfile', '-NonInteractive', '-OutputFormat', 'Text', '-EncodedCommand', [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($Program[$next])))) {
                    $info.ArgumentList.Add($argument)
                }
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
                $process = [System.Diagnostics.Process]::Start($info)
                $process.StandardInput.Close()
                # Both pipes are drained as the process writes, so it never waits on a full pipe.
                $running.Add([pscustomobject]@{
                        Index = $next; Process = $process; Watch = [System.Diagnostics.Stopwatch]::StartNew()
                        Output = $process.StandardOutput.ReadToEndAsync(); Errors = $process.StandardError.ReadToEndAsync()
                    })
                $next++
            }
            $finished = @($running | Where-Object { $_.Process.HasExited -or $_.Watch.Elapsed.TotalSeconds -ge $TimeoutSeconds })
            if ($finished.Count -eq 0) {
                [void] [System.Threading.Tasks.Task]::WaitAny([System.Threading.Tasks.Task[]] @($running | ForEach-Object { $_.Process.WaitForExitAsync() }), 1000)
                continue
            }
            foreach ($entry in $finished) {
                [void] $running.Remove($entry)
                $timedOut = -not $entry.Process.HasExited
                if ($timedOut) { $entry.Process.Kill($true) }
                $entry.Process.WaitForExit()
                [void] $entry.Output.GetAwaiter().GetResult()
                $results[$entry.Index] = [pscustomobject]@{
                    ExitCode = $entry.Process.ExitCode; TimedOut = $timedOut; Errors = $entry.Errors.GetAwaiter().GetResult()
                }
                $entry.Process.Dispose()
            }
        }
    }
    finally {
        foreach ($entry in $running) {
            if (-not $entry.Process.HasExited) { $entry.Process.Kill($true) }
            $entry.Process.Dispose()
        }
    }
    return , $results
}

# Why a process wrote no report, for a failure message.
function Get-PowerShellProcessFailure {
    param([Parameter(Mandatory = $true)][object] $Result, [Parameter(Mandatory = $true)][string] $Activity)

    if ($Result.TimedOut) { return "The $Activity process timed out." }
    $last = @($Result.Errors -split "`r?`n" | Where-Object { $_ }) | Select-Object -Last 1
    return ("The $Activity process exited with code $($Result.ExitCode) and wrote no result. $last").Trim()
}

# Quotes text for a single-quoted PowerShell string inside a program. PowerShell reads the
# typographic quotes U+2018 to U+201B as single quotes too, so a path with one is escaped as well.
function ConvertTo-PowerShellLiteral {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text)

    return "'" + [System.Management.Automation.Language.CodeGeneration]::EscapeSingleQuotedStringContent($Text) + "'"
}

# Runs each test file in a process of its own and merges the results in the order of the files. A
# file may change $env: and the location: in its process that touches no other file. Containers are
# reported only with their errors, as a failed test fails its container too.
function Invoke-PesterProcess {
    param(
        [Parameter(Mandatory = $true)][string[]] $TestPath,
        [Parameter(Mandatory = $true)][string] $PesterManifest,
        [ValidatePattern('^.[A-Za-z]+.ps1$')][string] $TestExtension = '.Tests.ps1',
        [hashtable] $Environment = @{},
        [ValidateRange(1, 64)][int] $ThrottleLimit = [Math]::Min(64, [Environment]::ProcessorCount)
    )

    $files = @(foreach ($item in $TestPath) {
            if (Test-Path -LiteralPath $item -PathType Container) {
                Get-ChildItem -LiteralPath $item -Recurse -File -Filter ('*' + $TestExtension) | Sort-Object FullName | ForEach-Object FullName
            }
            else { [System.IO.Path]::GetFullPath($item) }
        })
    # The largest files start first, so that no long file is left to run alone at the end.
    $started = @($files | Sort-Object { (Get-Item -LiteralPath $_).Length } -Descending)
    $folder = Join-Path ([System.IO.Path]::GetTempPath()) ('AdoToolkit-pester-' + [guid]::NewGuid().ToString('N'))
    [void] [System.IO.Directory]::CreateDirectory($folder)
    try {
        $reports = @($started | ForEach-Object { Join-Path $folder ([guid]::NewGuid().ToString('N') + '.json') })
        $programs = @(for ($index = 0; $index -lt $started.Count; $index++) {
                @"
Set-StrictMode -Version 2.0
`$ErrorActionPreference = 'Stop'
`$ProgressPreference = 'SilentlyContinue'
Import-Module -Name $(ConvertTo-PowerShellLiteral $PesterManifest)
`$configuration = New-PesterConfiguration
`$configuration.Run.Path = $(ConvertTo-PowerShellLiteral $started[$index])
`$configuration.Run.TestExtension = $(ConvertTo-PowerShellLiteral $TestExtension)
`$configuration.Run.PassThru = `$true
`$configuration.Run.Exit = `$false
`$configuration.Run.Throw = `$false
`$configuration.Output.Verbosity = 'None'
`$result = Invoke-Pester -Configuration `$configuration
`$summary = [ordered]@{
    Result = [string] `$result.Result; TotalCount = `$result.TotalCount; PassedCount = `$result.PassedCount
    FailedCount = `$result.FailedCount; SkippedCount = `$result.SkippedCount; NotRunCount = `$result.NotRunCount
    InconclusiveCount = `$result.InconclusiveCount
    Failed = @(foreach (`$test in `$result.Failed) { [ordered]@{ ExpandedPath = `$test.ExpandedPath; Message = [string] `$test.ErrorRecord.Exception.Message } })
    Containers = @(foreach (`$container in @(`$result.Containers | Where-Object { `$_.Result -eq 'Failed' -and @(`$_.ErrorRecord).Count -gt 0 })) {
            [ordered]@{ Item = [string] `$container.Item; Result = 'Failed'; Messages = @(foreach (`$record in @(`$container.ErrorRecord)) { [string] `$record.Exception.Message }) } })
}
[System.IO.File]::WriteAllText($(ConvertTo-PowerShellLiteral $reports[$index]), (ConvertTo-Json -InputObject `$summary -Depth 6 -Compress), [System.Text.UTF8Encoding]::new(`$false))
"@
            })
        $results = Invoke-PowerShellProcess -Program $programs -Environment $Environment -ThrottleLimit $ThrottleLimit
        $runs = @{}
        for ($index = 0; $index -lt $started.Count; $index++) {
            $summary = $null
            if (-not $results[$index].TimedOut -and (Test-Path -LiteralPath $reports[$index] -PathType Leaf)) {
                $summary = [System.IO.File]::ReadAllText($reports[$index]) | ConvertFrom-Json
            }
            if ($null -eq $summary) {
                $summary = [pscustomobject]@{
                    Result = 'Failed'; TotalCount = 0; PassedCount = 0; FailedCount = 0; SkippedCount = 0; NotRunCount = 0
                    InconclusiveCount = 0; Failed = @()
                    Containers = @([pscustomobject]@{ Item = $started[$index]; Result = 'Failed'; Messages = @(Get-PowerShellProcessFailure -Result $results[$index] -Activity 'test') })
                }
            }
            $runs[$started[$index]] = $summary
        }
    }
    finally { if (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force } }

    $ordered = @(foreach ($file in $files) { $runs[$file] })
    $sum = { param([string] $Name) [int] (($ordered | ForEach-Object { [int] $_.$Name } | Measure-Object -Sum).Sum) }
    return [pscustomobject]@{
        Result = if (@($ordered | Where-Object { $_.Result -eq 'Failed' }).Count -gt 0) { 'Failed' } else { 'Passed' }
        TotalCount = & $sum 'TotalCount'
        PassedCount = & $sum 'PassedCount'
        FailedCount = & $sum 'FailedCount'
        SkippedCount = & $sum 'SkippedCount'
        NotRunCount = & $sum 'NotRunCount'
        InconclusiveCount = & $sum 'InconclusiveCount'
        # Completeness is per file, as the Core runs check it per run: a file that discovered nothing
        # adds zero to every sum, and an inconclusive test is discovered but neither passed nor failed.
        Incomplete = @($ordered | Where-Object {
                [int] $_.TotalCount -eq 0 -or ([int] $_.PassedCount + [int] $_.FailedCount) -ne [int] $_.TotalCount
            }).Count -gt 0
        Failed = @($ordered | ForEach-Object { @($_.Failed) })
        Containers = @($ordered | ForEach-Object { @($_.Containers) })
    }
}
