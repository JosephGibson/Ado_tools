Describe 'tools/dev.ps1' {
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

    It 'detects supported stacks and only identifies actual test files' {
        $repository = Join-Path $TestDrive 'profile-sample'
        New-TestFile -Path (Join-Path $repository 'app.ps1')
        New-TestFile -Path (Join-Path $repository 'package.json') -Content '{"scripts":{"test":"node --test"}}'
        New-TestFile -Path (Join-Path $repository 'pyproject.toml') -Content @'
[project]
name = "sample"
'@
        New-TestFile -Path (Join-Path $repository 'tests\app.Tests.ps1')

        $projectProfile = Get-ProjectProfile -Root $repository

        $projectProfile.Stack | Should -Be @('node', 'powershell', 'python')
        $projectProfile.TestFiles | Should -Be @('tests/app.Tests.ps1')
        $projectProfile.Entrypoints | Should -Be @('app.ps1')
    }

    It 'does not count placeholders or fixtures under tests/ as tests' {
        $repository = Join-Path $TestDrive 'placeholder-sample'
        New-TestFile -Path (Join-Path $repository 'tests\.gitkeep')
        New-TestFile -Path (Join-Path $repository 'tests\fixtures\payload.json') -Content '{}'

        $projectProfile = Get-ProjectProfile -Root $repository

        $projectProfile.TestFiles.Count | Should -Be 0
    }

    It 'finds source while excluding secret and log files' {
        $repository = Join-Path $TestDrive 'find-sample'
        New-TestFile -Path (Join-Path $repository 'src\worker.ps1') -Content 'Invoke-Widget'
        New-TestFile -Path (Join-Path $repository '.env') -Content 'Invoke-Widget'
        New-TestFile -Path (Join-Path $repository 'secrets\notes.txt') -Content 'Invoke-Widget'
        New-TestFile -Path (Join-Path $repository 'debug.log') -Content 'Invoke-Widget'

        $result = Find-ProjectSource -Query 'Invoke-Widget' -Root $repository

        $result.Count | Should -Be 1
        $result.Results.Path | Should -Be @('src/worker.ps1')
    }

    It 'rejects sensitive path components beneath the repository root' {
        $repository = Join-Path $TestDrive 'safe-path-sample'

        Test-IsAgentSafePath -Path (Join-Path $repository 'secrets\nested\notes.txt') -Root $repository | Should -BeFalse
        Test-IsAgentSafePath -Path (Join-Path $repository '.env.local\nested\values.json') -Root $repository | Should -BeFalse
        Test-IsAgentSafePath -Path (Join-Path $repository 'src\notes.txt') -Root $repository | Should -BeTrue
    }

    It 'finds safe content in hidden repository configuration' {
        $repository = Join-Path $TestDrive 'hidden-find-sample'
        New-TestFile -Path (Join-Path $repository '.claude\rules\workflow.md') -Content 'Use deterministic discovery.'

        $result = Find-ProjectSource -Query 'deterministic discovery' -Root $repository

        $result.Count | Should -Be 1
        $result.Results.Path | Should -Be @('.claude/rules/workflow.md')
    }

    It 'reports the search pass that actually produced the results' {
        $repository = Join-Path $TestDrive 'tool-label-sample'
        New-TestFile -Path (Join-Path $repository 'src\widget-notes.md') -Content 'unrelated body text'

        $result = Find-ProjectSource -Query 'widget-notes' -Root $repository -Limit 1

        $result.Tool | Should -Be 'path'
        $result.Results.Match | Should -Be @('path')
    }

    It 'does not enumerate excluded dependency directories' {
        $repository = Join-Path $TestDrive 'excluded-sample'
        New-TestFile -Path (Join-Path $repository 'src\worker.ps1')
        New-TestFile -Path (Join-Path $repository 'node_modules\package\ignored.ps1')
        New-TestFile -Path (Join-Path $repository 'bin\generated.ps1')

        $files = Get-RepositoryFiles -Root $repository
        $paths = @($files | ForEach-Object { Get-RelativeRepositoryPath -Path $_.FullName -Root $repository })

        $paths | Should -Be @('src/worker.ps1')
    }

    It 'falls back to pruned filesystem discovery when ripgrep is unavailable' {
        $repository = Join-Path $TestDrive 'fallback-sample'
        New-TestFile -Path (Join-Path $repository 'src\worker.ps1')
        New-TestFile -Path (Join-Path $repository 'node_modules\package\ignored.ps1')
        Mock Get-FirstCommand { $null } -ParameterFilter { $Names -contains 'rg' }

        $files = Get-RepositoryFiles -Root $repository
        $paths = @($files | ForEach-Object { Get-RelativeRepositoryPath -Path $_.FullName -Root $repository })

        $paths | Should -Be @('src/worker.ps1')
    }

    It 'normalizes a relative repository root' {
        $repository = Join-Path $TestDrive 'relative-sample'
        New-TestFile -Path (Join-Path $repository 'app.ps1')
        Push-Location -LiteralPath $TestDrive
        try { $result = Get-ProjectContext -Root '.\relative-sample' }
        finally { Pop-Location }

        $result.Entrypoints | Should -Be @('app.ps1')
    }

    It 'returns a compact error for a missing source query' {
        { Find-ProjectSource -Root $TestDrive } | Should -Throw 'find requires -Query.'
    }

    It 'limits dependency output while retaining the total count' {
        $repository = Join-Path $TestDrive 'dependency-sample'
        New-TestFile -Path (Join-Path $repository 'package.json') -Content @'
{
  "dependencies": {
    "alpha": "1.0.0",
    "beta": "2.0.0",
    "gamma": "3.0.0"
  }
}
'@

        $result = Get-DependencyInventory -Root $repository -Limit 2

        $result.PackageCount | Should -Be 3
        $result.Packages.Count | Should -Be 2
        $result.Truncated | Should -BeTrue
    }

    It 'reports unpinned Python requirements without throwing' {
        $repository = Join-Path $TestDrive 'python-sample'
        New-TestFile -Path (Join-Path $repository 'requirements.txt') -Content @'
requests
urllib3>=2.0
'@

        $result = Get-DependencyInventory -Root $repository

        $result.PackageCount | Should -Be 2
        @($result.Packages | Where-Object { $_.Name -eq 'requests' }).Version | Should -BeNullOrEmpty
        @($result.Packages | Where-Object { $_.Name -eq 'urllib3' }).Version | Should -Be '>=2.0'
    }

    It 'reports an invalid dependency manifest without hiding the failure' {
        $repository = Join-Path $TestDrive 'invalid-manifest-sample'
        New-TestFile -Path (Join-Path $repository 'package.json') -Content '{ invalid json }'

        $result = Get-DependencyInventory -Root $repository

        $result.Status | Should -Be 'incomplete'
        $result.Errors | Should -Be @('package.json: invalid package.json')
    }

    It 'prohibits DTDs in dependency manifests' {
        $repository = Join-Path $TestDrive 'xml-safety-sample'
        New-TestFile -Path (Join-Path $repository 'sample.csproj') -Content '<!DOCTYPE Project [<!ENTITY unsafe "unsafe">]><Project><ItemGroup><PackageReference Include="Example" Version="1.0" /></ItemGroup></Project>'

        $result = Get-DependencyInventory -Root $repository

        $result.Status | Should -Be 'incomplete'
        $result.Errors.Count | Should -Be 1
    }

    It 'does not require Pester when a PowerShell project has no tests' {
        $repository = Join-Path $TestDrive 'diagnostics-sample'
        New-TestFile -Path (Join-Path $repository 'app.ps1')

        $result = Get-ProjectDiagnostics -Root $repository

        $result.Status | Should -BeIn @('ok', 'incomplete')
        @($result.Tools | Where-Object { $_.Name -eq 'Pester' }).Count | Should -Be 0
        @($result.Tools | Where-Object { $_.Name -eq 'PSScriptAnalyzer' }).Count | Should -Be 1
    }

    It 'reports missing compiled-help tooling before the product gate needs it' {
        $repository = Join-Path $TestDrive 'help-prerequisite'
        New-TestFile -Path (Join-Path $repository 'tools/package/Publish-AdoToolkitPackage.ps1')
        New-TestFile -Path (Join-Path $repository 'docs/commands/en-US/Get-Sample.md') -Content '# Get-Sample'
        Mock Get-Module { [pscustomobject]@{ Name = $Name; Version = [version]'1.25.0'; Path = 'synthetic.psd1' } }
        Mock Get-Module { @() } -ParameterFilter { $ListAvailable -and $Name -eq 'Microsoft.PowerShell.PlatyPS' }
        $result = Get-ProjectDiagnostics -Root $repository
        $result.Status | Should -Be 'incomplete'
        $result.RequiredProblems | Should -Contain 'Microsoft.PowerShell.PlatyPS'
    }

    It 'uses the release-pinned Pester when a newer compatible version is installed' {
        $repository = Join-Path $TestDrive 'release-pester'
        New-TestFile -Path (Join-Path $repository 'tools/tests/Sample.Tests.ps1') -Content "Describe 'sample' {}"
        Mock Get-Module {
            @([pscustomobject]@{ Name = 'Pester'; Version = [version]'5.9.1'; Path = 'pinned-pester.psd1' },
              [pscustomobject]@{ Name = 'Pester'; Version = [version]'5.99.0'; Path = 'newer-pester.psd1' })
        } -ParameterFilter { $ListAvailable -and $Name -eq 'Pester' }
        Mock Get-Module { @() } -ParameterFilter { -not $ListAvailable -and $Name -eq 'Pester' }
        Mock Import-Module { }
        Mock Invoke-ValidationStage {
            [pscustomobject]@{ Name = $Stage.Name; Status = 'pass'; ExitCode = 0; Summary = @(); Warnings = @() }
        }
        $previous = $env:ADOTOOLKIT_RELEASE_BUILD
        try {
            $env:ADOTOOLKIT_RELEASE_BUILD = '1'
            $null = Invoke-ProjectVerification -Root $repository -Stage 'powershell-test'
            Should -Invoke Import-Module -Exactly -Times 1 -ParameterFilter { $Name -eq 'pinned-pester.psd1' }
            Should -Invoke Import-Module -Exactly -Times 0 -ParameterFilter { $Name -eq 'newer-pester.psd1' }
        }
        finally { $env:ADOTOOLKIT_RELEASE_BUILD = $previous }
    }

    It 'treats a conventional src entry file as an entry point' {
        $repository = Join-Path $TestDrive 'entrypoint-sample'
        New-TestFile -Path (Join-Path $repository 'src\main.py')
        New-TestFile -Path (Join-Path $repository 'src\helpers.py')

        $projectProfile = Get-ProjectProfile -Root $repository

        $projectProfile.Entrypoints | Should -Be @('src/main.py')
    }

    It 'plans build and test stages for the detected stack' {
        $repository = Join-Path $TestDrive 'stack-plan-sample'
        New-TestFile -Path (Join-Path $repository 'package.json') -Content '{"name":"sample","scripts":{"build":"node build.js","test":"node --test"}}'
        $projectProfile = Get-ProjectProfile -Root $repository

        $plan = @(Get-StackValidationPlan -ProjectProfile $projectProfile)
        $skipped = @(Get-StackValidationPlan -ProjectProfile $projectProfile -SkipTests)

        $plan.Name | Should -Be @('configuration', 'node-build:package.json', 'node-test:package.json')
        $skipped.Name | Should -Be @('configuration', 'node-build:package.json')
    }

    It 'plans lint, test, and configuration stages for a PowerShell repository' {
        $repository = Join-Path $TestDrive 'powershell-plan-sample'
        New-TestFile -Path (Join-Path $repository 'tools\dev.ps1')
        New-TestFile -Path (Join-Path $repository 'settings.json') -Content '{}'
        New-TestFile -Path (Join-Path $repository 'tests\app.Tests.ps1')
        $projectProfile = Get-ProjectProfile -Root $repository

        $plan = @(Get-StackValidationPlan -ProjectProfile $projectProfile)

        $plan.Name | Should -Be @('powershell-lint', 'powershell-test', 'configuration')
    }

    It 'plans no stages for a repository with nothing to validate' {
        $repository = Join-Path $TestDrive 'empty-plan-sample'
        New-TestFile -Path (Join-Path $repository 'notes.md') -Content 'nothing to build'
        $projectProfile = Get-ProjectProfile -Root $repository

        @(Get-StackValidationPlan -ProjectProfile $projectProfile).Count | Should -Be 0
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

    It 'marks a missing analyzer as unavailable after parsing' {
        $repository = Join-Path $TestDrive 'missing-analyzer-sample'
        New-TestFile -Path (Join-Path $repository 'app.ps1') -Content 'Write-Output ready'
        Mock Get-Module { @() } -ParameterFilter { $ListAvailable -and $Name -eq 'PSScriptAnalyzer' }

        $outcome = Get-PowerShellLintOutcome -ProjectProfile (Get-ProjectProfile -Root $repository)

        $outcome.Failures | Should -BeNullOrEmpty
        $outcome.Unavailable | Should -BeTrue
        $outcome.Warnings | Should -Be @('The required PSScriptAnalyzer version is not installed (run bootstrap -Install).')
    }

    It 'uses a repository-owned check for product stages and retains template checks' {
        $repository = Join-Path $TestDrive 'owned-check-sample'
        New-TestFile -Path (Join-Path $repository 'package.json') -Content '{"name":"sample"}'
        New-TestFile -Path (Join-Path $repository 'tools\check.ps1') -Content 'exit 0'
        $projectProfile = Get-ProjectProfile -Root $repository

        $plan = @(Get-ValidationPlan -ProjectProfile $projectProfile)

        $plan.Name | Should -Be @('powershell-lint', 'configuration', 'tooling-layout', 'project-check')
    }

    It 'preserves skipped-tool warnings when reducing successful stage output' {
        $scriptPath = Join-Path $TestDrive 'stage.ps1'
        New-TestFile -Path $scriptPath -Content "Write-Output 'skipped (PSScriptAnalyzer is not installed)'"
        $stage = [pscustomobject]@{
            Name = 'sample'
            Executable = Join-Path $PSHOME 'pwsh.exe'
            Arguments = @('-NoProfile', '-File', $scriptPath)
        }

        $result = Invoke-ValidationStage -Stage $stage -Root $TestDrive

        $result.Status | Should -Be 'pass'
        $result.Warnings | Should -Be @('skipped (PSScriptAnalyzer is not installed)')
    }

    It 'strips terminal colour from captured stage output' {
        $scriptPath = Join-Path $TestDrive 'coloured.ps1'
        New-TestFile -Path $scriptPath -Content 'Write-Output "$([char] 27)[32mCHECK PASSED$([char] 27)[0m"'
        $stage = [pscustomobject]@{
            Name = 'coloured'
            Executable = Join-Path $PSHOME 'pwsh.exe'
            Arguments = @('-NoProfile', '-File', $scriptPath)
        }

        $result = Invoke-ValidationStage -Stage $stage -Root $TestDrive

        $result.Status | Should -Be 'pass'
        $result.Summary | Should -Be @('CHECK PASSED')
    }

    It 'writes one JSON document for a default invocation' {
        $pwsh = Join-Path $PSHOME 'pwsh.exe'
        $devScript = Join-Path $PSScriptRoot '..\dev.ps1'
        $output = @(& $pwsh -NoProfile -File $devScript context)

        $LASTEXITCODE | Should -Be 0
        $document = $output -join [Environment]::NewLine | ConvertFrom-Json
        $document.Status | Should -Be 'ok'
        $document.Commands.Verify | Should -Match 'tools\\dev\.ps1 verify'
        $document.ImportantPaths | Should -Contain '.claude/settings.json'
    }

    It 'blocks mutating Git commands in the Claude Code guard' {
        $pwsh = Join-Path $PSHOME 'pwsh.exe'
        $guard = Join-Path $PSScriptRoot '..\guard-git.ps1'

        foreach ($command in @('git config user.name example', 'git -C . commit -m test')) {
            $payload = @{ tool_input = @{ command = $command } } | ConvertTo-Json -Compress
            $null = @($payload | & $pwsh -NoProfile -File $guard 2>&1)

            $LASTEXITCODE | Should -Be 2
        }

        $payload = @{ tool_input = @{ command = 'git status' } } | ConvertTo-Json -Compress
        $null = @($payload | & $pwsh -NoProfile -File $guard 2>&1)

        $LASTEXITCODE | Should -Be 0
    }

    It 'keeps configured Claude hook helpers under tools' {
        $repository = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
        $outcome = Get-ToolingLayoutOutcome -ProjectProfile (Get-ProjectProfile -Root $repository)

        $outcome.Failures | Should -BeNullOrEmpty
        $outcome.Summary | Should -Be @('2 configured PowerShell hook helper(s) checked')
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

    It 'describes a repository that has nothing in it yet' {
        $repository = Join-Path $TestDrive 'empty-sample'
        [void] (New-Item -ItemType Directory -Path $repository -Force)

        $projectProfile = Get-ProjectProfile -Root $repository
        $context = Get-ProjectContext -Root $repository
        $verification = Invoke-ProjectVerification -Root $repository

        $projectProfile.FileCount | Should -Be 0
        $projectProfile.Stack | Should -BeNullOrEmpty
        $context.Status | Should -Be 'ok'
        $verification.Status | Should -Be 'unavailable'
    }

    It 'reads package references that carry no version of their own' {
        $repository = Join-Path $TestDrive 'central-packages-sample'
        New-TestFile -Path (Join-Path $repository 'app.csproj') -Content @'
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Managed" />
    <PackageReference Update="Pinned" Version="13.0.3" />
    <PackageReference Include="Nested">
      <Version>4.5.0</Version>
    </PackageReference>
  </ItemGroup>
</Project>
'@

        $result = Get-DependencyInventory -Root $repository

        $result.Status | Should -Be 'ok'
        $result.PackageCount | Should -Be 3
        @($result.Packages | Where-Object { $_.Name -eq 'Managed' }).Version | Should -BeNullOrEmpty
        @($result.Packages | Where-Object { $_.Name -eq 'Pinned' }).Version | Should -Be '13.0.3'
        @($result.Packages | Where-Object { $_.Name -eq 'Nested' }).Version | Should -Be '4.5.0'
    }

    It 'runs its in-process stages when called, with product check exit <CheckExit>' -TestCases @(
        @{ CheckExit = 0; CheckStatus = 'pass' }
        @{ CheckExit = 2; CheckStatus = 'unavailable' }
    ) {
        param($CheckExit, $CheckStatus)
        # Regression guard: a stage built with GetNewClosure is rebound to a dynamic
        # module whose scope chain excludes this script, so every in-process stage failed
        # with "term is not recognized" unless PowerShell had started dev.ps1 with -File.
        $pwsh = Join-Path $PSHOME 'pwsh.exe'
        # The regression concerns invocation scope, not the real product's prerequisites.
        # Exercise both ready and unavailable product checks in an isolated repository.
        $repository = Join-Path $TestDrive "invocation-$CheckExit"
        $devScript = Join-Path $repository 'tools/dev.ps1'
        New-TestFile $devScript (Get-Content -LiteralPath (Join-Path $PSScriptRoot '../dev.ps1') -Raw)
        foreach ($library in @('discovery', 'dependencies', 'validation', 'setup')) {
            New-TestFile (Join-Path $repository "tools/lib/$library.ps1") (Get-Content -LiteralPath (Join-Path $PSScriptRoot "../lib/$library.ps1") -Raw)
        }
        New-TestFile (Join-Path $repository 'tools/BuildModules.psd1') (Get-Content -LiteralPath (Join-Path $PSScriptRoot '../BuildModules.psd1') -Raw)
        New-TestFile (Join-Path $repository 'sample.json') '{}'
        New-TestFile (Join-Path $repository 'PSScriptAnalyzerSettings.psd1') (Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../PSScriptAnalyzerSettings.psd1') -Raw)
        New-TestFile (Join-Path $repository 'tools/check.ps1') ('[CmdletBinding()] param([switch] $SkipTests)' + [Environment]::NewLine + "exit $CheckExit")
        $command = "& '" + $devScript.Replace("'", "''") + "' verify -SkipTests; exit " + '$LASTEXITCODE'
        $arguments = @('-NoProfile', '-Command', $command)
        $output = @(& $pwsh @arguments)
        $invocationExit = $LASTEXITCODE
        $document = $output -join [Environment]::NewLine | ConvertFrom-Json

        $invocationExit | Should -Be 2
        $document.Status | Should -Be 'incomplete'
        @($document.Stages | Where-Object { $_.Name -ne 'project-check' -and $_.Status -ne 'pass' }) | Should -BeNullOrEmpty
        @($document.Stages | Where-Object { $_.Name -eq 'powershell-lint' }).Count | Should -Be 1
        ($document.Stages | Where-Object Name -eq 'project-check').Status | Should -Be $CheckStatus
        ($document.Stages | Where-Object Name -eq 'project-check').ExitCode | Should -Be $CheckExit
    }

    It 'passes a planned stage its arguments instead of capturing them' {
        $repository = Join-Path $TestDrive 'stage-arguments-sample'
        New-TestFile -Path (Join-Path $repository 'app.json') -Content '{"a":1}'
        $projectProfile = Get-ProjectProfile -Root $repository
        $stage = @(Get-StackValidationPlan -ProjectProfile $projectProfile) |
            Where-Object { $_.Name -eq 'configuration' }

        $result = Invoke-ValidationStage -Stage $stage -Root $repository

        $result.Status | Should -Be 'pass'
        $result.Summary | Should -Be @('1 configuration file(s) validated')
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

        $result = Invoke-ProjectVerification -Root $repository

        $result.Status | Should -Be 'pass'
        $result.Warnings | Should -Be @('project-check: note: 1 missing peer dependency')
    }

    It 'derives search exclusions from one list so discovery does not depend on ripgrep' {
        $globs = @(Get-RepositoryExclusionGlobs)
        $searchArguments = @(Get-RepositorySearchGlobs)

        foreach ($expected in @('!node_modules/**', '!**/node_modules/**', '!secret/**', '!secrets/**', '!**/.env', '!*.log')) {
            $globs | Should -Contain $expected
        }
        $searchArguments | Should -Contain '--no-ignore'
        Test-IsExcludedDirectoryName -Name 'secret' | Should -BeTrue
        Test-IsExcludedDirectoryName -Name 'src' | Should -BeFalse
    }

    It 'surfaces instruction files that scope rules to a subdirectory' {
        $repository = Join-Path $TestDrive 'nested-instructions-sample'
        New-TestFile -Path (Join-Path $repository 'AGENTS.md') -Content '# root'
        New-TestFile -Path (Join-Path $repository 'src\api\AGENTS.md') -Content '# api'
        $projectProfile = Get-ProjectProfile -Root $repository

        $paths = @(Get-AgentInstructionPaths -ProjectProfile $projectProfile)

        $paths | Should -Be @('AGENTS.md', 'src/api/AGENTS.md')
    }

    It 'keeps the workflow entry point first when the path budget is tight' {
        $repository = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

        $context = Get-ProjectContext -Root $repository

        $context.ImportantPaths[0] | Should -Be 'tools/dev.ps1'
        $context.ImportantPaths.Count | Should -BeLessOrEqual 40
        $context.ImportantPathCount | Should -BeGreaterOrEqual $context.ImportantPaths.Count
    }

    It 'blocks Git commands that hide behind a prefix, a wrapper, or a shell' {
        $pwsh = Join-Path $PSHOME 'pwsh.exe'
        $guard = Join-Path $PSScriptRoot '..\guard-git.ps1'
        $git = 'git'
        $blocked = @(
            'GIT_DIR=.git ' + $git + ' commit -m x'
            'env ' + $git + ' push'
            'timeout 30 ' + $git + ' push'
            'bash -c "' + $git + ' push"'
            'cmd /c ' + $git + ' push'
            $git + ' -c core.pager=start log'
            $git + ' --exec-path=/tmp status'
        )
        $allowed = @(
            $git + ' -C . status'
            $git + ' log --oneline main'
            'grep -n "' + $git + '" notes.md'
            'bash -lc "npm test"'
        )

        foreach ($command in $blocked) {
            $payload = @{ tool_input = @{ command = $command } } | ConvertTo-Json -Compress
            $null = @($payload | & $pwsh -NoProfile -File $guard 2>&1)
            $LASTEXITCODE | Should -Be 2 -Because "'$command' reaches Git"
        }

        foreach ($command in $allowed) {
            $payload = @{ tool_input = @{ command = $command } } | ConvertTo-Json -Compress
            $null = @($payload | & $pwsh -NoProfile -File $guard 2>&1)
            $LASTEXITCODE | Should -Be 0 -Because "'$command' does not change Git state"
        }
    }

    It 'rejects malformed configuration in the Claude Code edit guard' {
        $pwsh = Join-Path $PSHOME 'pwsh.exe'
        $hook = Join-Path $PSScriptRoot '..\validate-edit.ps1'
        $invalidFile = Join-Path $TestDrive 'invalid-settings.json'
        New-TestFile -Path $invalidFile -Content '{ invalid json }'
        $payload = @{ cwd = $TestDrive; tool_input = @{ file_path = $invalidFile } } | ConvertTo-Json -Compress

        $null = @($payload | & $pwsh -NoProfile -File $hook 2>&1)

        $LASTEXITCODE | Should -Be 2
    }
}
