Describe 'tools/dev.ps1 plan and stages' {
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

    It 'plans lint, test, and configuration stages for PowerShell tooling, and skips tests on request' {
        $repository = Join-Path $TestDrive 'powershell-plan-sample'
        New-TestFile -Path (Join-Path $repository 'tools\dev.ps1')
        New-TestFile -Path (Join-Path $repository 'settings.json') -Content '{}'
        New-TestFile -Path (Join-Path $repository 'tools\tests\app.Tests.ps1')
        # Product Pester files run against the staged module in the product gate, not here.
        New-TestFile -Path (Join-Path $repository 'tests\Shell\Module.Pester.ps1')
        $projectProfile = Get-ProjectProfile -Root $repository

        $plan = @(Get-BuiltinValidationPlan -ProjectProfile $projectProfile)

        $plan.Name | Should -Be @('powershell-lint', 'powershell-test', 'configuration')
        ($plan | Where-Object Name -eq 'powershell-test').ActionArguments.TestPath | Should -Be @(Join-Path $repository 'tools\tests\app.Tests.ps1')
        @(Get-BuiltinValidationPlan -ProjectProfile $projectProfile -SkipTests).Name | Should -Be @('powershell-lint', 'configuration')
    }

    It 'plans no stages for a repository with nothing to validate' {
        $repository = Join-Path $TestDrive 'empty-plan-sample'
        New-TestFile -Path (Join-Path $repository 'notes.txt') -Content 'nothing to build'
        $projectProfile = Get-ProjectProfile -Root $repository

        @(Get-ValidationPlan -ProjectProfile $projectProfile).Count | Should -Be 0
    }

    It 'reports product code without its gate as incomplete instead of passing on the built-in stages' {
        $repository = Join-Path $TestDrive 'missing-gate-sample'
        New-TestFile -Path (Join-Path $repository 'src\App\App.csproj') -Content '<Project Sdk="Microsoft.NET.Sdk" />'

        $result = Invoke-ProjectVerification -Root $repository

        $result.Stages.Name | Should -Be @('configuration', 'project-check')
        ($result.Stages | Where-Object Name -eq 'project-check').Status | Should -Be 'unavailable'
        ($result.Stages | Where-Object Name -eq 'project-check').Summary | Should -BeLike '*tools/check.ps1*'
        $result.Status | Should -Be 'incomplete'
    }

    It 'fails the configuration stage on malformed JSON and passes on valid JSON' {
        $repository = Join-Path $TestDrive 'configuration-sample'
        New-TestFile -Path (Join-Path $repository 'good.json') -Content '{"a":1}'
        $projectProfile = Get-ProjectProfile -Root $repository
        $passing = Get-ConfigurationOutcome -ProjectProfile $projectProfile

        New-TestFile -Path (Join-Path $repository 'bad.json') -Content '{ invalid json }'
        $failing = Get-ConfigurationOutcome -ProjectProfile (Get-ProjectProfile -Root $repository)

        $passing.Failures.Count | Should -Be 0
        $failing.Failures.Count | Should -Be 1
        $failing.Failures[0] | Should -BeLike 'bad.json*'
    }

    It 'reports a PowerShell parse error as a stage failure' {
        $repository = Join-Path $TestDrive 'lint-sample'
        New-TestFile -Path (Join-Path $repository 'broken.ps1') -Content 'function Incomplete {'
        $projectProfile = Get-ProjectProfile -Root $repository

        $outcome = Get-PowerShellLintOutcome -ProjectProfile $projectProfile

        $outcome.Failures.Count | Should -BeGreaterThan 0
        $outcome.Failures[0] | Should -BeLike 'broken.ps1:*'
    }

    It 'reports analyzer findings by file and line, in the order of the files' {
        # Without the analyzer the lint stage is unavailable, and this test incomplete with it.
        if (@(Get-BuildModule -Name PSScriptAnalyzer).Count -eq 0) { Set-ItResult -Skipped -Because 'PSScriptAnalyzer is not installed' }
        $repository = Join-Path $TestDrive 'analyzer-sample'
        New-TestFile -Path (Join-Path $repository 'first.ps1') -Content ("Set-StrictMode -Version 2.0`n" + 'gci')
        New-TestFile -Path (Join-Path $repository 'second.ps1') -Content 'gci'

        $outcome = Get-PowerShellLintOutcome -ProjectProfile (Get-ProjectProfile -Root $repository)

        $outcome.Failures | Should -Be @('first.ps1:2 PSAvoidUsingCmdletAliases', 'second.ps1:1 PSAvoidUsingCmdletAliases')
    }

    It 'runs an in-process stage and reports its outcome' {
        $stage = [pscustomobject]@{
            Name = 'in-process'
            Action = { [pscustomobject]@{ Failures = @('one problem'); Summary = @('done'); Warnings = @('a warning') } }
        }

        $result = Invoke-ValidationStage -Stage $stage -Root $TestDrive

        $result.Status | Should -Be 'fail'
        $result.ExitCode | Should -Be 1
        $result.Summary | Should -Be @('one problem', 'done')
        $result.Warnings | Should -Be @('a warning')
    }

    It 'reports an unavailable in-process stage without calling it a pass' {
        $stage = [pscustomobject]@{
            Name = 'in-process'
            Action = { [pscustomobject]@{ Failures = @(); Summary = @('tool unavailable'); Warnings = @('tool unavailable'); Unavailable = $true } }
        }

        $result = Invoke-ValidationStage -Stage $stage -Root $TestDrive

        $result.Status | Should -Be 'unavailable'
        $result.ExitCode | Should -Be 2
        $result.Summary | Should -Be @('tool unavailable')
    }

    It 'marks a missing Pester runner as unavailable' {
        Mock Get-Module { @() } -ParameterFilter { $Name -eq 'Pester' }

        $outcome = Get-PesterOutcome -TestPath @($TestDrive)

        $outcome.Failures | Should -BeNullOrEmpty
        $outcome.Unavailable | Should -BeTrue
        $outcome.Warnings | Should -Be @('Pester 5.x is required (run bootstrap -Install).')
    }

    It 'runs each test file in a process of its own and merges what they report, printing nothing' {
        $folder = Join-Path $TestDrive 'pester-processes'
        # The largest file starts first; one process at a time, the second file would see the
        # variable if both ran in one process.
        New-TestFile -Path (Join-Path $folder 'Sets.Tests.ps1') -Content @"
# $('padding ' * 40)
Describe 'Sets' { It 'sets a variable and prints' { `$env:ADOTOOLKIT_RUNNER_PROBE = 'set'; Write-Host 'printed'; [Console]::Out.WriteLine('written') } }
"@
        New-TestFile -Path (Join-Path $folder 'Reads.Tests.ps1') -Content @'
Describe 'Reads' {
    It 'does not see the variable' { $env:ADOTOOLKIT_RUNNER_PROBE | Should -BeNullOrEmpty }
    It 'fails' { 1 | Should -Be 2 }
}
'@
        New-TestFile -Path (Join-Path $folder 'Broken.Tests.ps1') -Content "Describe 'Broken' { throw 'discovery failed' }"
        # A process that ends without its report must fail, not drop out of the counts.
        New-TestFile -Path (Join-Path $folder 'Exits.Tests.ps1') -Content "Describe 'Exits' { It 'leaves the process' { [Environment]::Exit(0) } }"

        $streams = @(Invoke-PesterProcess -TestPath @($folder) -PesterManifest (Join-Path (Get-Module -Name Pester).ModuleBase 'Pester.psd1') -ThrottleLimit 1 *>&1)

        $streams.Count | Should -Be 1
        $result = $streams[0]
        $result.Result | Should -Be 'Failed'
        @($result.TotalCount, $result.PassedCount, $result.FailedCount) | Should -Be @(3, 2, 1)
        $result.Failed.ExpandedPath | Should -Be @('Reads.fails')
        $result.Failed[0].Message | Should -BeLike 'Expected 2, but got 1.*'
        @($result.Containers).Count | Should -Be 2
        $result.Containers[0].Item | Should -Be (Join-Path $folder 'Broken.Tests.ps1')
        $result.Containers[0].Messages | Should -Be @('discovery failed')
        $result.Containers[1].Item | Should -Be (Join-Path $folder 'Exits.Tests.ps1')
        $result.Containers[1].Messages | Should -Be @('The test process exited with code 0 and wrote no result.')
        $env:ADOTOOLKIT_RUNNER_PROBE | Should -BeNullOrEmpty
    }

    # "2 incomplete and never a pass": the Core path proves every discovered test ran, so the
    # Pester path must too. Both cases hide in an aggregate: a file that discovers nothing adds
    # zero to the sum, and an inconclusive test is counted as discovered but neither passed nor failed.
    It 'reports a Pester run as incomplete when <Case>' -TestCases @(
        @{ Case = 'one file discovers no tests'
            Content = 'if ($false) { Describe ''Never'' { It ''skipped by discovery'' { 1 | Should -Be 1 } } }' }
        @{ Case = 'a test is inconclusive'
            Content = 'Describe ''Probe'' { It ''is inconclusive'' { Set-ItResult -Inconclusive -Because ''a live server is needed'' } }' }
    ) {
        param($Case, $Content)
        $folder = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-TestFile -Path (Join-Path $folder 'Reads.Tests.ps1') -Content "Describe 'Reads' { It 'passes' { 1 | Should -Be 1 } }"
        New-TestFile -Path (Join-Path $folder 'Quiet.Tests.ps1') -Content $Content

        $outcome = Get-PesterOutcome -TestPath @($folder)

        $outcome.Failures | Should -BeNullOrEmpty
        $outcome.Unavailable | Should -BeTrue
        $outcome.Warnings | Should -Not -BeNullOrEmpty
    }

    It 'stops a process that runs past its limit and reports the timeout' {
        $watch = [System.Diagnostics.Stopwatch]::StartNew()

        $results = Invoke-PowerShellProcess -Program @('Start-Sleep -Seconds 60') -TimeoutSeconds 1

        $watch.Elapsed.TotalSeconds | Should -BeLessThan 30
        $results[0].TimedOut | Should -BeTrue
        Get-PowerShellProcessFailure -Result $results[0] -Activity 'test' | Should -Be 'The test process timed out.'
    }

    It 'marks a missing analyzer as unavailable after parsing' {
        $repository = Join-Path $TestDrive 'missing-analyzer-sample'
        New-TestFile -Path (Join-Path $repository 'app.ps1') -Content 'Write-Output ready'
        Mock Get-Module { @() } -ParameterFilter { $ListAvailable -and $Name -eq 'PSScriptAnalyzer' }

        $outcome = Get-PowerShellLintOutcome -ProjectProfile (Get-ProjectProfile -Root $repository)

        $outcome.Failures | Should -BeNullOrEmpty
        $outcome.Unavailable | Should -BeTrue
        $outcome.Warnings | Should -Be @('The required PSScriptAnalyzer version is not installed (run bootstrap -Install).')
    }

    It 'plans the built-in tooling stages before the product gate' {
        $repository = Join-Path $TestDrive 'owned-check-sample'
        New-TestFile -Path (Join-Path $repository 'settings.json') -Content '{"name":"sample"}'
        New-TestFile -Path (Join-Path $repository 'tools\check.ps1') -Content 'exit 0'
        $projectProfile = Get-ProjectProfile -Root $repository

        $plan = @(Get-ValidationPlan -ProjectProfile $projectProfile)

        $plan.Name | Should -Be @('powershell-lint', 'configuration', 'tooling-layout', 'project-check')
        ($plan | Where-Object Name -eq 'project-check').TimeoutSeconds | Should -BeGreaterThan 300
        (@(Get-ValidationPlan -ProjectProfile $projectProfile -SkipTests) | Where-Object Name -eq 'project-check').Arguments | Should -Contain '-SkipTests'
    }

    # What a stage process printed reaches the reduction only as lines, so these two give the lines
    # directly; tools/tests/Workflow.Tests.ps1 captures the lines of real processes.
    It 'preserves skipped-tool warnings when reducing successful stage output' {
        Mock Invoke-BoundedProcess { [pscustomobject]@{ ExitCode = 0; Lines = @('skipped (PSScriptAnalyzer is not installed)') } }
        $stage = [pscustomobject]@{ Name = 'sample'; Executable = Join-Path $PSHOME 'pwsh.exe'; Arguments = @('-NoProfile') }

        $result = Invoke-ValidationStage -Stage $stage -Root $TestDrive

        Should -Invoke Invoke-BoundedProcess -Times 1 -Exactly
        $result.Status | Should -Be 'pass'
        $result.Warnings | Should -Be @('skipped (PSScriptAnalyzer is not installed)')
    }

    It 'strips terminal colour from captured stage output' {
        Mock Invoke-BoundedProcess { [pscustomobject]@{ ExitCode = 0; Lines = @("$([char] 27)[32mCHECK PASSED$([char] 27)[0m") } }
        $stage = [pscustomobject]@{ Name = 'coloured'; Executable = Join-Path $PSHOME 'pwsh.exe'; Arguments = @('-NoProfile') }

        $result = Invoke-ValidationStage -Stage $stage -Root $TestDrive

        Should -Invoke Invoke-BoundedProcess -Times 1 -Exactly
        $result.Status | Should -Be 'pass'
        $result.Summary | Should -Be @('CHECK PASSED')
    }

    It 'keeps configured Claude hook helpers under tools and every skill paired' {
        $repository = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
        $outcome = Get-ToolingLayoutOutcome -ProjectProfile (Get-ProjectProfile -Root $repository)

        $outcome.Failures | Should -BeNullOrEmpty
        $outcome.Summary | Should -Be @('3 configured PowerShell hook helper(s) checked', '4 skill(s) paired between .agents and .claude', '3 subagent(s) named after their files')
    }

    It 'rejects hook helper paths that traverse out of tools' {
        $repository = Join-Path $TestDrive 'hook-traversal-sample'
        New-TestFile -Path (Join-Path $repository 'escaped.ps1') -Content 'Write-Output escaped'
        New-TestFile -Path (Join-Path $repository '.claude\settings.json') -Content @'
{
  "hooks": {
    "PostToolUse": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "pwsh.exe",
            "args": ["${CLAUDE_PROJECT_DIR}/tools/../escaped.ps1"]
          }
        ]
      }
    ]
  }
}
'@

        $outcome = Get-ToolingLayoutOutcome -ProjectProfile (Get-ProjectProfile -Root $repository)

        $outcome.Failures | Should -Be @('Hook helper ''${CLAUDE_PROJECT_DIR}/tools/../escaped.ps1'' must resolve under tools/.')
    }

    It 'passes a planned stage its arguments instead of capturing them' {
        $repository = Join-Path $TestDrive 'stage-arguments-sample'
        New-TestFile -Path (Join-Path $repository 'app.json') -Content '{"a":1}'
        $projectProfile = Get-ProjectProfile -Root $repository
        $stage = @(Get-BuiltinValidationPlan -ProjectProfile $projectProfile) |
            Where-Object { $_.Name -eq 'configuration' }

        $result = Invoke-ValidationStage -Stage $stage -Root $repository

        $result.Status | Should -Be 'pass'
        $result.Summary | Should -Be @('1 configuration file(s) validated')
    }
}
