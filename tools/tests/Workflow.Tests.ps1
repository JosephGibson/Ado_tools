Describe 'Reusable workflow contracts' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '../dev.ps1')
        function New-Fixture {
            param([string] $Path, [string] $Content = '')
            [void][System.IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
            Set-AtomicText -Path $Path -Content $Content
        }
    }

    It 'reports a missing Node test script as unavailable' {
        $root = Join-Path $TestDrive 'missing-script'
        New-Fixture (Join-Path $root 'package.json') '{"scripts":{"build":"node build.js"}}'
        $plan = @(Get-ValidationPlan (Get-ProjectProfile $root))
        $testStage = $plan | Where-Object Name -eq 'node-test:package.json'
        (Invoke-ValidationStage $testStage $root).Status | Should -Be 'unavailable'
        ($plan | Where-Object Name -eq 'node-build:package.json').Arguments | Should -Not -Contain '--if-present'
    }

    It 'uses each package directory and nearest declared package manager' {
        $root = Join-Path $TestDrive 'node-packages'
        New-Fixture (Join-Path $root 'package.json') '{"packageManager":"pnpm@10.0.0","scripts":{"test":"node --test"}}'
        New-Fixture (Join-Path $root 'packages/a/package.json') '{"scripts":{"lint":"eslint .","test":"node --test"}}'
        New-Fixture (Join-Path $root 'packages/b/package.json') '{"packageManager":"yarn@4.0.0","scripts":{"test":"node --test"}}'
        $plan = @(Get-ValidationPlan (Get-ProjectProfile $root))
        $a = $plan | Where-Object Name -eq 'node-test:packages/a/package.json'
        $b = $plan | Where-Object Name -eq 'node-test:packages/b/package.json'
        $a.Executable | Should -Be 'pnpm'
        $a.WorkingDirectory | Should -Be 'packages/a'
        $b.Executable | Should -Be 'yarn'
        $b.Arguments | Should -Be @('run', 'test')
    }

    It 'does not guess between conflicting package manager lockfiles' {
        $root = Join-Path $TestDrive 'conflicting-locks'
        New-Fixture (Join-Path $root 'package.json') '{}'
        New-Fixture (Join-Path $root 'package-lock.json') '{}'
        New-Fixture (Join-Path $root 'yarn.lock') ''
        $plan = @(Get-ValidationPlan (Get-ProjectProfile $root))
        (Invoke-ValidationStage ($plan | Where-Object Name -eq 'node-plan:package.json') $root).Status | Should -Be 'unavailable'
    }

    It 'builds every .NET project and gives test stages a build prerequisite' {
        $root = Join-Path $TestDrive 'dotnet-multiple'
        New-Fixture (Join-Path $root 'src/a/a.csproj') '<Project Sdk="Microsoft.NET.Sdk" />'
        New-Fixture (Join-Path $root 'src/b/b.csproj') '<Project Sdk="Microsoft.NET.Sdk" />'
        New-Fixture (Join-Path $root 'tests/a/a.csproj') '<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>'
        $plan = @(Get-ValidationPlan (Get-ProjectProfile $root))
        $builds = @($plan | Where-Object Name -like 'dotnet-build:*')
        $builds.Count | Should -Be 3
        foreach ($build in $builds) { $build.Arguments | Should -Contain '--no-restore' }
        $testStage = $plan | Where-Object Name -like 'dotnet-test:*'
        $testStage.DependsOn | Should -Be @('dotnet-build:tests/a/a.csproj')
        $testStage.Arguments | Should -Contain '--no-build'
    }

    It 'marks detected <Stack> without an environment contract incomplete' -TestCases @(
        @{ Stack = 'python'; Manifest = 'pyproject.toml'; Content = '[project]' }
        @{ Stack = 'rust'; Manifest = 'Cargo.toml'; Content = '[package]' }
        @{ Stack = 'go'; Manifest = 'go.mod'; Content = 'module example' }
        @{ Stack = 'java'; Manifest = 'pom.xml'; Content = '<project />' }
    ) {
        param($Stack, $Manifest, $Content)
        $root = Join-Path $TestDrive $Stack
        New-Fixture (Join-Path $root $Manifest) $Content
        $result = Invoke-ProjectVerification -Root $root
        $result.Status | Should -Be 'incomplete'
        ($result.Stages | Where-Object Name -eq "$Stack-plan").Status | Should -Be 'unavailable'
    }

    It 'previews the plan without invoking checks' {
        $root = Join-Path $TestDrive 'preview'
        New-Fixture (Join-Path $root 'app.json') '{}'
        Mock Invoke-ValidationStage { throw 'Preview executed a check' }
        (Get-ProjectValidationPlan -Root $root).Stages.Name | Should -Be @('configuration')
        Should -Invoke Invoke-ValidationStage -Times 0
    }

    It 'requires a manifest or project check for product source with no detected stack' {
        $root = Join-Path $TestDrive 'unconfigured-source'
        New-Fixture (Join-Path $root 'src/main.cs') 'System.Console.WriteLine("hello");'
        $result = Invoke-ProjectVerification -Root $root
        $result.Status | Should -Be 'incomplete'
        $result.Stages.Name | Should -Be @('dotnet-manifest')
    }

    It 'reports centrally declared NuGet package versions without evaluating MSBuild' {
        $root = Join-Path $TestDrive 'central-versions'
        New-Fixture (Join-Path $root 'Directory.Packages.props') '<Project><ItemGroup><PackageVersion Include="Example" Version="2.1.0" /></ItemGroup></Project>'
        $result = Get-DependencyInventory -Root $root
        $result.Packages[0].Version | Should -Be '2.1.0'
        $result.Packages[0].Scope | Should -Be 'central'
    }

    It 'rejects nonstandard JSON that native package tools would reject' -TestCases @(
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
        $result = Invoke-ProjectVerification -Root $root -Stage configuration
        $result.Status | Should -Be 'incomplete'
        $result.Stages[0].Status | Should -Be 'pass'
        { Invoke-ProjectVerification -Root $root -Stage typo } | Should -Throw '*Unknown stage*'
    }

    It 'includes build prerequisites for selected tests and blocks them after failure' {
        $root = Join-Path $TestDrive 'dependencies'
        New-Fixture (Join-Path $root 'tests.csproj') '<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>'
        Mock Invoke-ValidationStage {
            param($Stage)
            [pscustomobject]@{ Name = $Stage.Name; Status = 'fail'; ExitCode = 1; Summary = @('build failed'); Warnings = @(); AdvisoryWarnings = $false }
        } -ParameterFilter { $Stage.Name -like 'dotnet-build:*' }
        $result = Invoke-ProjectVerification -Root $root -Stage 'dotnet-test:tests.csproj'
        $result.Stages.Name | Should -Be @('dotnet-build:tests.csproj', 'dotnet-test:tests.csproj')
        $result.Stages[1].Status | Should -Be 'unavailable'
        $result.Status | Should -Be 'fail'
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
        $result.ScopedInstructions | Should -Be @('AGENTS.md', 'src/api/AGENTS.md')
        $result.ImportantPaths | Should -Contain '.claude/rules/api/style.md'
        { Get-ProjectContext -Root $root -Path '../outside.cs' } | Should -Throw '*safe repository path*'
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
        New-Fixture (Join-Path $root 'NODE_MODULES/hidden.txt') 'unique-fixture'
        New-Fixture (Join-Path $root '.ENV.PRODUCTION') 'unique-fixture'
        New-Fixture (Join-Path $root '.claude/settings.local.json') '{}'
        $native = @(Get-RepositoryFiles -Root $root | ForEach-Object Name)
        Mock Get-FirstCommand { $null } -ParameterFilter { $Names -contains 'rg' }
        $fallback = @(Get-RepositoryFiles -Root $root | ForEach-Object Name)
        $native | Should -Be @('visible.txt')
        $fallback | Should -Be $native
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

    It 'initializes both identities, escapes text and is idempotent' {
        $root = Join-Path $TestDrive 'init'
        $content = "<!-- project:start -->`n# Template`n<!-- project:end -->`nKeep this workflow.`n"
        New-Fixture (Join-Path $root 'README.md') $content
        New-Fixture (Join-Path $root 'AGENTS.md') $content
        $first = Initialize-Project -Root $root -ProjectName 'Sample App' -Description 'Costs $5 and uses [data] *carefully*.'
        $second = Initialize-Project -Root $root -ProjectName 'Sample App' -Description 'Costs $5 and uses [data] *carefully*.'
        $first.Changed.Count | Should -Be 2
        $second.Changed | Should -BeNullOrEmpty
        $readme = Get-Content -LiteralPath (Join-Path $root 'README.md') -Raw
        $readme | Should -Match 'Keep this workflow'
        $readme | Should -Match ([regex]::Escape('Costs $5 and uses \[data\] \*carefully\*.'))
        (Get-ProjectContext -Root $root).Project | Should -Be 'Sample App'
    }

    It 'preflights both identity files before changing either' {
        $root = Join-Path $TestDrive 'init-preflight'
        $content = '<!-- project:start -->old<!-- project:end -->'
        New-Fixture (Join-Path $root 'README.md') $content
        New-Fixture (Join-Path $root 'AGENTS.md') '# No markers'
        { Initialize-Project -Root $root -ProjectName Sample -Description Example } | Should -Throw '*marker pair*'
        (Get-Content -LiteralPath (Join-Path $root 'README.md') -Raw) | Should -Be $content
        @(Get-ChildItem -LiteralPath $root -Filter '*.tmp' -Force).Count | Should -Be 0
    }

    It 'rolls back the first identity if replacing the second fails' {
        $root = Join-Path $TestDrive 'init-rollback'
        $content = '<!-- project:start -->old<!-- project:end -->'
        New-Fixture (Join-Path $root 'README.md') $content
        New-Fixture (Join-Path $root 'AGENTS.md') $content
        Mock Set-AtomicText { throw 'simulated write failure' } -ParameterFilter { $Path -like '*AGENTS.md' }
        { Initialize-Project -Root $root -ProjectName Sample -Description Example } | Should -Throw '*simulated write failure*'
        (Get-Content -LiteralPath (Join-Path $root 'README.md') -Raw) | Should -Be $content
    }

    It 'rejects multiline identity input' {
        { Initialize-Project -Root $TestDrive -ProjectName Sample -Description "line`nline" } | Should -Throw '*single-line*'
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
        Mock Get-Module { [pscustomobject]@{Version=[version]'5.9.1'} } -ParameterFilter { $Name -eq 'Pester' }
        Mock Invoke-Pester {
            [pscustomobject]@{
                Result='Failed'; TotalCount=0; PassedCount=0; FailedCount=0; SkippedCount=0; NotRunCount=0; Failed=@()
                Containers=@([pscustomobject]@{Result='Failed'; Item='broken.Tests.ps1'; ErrorRecord=@([pscustomobject]@{Exception=[Exception]::new('Discovery failed')})})
            }
        }
        $result = Get-PesterOutcome -TestPath @($TestDrive)
        $result.Failures | Should -Be @('broken.Tests.ps1: Discovery failed')
    }

    It 'marks empty or skipped Pester runs unavailable' -TestCases @(
        @{Total=0; Skipped=0}
        @{Total=1; Skipped=1}
    ) {
        param($Total, $Skipped)
        Mock Get-Module { [pscustomobject]@{Version=[version]'5.9.1'} } -ParameterFilter { $Name -eq 'Pester' }
        Mock Invoke-Pester {
            [pscustomobject]@{Result='Passed'; TotalCount=$Total; PassedCount=0; FailedCount=0; SkippedCount=$Skipped; NotRunCount=0; Failed=@(); Containers=@()}
        }
        (Get-PesterOutcome -TestPath @($TestDrive)).Unavailable | Should -BeTrue
    }
}
