Describe 'tools/dev.ps1 verify' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\dev.ps1')

        function New-TestFile {
            param(
                [Parameter(Mandatory = $true)][string] $Path,
                [AllowEmptyString()][string] $Content = ''
            )

            $parent = Split-Path -Parent $Path
            if (-not (Test-Path -LiteralPath $parent)) {
                [void] (New-Item -ItemType Directory -Path $parent -Force)
            }
            Set-Content -LiteralPath $Path -Value $Content -Encoding utf8
        }
    }

    It 'summarizes a passing gate by the result lines it marks' {
        $repository = Join-Path $TestDrive 'marked-summary-sample'
        New-TestFile -Path (Join-Path $repository 'tools\check.ps1') -Content @'
Write-Output 'Determining projects to restore...'
Write-Output 'check: build succeeded (Release)'
Write-Output 'Passed!  - Failed: 0, Passed: 3'
Write-Output 'check: Core tests (en-US): 3 passed, 0 failed, 3 executed, 3 discovered'
Write-Output 'Package ready'
exit 0
'@
        # The lint stage is not the subject, and it is unavailable where no analyzer is installed.
        Mock Get-PowerShellLintOutcome { [pscustomobject]@{ Failures = @(); Summary = @(); Warnings = @() } }

        $result = Invoke-ProjectVerification -Root $repository

        $result.Status | Should -Be 'pass'
        ($result.Stages | Where-Object Name -eq 'project-check').Summary |
            Should -Be @('build succeeded (Release)', 'Core tests (en-US): 3 passed, 0 failed, 3 executed, 3 discovered')
        $result.Warnings | Should -BeNullOrEmpty
    }

    # project-check runs in a runspace of its own while the in-process stages run in this one.
    It 'reports the product gate failing while an in-process stage is still running' {
        $repository = Join-Path $TestDrive 'concurrent-failure'
        $gateStarted = Join-Path $TestDrive 'concurrent-failure-started.txt'
        $gateReleased = Join-Path $TestDrive 'concurrent-failure-released.txt'
        New-TestFile -Path (Join-Path $repository 'settings.json') -Content '{}'
        New-TestFile -Path (Join-Path $repository 'tools\check.ps1') -Content @"
Set-Content -LiteralPath '$gateStarted' -Value started
`$deadline = [DateTime]::UtcNow.AddSeconds(30)
while (-not (Test-Path -LiteralPath '$gateReleased') -and [DateTime]::UtcNow -lt `$deadline) { Start-Sleep -Milliseconds 50 }
Write-Output 'check: synthetic failure'
exit 1
"@
        Mock Get-PowerShellLintOutcome { [pscustomobject]@{ Failures = @(); Summary = @(); Warnings = @() } }
        # The configuration stage waits for the gate to start, releases it, and is still running
        # when the gate fails. Run one after the other, it would wait in vain.
        Mock Get-ConfigurationOutcome {
            $deadline = [DateTime]::UtcNow.AddSeconds(30)
            while (-not (Test-Path -LiteralPath $gateStarted) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 50 }
            if (-not (Test-Path -LiteralPath $gateStarted)) {
                return [pscustomobject]@{ Failures = @('the product gate did not start beside this stage'); Summary = @(); Warnings = @() }
            }
            Set-Content -LiteralPath $gateReleased -Value released
            Start-Sleep -Seconds 2
            [pscustomobject]@{ Failures = @(); Summary = @('ran beside the product gate'); Warnings = @() }
        }

        $result = Invoke-ProjectVerification -Root $repository

        $result.Status | Should -Be 'fail'
        $result.Stages.Name | Should -Be @('powershell-lint', 'configuration', 'tooling-layout', 'project-check')
        ($result.Stages | Where-Object Name -eq 'configuration').Status | Should -Be 'pass'
        ($result.Stages | Where-Object Name -eq 'configuration').Summary | Should -Be @('ran beside the product gate')
        $gate = $result.Stages | Where-Object Name -eq 'project-check'
        $gate.Status | Should -Be 'fail'
        $gate.ExitCode | Should -Be 1
        $gate.Summary | Should -Contain 'check: synthetic failure'
        @($result.Stages | Where-Object { $null -eq $_.DurationMs }) | Should -BeNullOrEmpty
    }

    It 'stops the process tree of a stage that times out while an in-process stage is running' {
        $folder = Join-Path $TestDrive 'concurrent-timeout'
        $marker = Join-Path $folder 'grandchild.txt'
        $slowScript = Join-Path $folder 'slow.ps1'
        New-TestFile -Path (Join-Path $folder 'sample.json') -Content '{}'
        # The stage starts a process of its own, writes its ID, and outlives the timeout.
        New-TestFile -Path $slowScript -Content @'
param([string] $Marker)
$grandchild = Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep -Seconds 120') -NoNewWindow -PassThru
Set-Content -LiteralPath $Marker -Value $grandchild.Id
Start-Sleep -Seconds 120
'@
        # The in-process stage returns only once it has seen that process stopped.
        Mock Get-ValidationPlan {
            @(
                [pscustomobject]@{
                    Name = 'waiting'
                    Action = {
                        param([string] $Marker)
                        $deadline = [DateTime]::UtcNow.AddSeconds(60)
                        $id = 0
                        while ($id -eq 0 -and [DateTime]::UtcNow -lt $deadline) {
                            if (Test-Path -LiteralPath $Marker) { [void] [int]::TryParse((Get-Content -LiteralPath $Marker -Raw -ErrorAction SilentlyContinue), [ref] $id) }
                            if ($id -eq 0) { Start-Sleep -Milliseconds 50 }
                        }
                        if ($id -eq 0) { return [pscustomobject]@{ Failures = @('the slow stage did not start beside this stage'); Summary = @(); Warnings = @() } }
                        while ($null -ne (Get-Process -Id $id -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
                        if ($null -ne (Get-Process -Id $id -ErrorAction SilentlyContinue)) {
                            return [pscustomobject]@{ Failures = @('the slow stage was still running'); Summary = @(); Warnings = @() }
                        }
                        [pscustomobject]@{ Failures = @(); Summary = @('saw the slow stage stopped'); Warnings = @() }
                    }
                    ActionArguments = @{ Marker = $marker }
                }
                [pscustomobject]@{ Name = 'slow'; Executable = Join-Path $PSHOME 'pwsh.exe'; Arguments = @('-NoProfile', '-File', $slowScript, $marker); TimeoutSeconds = 8 }
            )
        }

        try { $result = Invoke-ProjectVerification -Root $folder }
        finally {
            $id = 0
            if (Test-Path -LiteralPath $marker) { [void] [int]::TryParse((Get-Content -LiteralPath $marker -Raw), [ref] $id) }
            if ($id -gt 0) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue }
        }

        $result.Status | Should -Be 'fail'
        $result.Stages.Name | Should -Be @('waiting', 'slow')
        ($result.Stages | Where-Object Name -eq 'waiting').Status | Should -Be 'pass'
        ($result.Stages | Where-Object Name -eq 'waiting').Summary | Should -Be @('saw the slow stage stopped')
        $slow = $result.Stages | Where-Object Name -eq 'slow'
        $slow.Status | Should -Be 'fail'
        $slow.Summary | Should -Be @('Stage timed out after 8 seconds.')
        $slow.DurationMs | Should -BeGreaterOrEqual 8000
    }

    It 'reads a repository check script exit code 2 as incomplete, not failed' {
        $repository = Join-Path $TestDrive 'check-contract-sample'
        New-TestFile -Path (Join-Path $repository 'tools\check.ps1') -Content @'
Write-Output 'analyzer is not installed'
exit 2
'@

        $result = Invoke-ProjectVerification -Root $repository

        ($result.Stages | Where-Object Name -eq project-check).Status | Should -Be 'unavailable'
        $result.Status | Should -Be 'incomplete'
    }

    It 'reports scraped output from a passing stage without failing the run' {
        $repository = Join-Path $TestDrive 'advisory-warning-sample'
        New-TestFile -Path (Join-Path $repository 'tools\check.ps1') -Content @'
Write-Output 'note: 1 missing peer dependency'
exit 0
'@
        # The lint stage is not the subject, and it is unavailable where no analyzer is installed.
        Mock Get-PowerShellLintOutcome { [pscustomobject]@{ Failures = @(); Summary = @(); Warnings = @() } }

        $result = Invoke-ProjectVerification -Root $repository

        $result.Status | Should -Be 'pass'
        $result.Warnings | Should -Be @('project-check: note: 1 missing peer dependency')
    }
}
