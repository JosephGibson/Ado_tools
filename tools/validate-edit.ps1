# Claude Code PostToolUse hook: syntax gate on files the agent wrote.
# Catches broken PowerShell, malformed JSON (including .claude/*.json) and malformed XML
# (project files, string catalogs, the format file) immediately.
# Exit 2 returns stderr to Claude so it fixes the file in the same turn.
# The path rules and the JSON and XML readers are those of tools/dev.ps1, which this hook loads.
[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

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

$toolInput = Get-Property -Object $payload -Name 'tool_input'
$editedPath = [string](Get-Property -Object $toolInput -Name 'file_path')
if ([string]::IsNullOrWhiteSpace($editedPath)) { exit 0 }
$hookRoot = $env:CLAUDE_PROJECT_DIR
if ([string]::IsNullOrWhiteSpace($hookRoot)) { $hookRoot = [string](Get-Property -Object $payload -Name 'cwd') }
if ([string]::IsNullOrWhiteSpace($hookRoot)) { $hookRoot = Split-Path -Parent $PSScriptRoot }
# Any other exit code is a hook error that Claude never sees, so tooling that does not load
# must be reported here: no edit is checked until it loads again.
try { . (Join-Path $PSScriptRoot 'dev.ps1') }
catch {
    [Console]::Error.WriteLine("tools/validate-edit.ps1 could not load tools/dev.ps1, so $editedPath was not checked: $($_.Exception.Message)")
    exit 2
}
$path = $editedPath
if (-not [System.IO.Path]::IsPathRooted($path)) { $path = Join-Path $hookRoot $path }
if (-not (Test-IsRepositoryPath -Path $path -Root $hookRoot) -or -not (Test-IsAgentSafePath -Path $path -Root $hookRoot)) { exit 0 }
if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { exit 0 }
if (-not (Test-IsSafeRepositoryFile -File (Get-Item -LiteralPath $path -Force) -Root $hookRoot)) { exit 0 }

switch -Regex ([System.IO.Path]::GetExtension($path)) {
    '^\.ps(m|d)?1$' {
        $errors = $null
        [void] [System.Management.Automation.Language.Parser]::ParseFile($path, [ref] $null, [ref] $errors)
        if ($errors -and $errors.Count -gt 0) {
            $detail = ($errors | Select-Object -First 5 | ForEach-Object {
                "  line $($_.Extent.StartLineNumber): $($_.Message)"
            }) -join "`n"
            [Console]::Error.WriteLine("PowerShell syntax errors in ${path}:`n$detail")
            exit 2
        }
    }
    '^\.json$' {
        try { [void] (ConvertFrom-StrictJson -Text (Get-Content -LiteralPath $path -Raw)) }
        catch {
            [Console]::Error.WriteLine("Invalid JSON in ${path}: $($_.Exception.Message)")
            exit 2
        }
    }
    '^\.(csproj|props|targets|slnx|config|xml|resx|ps1xml)$' {
        # A hook that parses agent-written XML must not resolve a DTD or an external
        # entity as part of validation: Get-SafeXmlDocument prohibits both.
        try { [void] (Get-SafeXmlDocument -Path $path) }
        catch {
            [Console]::Error.WriteLine("Malformed XML in ${path}: $($_.Exception.Message)")
            exit 2
        }
    }
}

exit 0
