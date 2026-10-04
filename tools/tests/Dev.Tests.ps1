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

    It 'detects the .NET and PowerShell stacks and only identifies actual test files' {
        $repository = Join-Path $TestDrive 'profile-sample'
        New-TestFile -Path (Join-Path $repository 'tools\app.ps1')
        New-TestFile -Path (Join-Path $repository 'src\App\App.csproj') -Content '<Project Sdk="Microsoft.NET.Sdk" />'
        New-TestFile -Path (Join-Path $repository 'src\App\Program.cs')
        New-TestFile -Path (Join-Path $repository 'tests\App.Tests\SampleTests.cs')
        New-TestFile -Path (Join-Path $repository 'tests\App.Shell.Tests\Sample.Pester.ps1')
        New-TestFile -Path (Join-Path $repository 'tests\App.Shell.Tests\Support\Server.ps1')
        New-TestFile -Path (Join-Path $repository 'tools\tests\app.Tests.ps1')
        # Other ecosystems are not part of this product and are not reported.
        New-TestFile -Path (Join-Path $repository 'package.json') -Content '{}'

        $projectProfile = Get-ProjectProfile -Root $repository

        $projectProfile.Stack | Should -Be @('dotnet', 'powershell')
        $projectProfile.Languages | Should -Be @('C#', 'PowerShell')
        $projectProfile.ProjectFiles | Should -Be @('src/App/App.csproj')
        $projectProfile.TestFiles | Sort-Object | Should -Be @('tests/App.Shell.Tests/Sample.Pester.ps1', 'tests/App.Tests/SampleTests.cs', 'tools/tests/app.Tests.ps1')
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

    It 'excludes every .env prefix in both discovery implementations' -ForEach @(
        @{ UseRipgrep = $true }
        @{ UseRipgrep = $false }
    ) {
        $repository = Join-Path $TestDrive "env-prefix-$UseRipgrep"
        New-TestFile -Path (Join-Path $repository 'src/worker.ps1') -Content 'SyntheticMarker'
        foreach ($name in @('.env', '.env.local', '.envrc', '.environment')) {
            New-TestFile -Path (Join-Path $repository $name) -Content 'SyntheticMarker'
            New-TestFile -Path (Join-Path $repository "nested/$name/notes.txt") -Content 'SyntheticMarker'
            Test-IsAgentSafePath -Path (Join-Path $repository $name) -Root $repository | Should -BeFalse
        }
        if (-not $UseRipgrep) { Mock Get-FirstCommand { $null } -ParameterFilter { $Names -contains 'rg' } }

        $result = Find-ProjectSource -Query 'SyntheticMarker' -Root $repository

        $result.Results.Path | Should -Be @('src/worker.ps1')
        @(Get-RepositoryFiles -Root $repository).Count | Should -Be 1
    }

    # Claude Code creates a Git worktree, a full checkout, under .claude/worktrees/<name>/.
    It 'neither lists nor searches the files of a Claude Code worktree, with ripgrep <UseRipgrep>' -ForEach @(
        @{ UseRipgrep = $true }
        @{ UseRipgrep = $false }
    ) {
        $repository = Join-Path $TestDrive "worktree-discovery-$UseRipgrep"
        $worktree = Join-Path $repository '.claude/worktrees/feature'
        New-TestFile -Path (Join-Path $repository 'src/worker.ps1') -Content 'SyntheticMarker'
        New-TestFile -Path (Join-Path $repository '.claude/settings.json') -Content '{}'
        New-TestFile -Path (Join-Path $worktree 'src/worker.ps1') -Content 'SyntheticMarker'
        New-TestFile -Path (Join-Path $worktree 'docs/notes.md') -Content 'SyntheticMarker'
        if (-not $UseRipgrep) { Mock Get-FirstCommand { $null } -ParameterFilter { $Names -contains 'rg' } }

        @(Get-RepositoryFiles -Root $repository | ForEach-Object { Get-RelativeRepositoryPath -Path $_.FullName -Root $repository }) |
            Should -Be @('.claude/settings.json', 'src/worker.ps1')
        (Find-ProjectSource -Query 'SyntheticMarker' -Root $repository).Results.Path | Should -Be @('src/worker.ps1')
        (Find-ProjectSource -Query 'notes' -Root $repository).Count | Should -Be 0
        Test-IsSafeRepositoryTarget -Path (Join-Path $worktree 'src/worker.ps1') -Root $repository | Should -BeFalse
        # Inside the worktree, discovery sees its files: the exclusion is anchored at the root.
        @(Get-RepositoryFiles -Root $worktree | ForEach-Object { Get-RelativeRepositoryPath -Path $_.FullName -Root $worktree }) |
            Should -Be @('docs/notes.md', 'src/worker.ps1')
        (Find-ProjectSource -Query 'SyntheticMarker' -Root $worktree).Results.Path | Should -Be @('docs/notes.md', 'src/worker.ps1')
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

    It 'does not enumerate excluded output directories' {
        $repository = Join-Path $TestDrive 'excluded-sample'
        New-TestFile -Path (Join-Path $repository 'src\worker.ps1')
        New-TestFile -Path (Join-Path $repository 'artifacts\package\ignored.ps1')
        New-TestFile -Path (Join-Path $repository 'bin\generated.ps1')

        $files = Get-RepositoryFiles -Root $repository
        $paths = @($files | ForEach-Object { Get-RelativeRepositoryPath -Path $_.FullName -Root $repository })

        $paths | Should -Be @('src/worker.ps1')
    }

    It 'falls back to pruned filesystem discovery when ripgrep is unavailable' {
        $repository = Join-Path $TestDrive 'fallback-sample'
        New-TestFile -Path (Join-Path $repository 'src\worker.ps1')
        New-TestFile -Path (Join-Path $repository 'obj\package\ignored.ps1')
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

        $result.Status | Should -Be 'ok'
        $result.Stack | Should -Be @('powershell')
    }

    It 'returns a compact error for a missing source query' {
        { Find-ProjectSource -Root $TestDrive } | Should -Throw 'find requires -Query.'
    }

    It 'limits dependency output while retaining the total count' {
        $repository = Join-Path $TestDrive 'dependency-sample'
        New-TestFile -Path (Join-Path $repository 'Directory.Packages.props') -Content @'
<Project>
  <ItemGroup>
    <PackageVersion Include="Alpha" Version="1.0.0" />
    <PackageVersion Include="Beta" Version="2.0.0" />
    <PackageVersion Include="Gamma" Version="3.0.0" />
  </ItemGroup>
</Project>
'@

        $result = Get-DependencyInventory -Root $repository -Limit 2

        $result.PackageCount | Should -Be 3
        $result.Packages.Name | Should -Be @('Alpha', 'Beta')
        $result.Truncated | Should -BeTrue
    }

    It 'reports an invalid dependency manifest without hiding the failure' {
        $repository = Join-Path $TestDrive 'invalid-manifest-sample'
        New-TestFile -Path (Join-Path $repository 'sample.csproj') -Content '<Project><ItemGroup>'
        New-TestFile -Path (Join-Path $repository 'Directory.Packages.props') -Content '<Project><ItemGroup><PackageVersion Include="Kept" Version="1.0.0" /></ItemGroup></Project>'

        $result = Get-DependencyInventory -Root $repository

        $result.Status | Should -Be 'incomplete'
        @($result.Errors).Count | Should -Be 1
        $result.Errors[0] | Should -BeLike 'sample.csproj: *'
        $result.Packages.Name | Should -Be @('Kept')
    }

    It 'names the tools verify needs for the repository content' {
        $repository = Join-Path $TestDrive 'tools-sample'
        New-TestFile -Path (Join-Path $repository 'src\App\App.csproj') -Content '<Project Sdk="Microsoft.NET.Sdk" />'
        New-TestFile -Path (Join-Path $repository 'tools\tests\app.Tests.ps1')
        New-TestFile -Path (Join-Path $repository 'docs\commands\en-US\Get-Sample.md') -Content '# Get-Sample'

        (Get-DependencyInventory -Root $repository).ValidationTools |
            Should -Be @('dotnet', 'Microsoft.PowerShell.PlatyPS', 'Pester', 'PSScriptAnalyzer')
        (Get-ProjectDiagnostics -Root $repository).Tools.Name |
            Should -Be @('PowerShell', 'Pester', 'PSScriptAnalyzer', 'dotnet', 'Microsoft.PowerShell.PlatyPS', 'ripgrep')
    }

    # gh opens the pull request of the release handoff. It gates no stage, so it is recommended, and
    # it is listed only where workflow files show that the remote is GitHub.
    It 'recommends gh only in a repository that has GitHub workflows' {
        $withWorkflow = Join-Path $TestDrive 'gh-workflow'
        New-TestFile -Path (Join-Path $withWorkflow '.github\workflows\verify.yml') -Content 'name: Verify'
        $withoutWorkflow = Join-Path $TestDrive 'gh-no-workflow'
        New-TestFile -Path (Join-Path $withoutWorkflow 'app.ps1')

        $listed = @((Get-ProjectDiagnostics -Root $withWorkflow).Tools | Where-Object { $_.Name -eq 'gh' })

        $listed.Count | Should -Be 1
        $listed[0].Level | Should -Be 'recommended'
        @((Get-ProjectDiagnostics -Root $withoutWorkflow).Tools | Where-Object { $_.Name -eq 'gh' }) | Should -BeNullOrEmpty
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
        $pinned = [version] (Import-PowerShellDataFile -LiteralPath (Join-Path $PSScriptRoot '../BuildModules.psd1')).Pester
        Mock Get-Module {
            @([pscustomobject]@{ Name = 'Pester'; Version = $pinned; Path = 'pinned-pester.psd1' },
              [pscustomobject]@{ Name = 'Pester'; Version = [version]::new($pinned.Major, $pinned.Minor + 1, 0); Path = 'newer-pester.psd1' })
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

    # The help compiler is required by the product gate, so bootstrap must be able to install it.
    It 'installs every missing build module, including the help compiler, within its supported range' {
        Mock Get-ProjectDiagnostics {
            [pscustomobject]@{
                Status = 'incomplete'
                Tools = @(
                    [pscustomobject]@{ Name = 'Pester'; Level = 'required'; State = 'missing' }
                    [pscustomobject]@{ Name = 'PSScriptAnalyzer'; Level = 'required'; State = 'missing' }
                    [pscustomobject]@{ Name = 'Microsoft.PowerShell.PlatyPS'; Level = 'required'; State = 'outdated' }
                    [pscustomobject]@{ Name = 'dotnet'; Level = 'required'; State = 'missing' }
                )
            }
        }
        Mock Install-Module { }

        $result = Invoke-ToolBootstrap -Root $TestDrive -Install

        @($result.Actions | Where-Object Status -eq 'installed').Name | Should -Be @('Pester', 'PSScriptAnalyzer', 'Microsoft.PowerShell.PlatyPS')
        @($result.Actions | Where-Object Status -eq 'unsupported').Name | Should -Be @('dotnet')
        Should -Invoke Install-Module -Exactly -Times 1 -ParameterFilter { $Name -eq 'Microsoft.PowerShell.PlatyPS' -and $MinimumVersion -eq '1.0' -and $MaximumVersion -eq '1.999.999' -and $Scope -eq 'CurrentUser' }
        Should -Invoke Install-Module -Exactly -Times 1 -ParameterFilter { $Name -eq 'Pester' -and $MaximumVersion -eq '5.999.999' }
        Should -Invoke Install-Module -Exactly -Times 3
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

    It 'offers only the commands that apply to this repository' {
        $entryPoint = Get-Command -Name (Join-Path $PSScriptRoot '..\dev.ps1')

        $commands = $entryPoint.Parameters['Command'].Attributes | Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] }
        $commands.ValidValues | Should -Be @('inspect', 'context', 'find', 'plan', 'verify', 'diagnose', 'deps', 'bootstrap')
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

    It 'derives search exclusions from one list so discovery does not depend on ripgrep' {
        $globs = @(Get-RepositoryExclusionGlobs)
        $searchArguments = @(Get-RepositorySearchGlobs)

        foreach ($expected in @('!artifacts/**', '!**/artifacts/**', '!secret/**', '!secrets/**', '!**/.env*', '!*.log')) {
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

    It 'lists the instruction files of an inspection as an array, for one file and for none' {
        $one = Join-Path $TestDrive 'one-instruction-sample'
        $none = Join-Path $TestDrive 'no-instruction-sample'
        New-TestFile -Path (Join-Path $one 'AGENTS.md') -Content '# Sample'
        New-TestFile -Path (Join-Path $none 'notes.txt')

        $single = Get-ProjectInspection -Root $one | ConvertTo-Json -Depth 12 -Compress
        $empty = Get-ProjectInspection -Root $none | ConvertTo-Json -Depth 12 -Compress

        $single | Should -Match ([regex]::Escape('"AgentInstructions":["AGENTS.md"]'))
        $empty | Should -Match ([regex]::Escape('"AgentInstructions":[]'))
    }

    It 'keeps the workflow entry point first when the path budget is tight' {
        $repository = Join-Path $TestDrive 'path-budget-sample'
        New-TestFile -Path (Join-Path $repository 'tools\dev.ps1')
        foreach ($index in 1..45) {
            New-TestFile -Path (Join-Path $repository ('src\area{0:d2}\AGENTS.md' -f $index)) -Content '# area'
        }

        $context = Get-ProjectContext -Root $repository

        $context.ImportantPaths[0] | Should -Be 'tools/dev.ps1'
        $context.ImportantPaths.Count | Should -Be 40
        $context.ImportantPaths[39] | Should -Be 'src/area39/AGENTS.md'
        $context.ImportantPathCount | Should -Be 46
    }
}
