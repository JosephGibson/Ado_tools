BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    . (Join-Path $PSScriptRoot '../lib/dependencies.ps1')
    function gh { throw 'Unexpected GitHub request.' }
    $repositoryRoot = Join-Path $PSScriptRoot '../..'
    $workflowPath = Join-Path $repositoryRoot '.github/workflows/release.yml'
    # The composite action that installs the toolchain for release.yml and verify.yml.
    $setupPath = Join-Path $repositoryRoot '.github/actions/setup/action.yml'
    # The run block of a named step, of the release workflow or of the setup action, as a script.
    function Get-ReleaseStep {
        param([string] $Name, [string] $Path = $workflowPath)
        $body = [Collections.Generic.List[string]]::new()
        # The indentation of the keys of the step, once its name is found.
        $keys = $null
        $reading = $false
        foreach ($line in Get-Content -LiteralPath $Path) {
            if ($null -eq $keys) {
                if ($line -match '^(\s+)- name: (.*)$' -and $matches[2].StartsWith($Name, [StringComparison]::Ordinal)) { $keys = $matches[1] + '  ' }
                continue
            }
            if ($line.StartsWith($keys.Substring(2) + '- ')) { break }
            if ($line -eq "${keys}run: |") { $reading = $true; continue }
            if ($reading -and $line.StartsWith("$keys  ")) { $body.Add($line.Substring($keys.Length + 2)) }
        }
        if ($body.Count -eq 0) { throw "Step not found in $(Split-Path -Leaf $Path): $Name" }
        return [scriptblock]::Create($body -join "`n")
    }
    # The files the publish step reads: the two checksums and CHANGELOG.md. The test drive outlives
    # a test, so a changelog left by an earlier test is replaced or removed.
    function Set-PublishInput {
        param([string[]] $Changelog, [string] $Version = '0.2.0')
        $output = Join-Path $env:GITHUB_WORKSPACE 'artifacts/release'
        [void] [IO.Directory]::CreateDirectory($output)
        foreach ($name in @("AdoToolkit-$Version.zip", "AdoToolkit-$Version-win-x64.zip")) {
            [IO.File]::WriteAllText((Join-Path $output "$name.sha256"), ('a' * 64) + "  $name`n")
        }
        $path = Join-Path $env:GITHUB_WORKSPACE 'CHANGELOG.md'
        if ($Changelog) { [IO.File]::WriteAllLines($path, $Changelog) }
        elseif (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
        return $output
    }
    # What verify requires of CHANGELOG.md for a version: the heading that the release skill
    # prescribes, a section that the publish step accepts, and the link to the notes in the text
    # that step writes.
    function Assert-ChangelogSection {
        param([string[]] $Changelog, [string] $Version)
        $heading = '^## ' + [regex]::Escape($Version) + ' - \d{4}-\d{2}-\d{2}$'
        @($Changelog | Where-Object { $_ -cmatch $heading }).Count | Should -Be 1 -Because "CHANGELOG.md needs one heading '## $Version - <yyyy-MM-dd>'"
        $env:VERSION = $Version
        $env:RELEASE_TAG = "v$Version"
        $null = Set-PublishInput -Changelog $Changelog -Version $Version
        & (Get-ReleaseStep -Name 'Publish the GitHub release')
        Get-Content -LiteralPath (Join-Path $env:RUNNER_TEMP 'release-notes.md') -Raw |
            Should -Match ([regex]::Escape("/blob/v$Version/docs/release-$Version.md)")) -Because 'the section links its release notes'
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
        foreach ($name in @('RUNNER_TEMP', 'GITHUB_PATH', 'GITHUB_OUTPUT', 'GITHUB_ENV', 'GITHUB_REF_NAME', 'GITHUB_WORKSPACE', 'GITHUB_SERVER_URL', 'GITHUB_REPOSITORY',
                'RELEASE_TAG', 'POWERSHELL_VERSION', 'POWERSHELL_SHA256', 'ACTIONLINT_VERSION', 'ACTIONLINT_SHA256', 'VERSION')) {
            $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        }
        $env:RUNNER_TEMP = $TestDrive
        $env:GITHUB_PATH = Join-Path $TestDrive 'github-path.txt'
        $env:GITHUB_OUTPUT = Join-Path $TestDrive 'github-output.txt'
        $env:GITHUB_ENV = Join-Path $TestDrive 'github-env.txt'
        $env:POWERSHELL_VERSION = '7.6.6'
        $env:GITHUB_WORKSPACE = Join-Path $TestDrive 'workspace'
        $env:GITHUB_SERVER_URL = 'https://github.example.test'
        $env:GITHUB_REPOSITORY = 'example/AdoToolkit'
        $env:VERSION = '0.2.0'
        # As in a manual run: the ref of the run is a branch, and the tag comes from the input.
        $env:GITHUB_REF_NAME = 'main'
        $env:RELEASE_TAG = 'v0.2.0'
        Mock Invoke-WebRequest { throw 'Unexpected network request.' }
    }
    AfterEach {
        foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    }

    It 'requires the exact evaluated version tag: <Tag>' -TestCases @(
        @{ Tag = 'v0.2.0'; TagMatches = $true },
        @{ Tag = 'v0.1.1'; TagMatches = $false },
        @{ Tag = 'v0.2.0-preview'; TagMatches = $false },
        @{ Tag = 'main'; TagMatches = $false }
    ) {
        param($Tag, $TagMatches)
        Mock dotnet { $global:LASTEXITCODE = 0; '0.2.0' }
        $env:RELEASE_TAG = $Tag
        $step = Get-ReleaseStep -Name 'Check the tag against the module version'
        if ($TagMatches) {
            & $step
            Get-Content -LiteralPath $env:GITHUB_OUTPUT | Should -Contain 'value=0.2.0'
        }
        else { { & $step } | Should -Throw '*does not match*' }
    }

    # A manual run publishes a tag whose run did not start or failed for a reason outside the
    # tagged files. Its ref is the branch it was started on, so no step may read the ref.
    It 'releases the pushed tag, or the tag named for a manual run' {
        $lines = @(Get-Content -LiteralPath $workflowPath)
        $start = [array]::IndexOf($lines, '  workflow_dispatch:')
        $start | Should -BeGreaterThan -1
        $lines[($start + 1)..($start + 5)] | ForEach-Object { $_.TrimEnd() } |
            Should -Be @('    inputs:', '      tag:', "        description: 'Existing tag to publish, in the form v<major>.<minor>.<patch>'", '        required: true', '        type: string')
        $lines | Should -Contain '      RELEASE_TAG: ${{ inputs.tag || github.ref_name }}'
        $lines | Should -Contain '          ref: refs/tags/${{ inputs.tag || github.ref_name }}'
        $lines | Where-Object { $_ -match 'GITHUB_REF|github\.ref' -and $_ -notmatch [regex]::Escape('${{ inputs.tag || github.ref_name }}') } | Should -BeNullOrEmpty
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
        # The action pins the runtime hash, as it pins the version; here the pin names the synthetic archive.
        $env:POWERSHELL_SHA256 = (Get-FileHash -InputStream ([IO.MemoryStream]::new([byte[]]@(1, 2, 3, 4))) -Algorithm SHA256).Hash.ToLowerInvariant()
        $step = Get-ReleaseStep -Path $setupPath -Name 'Install the pinned PowerShell'
        if ($Valid) {
            & $step
            Should -Invoke Expand-Archive -Exactly -Times 1
            # The packaging and publishing steps of the release job name the runtime by this version.
            Get-Content -LiteralPath $env:GITHUB_ENV | Should -Contain 'POWERSHELL_VERSION=7.6.6'
        }
        else {
            { & $step } | Should -Throw '*does not match its published hash*'
            Should -Invoke Expand-Archive -Exactly -Times 0
        }
    }

    # The published hashes file comes from the same release as the archive, so it cannot catch a
    # replaced upstream asset; the setup action pins the SHA-256 of the runtime that is bundled.
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
        { & (Get-ReleaseStep -Path $setupPath -Name 'Install the pinned PowerShell') } | Should -Throw '*pinned*'
        $env:POWERSHELL_SHA256 = ''
        { & (Get-ReleaseStep -Path $setupPath -Name 'Install the pinned PowerShell') } | Should -Throw '*pinned*'
        Should -Invoke Expand-Archive -Exactly -Times 0
        $pinned = [regex]::Match((Get-Content -LiteralPath $setupPath -Raw), "(?m)^\s+POWERSHELL_SHA256: '([0-9a-f]{64})'\s*$")
        $pinned.Success | Should -BeTrue
        # The SHA-256 published in hashes.sha256 of PowerShell 7.6.6, as recorded in the previous portable bundle.
        $pinned.Groups[1].Value | Should -Be '02fe458be20493fbdf43f61ea20610b811ee6c738ab1676c61b9cfcd1a33c860'
    }

    # actionlint decides the workflow-lint stage of verify in both workflows, so the runner gets
    # one version of it, checked against a hash that is recorded here and not beside the download.
    It 'checks the actionlint archive against the pinned hash before extraction (valid=<Valid>)' -TestCases @(
        @{ Valid = $true }, @{ Valid = $false }
    ) {
        param($Valid)
        $script:requested = @()
        Mock Invoke-WebRequest { $script:requested += [string] $Uri; [IO.File]::WriteAllBytes($OutFile, [byte[]]@(5, 6, 7)) }
        Mock Expand-Archive { }
        $env:ACTIONLINT_VERSION = '1.2.3'
        $env:ACTIONLINT_SHA256 = if ($Valid) { (Get-FileHash -InputStream ([IO.MemoryStream]::new([byte[]]@(5, 6, 7))) -Algorithm SHA256).Hash.ToLowerInvariant() } else { '0' * 64 }
        $step = Get-ReleaseStep -Path $setupPath -Name 'Install the pinned actionlint'
        if ($Valid) {
            & $step
            Should -Invoke Expand-Archive -Exactly -Times 1
            $script:requested | Should -Be @('https://github.com/rhysd/actionlint/releases/download/v1.2.3/actionlint_1.2.3_windows_amd64.zip')
            Get-Content -LiteralPath $env:GITHUB_PATH | Should -Contain (Join-Path $env:RUNNER_TEMP 'actionlint')
        }
        else {
            { & $step } | Should -Throw '*does not match the pinned hash*'
            Should -Invoke Expand-Archive -Exactly -Times 0
        }
        $text = Get-Content -LiteralPath $setupPath -Raw
        $text | Should -Match "(?m)^\s+ACTIONLINT_VERSION: '\d+\.\d+\.\d+'\s*$"
        $text | Should -Match "(?m)^\s+ACTIONLINT_SHA256: '[0-9a-f]{64}'\s*$"
    }

    # Third-party modules, packages and tests run in this job; the write token must not sit in .git/config.
    It 'checks out without persisting the write-scoped token' {
        $text = Get-Content -LiteralPath $workflowPath -Raw
        $checkout = [regex]::Match($text, '(?ms)^      - uses: actions/checkout@[^\r\n]+\r?\n((?:        [^\r\n]*\r?\n)*)')
        $checkout.Success | Should -BeTrue
        $checkout.Groups[1].Value | Should -Match '(?m)^        with:\s*$'
        $checkout.Groups[1].Value | Should -Match '(?m)^          persist-credentials: false\s*$'
    }

    # A tag can be moved to other code; a commit cannot. The job holds a token that can publish.
    # The setup action is part of this repository and is named by its path.
    It 'pins every action to a commit and names its version, in the workflow and in the setup action' {
        foreach ($path in @($workflowPath, $setupPath)) {
            $uses = @(Get-Content -LiteralPath $path | Where-Object { $_ -match '^\s*-?\s*uses:' -and $_ -cne '      - uses: ./.github/actions/setup' })
            $uses.Count | Should -BeGreaterThan 0
            foreach ($line in $uses) {
                $line | Should -MatchExactly '^\s+- uses: [A-Za-z0-9_.-]+/[A-Za-z0-9_./-]+@[0-9a-f]{40} # v\d+\.\d+\.\d+$'
            }
        }
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
        $output = Set-PublishInput -Changelog @('# Changelog', '', '## [0.2.0] - 2026-01-15', '', '- A change.')
        $script:publishArguments = @()
        Mock gh { $script:publishArguments = @($args); $global:LASTEXITCODE = 0 }
        & (Get-ReleaseStep -Name 'Publish the GitHub release')
        # The release is created for the tag, not for the ref of the run.
        $script:publishArguments[0..2] | Should -Be @('release', 'create', 'v0.2.0')
        foreach ($name in @('AdoToolkit-0.2.0-win-x64.zip', 'AdoToolkit-0.2.0-win-x64.zip.sha256',
                'AdoToolkit-0.2.0.zip', 'AdoToolkit-0.2.0.zip.sha256', 'Install-AdoToolkit.ps1')) {
            $script:publishArguments | Should -Contain (Join-Path $output $name)
        }
        $script:publishArguments | Should -Contain '--notes-file'
        $notes = Get-Content -LiteralPath (Join-Path $env:RUNNER_TEMP 'release-notes.md') -Raw
        $notes | Should -Match '(?m)^## Portable download'
        $notes | Should -Match 'Start-AdoToolkit\.cmd'
        $notes | Should -Match 'PowerShell 7\.6\.6'
        $notes | Should -Match 'Unblock'
        $notes | Should -Match '## Module-only installation'
        Should -Invoke Invoke-WebRequest -Exactly -Times 0
    }

    It 'puts the CHANGELOG.md section of the tagged version above the installation instructions' {
        $null = Set-PublishInput -Changelog @(
            '# Changelog', '', 'Introduction.', '',
            '## 0.3.0 - 2026-02-01', '', '### Added', '', '- A later change.', '',
            '## 0.2.0 - 2026-01-15', '', '### Fixed', '', '- The tagged change.', '',
            'Details: [release notes](docs/release-0.2.0.md) and the [guide](https://docs.example.test/guide).', '',
            '## 0.1.0 - 2026-01-01', '', '- An earlier change.')
        Mock gh { $global:LASTEXITCODE = 0 }
        & (Get-ReleaseStep -Name 'Publish the GitHub release')
        $notes = Get-Content -LiteralPath (Join-Path $env:RUNNER_TEMP 'release-notes.md') -Raw
        $notes | Should -Match '^## Changes\r?\n\r?\n### Fixed\r?\n\r?\n- The tagged change\.\r?\n'
        $notes.IndexOf('- The tagged change.', [StringComparison]::Ordinal) | Should -BeLessThan $notes.IndexOf('## Portable download', [StringComparison]::Ordinal)
        $notes | Should -Not -Match 'A later change|An earlier change|Introduction'
        # The release page is outside the repository: a relative link names the file at the tag.
        $notes | Should -Match ([regex]::Escape('[release notes](https://github.example.test/example/AdoToolkit/blob/v0.2.0/docs/release-0.2.0.md)'))
        $notes | Should -Match ([regex]::Escape('[guide](https://docs.example.test/guide)'))
        Should -Invoke gh -Exactly -Times 1
    }

    # Pushing the tag publishes at once, so a version without its changelog section must stop here.
    It 'fails before publishing when CHANGELOG.md has no section for the tagged version (<Case>)' -TestCases @(
        @{ Case = 'no file'; Changelog = @() },
        @{ Case = 'other versions only'; Changelog = @('# Changelog', '', '## 0.2.01 - 2026-01-20', '', '- Another version.', '', '## 0.1.0 - 2026-01-01', '', '- An earlier change.') },
        @{ Case = 'empty section'; Changelog = @('# Changelog', '', '## 0.2.0 - 2026-01-15', '', '## 0.1.0 - 2026-01-01', '', '- An earlier change.') }
    ) {
        param($Case, $Changelog)
        $null = Set-PublishInput -Changelog $Changelog
        Mock gh { $global:LASTEXITCODE = 0 }
        { & (Get-ReleaseStep -Name 'Publish the GitHub release') } | Should -Throw '*CHANGELOG.md has no section for 0.2.0*'
        Should -Invoke gh -Exactly -Times 0
    }

    # The publish step is the last one of a run that the pushed tag started. The same requirement
    # is checked here, in verify, so that a version without its section fails before a tag exists.
    It 'accepts a CHANGELOG.md section with a dated heading, entries and the link to the notes' {
        Mock gh { $global:LASTEXITCODE = 0 }
        Assert-ChangelogSection -Version '0.2.0' -Changelog @(
            '# Changelog', '', '## 0.2.0 - 2026-01-15', '', '### Fixed', '', '- A change.', '',
            'Details: [release notes](docs/release-0.2.0.md)', '', '## Earlier versions', '', 'Older.')
    }

    It 'rejects a CHANGELOG.md section that the release skill would not write (<Case>)' -TestCases @(
        @{ Case = 'no section'; Changelog = @('# Changelog', '', '## 0.1.0 - 2026-01-01', '', '- A change.', '', 'Details: [release notes](docs/release-0.1.0.md)') },
        @{ Case = 'heading without a date'; Changelog = @('# Changelog', '', '## 0.2.0', '', '- A change.', '', 'Details: [release notes](docs/release-0.2.0.md)') },
        @{ Case = 'no entries'; Changelog = @('# Changelog', '', '## 0.2.0 - 2026-01-15', '', '## 0.1.0 - 2026-01-01', '', '- A change.') },
        @{ Case = 'no link to the notes'; Changelog = @('# Changelog', '', '## 0.2.0 - 2026-01-15', '', '- A change.') }
    ) {
        param($Case, $Changelog)
        Mock gh { $global:LASTEXITCODE = 0 }
        { Assert-ChangelogSection -Version '0.2.0' -Changelog $Changelog } | Should -Throw
    }

    It 'has that CHANGELOG.md section for the module version' {
        $properties = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
        $version = [regex]::Match($properties, '<VersionPrefix>([^<]+)</VersionPrefix>').Groups[1].Value
        $version | Should -MatchExactly '^\d+\.\d+\.\d+$'
        Mock gh { $global:LASTEXITCODE = 0 }
        Assert-ChangelogSection -Version $version -Changelog @(Get-Content -LiteralPath (Join-Path $repositoryRoot 'CHANGELOG.md'))
    }
}
