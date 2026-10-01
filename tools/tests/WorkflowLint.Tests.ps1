BeforeAll {
    . (Join-Path $PSScriptRoot '../dev.ps1')
    function New-Fixture {
        param([string] $Path, [string] $Content = '')
        [void][System.IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
        [System.IO.File]::WriteAllText($Path, $Content.Replace("`r`n", "`n"), [System.Text.UTF8Encoding]::new($false))
    }
    # A repository with two workflows, a composite action and another YAML file under .github.
    function New-WorkflowRepository {
        param([string] $Name)
        $root = Join-Path $TestDrive $Name
        New-Fixture (Join-Path $root '.github/workflows/release.yml') 'name: Release'
        New-Fixture (Join-Path $root '.github/workflows/verify.yaml') 'name: Verify'
        New-Fixture (Join-Path $root '.github/actions/setup/action.yml') 'name: Setup'
        New-Fixture (Join-Path $root '.github/dependabot.yml') 'version: 2'
        return $root
    }
    function winget { throw 'Unexpected installation.' }
}

Describe 'Workflow lint stage' {
    It 'is part of the plan only when the repository has workflow files' {
        $with = New-WorkflowRepository 'plan-with'
        $without = Join-Path $TestDrive 'plan-without'
        New-Fixture (Join-Path $without '.github/dependabot.yml') 'version: 2'
        New-Fixture (Join-Path $without 'app.json') '{}'
        (Get-ProjectValidationPlan -Root $with).Stages.Name | Should -Be @('workflow-lint')
        (Get-ProjectValidationPlan -Root $without).Stages.Name | Should -Be @('configuration')
    }

    It 'hands actionlint the workflow files only and passes when it finds nothing' {
        $root = New-WorkflowRepository 'clean'
        $script:lint = $null
        Mock Get-FirstCommand { [pscustomobject]@{ Source = 'C:\tools\actionlint.exe' } } -ParameterFilter { $Names -contains 'actionlint' }
        Mock Invoke-BoundedProcess {
            $script:lint = @{ Executable = $Executable; Arguments = $Arguments; WorkingDirectory = $WorkingDirectory }
            [pscustomobject]@{ ExitCode = 0; Lines = @() }
        }
        $outcome = Get-WorkflowLintOutcome -ProjectProfile (Get-ProjectProfile -Root $root)
        $outcome.Failures | Should -BeNullOrEmpty
        $outcome.Summary | Should -Be @('2 workflow file(s) linted with actionlint')
        $outcome.PSObject.Properties['Unavailable'] | Should -BeNullOrEmpty
        $script:lint.Executable | Should -Be 'C:\tools\actionlint.exe'
        $script:lint.WorkingDirectory | Should -Be (Resolve-Path -LiteralPath $root).Path
        # The run blocks are PowerShell: the shell and Python integrations stay off wherever verify runs.
        $script:lint.Arguments | Should -Be @('-no-color', '-oneline', '-shellcheck=', '-pyflakes=', '.github/workflows/release.yml', '.github/workflows/verify.yaml')
    }

    It 'reports each finding of actionlint and fails verify' {
        $root = New-WorkflowRepository 'findings'
        $findings = @('.github/workflows/release.yml:3:5: unexpected key "step" for "job" section [syntax-check]', '.github/workflows/verify.yaml:9:15: property "tga" is not defined in object type {tag: string} [expression]')
        Mock Get-FirstCommand { [pscustomobject]@{ Source = 'actionlint.exe' } } -ParameterFilter { $Names -contains 'actionlint' }
        Mock Invoke-BoundedProcess { [pscustomobject]@{ ExitCode = 1; Lines = @($findings[0], '', "  $($findings[1])") } }
        $result = Invoke-ProjectVerification -Root $root
        $result.Status | Should -Be 'fail'
        $stage = $result.Stages | Where-Object Name -eq 'workflow-lint'
        $stage.Status | Should -Be 'fail'
        $stage.Summary | Should -Be ($findings + '2 workflow file(s) linted with actionlint')
    }

    It 'fails when actionlint stops without naming a finding' {
        $root = New-WorkflowRepository 'fatal'
        Mock Get-FirstCommand { [pscustomobject]@{ Source = 'actionlint.exe' } } -ParameterFilter { $Names -contains 'actionlint' }
        Mock Invoke-BoundedProcess { [pscustomobject]@{ ExitCode = 3; Lines = @() } }
        (Get-WorkflowLintOutcome -ProjectProfile (Get-ProjectProfile -Root $root)).Failures | Should -Be @('actionlint exited with 3.')
    }

    # A workflow that nothing linted is not a verified workflow.
    It 'makes verify incomplete, not passing, when actionlint is not installed' {
        $root = New-WorkflowRepository 'missing-tool'
        Mock Get-FirstCommand { $null } -ParameterFilter { $Names -contains 'actionlint' }
        Mock Invoke-BoundedProcess { throw 'Nothing to run.' }
        $result = Invoke-ProjectVerification -Root $root
        $result.Status | Should -Be 'incomplete'
        ($result.Stages | Where-Object Name -eq 'workflow-lint').Status | Should -Be 'unavailable'
        $result.Warnings | Should -Be @('workflow-lint: actionlint is required (run bootstrap -Install).')
    }
}

Describe 'actionlint as a prerequisite' {
    It 'is required when the repository has workflow files, and only then' {
        $with = New-WorkflowRepository 'tools-with'
        $without = Join-Path $TestDrive 'tools-without'
        New-Fixture (Join-Path $without 'app.json') '{}'
        Mock Get-FirstCommand { $null } -ParameterFilter { $Names -contains 'actionlint' }
        $diagnostics = Get-ProjectDiagnostics -Root $with
        $tool = $diagnostics.Tools | Where-Object Name -eq 'actionlint'
        $tool.Level | Should -Be 'required'
        $tool.State | Should -Be 'missing'
        $diagnostics.Status | Should -Be 'incomplete'
        $diagnostics.RequiredProblems | Should -Be @('actionlint')
        (Get-DependencyInventory -Root $with).ValidationTools | Should -Contain 'actionlint'
        (Get-ProjectDiagnostics -Root $without).Tools.Name | Should -Not -Contain 'actionlint'
    }

    It 'is installed by bootstrap with winget, and only on request' {
        Mock Get-ProjectDiagnostics {
            [pscustomobject]@{ Status = 'incomplete'; Tools = @([pscustomobject]@{ Name = 'actionlint'; Level = 'required'; State = 'missing' }) }
        }
        Mock Get-FirstCommand { [pscustomobject]@{ Source = 'winget.exe' } } -ParameterFilter { $Names -contains 'winget' }
        $script:wingetArguments = @()
        Mock winget { $script:wingetArguments = @($args); $global:LASTEXITCODE = 0 }

        (Invoke-ToolBootstrap -Root $TestDrive).Actions | Should -BeNullOrEmpty
        Should -Invoke winget -Exactly -Times 0

        $result = Invoke-ToolBootstrap -Root $TestDrive -Install
        $result.Actions.Name | Should -Be @('actionlint')
        $result.Actions.Status | Should -Be @('installed')
        $script:wingetArguments[0..5] | Should -Be @('install', '--id', 'rhysd.actionlint', '--exact', '--source', 'winget')
        # The mocked diagnostics still report it missing, as when winget extended PATH for new processes only.
        $result.Notes | Should -Contain 'A tool that winget installed is still missing here: it is found from a new terminal.'
    }

    It 'reports a failed winget installation' {
        Mock Get-ProjectDiagnostics {
            [pscustomobject]@{ Status = 'incomplete'; Tools = @([pscustomobject]@{ Name = 'actionlint'; Level = 'required'; State = 'missing' }) }
        }
        Mock Get-FirstCommand { [pscustomobject]@{ Source = 'winget.exe' } } -ParameterFilter { $Names -contains 'winget' }
        Mock winget { $global:LASTEXITCODE = 1; 'No package found matching input criteria.' }
        $result = Invoke-ToolBootstrap -Root $TestDrive -Install
        $result.Status | Should -Be 'incomplete'
        $result.Actions.Status | Should -Be @('failed')
        $result.Actions.Detail | Should -Be @('No package found matching input criteria.')
    }
}
