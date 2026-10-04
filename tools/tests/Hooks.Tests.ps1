# The guards decide from the command text alone. Dot-sourced, each defines its functions and stops,
# so the cases run in this process; one call per hook checks it the way Claude Code runs it.
Describe 'tools/guard-git.ps1' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\guard-git.ps1')
    }

    It 'blocks Git commands that change state, run a program, or hide behind a prefix, a wrapper, or a shell' {
        $git = 'git'
        $blocked = @(
            $git + ' config user.name example'
            $git + ' -C . commit -m test'
            'GIT_DIR=.git ' + $git + ' commit -m x'
            'env ' + $git + ' push'
            'timeout 30 ' + $git + ' push'
            'bash -c "' + $git + ' push"'
            'cmd /c ' + $git + ' push'
            $git + ' -c core.pager=start log'
            $git + ' -ccore.pager=example log'
            $git + ' --exec-path=/tmp status'
            $git + ' diff --ext-diff'
            $git + ' log --textconv'
            $git + ' diff --output=out.txt'
            'x=$(' + $git + ' push)'
            'branch=$(' + $git + ' rev-parse --abbrev-ref HEAD)'
            'echo `' + $git + ' push`'
            'if true; then ' + $git + ' push; fi'
            'if false; then true; elif ' + $git + ' push; then true; fi'
            'eval "' + $git + ' push"'
            'exec ' + $git + ' push'
        )

        foreach ($command in $blocked) {
            Test-IsBlockedGitCommand -CommandText $command | Should -BeTrue -Because "'$command' reaches Git"
        }
    }

    It 'allows the read-only subcommands and commands that only name Git' {
        $git = 'git'
        $allowed = @(
            $git + ' status'
            $git + ' -C . status'
            $git + ' log --oneline main'
            'grep -n "' + $git + '" notes.md'
            'bash -lc "npm test"'
            'changes=$(' + $git + ' status --short)'
            # The arguments of a script started with -File are data, not command text.
            'pwsh -NoProfile -File .\tools\dev.ps1 find -Query ' + $git
            'pwsh -NoProfile -File .\tools\dev.ps1 find -Query ''' + $git + ' commit'''
        )

        foreach ($command in $allowed) {
            Test-IsBlockedGitCommand -CommandText $command | Should -BeFalse -Because "'$command' does not change Git state"
        }
    }

    It 'gives a reason for a blocked command and lets every other payload through' {
        $payload = { param([string] $Command) @{ tool_input = @{ command = $Command } } | ConvertTo-Json -Compress }

        Get-GitGuardReason -Payload (& $payload 'git -C . commit -m test') | Should -BeLike 'Blocked by tools/guard-git.ps1:*'
        Get-GitGuardReason -Payload (& $payload 'git status') | Should -BeNullOrEmpty
        foreach ($other in @('', ' ', '{not json', '{}', '{"tool_input":{}}', (& $payload ''))) {
            Get-GitGuardReason -Payload $other | Should -BeNullOrEmpty -Because "'$other' names no command to block"
        }
    }

    It 'exits 2 with its reason when Claude Code passes it a blocked command' {
        $payload = @{ tool_input = @{ command = 'git -C . commit -m test' } } | ConvertTo-Json -Compress
        $output = @($payload | & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File (Join-Path $PSScriptRoot '..\guard-git.ps1') 2>&1)

        $LASTEXITCODE | Should -Be 2
        "$output" | Should -BeLike 'Blocked by tools/guard-git.ps1:*'
    }
}

Describe 'tools/guard-readonly.ps1' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\guard-readonly.ps1')
    }

    It 'lets the read-only subagents run only git diff and the find command' {
        $allowed = @(
            'git diff'
            'git diff --name-status --no-renames origin/main...HEAD -- tools/dev.ps1'
            'git diff HEAD -- "docs/commands/en-US/Get-Sample.md"'
            "pwsh -NoProfile -File .\tools\dev.ps1 find -Query 'Get-Sample' -Limit 10"
            # A quoted query is data for the script.
            "pwsh -NoProfile -File ./tools/dev.ps1 find -Query 'a; b | c'"
        )
        $blocked = @(
            'git status'
            'git diff --output=diff.txt'
            'git diff --ext-diff'
            'git diff --no-index one.txt two.txt'
            'git -C . diff'
            'git diff; git status'
            'git diff | Out-File diff.txt'
            'git diff > diff.txt'
            'git diff $(git status)'
            'git diff `git status`'
            "git diff \'; git status ;'"
            "git diff`ngit status"
            'cat tools/dev.ps1'
            'pwsh -NoProfile -File .\tools\dev.ps1 verify'
            'pwsh -NoProfile -Command Get-Date'
        )

        foreach ($command in $allowed) {
            Test-IsAllowedCommand -CommandText $command | Should -BeTrue -Because "'$command' only reads"
        }
        foreach ($command in $blocked) {
            Test-IsAllowedCommand -CommandText $command | Should -BeFalse -Because "'$command' is not git diff or the find command alone"
        }
    }

    It 'gives a reason for any other command and lets the allowed commands and unreadable payloads through' {
        $payload = { param([string] $Command) @{ tool_input = @{ command = $Command } } | ConvertTo-Json -Compress }

        Get-ReadOnlyGuardReason -Payload (& $payload 'git status') | Should -BeLike 'Blocked by tools/guard-readonly.ps1:*'
        Get-ReadOnlyGuardReason -Payload (& $payload 'git diff') | Should -BeNullOrEmpty
        foreach ($other in @('', ' ', '{not json', '{}', '{"tool_input":{}}', (& $payload ''))) {
            Get-ReadOnlyGuardReason -Payload $other | Should -BeNullOrEmpty -Because "'$other' names no command to block"
        }
    }

    It 'exits 2 with its reason when Claude Code passes it any other command' {
        $payload = @{ tool_input = @{ command = 'git status' } } | ConvertTo-Json -Compress
        $output = @($payload | & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File (Join-Path $PSScriptRoot '..\guard-readonly.ps1') 2>&1)

        $LASTEXITCODE | Should -Be 2
        "$output" | Should -BeLike 'Blocked by tools/guard-readonly.ps1:*'
    }
}

