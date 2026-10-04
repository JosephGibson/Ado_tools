# Claude Code hooks of the planning procedure: a plan reaches the developer with its critique.
# PreToolUse on ExitPlanMode blocks a plan that records no critique pass, and exit 2 returns
# stderr to Claude as the reason. PostToolUse on Write adds a reminder, without blocking, when a
# plan written under docs/plans/ records none.
#
# The hook reads the plan text of the call and nothing on disk, and it checks the record, not the
# run: like tools/guard-git.ps1 it is a speed bump, and the rule in AGENTS.md holds without it.
[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# The record: a Critique heading, or a line that opens with the word, such as
# `Critique: skipped - no codex on PATH`. Matched at the start of a line so that a plan
# discussing a critique in prose does not pass for one.
$script:CritiqueRecordPattern = '(?im)^[ \t]{0,3}(#{1,6}[ \t]+)?Critique\b'

# A plan file of the repository, and not the index of docs/plans/.
$script:PlanFilePattern = '(?i)(^|/)docs/plans/(?!README\.md$)[^/]+\.md$'

function Get-Property {
    param([object] $Object, [string] $Name)
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Test-RecordsCritique {
    param([AllowEmptyString()][string] $Text)

    if ([string]::IsNullOrWhiteSpace($Text)) { return $false }
    return $Text -match $script:CritiqueRecordPattern
}

# The payload of a hook call as an object, or $null when it cannot be read. An unreadable
# payload lets the call through, as in tools/guard-git.ps1.
function Get-HookPayload {
    param([AllowEmptyString()][string] $Payload)

    if ([string]::IsNullOrWhiteSpace($Payload)) { return $null }
    try { return $Payload | ConvertFrom-Json } catch { return $null }
}

# Why the plan of an ExitPlanMode call cannot be presented, or $null to let the call through.
function Get-PlanGuardReason {
    param([AllowEmptyString()][string] $Payload)

    $data = Get-HookPayload -Payload $Payload
    if ($null -eq $data) { return $null }
    $toolName = [string](Get-Property -Object $data -Name 'tool_name')
    if ($toolName -and $toolName -cne 'ExitPlanMode') { return $null }
    $plan = [string](Get-Property -Object (Get-Property -Object $data -Name 'tool_input') -Name 'plan')
    if ([string]::IsNullOrWhiteSpace($plan) -or (Test-RecordsCritique -Text $plan)) { return $null }
    return 'Blocked by tools/guard-plan.ps1: this plan records no critique pass. Follow the ' +
        'critique-plan skill, then present the plan with a Critique section that gives each ' +
        'finding a verdict: applied, rejected or owed. When the critic cannot run, record ' +
        '"Critique: skipped" and the reason. The hook checks the record, not the run.'
}

# The reminder for a plan written without a record, or $null. Only Write carries the whole file,
# so an Edit that revises a plan is not inspected and the reminder arrives once, when it appears.
function Get-PlanWriteReminder {
    param([AllowEmptyString()][string] $Payload)

    $data = Get-HookPayload -Payload $Payload
    if ($null -eq $data) { return $null }
    $toolName = [string](Get-Property -Object $data -Name 'tool_name')
    if ($toolName -and $toolName -cne 'Write') { return $null }
    $toolInput = Get-Property -Object $data -Name 'tool_input'
    $path = ([string](Get-Property -Object $toolInput -Name 'file_path')).Replace('\', '/')
    if ($path -notmatch $script:PlanFilePattern) { return $null }
    $content = [string](Get-Property -Object $toolInput -Name 'content')
    if ([string]::IsNullOrWhiteSpace($content) -or (Test-RecordsCritique -Text $content)) { return $null }
    return "$path records no critique pass. Follow the critique-plan skill before the plan is " +
        'final, and give each finding a verdict in a Critique section: applied, rejected or owed.'
}

# Dot-sourced, the script defines its functions and stops, so tests check the rules in one process.
if ($MyInvocation.InvocationName -eq '.') { return }

try { $raw = [Console]::In.ReadToEnd() } catch { exit 0 }

$reason = Get-PlanGuardReason -Payload $raw
if ($null -ne $reason) {
    [Console]::Error.WriteLine($reason)
    exit 2
}

$reminder = Get-PlanWriteReminder -Payload $raw
if ($null -ne $reminder) {
    # Context, not an error: the file is written, and the pass is owed before the plan is final.
    $context = @{ hookSpecificOutput = @{ hookEventName = 'PostToolUse'; additionalContext = $reminder } }
    [Console]::Out.WriteLine(($context | ConvertTo-Json -Compress -Depth 4))
}

exit 0
