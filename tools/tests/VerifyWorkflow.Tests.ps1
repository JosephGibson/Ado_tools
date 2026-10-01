BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    $github = Join-Path $PSScriptRoot '../../.github'
    $verifyPath = Join-Path $github 'workflows/verify.yml'
    $releasePath = Join-Path $github 'workflows/release.yml'
    $setupPath = Join-Path $github 'actions/setup/action.yml'
    # A named step as written: its lines from "- name:" to the next step.
    function Get-WorkflowStep {
        param([string] $Path, [string] $Name)
        $step = [Collections.Generic.List[string]]::new()
        $reading = $false
        foreach ($line in Get-Content -LiteralPath $Path) {
            if ($line.StartsWith('      - ')) {
                if ($reading) { break }
                $reading = $line.StartsWith("      - name: $Name", [StringComparison]::Ordinal)
            }
            if ($reading -and $line.Trim()) { $step.Add($line) }
        }
        if ($step.Count -eq 0) { throw "Step not found in $(Split-Path -Leaf $Path): $Name" }
        return $step.ToArray()
    }
}

Describe 'Pull request verification workflow' {
    It 'runs on a pull request to main and on nothing else' {
        $lines = @(Get-Content -LiteralPath $verifyPath)
        $start = [array]::IndexOf($lines, 'on:')
        $start | Should -BeGreaterThan -1
        $trigger = @($lines | Select-Object -Skip ($start + 1) | Where-Object { $_.Trim() })
        $end = 0
        while ($end -lt $trigger.Count -and $trigger[$end].StartsWith(' ')) { $end++ }
        $trigger[0..($end - 1)] | Should -Be @('  pull_request:', '    branches: [main]')
    }

    # A pull request runs code that was not reviewed yet: it gets a token that can only read.
    It 'holds a read-only token and no secret' {
        $lines = @(Get-Content -LiteralPath $verifyPath)
        @($lines | Where-Object { $_ -match '^\s*permissions:' }) | Should -Be @('permissions:')
        $lines[[array]::IndexOf($lines, 'permissions:') + 1] | Should -Be '  contents: read'
        $lines[[array]::IndexOf($lines, 'permissions:') + 2] | Should -Be ''
        $lines | Where-Object { $_ -match 'secrets\.|github\.token|_TOKEN|pull_request_target' } | Should -BeNullOrEmpty
    }

    It 'checks out without persisting the token' {
        $text = Get-Content -LiteralPath $verifyPath -Raw
        $checkout = [regex]::Match($text, '(?ms)^      - uses: actions/checkout@[^\r\n]+\r?\n((?:        [^\r\n]*\r?\n)*)')
        $checkout.Success | Should -BeTrue
        $checkout.Groups[1].Value | Should -Match '(?m)^          persist-credentials: false\s*$'
    }

    It 'pins every action to a commit and names its version, as the release workflow does' {
        $released = @(Get-Content -LiteralPath $releasePath | Where-Object { $_ -match '^\s*-?\s*uses:' })
        $uses = @(Get-Content -LiteralPath $verifyPath | Where-Object { $_ -match '^\s*-?\s*uses:' -and $_ -cne '      - uses: ./.github/actions/setup' })
        $uses.Count | Should -BeGreaterThan 0
        foreach ($line in $uses) {
            $line | Should -MatchExactly '^\s+- uses: [A-Za-z0-9_.-]+/[A-Za-z0-9_./-]+@[0-9a-f]{40} # v\d+\.\d+\.\d+$'
            $released | Should -Contain $line
        }
    }

    # One runtime, one actionlint and one set of modules decide both gates: a pull request that
    # passes here is verified with what the release will be verified with. The setup action holds
    # every pin, so neither workflow repeats one; ReleaseWorkflow.Tests.ps1 runs its steps.
    It 'installs the toolchain with the setup action, as the release workflow does: <Workflow>' -TestCases @(
        @{ Workflow = 'verify.yml' }, @{ Workflow = 'release.yml' }
    ) {
        param($Workflow)
        $lines = @(Get-Content -LiteralPath (Join-Path $github "workflows/$Workflow"))
        $checkout = [array]::IndexOf($lines, @($lines | Where-Object { $_.StartsWith('      - uses: actions/checkout@') })[0])
        $setup = [array]::IndexOf($lines, '      - uses: ./.github/actions/setup')
        $firstRun = [array]::IndexOf($lines, @($lines | Where-Object { $_.StartsWith('      - name: ') })[0])
        $setup | Should -BeGreaterThan $checkout
        $setup | Should -BeLessThan $firstRun
        $lines | Should -Contain "      ADOTOOLKIT_RELEASE_BUILD: '1'"
        $lines | Where-Object { $_ -match '_SHA256|POWERSHELL_VERSION:|ACTIONLINT_VERSION|setup-dotnet|Install-PSResource' } | Should -BeNullOrEmpty
        (Get-Content -LiteralPath $setupPath -Raw) | Should -Match '(?m)^  using: composite\s*$'
    }

    It 'restores in locked mode, as the release workflow does' {
        $step = Get-WorkflowStep -Path $verifyPath -Name 'Restore'
        $step | Should -Be (Get-WorkflowStep -Path $releasePath -Name 'Restore')
        $step | Should -Contain '        run: dotnet restore AdoToolkit.slnx --locked-mode'
    }

    It 'runs verify and fails the job unless it exits 0 (exit code <Code>)' -TestCases @(
        @{ Code = 0 }, @{ Code = 1 }, @{ Code = 2 }
    ) {
        param($Code)
        $script:verifyArguments = @()
        Mock pwsh { $script:verifyArguments = @($args); $global:LASTEXITCODE = $Code }
        $lines = @(Get-WorkflowStep -Path $verifyPath -Name 'Verify')
        $run = [array]::IndexOf($lines, '        run: |')
        $run | Should -BeGreaterThan -1
        $step = [scriptblock]::Create((($lines | Select-Object -Skip ($run + 1) | ForEach-Object { $_.Substring(10) }) -join "`n"))
        if ($Code -eq 0) { & $step }
        else { { & $step } | Should -Throw "*verify exited with $Code*" }
        $script:verifyArguments | Should -Be @('-NoProfile', '-File', './tools/dev.ps1', 'verify')
    }
}

# Actions are pinned by commit, so nothing moves them: Dependabot proposes the newer commit in a
# pull request, which the workflow above verifies.
Describe 'Action updates' {
    It 'watches the workflows and every local action for newer action releases' {
        $lines = @(Get-Content -LiteralPath (Join-Path $github 'dependabot.yml') | Where-Object { $_ -notmatch '^\s*(#|$)' })
        $lines | Should -Contain '  - package-ecosystem: github-actions'
        $start = [array]::IndexOf($lines, '    directories:')
        $start | Should -BeGreaterThan -1
        $watched = @($lines | Select-Object -Skip ($start + 1) | Where-Object { $_ -match '^      - ' } | ForEach-Object { $_.Substring(8).Trim() })
        # "/" covers .github/workflows; a composite action is watched through its own directory.
        $expected = @('/') + @(Get-ChildItem -LiteralPath (Join-Path $github 'actions') -Directory | Sort-Object Name | ForEach-Object { "/.github/actions/$($_.Name)" })
        $watched | Should -Be $expected
        $lines | Should -Contain '    schedule:'
    }
}
