# Claude Code PostToolUse hook: dependency-free syntax gate on files the agent wrote.
# Catches broken PowerShell and malformed JSON (including .claude/*.json) immediately.
# Exit 2 returns stderr to Claude so it fixes the file in the same turn.
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
. (Join-Path $PSScriptRoot 'dev.ps1')
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
    '^\.(csproj|fsproj|vbproj|props|targets|slnx|config|xml)$' {
        # A hook that parses agent-written XML must not resolve a DTD or an external
        # entity as part of validation.
        $reader = $null
        try {
            $settings = New-Object System.Xml.XmlReaderSettings
            $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
            $settings.XmlResolver = $null
            $reader = [System.Xml.XmlReader]::Create($path, $settings)
            $document = New-Object System.Xml.XmlDocument
            $document.XmlResolver = $null
            $document.Load($reader)
        }
        catch {
            [Console]::Error.WriteLine("Malformed XML in ${path}: $($_.Exception.Message)")
            exit 2
        }
        finally {
            if ($null -ne $reader) { $reader.Dispose() }
        }
    }
}

exit 0
