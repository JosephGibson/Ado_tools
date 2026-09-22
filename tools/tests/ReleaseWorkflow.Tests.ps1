BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    . (Join-Path $PSScriptRoot '../lib/dependencies.ps1')
    function gh { throw 'Unexpected GitHub request.' }
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
        foreach ($name in @('RUNNER_TEMP', 'GITHUB_PATH', 'GITHUB_OUTPUT', 'GITHUB_REF_NAME', 'GITHUB_WORKSPACE', 'POWERSHELL_VERSION', 'POWERSHELL_SHA256', 'VERSION')) {
            $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        }
        $env:RUNNER_TEMP = $TestDrive
        $env:GITHUB_PATH = Join-Path $TestDrive 'github-path.txt'
        $env:GITHUB_OUTPUT = Join-Path $TestDrive 'github-output.txt'
        $env:POWERSHELL_VERSION = '7.6.6'
        $env:GITHUB_WORKSPACE = Join-Path $TestDrive 'workspace'
        $env:VERSION = '0.2.0'
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
        # The job pins the runtime hash, as it pins the version; here the pin names the synthetic archive.
        $env:POWERSHELL_SHA256 = (Get-FileHash -InputStream ([IO.MemoryStream]::new([byte[]]@(1, 2, 3, 4))) -Algorithm SHA256).Hash.ToLowerInvariant()
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

    # The published hashes file comes from the same release as the archive, so it cannot catch a
    # replaced upstream asset; the workflow pins the SHA-256 of the runtime it bundles.
    It 'refuses a runtime that matches its published hash but not the pinned hash' {
        Mock Invoke-WebRequest {
            if ($Uri.AbsolutePath.EndsWith('/hashes.sha256', [StringComparison]::Ordinal)) {
                $zip = Join-Path $env:RUNNER_TEMP "PowerShell-$env:POWERSHELL_VERSION-win-x64.zip"
                $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
                [IO.File]::WriteAllText($OutFile, "$hash *PowerShell-$env:POWERSHELL_VERSION-win-x64.zip`n")
            }
            else { [IO.File]::WriteAllBytes($OutFile, [byte[]]@(9, 9, 9)) }
        }
        Mock Expand-Archive { }
        $env:POWERSHELL_SHA256 = '0' * 64
        { & (Get-ReleaseStep -Name 'Install PowerShell ') } | Should -Throw '*pinned*'
        $env:POWERSHELL_SHA256 = ''
        { & (Get-ReleaseStep -Name 'Install PowerShell ') } | Should -Throw '*pinned*'
        Should -Invoke Expand-Archive -Exactly -Times 0
        $pinned = [regex]::Match((Get-Content -LiteralPath $workflowPath -Raw), "(?m)^\s+POWERSHELL_SHA256: '([0-9a-f]{64})'\s*$")
        $pinned.Success | Should -BeTrue
        # The SHA-256 published in hashes.sha256 of PowerShell 7.6.6, as recorded in the previous portable bundle.
        $pinned.Groups[1].Value | Should -Be '02fe458be20493fbdf43f61ea20610b811ee6c738ab1676c61b9cfcd1a33c860'
    }

    # Third-party modules, packages and tests run in this job; the write token must not sit in .git/config.
    It 'checks out without persisting the write-scoped token' {
        $text = Get-Content -LiteralPath $workflowPath -Raw
        $checkout = [regex]::Match($text, '(?ms)^      - uses: actions/checkout@[^\r\n]+\r?\n((?:        [^\r\n]*\r?\n)*)')
        $checkout.Success | Should -BeTrue
        $checkout.Groups[1].Value | Should -Match '(?m)^        with:\s*$'
        $checkout.Groups[1].Value | Should -Match '(?m)^          persist-credentials: false\s*$'
    }

    It 'passes the previously verified runtime archive and checksums into portable packaging' {
        $script:packagingCalls = [Collections.Generic.List[object]]::new()
        Mock pwsh { $script:packagingCalls.Add(@($args)); $global:LASTEXITCODE = 0 }
        & (Get-ReleaseStep -Name 'Package the verified build')
        $script:packagingCalls.Count | Should -Be 2
        $script:packagingCalls[0] | Should -Contain '-NoBuild'
        $script:packagingCalls[1] | Should -Contain (Join-Path $env:RUNNER_TEMP 'PowerShell-7.6.6-win-x64.zip')
        $script:packagingCalls[1] | Should -Contain (Join-Path $env:RUNNER_TEMP 'powershell-hashes.sha256')
        $script:packagingCalls[1] | Should -Contain '-PowerShellVersion'
        $script:packagingCalls[1] | Should -Contain '7.6.6'
    }

    It 'fails the release step when the portable launcher check fails' {
        Mock pwsh { $global:LASTEXITCODE = 1 }
        { & (Get-ReleaseStep -Name 'Check the portable launcher offline') } | Should -Throw '*Portable launcher check failed*'
    }

    It 'publishes both ZIPs and checksums with portable-first user instructions' {
        $output = Join-Path $env:GITHUB_WORKSPACE 'artifacts/release'
        [void] [IO.Directory]::CreateDirectory($output)
        foreach ($name in @('AdoToolkit-0.2.0.zip', 'AdoToolkit-0.2.0-win-x64.zip')) {
            [IO.File]::WriteAllText((Join-Path $output "$name.sha256"), ('a' * 64) + "  $name`n")
        }
        $script:publishArguments = @()
        Mock gh { $script:publishArguments = @($args); $global:LASTEXITCODE = 0 }
        & (Get-ReleaseStep -Name 'Publish the GitHub release')
        foreach ($name in @('AdoToolkit-0.2.0-win-x64.zip', 'AdoToolkit-0.2.0-win-x64.zip.sha256',
                'AdoToolkit-0.2.0.zip', 'AdoToolkit-0.2.0.zip.sha256', 'Install-AdoToolkit.ps1')) {
            $script:publishArguments | Should -Contain (Join-Path $output $name)
        }
        $script:publishArguments | Should -Contain '--notes-file'
        $notes = Get-Content -LiteralPath (Join-Path $env:RUNNER_TEMP 'release-notes.md') -Raw
        $notes | Should -Match '^## Portable download'
        $notes | Should -Match 'Start-AdoToolkit\.cmd'
        $notes | Should -Match 'PowerShell 7\.6\.6'
        $notes | Should -Match 'Unblock'
        $notes | Should -Match '## Module-only installation'
        Should -Invoke Invoke-WebRequest -Exactly -Times 0
    }
}