Describe 'tools/validate-edit.ps1' {
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

    # Discovery leaves .claude/worktrees out of the main checkout; the hook must still check
    # what an agent edits there, whichever checkout Claude Code names as the project.
    It 'checks a file edited in a Claude Code worktree when the project directory is <ProjectDirectory>' -ForEach @(
        @{ ProjectDirectory = 'the main checkout' }
        @{ ProjectDirectory = 'the worktree' }
    ) {
        $pwsh = Join-Path $PSHOME 'pwsh.exe'
        $hook = Join-Path $PSScriptRoot '..\validate-edit.ps1'
        $repository = Join-Path $TestDrive ('worktree-edit-' + $ProjectDirectory.Replace(' ', '-'))
        $worktree = Join-Path $repository '.claude\worktrees\feature'
        $broken = Join-Path $worktree 'tools\broken.ps1'
        New-TestFile -Path $broken -Content 'function Incomplete {'
        $payload = @{ cwd = $worktree; tool_input = @{ file_path = $broken } } | ConvertTo-Json -Compress
        $previous = $env:CLAUDE_PROJECT_DIR
        try {
            $env:CLAUDE_PROJECT_DIR = if ($ProjectDirectory -eq 'the worktree') { $worktree } else { $repository }
            $output = @($payload | & $pwsh -NoProfile -File $hook 2>&1)
        }
        finally { $env:CLAUDE_PROJECT_DIR = $previous }

        $LASTEXITCODE | Should -Be 2
        "$output" | Should -BeLike '*PowerShell syntax errors*broken.ps1*'
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

    # The edit guard takes its path rules from tools/dev.ps1. A hook that stops with any other
    # exit code is ignored, so a syntax error in the tooling would switch the guard off.
    It 'reports tooling that does not load instead of leaving the edit unchecked' {
        $pwsh = Join-Path $PSHOME 'pwsh.exe'
        $repository = Join-Path $TestDrive 'broken-tooling'
        foreach ($name in @('validate-edit.ps1', 'dev.ps1', 'lib/discovery.ps1', 'lib/dependencies.ps1', 'lib/processes.ps1', 'lib/validation.ps1')) {
            New-TestFile (Join-Path $repository "tools/$name") (Get-Content -LiteralPath (Join-Path $PSScriptRoot "../$name") -Raw)
        }
        New-TestFile (Join-Path $repository 'tools/lib/setup.ps1') 'function Incomplete {'
        $edited = Join-Path $repository 'settings.json'
        New-TestFile $edited '{}'
        $payload = @{ cwd = $repository; tool_input = @{ file_path = $edited } } | ConvertTo-Json -Compress

        $output = @($payload | & $pwsh -NoProfile -File (Join-Path $repository 'tools/validate-edit.ps1') 2>&1)

        $LASTEXITCODE | Should -Be 2
        "$output" | Should -BeLike '*could not load tools/dev.ps1*settings.json was not checked*'
    }

    # String catalogs and the format file are XML that an agent edits by hand.
    It 'checks <Name> as XML in the edit guard and in the configuration stage' -TestCases @(
        @{ Name = 'Strings.fr.resx' }
        @{ Name = 'AdoToolkit.Format.ps1xml' }
    ) {
        param($Name)
        $pwsh = Join-Path $PSHOME 'pwsh.exe'
        $hook = Join-Path $PSScriptRoot '..\validate-edit.ps1'
        $repository = Join-Path $TestDrive ('xml-' + $Name)
        $file = Join-Path $repository $Name
        New-TestFile -Path $file -Content '<root><data name="Unclosed"></root>'
        $payload = @{ cwd = $repository; tool_input = @{ file_path = $file } } | ConvertTo-Json -Compress

        $null = @($payload | & $pwsh -NoProfile -File $hook 2>&1)
        $LASTEXITCODE | Should -Be 2
        (Get-ConfigurationOutcome -ProjectProfile (Get-ProjectProfile -Root $repository)).Failures | Should -BeLike "$Name*"

        New-TestFile -Path $file -Content '<root><data name="Closed" /></root>'
        $null = @($payload | & $pwsh -NoProfile -File $hook 2>&1)
        $LASTEXITCODE | Should -Be 0
        (Get-ConfigurationOutcome -ProjectProfile (Get-ProjectProfile -Root $repository)).Failures | Should -BeNullOrEmpty
    }
}
