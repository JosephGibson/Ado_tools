Describe 'Workflow contracts' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '../dev.ps1')
        function New-Fixture {
            param([string] $Path, [string] $Content = '')
            [void][System.IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
            [System.IO.File]::WriteAllText($Path, $Content.Replace("`r`n", "`n"), [System.Text.UTF8Encoding]::new($false))
        }
        # The version a mocked Pester reports: a release build accepts only the pinned one.
        $pesterVersion = [version] (Import-PowerShellDataFile -LiteralPath (Join-Path $PSScriptRoot '../BuildModules.psd1')).Pester
    }

    It 'previews the plan without invoking checks' {
        $root = Join-Path $TestDrive 'preview'
        New-Fixture (Join-Path $root 'app.json') '{}'
        Mock Invoke-ValidationStage { throw 'Preview executed a check' }
        (Get-ProjectValidationPlan -Root $root).Stages.Name | Should -Be @('configuration')
        Should -Invoke Invoke-ValidationStage -Times 0
    }

    It 'previews the product gate with its command line' {
        $root = Join-Path $TestDrive 'preview-gate'
        New-Fixture (Join-Path $root 'tools/check.ps1') 'exit 0'
        $stage = (Get-ProjectValidationPlan -Root $root -SkipTests).Stages | Where-Object Name -eq 'project-check'
        $stage.Executable | Should -Be (Join-Path $PSHOME 'pwsh.exe')
        $stage.Arguments | Should -Be @('-NoProfile', '-File', (Join-Path $root 'tools\check.ps1'), '-SkipTests')
    }

    It 'reports centrally declared NuGet package versions without evaluating MSBuild' {
        $root = Join-Path $TestDrive 'central-versions'
        New-Fixture (Join-Path $root 'Directory.Packages.props') '<Project><ItemGroup><PackageVersion Include="Example" Version="2.1.0" /></ItemGroup></Project>'
        $result = Get-DependencyInventory -Root $root
        $result.Packages[0].Version | Should -Be '2.1.0'
        $result.Packages[0].Scope | Should -Be 'central'
    }

    It 'rejects nonstandard JSON that native tools would reject' -TestCases @(
        @{Text = '{"a":1,}'}
        @{Text = '{/*comment*/"a":1}'}
    ) {
        param($Text)
        { ConvertFrom-StrictJson -Text $Text } | Should -Throw
    }

    It 'rejects malformed hook objects and missing instruction imports' {
        $root = Join-Path $TestDrive 'configuration-contract'
        New-Fixture (Join-Path $root '.claude/settings.json') '{"hooks":null}'
        New-Fixture (Join-Path $root 'CLAUDE.md') '@missing.md'
        $result = Get-ToolingLayoutOutcome -ProjectProfile (Get-ProjectProfile -Root $root)
        $result.Failures.Count | Should -Be 2
    }

    It 'reports a passing selected check as incomplete and rejects unknown names' {
        $root = Join-Path $TestDrive 'selected'
        New-Fixture (Join-Path $root 'app.json') '{}'
        New-Fixture (Join-Path $root 'tools/sample.ps1') 'Write-Output ready'
        $result = Invoke-ProjectVerification -Root $root -Stage configuration
        $result.Status | Should -Be 'incomplete'
        $result.Stages.Name | Should -Be @('configuration')
        $result.Stages[0].Status | Should -Be 'pass'
        $result.Warnings | Should -Contain 'Only the selected stages ran; run verify for the full gate.'
        { Invoke-ProjectVerification -Root $root -Stage typo } | Should -Throw '*Unknown stage*'
    }

    It 'rejects an unknown stage name in a repository that has no stage' {
        $root = Join-Path $TestDrive 'selected-nothing'
        New-Fixture (Join-Path $root 'notes.txt') 'nothing to validate'
        { Invoke-ProjectVerification -Root $root -Stage typo } | Should -Throw '*Unknown stage*'
    }

    It 'does not mistake PowerShell fixtures for Pester test containers' {
        $root = Join-Path $TestDrive 'test-discovery'
        New-Fixture (Join-Path $root 'tests/fixtures/helper.ps1') 'Write-Output helper'
        New-Fixture (Join-Path $root 'tools/tests/Check.Tests.ps1') 'Describe check {}'
        $project = Get-ProjectProfile -Root $root
        $project.TestFiles | Should -Be @('tools/tests/Check.Tests.ps1')
        (Get-ProjectContext -Root $root).ProductTests | Should -Be 0
    }

    It 'surfaces matching scoped instructions and nested Claude rules' {
        $root = Join-Path $TestDrive 'instructions'
        New-Fixture (Join-Path $root 'AGENTS.md') '# Root'
        New-Fixture (Join-Path $root 'src/api/AGENTS.md') '# API'
        New-Fixture (Join-Path $root 'src/ui/AGENTS.md') '# UI'
        New-Fixture (Join-Path $root '.claude/rules/api/style.md') '# Style'
        $result = Get-ProjectContext -Root $root -Path 'src/api/new.cs'
        $result.Project | Should -Be 'Root'
        $result.ScopedInstructions | Should -Be @('AGENTS.md', 'src/api/AGENTS.md')
        $result.ImportantPaths | Should -Contain '.claude/rules/api/style.md'
        { Get-ProjectContext -Root $root -Path '../outside.cs' } | Should -Throw '*safe repository path*'
    }

    It 'scopes instructions for an absolute path as for the same relative path' {
        $root = Join-Path $TestDrive 'absolute-scope'
        New-Fixture (Join-Path $root 'AGENTS.md') '# Root'
        New-Fixture (Join-Path $root 'src/api/AGENTS.md') '# API'
        $result = Get-ProjectContext -Root $root -Path (Join-Path $root 'src\api\new.cs')
        $result.ScopedInstructions | Should -Be @('AGENTS.md', 'src/api/AGENTS.md')
        { Get-ProjectContext -Root $root -Path (Join-Path $TestDrive 'outside.cs') } | Should -Throw '*safe repository path*'
    }

    It 'lists workflow, instruction, configuration and project files as important paths, not test files' {
        $root = Join-Path $TestDrive 'important-paths'
        New-Fixture (Join-Path $root 'tools/dev.ps1') ''
        New-Fixture (Join-Path $root 'docs/tooling.md') '# Tooling'
        New-Fixture (Join-Path $root 'AGENTS.md') '# Sample'
        New-Fixture (Join-Path $root 'global.json') '{}'
        New-Fixture (Join-Path $root 'src/App/App.csproj') '<Project Sdk="Microsoft.NET.Sdk" />'
        New-Fixture (Join-Path $root 'tests/App.Tests/SampleTests.cs') ''
        $result = Get-ProjectContext -Root $root
        $result.ImportantPaths | Should -Be @('tools/dev.ps1', 'docs/tooling.md', 'AGENTS.md', 'global.json', 'src/App/App.csproj')
        $result.Tests | Should -Be 1
    }

    It 'returns line numbers and the output limit signal for literal content matches' {
        $root = Join-Path $TestDrive 'line-number'
        New-Fixture (Join-Path $root 'src/a.txt') "first`nneedle [literal]`nthird"
        $result = Find-ProjectSource -Root $root -Query 'needle [literal]' -Limit 1
        $result.Results[0].Line | Should -Be 2
        $result.LimitReached | Should -BeTrue
    }

    It 'uses the same case-insensitive exclusions with and without ripgrep' {
        $root = Join-Path $TestDrive 'search-parity'
        New-Fixture (Join-Path $root 'src/visible.txt') 'unique-fixture'
        New-Fixture (Join-Path $root 'SECRETS/hidden.txt') 'unique-fixture'
        New-Fixture (Join-Path $root 'ARTIFACTS/hidden.txt') 'unique-fixture'
        New-Fixture (Join-Path $root '.ENV.PRODUCTION') 'unique-fixture'
        New-Fixture (Join-Path $root '.claude/settings.local.json') '{}'
        # ripgrep applies a file glob to a directory name too, and does not descend into a match.
        New-Fixture (Join-Path $root 'credentials/hidden.txt') 'unique-fixture'
        New-Fixture (Join-Path $root 'src/Run.LOG/hidden.txt') 'unique-fixture'
        $native = @(Get-RepositoryFiles -Root $root | ForEach-Object Name)
        Mock Get-FirstCommand { $null } -ParameterFilter { $Names -contains 'rg' }
        $fallback = @(Get-RepositoryFiles -Root $root | ForEach-Object Name)
        $native | Should -Be @('visible.txt')
        $fallback | Should -Be $native
        (Find-ProjectSource -Root $root -Query 'unique-fixture').Results.Path | Should -Be @('src/visible.txt')
        Test-IsSafeRepositoryFile -File (Get-Item -LiteralPath (Join-Path $root 'credentials/hidden.txt')) -Root $root | Should -BeFalse
    }

    It 'skips large and binary content in the filesystem fallback' {
        $root = Join-Path $TestDrive 'content-budget'
        New-Fixture (Join-Path $root 'large.txt') ('x' * (1MB + 1) + 'needle')
        New-Fixture (Join-Path $root 'binary.dat') "`0needle"
        New-Fixture (Join-Path $root 'visible.txt') 'needle'
        Mock Get-FirstCommand { $null } -ParameterFilter { $Names -contains 'rg' }
        (Find-ProjectSource -Root $root -Query needle).Results.Path | Should -Be @('visible.txt')
    }

    It 'rejects files beneath a linked directory and omits them from discovery' {
        $root = Join-Path $TestDrive 'links'
        $destination = Join-Path $TestDrive 'link-destination'
        New-Fixture (Join-Path $root 'visible.txt') 'visible'
        New-Fixture (Join-Path $destination 'outside.txt') 'outside'
        $junction = Join-Path $root 'linked'
        [void](New-Item -ItemType Junction -Path $junction -Target $destination)
        $file = Get-Item -LiteralPath (Join-Path $junction 'outside.txt')
        (Test-IsSafeRepositoryFile -File $file -Root $root) | Should -BeFalse
        @(Get-RepositoryFiles -Root $root | ForEach-Object Name) | Should -Be @('visible.txt')
    }

    It 'passes shell metacharacters as literal arguments to external stages' {
        $root = Join-Path $TestDrive 'path [with spaces]'
        $script = Join-Path $root 'echo.ps1'
        New-Fixture $script 'param([string]$Value) Write-Output $Value'
        $literal = 'literal $() ` ; & [text]'
        $result = Invoke-BoundedProcess -Executable (Join-Path $PSHOME 'pwsh.exe') -Arguments @('-NoProfile', '-File', $script, $literal) -WorkingDirectory $root
        $result.ExitCode | Should -Be 0
        $result.Lines | Should -Be @($literal)
    }

    It 'bounds noisy output while preserving a nonzero process exit code' {
        $script = Join-Path $TestDrive 'noise.ps1'
        New-Fixture $script '1..300 | ForEach-Object { Write-Output "line $_" }; exit 7'
        $result = Invoke-BoundedProcess -Executable (Join-Path $PSHOME 'pwsh.exe') -Arguments @('-NoProfile', '-File', $script) -WorkingDirectory $TestDrive
        $result.ExitCode | Should -Be 7
        $result.Lines.Count | Should -Be 120
        $result.Lines[-1] | Should -Be 'line 300'
    }

    It 'returns non-ASCII output and arguments of an external stage unchanged' {
        # An accented letter, and a check mark that no single-byte code page holds.
        $text = [string][char]0xE9 + [char]0x2713
        $script = Join-Path $TestDrive 'accent.ps1'
        New-Fixture $script 'param([string] $Value) Write-Output ([string][char]0xE9 + [char]0x2713); Write-Output $Value'
        $result = Invoke-BoundedProcess -Executable (Join-Path $PSHOME 'pwsh.exe') -Arguments @('-NoProfile', '-File', $script, "argument $text") -WorkingDirectory $TestDrive
        $result.ExitCode | Should -Be 0
        $result.Lines | Should -Be @($text, "argument $text")
        # A stage that cannot start reports through the error stream of the child.
        $absent = Invoke-BoundedProcess -Executable (Join-Path $TestDrive "absent-$text.exe") -Arguments @() -WorkingDirectory $TestDrive
        $absent.ExitCode | Should -Be 1
        $absent.Lines -join ' ' | Should -BeLike "*absent-$text.exe*"
    }

    It 'terminates external checks that exceed their timeout' {
        $script = Join-Path $TestDrive 'timeout.ps1'
        New-Fixture $script 'Start-Sleep -Seconds 30'
        { Invoke-BoundedProcess -Executable (Join-Path $PSHOME 'pwsh.exe') -Arguments @('-NoProfile', '-File', $script) -WorkingDirectory $TestDrive -TimeoutSeconds 1 } | Should -Throw '*timed out*'
    }

    It 'blocks Git execution and output overrides after a read subcommand' {
        $guard = Join-Path $PSScriptRoot '../guard-git.ps1'
        foreach ($command in @('git diff --ext-diff', 'git log --textconv', 'git diff --output=out.txt', 'git -ccore.pager=example log')) {
            $payload = @{tool_input=@{command=$command}} | ConvertTo-Json -Compress
            $null = @($payload | & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File $guard 2>&1)
            $LASTEXITCODE | Should -Be 2
        }
    }

    It 'does not parse sensitive or out-of-scope edit targets' {
        $root = Join-Path $TestDrive 'hook-scope'
        $inside = Join-Path $root 'secrets/data.json'
        $outside = Join-Path $TestDrive 'outside.json'
        New-Fixture $inside '{invalid}'
        New-Fixture $outside '{invalid}'
        $hook = Join-Path $PSScriptRoot '../validate-edit.ps1'
        foreach ($path in @($inside, $outside)) {
            $payload = @{cwd=$root; tool_input=@{file_path=$path}} | ConvertTo-Json -Compress
            $null = @($payload | & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File $hook 2>&1)
            $LASTEXITCODE | Should -Be 0
        }
    }

    It 'does not pass Pester container errors when there are no failed test blocks' {
        Mock Get-Module { [pscustomobject]@{Version=$pesterVersion} } -ParameterFilter { $Name -eq 'Pester' }
        Mock Invoke-Pester {
            [pscustomobject]@{
                Result='Failed'; TotalCount=0; PassedCount=0; FailedCount=0; SkippedCount=0; NotRunCount=0; Failed=@()
                Containers=@([pscustomobject]@{Result='Failed'; Item='broken.Tests.ps1'; ErrorRecord=@([pscustomobject]@{Exception=[Exception]::new('Discovery failed')})})
            }
        }
        $result = Get-PesterOutcome -TestPath @($TestDrive)
        $result.Failures | Should -Be @('broken.Tests.ps1: Discovery failed')
    }

    It 'counts the failing tests it does not list apart from the container errors' {
        Mock Get-Module { [pscustomobject]@{Version=$pesterVersion} } -ParameterFilter { $Name -eq 'Pester' }
        Mock Invoke-Pester {
            [pscustomobject]@{
                Result='Failed'; TotalCount=12; PassedCount=0; FailedCount=12; SkippedCount=0; NotRunCount=0
                Failed=@(1..12 | ForEach-Object { [pscustomobject]@{ExpandedPath="Suite.Test $_"; ErrorRecord=[pscustomobject]@{Exception=[Exception]::new('Expected 1, but got 2.')}} })
                Containers=@([pscustomobject]@{Result='Failed'; Item='broken.Tests.ps1'; ErrorRecord=@(1..3 | ForEach-Object { [pscustomobject]@{Exception=[Exception]::new("Setup failed $_")} })})
            }
        }
        $result = Get-PesterOutcome -TestPath @($TestDrive)
        $result.Failures.Count | Should -Be 14
        $result.Failures[0..2] | Should -Be @('broken.Tests.ps1: Setup failed 1', 'broken.Tests.ps1: Setup failed 2', 'broken.Tests.ps1: Setup failed 3')
        $result.Failures[12] | Should -Be 'Suite.Test 10: Expected 1, but got 2.'
        $result.Failures[13] | Should -Be '... and 2 further failing test(s)'
    }

    It 'marks empty or skipped Pester runs unavailable' -TestCases @(
        @{Total=0; Skipped=0}
        @{Total=1; Skipped=1}
    ) {
        param($Total, $Skipped)
        Mock Get-Module { [pscustomobject]@{Version=$pesterVersion} } -ParameterFilter { $Name -eq 'Pester' }
        Mock Invoke-Pester {
            [pscustomobject]@{Result='Passed'; TotalCount=$Total; PassedCount=0; FailedCount=0; SkippedCount=$Skipped; NotRunCount=0; Failed=@(); Containers=@()}
        }
        (Get-PesterOutcome -TestPath @($TestDrive)).Unavailable | Should -BeTrue
    }
}
