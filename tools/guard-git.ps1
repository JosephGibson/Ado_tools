# Claude Code PreToolUse hook: repository state belongs to the user, not the agent.
# Blocks Git commands other than the approved read-only subcommands.
# Exit 2 blocks the call and returns stderr to Claude as the reason.
#
# This is a speed bump, not a security boundary: it matches the command text Claude
# writes, so a program that reaches Git without naming it still gets through. It is
# deliberately conservative and will sometimes block a command that only quotes a Git
# subcommand. Ask the user to run the command rather than routing around the guard.
[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$script:ReadOnlySubcommands = @('status', 'diff', 'log', 'show', 'blame')

# Options that make Git run a program of the caller's choosing. A read-only subcommand
# carrying one of these is an arbitrary-code path, not a read: `git -c core.pager=<cmd>
# log` runs <cmd>. Matched case-sensitively so benign `-C <dir>` stays allowed.
$script:ForbiddenGitOptions = @('-c', '--config-env', '--exec-path', '--ext-diff', '--textconv', '--output', '--open-files-in-pager')
$script:GitOptionsWithValues = @('-C', '-c', '--git-dir', '--work-tree', '--namespace', '--super-prefix', '--config-env', '--exec-path')

# Programs that run their argument as a command, so the real program is further along.
$script:CommandWrappers = @('env', 'sudo', 'doas', 'xargs', 'time', 'timeout', 'nice', 'nohup', 'stdbuf', 'command', 'builtin')

# Shells that take a command as a string; their arguments are re-parsed and re-checked.
$script:ShellWrappers = @('bash', 'sh', 'zsh', 'dash', 'ksh', 'pwsh', 'powershell', 'cmd')

try { $raw = [Console]::In.ReadToEnd() } catch { exit 0 }
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }

