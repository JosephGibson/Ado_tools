BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    . (Join-Path $PSScriptRoot '../lib/dependencies.ps1')
    $workflowPath = Join-Path $PSScriptRoot '../../.github/workflows/release.yml'
    function Get-ReleaseStep {
        param([string] $Name)
        $lines = Get-Content -LiteralPath $workflowPath
        $found = $false
        $reading = $false
        $body = [Collections.Generic.List[string]]::new()
        foreach ($line in $lines) {
            if ($line.StartsWith('      - name: ')) {
                if ($reading) { break }
                $found = $line.Substring(14).StartsWith($Name, [StringComparison]::Ordinal)
            }
            if ($found -and $line -eq '        run: |') { $reading = $true; continue }
            if ($reading -and $line.StartsWith('          ')) { $body.Add($line.Substring(10)) }
        }
        if ($body.Count -eq 0) { throw "Release step not found: $Name" }
        return [scriptblock]::Create($body -join "`n")
    }
}

Describe 'Release module selection' {
    BeforeEach { $previousRelease = $env:ADOTOOLKIT_RELEASE_BUILD; $env:ADOTOOLKIT_RELEASE_BUILD = '1' }
    AfterEach { $env:ADOTOOLKIT_RELEASE_BUILD = $previousRelease }

    It 'selects the exact <ModuleName> pin and refuses a newer substitute' -TestCases @(
        @{ ModuleName = 'Pester'; Pinned = '5.9.1'; Newer = '5.99.0' },
        @{ ModuleName = 'PSScriptAnalyzer'; Pinned = '1.25.0'; Newer = '1.99.0' },
        @{ ModuleName = 'Microsoft.PowerShell.PlatyPS'; Pinned = '1.0.3'; Newer = '1.99.0' }
    ) {
        param($ModuleName, $Pinned, $Newer)
        Mock Get-Module {
            [pscustomobject]@{ Version = [version] $Pinned; Path = 'pinned.psd1' }
            [pscustomobject]@{ Version = [version] $Newer; Path = 'newer.psd1' }
        }
        (Get-BuildModule -Name $ModuleName).Path | Should -Be 'pinned.psd1'
        Mock Get-Module { [pscustomobject]@{ Version = [version] $Newer; Path = 'newer.psd1' } }
        @(Get-BuildModule -Name $ModuleName).Count | Should -Be 0
    }
}

Describe 'Release workflow external contracts without network calls' {
    BeforeEach {
        $savedEnvironment = @{}
        foreach ($name in @('RUNNER_TEMP', 'GITHUB_PATH', 'GITHUB_OUTPUT', 'GITHUB_REF_NAME', 'POWERSHELL_VERSION')) {
            $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        }
        $env:RUNNER_TEMP = $TestDrive
        $env:GITHUB_PATH = Join-Path $TestDrive 'github-path.txt'
        $env:GITHUB_OUTPUT = Join-Path $TestDrive 'github-output.txt'
        $env:POWERSHELL_VERSION = '7.6.6'
        Mock Invoke-WebRequest { throw 'Unexpected network request.' }
    }
    AfterEach {
        foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    }

    It 'requires the exact evaluated version tag: <Tag>' -TestCases @(
        @{ Tag = 'v0.2.0'; TagMatches = $true },
        @{ Tag = 'v0.1.1'; TagMatches = $false },
        @{ Tag = 'v0.2.0-preview'; TagMatches = $false }
    ) {
        param($Tag, $TagMatches)
        Mock dotnet { $global:LASTEXITCODE = 0; '0.2.0' }
        $env:GITHUB_REF_NAME = $Tag
        $step = Get-ReleaseStep -Name 'Check the tag against the module version'
        if ($TagMatches) {
            & $step
            Get-Content -LiteralPath $env:GITHUB_OUTPUT | Should -Contain 'value=0.2.0'
        }
        else { { & $step } | Should -Throw '*does not match*' }
    }

    It 'checks the UTF-16LE published checksum before extraction (valid=<Valid>)' -TestCases @(
        @{ Valid = $true }, @{ Valid = $false }
    ) {
        param($Valid)
        Mock Invoke-WebRequest {
            if ($Uri.AbsolutePath.EndsWith('/hashes.sha256', [StringComparison]::Ordinal)) {
                $zip = Join-Path $env:RUNNER_TEMP "PowerShell-$env:POWERSHELL_VERSION-win-x64.zip"
                $hash = if ($Valid) { (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash } else { '0' * 64 }
                [IO.File]::WriteAllText($OutFile, "$hash *PowerShell-$env:POWERSHELL_VERSION-win-x64.zip`r`n", [Text.Encoding]::Unicode)
            }
            else { [IO.File]::WriteAllBytes($OutFile, [byte[]]@(1, 2, 3, 4)) }
        }
        Mock Expand-Archive { }
        $step = Get-ReleaseStep -Name 'Install PowerShell '
        if ($Valid) {
            & $step
            Should -Invoke Expand-Archive -Exactly -Times 1
        }
        else {
            { & $step } | Should -Throw '*does not match its published hash*'
            Should -Invoke Expand-Archive -Exactly -Times 0
        }
    }
}
