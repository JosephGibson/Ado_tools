# Claude Code PreToolUse hook of the read-only subagents, .claude/agents/area-reviewer.md and
# .claude/agents/docs-sync.md: their shell runs git diff and tools/dev.ps1 find, nothing else.
# Exit 2 blocks the call and returns stderr to the agent as the reason.
#
# Like tools/guard-git.ps1, which still checks every command, this matches the command text and
# is not a sandbox. It allows one simple command and rejects anything it cannot read plainly.
[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# The options of tools/guard-git.ps1 that make Git run a program or write a file, and
# --no-index, which compares files outside the repository.
$script:ForbiddenDiffOptions = @('-c', '--config-env', '--exec-path', '--ext-diff', '--textconv', '--output', '--open-files-in-pager', '--no-index')

function Get-Property {
    param([object] $Object, [string] $Name)
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

# The words of a command that is one plain program call, or $null. Outside single quotes no
# character may chain, pipe, redirect, substitute or expand, in Bash or in PowerShell, and an
# escaped quote, which would shift the quoting, is refused.
function Get-PlainCommandWord {
    param([Parameter(Mandatory = $true)][string] $CommandText)

    if ($CommandText -match '\\[''"]') { return $null }
    $unquoted = [regex]::Replace($CommandText, "'[^']*'", "''")
    if ($unquoted -match '[;&|<>`$(){}\r\n]') { return $null }

    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseInput($CommandText, [ref] $tokens, [ref] $parseErrors)
    if (@($parseErrors).Count -gt 0) { return $null }
    $statements = @($ast.EndBlock.Statements)
    if ($null -ne $ast.BeginBlock -or $null -ne $ast.ProcessBlock -or $statements.Count -ne 1 -or
        $statements[0] -isnot [System.Management.Automation.Language.PipelineAst]) { return $null }
    $elements = @($statements[0].PipelineElements)
    if ($elements.Count -ne 1 -or $elements[0] -isnot [System.Management.Automation.Language.CommandAst] -or
        @($elements[0].Redirections).Count -gt 0) { return $null }

    $words = New-Object System.Collections.Generic.List[string]
    foreach ($element in $elements[0].CommandElements) {
        if ($element -is [System.Management.Automation.Language.StringConstantExpressionAst]) { $words.Add([string] $element.Value) }
        elseif ($element -is [System.Management.Automation.Language.CommandParameterAst] -or
            $element -is [System.Management.Automation.Language.ConstantExpressionAst]) { $words.Add([string] $element.Extent.Text) }
        else { return $null }
    }
    return , $words.ToArray()
}

function Test-IsAllowedCommand {
    param([string] $CommandText)

    if ([string]::IsNullOrWhiteSpace($CommandText)) { return $false }
    $words = Get-PlainCommandWord -CommandText $CommandText.Trim()
    if ($null -eq $words -or $words.Count -lt 2) { return $false }
    $program = ([System.IO.Path]::GetFileName($words[0]) -replace '(?i)\.exe$', '').ToLowerInvariant()

    if ($program -eq 'git') {
        if ($words[1] -cne 'diff') { return $false }
        foreach ($word in $words | Select-Object -Skip 2) {
            if ($word -eq '--') { break }
            if (($word -split '=', 2)[0] -cin $script:ForbiddenDiffOptions) { return $false }
        }
        return $true
    }

    # What follows -File is data for the script, so only the script and its command are fixed.
    if ($program -eq 'pwsh') {
        if ($words.Count -lt 5) { return $false }
        $scriptPath = $words[3].Replace('\', '/') -replace '^\./', ''
        return $words[1] -eq '-NoProfile' -and $words[2] -eq '-File' -and $scriptPath -eq 'tools/dev.ps1' -and $words[4] -eq 'find'
    }
    return $false
}

# The hook's answer to one payload: why it blocks the command, or $null to let it run. A payload
# it cannot read lets the command run.
function Get-ReadOnlyGuardReason {
    param([AllowEmptyString()][string] $Payload)

    if ([string]::IsNullOrWhiteSpace($Payload)) { return $null }
    try { $data = $Payload | ConvertFrom-Json } catch { return $null }
    $toolInput = Get-Property -Object $data -Name 'tool_input'
    $command = [string](Get-Property -Object $toolInput -Name 'command')
    if ([string]::IsNullOrWhiteSpace($command) -or (Test-IsAllowedCommand -CommandText $command)) { return $null }
    return 'Blocked by tools/guard-readonly.ps1: this agent is read-only. Its shell runs one command at a ' +
        "time, either git diff <arguments> or pwsh -NoProfile -File .\tools\dev.ps1 find -Query '<literal>', " +
        'without chaining, pipes, redirection or substitution. Use Read, Grep and Glob for everything else.'
}

# Dot-sourced, the script defines its functions and stops, so tests check the rules in one process.
if ($MyInvocation.InvocationName -eq '.') { return }

try { $raw = [Console]::In.ReadToEnd() } catch { exit 0 }
$reason = Get-ReadOnlyGuardReason -Payload $raw
if ($null -eq $reason) { exit 0 }
[Console]::Error.WriteLine($reason)
exit 2