function Get-Property {
    param([object] $Object, [string] $Name)
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-CommandElementText {
    param([object] $Element)

    if ($null -eq $Element) { return $null }
    if ($Element -is [System.Management.Automation.Language.StringConstantExpressionAst]) {
        return [string] $Element.Value
    }
    if ($Element -is [System.Management.Automation.Language.ExpandableStringExpressionAst]) {
        return [string] $Element.Value
    }
    if ($Element -is [System.Management.Automation.Language.CommandParameterAst]) {
        return [string] $Element.Extent.Text
    }
    # A bare number, such as the seconds in `timeout 30 git push`, parses as a constant
    # rather than a string. Return its text so the wrapper scan can step over it.
    if ($Element -is [System.Management.Automation.Language.ConstantExpressionAst]) {
        return [string] $Element.Extent.Text
    }
    return $null
}

function Get-CommandLeafName {
    param([string] $Text)

    if ([string]::IsNullOrWhiteSpace($Text)) { return '' }
    $leaf = $Text
    try { $leaf = [System.IO.Path]::GetFileName($Text) } catch { $leaf = $Text }
    if ([string]::IsNullOrWhiteSpace($leaf)) { $leaf = $Text }
    return ($leaf -replace '(?i)\.exe$', '').ToLowerInvariant()
}

function Get-ShellCommandArgument {
    param(
        [Parameter(Mandatory = $true)][object[]] $Element,
        [Parameter(Mandatory = $true)][int] $StartIndex
    )

    # A shell's command can arrive as one quoted string (bash -c "git push") or as loose
    # words (cmd /c git push), so check each non-flag argument and their concatenation.
    $arguments = New-Object System.Collections.ArrayList
    for ($cursor = $StartIndex + 1; $cursor -lt $Element.Count; $cursor++) {
        $text = Get-CommandElementText -Element $Element[$cursor]
        if ([string]::IsNullOrWhiteSpace($text) -or $text.StartsWith('-') -or $text.StartsWith('/')) { continue }
        [void] $arguments.Add($text)
    }
    if ($arguments.Count -gt 1) { [void] $arguments.Add(($arguments -join ' ')) }

    return @($arguments | Select-Object -Unique)
}

function Get-GitInvocation {
    param([Parameter(Mandatory = $true)][object] $CommandAst)

    $elements = @($CommandAst.CommandElements)
    $index = 0

    # Step over whatever stands in front of the real program: leading VAR=value
    # assignments, wrapper programs such as env or xargs, and their flags and numeric
    # arguments. Stop at the first token that names some other program, so a command
    # that merely mentions "git" as an argument is not treated as a Git invocation.
    while ($index -lt $elements.Count) {
        $text = Get-CommandElementText -Element $elements[$index]
        if ([string]::IsNullOrWhiteSpace($text)) { return $null }
        if ($text.StartsWith('-') -or $text -match '^[A-Za-z_][A-Za-z0-9_]*=' -or $text -match '^\d+[a-zA-Z]?$') {
            $index++
            continue
        }

        $leaf = Get-CommandLeafName -Text $text
        if ($leaf -eq 'git') { break }
        if ($leaf -in $script:ShellWrappers) {
            return [pscustomobject]@{
                Kind = 'shell'
                Scripts = @(Get-ShellCommandArgument -Element $elements -StartIndex $index)
            }
        }
        if ($leaf -in $script:CommandWrappers) {
            $index++
            continue
        }
        return $null
    }
    if ($index -ge $elements.Count) { return $null }

    $subcommand = ''
    $hasForbiddenOption = $false
    foreach ($element in $elements | Select-Object -Skip ($index + 1)) {
        $argument = Get-CommandElementText -Element $element
        if ($argument -eq '--') { break }
        if ($argument -cmatch '^-c.+' -or ($argument -split '=', 2)[0] -cin $script:ForbiddenGitOptions) { $hasForbiddenOption = $true }
    }
    for ($cursor = $index + 1; $cursor -lt $elements.Count; $cursor++) {
        $value = Get-CommandElementText -Element $elements[$cursor]
        if ([string]::IsNullOrWhiteSpace($value)) { break }
        if ($value.StartsWith('-')) {
            $optionName = ($value -split '=', 2)[0]
            if ($optionName -cin $script:ForbiddenGitOptions) { $hasForbiddenOption = $true }
            if ($optionName -in $script:GitOptionsWithValues -and -not $value.Contains('=')) {
                $cursor++
                if ($cursor -ge $elements.Count) { break }
            }
            continue
        }
        $subcommand = $value.ToLowerInvariant()
        break
    }

    return [pscustomobject]@{ Kind = 'git'; Subcommand = $subcommand; HasForbiddenOption = $hasForbiddenOption }
}

function Test-IsBlockedGitCommand {
    param(
        [Parameter(Mandatory = $true)][string] $CommandText,
        [int] $Depth = 0
    )

    if ([string]::IsNullOrWhiteSpace($CommandText)) { return $false }
    if ($Depth -gt 4) { return $CommandText -match '(?i)\bgit(?:\.exe)?\b' }

    $tokens = $null
    $parseErrors = $null
    try {
        $ast = [System.Management.Automation.Language.Parser]::ParseInput($CommandText, [ref] $tokens, [ref] $parseErrors)
        $commands = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] }, $true))
    }
    catch {
        # Nothing could be established about the command. Block it only if it names Git.
        return $CommandText -match '(?i)(^|[\s;&|(''"])git([\s;&|)''"]|$)'
    }

    foreach ($commandAst in $commands) {
        $invocation = Get-GitInvocation -CommandAst $commandAst
        if ($null -eq $invocation) { continue }
        if ($invocation.Kind -eq 'git') {
            if ($invocation.HasForbiddenOption) { return $true }
            if ($invocation.Subcommand -notin $script:ReadOnlySubcommands) { return $true }
            continue
        }
        foreach ($script in $invocation.Scripts) {
            if (Test-IsBlockedGitCommand -CommandText $script -Depth ($Depth + 1)) { return $true }
        }
    }

    return $false
}

$toolInput = Get-Property -Object $payload -Name 'tool_input'
$command = [string](Get-Property -Object $toolInput -Name 'command')
if ([string]::IsNullOrWhiteSpace($command)) { exit 0 }

if (Test-IsBlockedGitCommand -CommandText $command) {
    [Console]::Error.WriteLine(
        'Blocked by tools/guard-git.ps1: the user owns Git repository state. ' +
        'Only git status, diff, log, show, and blame are allowed, and not with -c, ' +
        '--config-env, or --exec-path. Ask the user to run other Git commands.')
    exit 2
}

exit 0
