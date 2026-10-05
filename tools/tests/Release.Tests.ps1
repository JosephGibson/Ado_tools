BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    . (Join-Path $PSScriptRoot '../package/Release.Common.ps1')
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    # Pester mocks only a command that exists; neither tool is called for real here.
    function git { throw 'Unexpected git call.' }
    function gh { throw 'Unexpected GitHub request.' }

    function New-TestFile {
        param([Parameter(Mandatory = $true)][string] $FilePath, [AllowEmptyString()][string] $Content = '')
        [void] [IO.Directory]::CreateDirectory((Split-Path -Parent $FilePath))
        [IO.File]::WriteAllText($FilePath, $Content.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
    }

    # A repository at 0.3.0 with the notes of 0.3.0 and 0.2.0 under docs/ and older ones archived.
    # The 0.3.0 notes link a single version in their carry-over rows, as those of 0.11.0 do; the
    # 0.1.0 notes link 0.0.5, which no later list carries.
    function New-ReleaseRepository {
        $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-TestFile (Join-Path $root 'Directory.Build.props') "<Project>`n  <PropertyGroup>`n    <VersionPrefix>0.3.0</VersionPrefix>`n  </PropertyGroup>`n</Project>`n"
        New-TestFile (Join-Path $root 'README.md') "# Sample`n`n**Version 0.3.0** · Windows`n`nSee the [changelog](CHANGELOG.md).`n"
        New-TestFile (Join-Path $root 'docs/guides/getting-started.md') @'
# Getting started

To update from AdoToolkit 0.3.0 on, run the updater.

```powershell
.\Install-AdoToolkit.ps1 -Path .\AdoToolkit-0.3.0.zip
(Get-FileHash .\AdoToolkit-0.3.0-win-x64.zip).Hash
```
'@
        New-TestFile (Join-Path $root 'tools/package/Install-AdoToolkit.ps1') "<#`n.EXAMPLE`n.\Install-AdoToolkit.ps1 -Path .\AdoToolkit-0.3.0.zip`n#>`nparam()`n"
        New-TestFile (Join-Path $root 'tools/tests/Sample.Tests.ps1') "# A 0.3.0 installation is refused on purpose.`n"
        New-TestFile (Join-Path $root 'CHANGELOG.md') @'
# Changelog

Introduction.

## 0.3.0 - 2026-01-03

### Fixed

- A fix.

Details: [release notes](docs/release-0.3.0.md)

## 0.2.0 - 2026-01-02

### Added

- A feature.

Details: [release notes](docs/release-0.2.0.md)

## 0.1.0 - 2026-01-01

- The first version.

Details: [release notes](docs/archive/release-0.1.0.md)
'@
        New-TestFile (Join-Path $root 'docs/release-0.3.0.md') @'
# Sample 0.3.0 release notes

Written by hand, before the template.

### Findings not fixed

| Finding | Reason |
| --- | --- |
| Earlier findings | See the [0.2.0 notes](release-0.2.0.md#findings-not-fixed) |

### Known limitations

| Limitation | Notes |
| --- | --- |
| Limits carried over | The [0.2.0](release-0.2.0.md#known-limitations) known limitations still apply |

## Work-PC Live checks

The [0.2.0 checks](release-0.2.0.md#work-pc-live-checks) still apply.
'@
        New-TestFile (Join-Path $root 'docs/release-0.2.0.md') @'
# Sample 0.2.0 release notes

Written by hand.

### Findings not fixed

| Finding | Reason |
| --- | --- |
| Earlier findings | See the [0.1.0](archive/release-0.1.0.md#findings-not-fixed) and [0.0.9](archive/release-0.0.9.md#findings-not-fixed) notes |

### Known limitations

| Limitation | Notes |
| --- | --- |
| Limits carried over | The [0.1.0](archive/release-0.1.0.md#known-limitations) known limitations still apply |

## Work-PC Live checks

None.
'@
        New-TestFile (Join-Path $root 'docs/archive/release-0.1.0.md') "# Sample 0.1.0 release notes`n`n### Findings not fixed`n`nSuperseded: [0.0.5](release-0.0.5.md#findings-not-fixed).`n`n### Known limitations`n`nNone.`n"
        New-TestFile (Join-Path $root 'docs/archive/release-0.0.9.md') "# Sample 0.0.9 release notes`n`n### Findings not fixed`n`nNone.`n"
        New-TestFile (Join-Path $root 'docs/archive/release-0.0.5.md') "# Sample 0.0.5 release notes`n`n### Findings not fixed`n`nNone.`n"
        New-TestFile (Join-Path $root 'docs/archive/README.md') @'
# Archive

| Document | Contents |
| --- | --- |
| [release-0.0.5.md](release-0.0.5.md) | 0.0.5 release notes: the start |
| [release-0.0.9.md](release-0.0.9.md) | 0.0.9 release notes: a fix |
| [release-0.1.0.md](release-0.1.0.md) | 0.1.0 release notes: the first version |

| Citation | Resolves to |
| --- | --- |
| `V-01` | Under [Known limitations](../release-0.2.0.md#known-limitations) in the 0.2.0 notes |
'@
        New-TestFile (Join-Path $root 'docs/tooling.md') "# Tooling`n`nThe 0.2.0 notes are ``docs/release-0.2.0.md``.`n"
        return $root
    }

    function New-Fragment {
        param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Name, [Parameter(Mandatory = $true)][hashtable] $Data)
        New-TestFile (Join-Path $Root "docs/unreleased/$Name") (ConvertTo-Json -InputObject $Data -Depth 5)
    }

    # Three valid fragments: a code fix with its pre-fix failure, a documentation change and an
    # internal change.
    function Add-SampleFragment {
        param([Parameter(Mandatory = $true)][string] $Root)
        New-Fragment $Root '20260110-120000-retry-fix.json' @{
            kind = 'fixed'; changelog = 'A request is retried only when the connection was lost.'
            finding = 'A body read error was retried after the server had answered; the retry policy now checks the cause.'
            regressionTest = 'RetryPolicyTests.BodyReadErrorsAreRetriedOnlyWhenTheConnectionWasLost'
            files = @('src/AdoToolkit.Core/Http/RetryPolicy.cs')
            preFix = @{ failure = 'Assert.Throws() Failure: No exception was thrown'; totals = 'Failed 1, passed 2, total 3' }
        }
        New-Fragment $Root '20260110-130000-guide-wording.json' @{
            kind = 'changed'; changelog = 'The guide says which commands take -Project.'
            visible = 'The getting-started guide names the commands that take -Project.'; files = @('docs/guides/getting-started.md')
        }
        New-Fragment $Root '20260110-140000-tooling.json' @{ kind = 'internal'; changelog = 'The release scripts assemble the notes.' }
    }

    function Set-GitState {
        param([string] $StatusLine = '## 0.4.0...origin/0.4.0', [bool] $TagExists = $false)
        $script:gitStatus = @($StatusLine, ' M README.md')
        $script:tagExists = $TagExists
        $script:gitLog = @(
            ('d' * 40) + "`td444444`tAdoToolkit 0.4.0: the version after (#4)"
            ('c' * 40) + "`tc333333`tAdoToolkit 0.3.0: the version before (#3)"
            ('b' * 40) + "`tb222222`tAdoToolkit 0.2.0: an older version (#2)"
        )
    }

    function Get-Text {
        param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Relative)
        return [IO.File]::ReadAllText((Join-Path $Root $Relative))
    }

    # Replaces every release:todo marker, as the agent does.
    function Clear-ReleaseMarker {
        param([Parameter(Mandatory = $true)][string] $Root, [Parameter(Mandatory = $true)][string] $Version)
        foreach ($relative in @('CHANGELOG.md', "docs/release-$Version.md", 'docs/archive/README.md')) {
            $path = Join-Path $Root $relative
            $text = [IO.File]::ReadAllText($path)
            $text = [regex]::Replace($text, '<!-- release:todo:[^>]*-->', 'Written by the agent.')
            [IO.File]::WriteAllText($path, $text)
        }
    }
}

Describe 'README version line' {
    It 'names the module version once and links no release notes' {
        $version = Get-AdoDeclaredVersion -Root $repositoryRoot
        @(Get-AdoReadmeProblem -Text (Get-Text $repositoryRoot 'README.md') -Version $version) | Should -BeNullOrEmpty
    }

    It 'reports a README that <Case>' -TestCases @(
        @{ Case = 'misses the bump'; Text = "**Version 0.1.0** · Windows`n"; Expected = '*does not name 0.2.0*' },
        @{ Case = 'has two version lines'; Text = "**Version 0.2.0**`n`n**Version 0.1.0** was the first.`n"; Expected = '*2 lines*' },
        @{ Case = 'has no version line'; Text = "Version 0.2.0`n"; Expected = '*0 lines*' },
        @{ Case = 'links release notes'; Text = "**Version 0.2.0**`n`nSee the [notes](docs/release-0.2.0.md).`n"; Expected = '*links release notes*' }
    ) {
        param($Case, $Text, $Expected)
        @(Get-AdoReadmeProblem -Text $Text -Version '0.2.0') -join ' ' | Should -BeLike $Expected
    }
}

Describe 'Change fragments' {
    It 'accepts every fragment in docs/unreleased/' {
        foreach ($fragment in @(Get-AdoReleaseFragment -Root $repositoryRoot)) {
            $fragment.Problems | Should -BeNullOrEmpty -Because $fragment.Name
        }
    }

    It 'refuses a fragment that <Case>' -TestCases @(
        @{ Case = 'has an unknown kind'; Data = @{ kind = 'improved'; changelog = 'A line.' }; Expected = '*kind must be*' },
        @{ Case = 'has no changelog line'; Data = @{ kind = 'internal' }; Expected = '*changelog is required*' },
        @{ Case = 'spreads a text over two lines'; Data = @{ kind = 'changed'; changelog = "One`nTwo" }; Expected = '*changelog must be one line*' },
        @{ Case = 'misspells a property'; Data = @{ kind = 'changed'; changelog = 'A line.'; visibel = 'A change.' }; Expected = "*unknown property 'visibel'*" },
        @{ Case = 'fixes without a finding'; Data = @{ kind = 'fixed'; changelog = 'A line.' }; Expected = '*needs its finding*' },
        @{ Case = 'gives a finding without its files'; Data = @{ kind = 'fixed'; changelog = 'A line.'; finding = 'Wrong.' }; Expected = '*finding names its files*' },
        @{ Case = 'records a failure in a fragment without a finding'; Data = @{ kind = 'internal'; changelog = 'A line.'; preFix = @{ failure = 'Failed'; totals = 'Failed 1' } }; Expected = '*needs the regressionTest*' },
        @{ Case = 'fixes code without pre-fix evidence'; Data = @{ kind = 'fixed'; changelog = 'A line.'; finding = 'Wrong.'; files = @('src/A.cs') }; Expected = '*needs preFix*' },
        @{ Case = 'records a failure without its test'; Data = @{ kind = 'fixed'; changelog = 'A line.'; finding = 'Wrong.'; files = @('src/A.cs'); preFix = @{ failure = 'Failed'; totals = 'Failed 1' } }; Expected = '*needs the regressionTest*' },
        @{ Case = 'gives a malformed preFix'; Data = @{ kind = 'fixed'; changelog = 'A line.'; finding = 'Wrong.'; files = @('src/A.cs'); preFix = @{ failure = 'Failed' } }; Expected = '*preFix must be*' },
        @{ Case = 'names a path outside the repository'; Data = @{ kind = 'changed'; changelog = 'A line.'; files = @('../secret.txt') }; Expected = '*files must be*' }
    ) {
        param($Case, $Data, $Expected)
        $parsed = ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $Data -Depth 5) -AsHashtable
        @(Test-AdoReleaseFragment -Data $parsed) -join ' ' | Should -BeLike $Expected
    }

    It 'accepts a fix of code with a stated reason, and a fix of documentation without evidence' {
        foreach ($data in @(
                @{ kind = 'fixed'; changelog = 'A line.'; finding = 'Wrong.'; files = @('tools/a.ps1'); preFix = @{ notReproduced = 'needs a code page 850 console' } },
                @{ kind = 'security'; changelog = 'A line.'; finding = 'Wrong.'; files = @('src/A.cs'); preFix = @{ none = 'a configuration change' } },
                @{ kind = 'fixed'; changelog = 'A line.'; finding = 'A guide was wrong.'; files = @('docs/guides/a.md', 'README.md') })) {
            $parsed = ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $data -Depth 5) -AsHashtable
            Test-AdoReleaseFragment -Data $parsed | Should -BeNullOrEmpty
        }
    }
}

Describe 'Start-AdoToolkitRelease' {
    BeforeEach {
        Set-GitState
        Mock git {
            $global:LASTEXITCODE = 0
            switch ($args[0]) {
                'status' { $script:gitStatus }
                'rev-parse' { $global:LASTEXITCODE = $(if ($script:tagExists) { 0 } else { 1 }) }
                'log' { $script:gitLog }
                default { throw "Unexpected git $($args -join ' ')" }
            }
        }
        Mock gh { $global:LASTEXITCODE = 1; 'release not found' }
        $repo = New-ReleaseRepository
    }

    It 'bumps only the declared patterns and keeps a deliberate mention of the old version' {
        $result = Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01'
        $result.Mode | Should -Be 'fresh'
        $result.Base | Should -Be 'c333333'
        (Get-Text $repo 'Directory.Build.props') | Should -Match '<VersionPrefix>0\.4\.0</VersionPrefix>'
        (Get-Text $repo 'README.md') | Should -Match '\*\*Version 0\.4\.0\*\*'
        $guide = Get-Text $repo 'docs/guides/getting-started.md'
        $guide | Should -Match 'AdoToolkit-0\.4\.0\.zip'
        $guide | Should -Match 'AdoToolkit-0\.4\.0-win-x64\.zip'
        $guide | Should -Match 'from AdoToolkit 0\.3\.0 on'
        (Get-Text $repo 'tools/package/Install-AdoToolkit.ps1') | Should -Match 'AdoToolkit-0\.4\.0\.zip'
        ($result.Bump | Where-Object Path -eq 'docs/guides/getting-started.md').Replacements | Should -Be 2
        # A test and a comment that name the old version on purpose are listed, not changed.
        ($result.Leftovers.Other | ForEach-Object Path) | Should -Contain 'tools/tests/Sample.Tests.ps1'
        ($result.Leftovers.Other | ForEach-Object Path) | Should -Contain 'docs/guides/getting-started.md'
        (Get-Text $repo 'tools/tests/Sample.Tests.ps1') | Should -Match '0\.3\.0'
    }

    It 'fails before writing anything when a declared file holds no version' {
        $readme = Join-Path $repo 'README.md'
        [IO.File]::WriteAllText($readme, "# Sample`n`nNo version line.`n")
        { Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01' } | Should -Throw '*README.md*'
        (Get-Text $repo 'Directory.Build.props') | Should -Match '0\.3\.0'
    }

    It 'fails when a declared field still holds the old version after the bump' {
        Mock Write-AdoReleaseText { } -ParameterFilter { $Path -like '*README.md' }
        { Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01' } | Should -Throw '*still holds 0.3.0: README.md*'
    }

    It 'archives the older notes, adds their index row and repoints links, anchors and V-nn rows' {
        $result = Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01'
        Test-Path -LiteralPath (Join-Path $repo 'docs/release-0.2.0.md') | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $repo 'docs/archive/release-0.2.0.md') | Should -BeTrue
        (Get-Text $repo 'CHANGELOG.md') | Should -Match ([regex]::Escape('Details: [release notes](docs/archive/release-0.2.0.md)'))
        $old = Get-Text $repo 'docs/release-0.3.0.md'
        $old | Should -Match ([regex]::Escape('(archive/release-0.2.0.md#findings-not-fixed)'))
        $old | Should -Match ([regex]::Escape('(archive/release-0.2.0.md#work-pc-live-checks)'))
        $index = Get-Text $repo 'docs/archive/README.md'
        $index | Should -Match ([regex]::Escape('[Known limitations](release-0.2.0.md#known-limitations)'))
        # Notes written before the template have no summary line, so their row waits for the agent.
        $index | Should -Match ([regex]::Escape('| [release-0.2.0.md](release-0.2.0.md) | 0.2.0 release notes: <!-- release:todo'))
        $index.IndexOf('release-0.1.0.md)', [StringComparison]::Ordinal) | Should -BeLessThan $index.IndexOf('[release-0.2.0.md]', [StringComparison]::Ordinal)
        (Get-Text $repo 'docs/tooling.md') | Should -Match '`docs/archive/release-0\.2\.0\.md`'
        # The moved file itself is unedited.
        (Get-Text $repo 'docs/archive/release-0.2.0.md') | Should -Match ([regex]::Escape('(archive/release-0.1.0.md#findings-not-fixed)'))
        $result.Markers | Should -Contain 'docs/archive/README.md:8'
    }

    It 'assembles the changelog section and the notes from the fragments, in file-name order' {
        Add-SampleFragment -Root $repo
        $result = Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01'
        $result.Fragments | Should -Be @('20260110-120000-retry-fix.json', '20260110-130000-guide-wording.json', '20260110-140000-tooling.json')
        $changelog = Get-Text $repo 'CHANGELOG.md'
        $section = $changelog.Substring($changelog.IndexOf('## 0.4.0', [StringComparison]::Ordinal))
        $section = $section.Substring(0, $section.IndexOf('## 0.3.0', [StringComparison]::Ordinal))
        $section | Should -Match '(?s)^## 0\.4\.0 - 2026-02-01\n\n### Changed\n\n- The guide says which commands take -Project\.\n\n### Fixed\n\n- A request is retried only when the connection was lost\.\n\nDetails: \[release notes\]\(docs/release-0\.4\.0\.md\)\n\n$'
        $changelog | Should -Not -Match 'assemble the notes'
        $notes = Get-Text $repo 'docs/release-0.4.0.md'
        $notes | Should -Match '(?m)^Prepared on 2026-02-01 from the branch `0\.4\.0`, which starts at `c333333`'
        $notes | Should -Match ([regex]::Escape('| R-1 | A body read error was retried after the server had answered; the retry policy now checks the cause. | `RetryPolicyTests.BodyReadErrorsAreRetriedOnlyWhenTheConnectionWasLost` | `src/AdoToolkit.Core/Http/RetryPolicy.cs` | Assert.Throws() Failure: No exception was thrown; Failed 1, passed 2, total 3 |'))
        $notes | Should -Match '(?m)^### Visible changes for 0\.3\.0 users\n\n- The getting-started guide names the commands that take -Project\.$'
        $notes | Should -Match '(?m)^### Internal changes\n\n- The release scripts assemble the notes\.$'
        $notes | Should -Match '(?m)^### Public contract changes\n\nNone\.$'
        # The 0.3.0 notes link one version, so its list is read; 0.0.5 left the lists before 0.2.0.
        $notes | Should -Match ([regex]::Escape('| The findings of 0.3.0, 0.2.0, 0.1.0 and 0.0.9 | Unchanged; see the [0.3.0](release-0.3.0.md#findings-not-fixed), [0.2.0](archive/release-0.2.0.md#findings-not-fixed), [0.1.0](archive/release-0.1.0.md#findings-not-fixed) and [0.0.9](archive/release-0.0.9.md#findings-not-fixed) notes |'))
        $notes | Should -Match ([regex]::Escape('| Limits carried over | The [0.3.0](release-0.3.0.md#known-limitations), [0.2.0](archive/release-0.2.0.md#known-limitations) and [0.1.0](archive/release-0.1.0.md#known-limitations) known limitations still apply |'))
        $notes | Should -Not -Match '0\.0\.5'
        $state = Read-AdoReleaseState -Root $repo -Name 'release-prep.json'
        $state['AssemblyComplete'] | Should -BeTrue
        @($state['Fragments'] | ForEach-Object { $_['Name'] }) | Should -Be $result.Fragments
        # The prep script deletes no fragment: the finalize script does, last.
        @(Get-ChildItem -LiteralPath (Join-Path $repo 'docs/unreleased') -Filter '*.json').Count | Should -Be 3
    }

    It 'writes a marker bullet when no fragment carries a changelog line' {
        New-Fragment $repo '20260110-140000-tooling.json' @{ kind = 'internal'; changelog = 'The release scripts assemble the notes.' }
        $null = Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01'
        (Get-Text $repo 'CHANGELOG.md') | Should -Match '(?m)^### Changed\n\n- <!-- release:todo: '
    }

    It 'refuses an invalid fragment before anything is written' {
        Add-SampleFragment -Root $repo
        New-Fragment $repo '20260110-150000-bad.json' @{ kind = 'fixed'; changelog = 'A line.'; finding = 'Wrong.'; files = @('src/A.cs') }
        { Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01' } | Should -Throw '*20260110-150000-bad.json: a fix of code needs preFix*'
        Test-Path -LiteralPath (Join-Path $repo 'docs/release-0.4.0.md') | Should -BeFalse
        (Get-Text $repo 'CHANGELOG.md') | Should -Not -Match '## 0\.4\.0'
    }

    It 'skips assembly on a resume after it completed' {
        Add-SampleFragment -Root $repo
        $null = Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01'
        $notes = Join-Path $repo 'docs/release-0.4.0.md'
        [IO.File]::WriteAllText($notes, ([IO.File]::ReadAllText($notes) + "Prose the agent wrote.`n"))
        $result = Invoke-AdoReleaseStart -Root $repo -Version '0.4.0'
        $result.Mode | Should -Be 'resume'
        $result.Assembly | Should -Be 'already assembled'
        $result.Changed | Should -BeNullOrEmpty
        [IO.File]::ReadAllText($notes) | Should -Match 'Prose the agent wrote'
        @([regex]::Matches((Get-Text $repo 'CHANGELOG.md'), '(?m)^## 0\.4\.0')).Count | Should -Be 1
    }

    It 'resumes after the finish deleted the journaled fragments' {
        Add-SampleFragment -Root $repo
        $null = Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01'
        Remove-Item -LiteralPath (Join-Path $repo 'docs/unreleased') -Recurse -Force
        (Invoke-AdoReleaseStart -Root $repo -Version '0.4.0').Assembly | Should -Be 'already assembled'
    }

    It 'refuses a resume when a fragment was <Case> after the journal' -TestCases @(
        @{ Case = 'added' }, @{ Case = 'changed' }
    ) {
        param($Case)
        Add-SampleFragment -Root $repo
        $null = Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01'
        if ($Case -eq 'added') { New-Fragment $repo '20260111-090000-late.json' @{ kind = 'internal'; changelog = 'Late.' } }
        else { New-Fragment $repo '20260110-140000-tooling.json' @{ kind = 'internal'; changelog = 'Edited.' } }
        { Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' } | Should -Throw "*$Case after the journal*"
    }

    It 'resumes after a failure that followed the bump, with the recorded old version and base' {
        Add-SampleFragment -Root $repo
        $script:readFragments = ${function:Get-AdoReleaseFragment}
        $script:interrupt = $true
        Mock Get-AdoReleaseFragment { if ($script:interrupt) { throw 'interrupted' }; & $script:readFragments @PesterBoundParameters }
        { Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01' } | Should -Throw '*interrupted*'
        (Get-Text $repo 'Directory.Build.props') | Should -Match '0\.4\.0'
        Test-Path -LiteralPath (Join-Path $repo 'docs/archive/release-0.2.0.md') | Should -BeTrue
        $script:interrupt = $false
        # From now on the history would name 0.4.0 as the newest version; the record wins.
        $script:gitLog = @(('e' * 40) + "`te555555`tAdoToolkit 0.4.0: not this one")
        $result = Invoke-AdoReleaseStart -Root $repo -Version '0.4.0'
        $result.Mode | Should -Be 'resume'
        $result.Old | Should -Be '0.3.0'
        $result.Base | Should -Be 'c333333'
        ($result.Bump | ForEach-Object State | Select-Object -Unique) | Should -Be 'already bumped'
        $result.Archive.State | Should -Be 'already moved'
        $result.Assembly | Should -Be 'assembled'
        (Get-Text $repo 'docs/release-0.4.0.md') | Should -Match 'which starts at `c333333`'
    }

    It 'refuses a bumped tree whose release-prep.json <Case>' -TestCases @(
        @{ Case = 'is missing' }, @{ Case = 'records another version' }
    ) {
        param($Case)
        $props = Join-Path $repo 'Directory.Build.props'
        [IO.File]::WriteAllText($props, [IO.File]::ReadAllText($props).Replace('0.3.0', '0.4.0'))
        if ($Case -ne 'is missing') { Write-AdoReleaseState -Root $repo -Name 'release-prep.json' -State @{ Version = '0.3.5'; Old = '0.3.0' } }
        { Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' } | Should -Throw '*records no preparation*'
    }

    It 'refuses <Case>' -TestCases @(
        @{ Case = 'main'; Status = '## main...origin/main'; Tag = $false; Expected = '*never on main*' },
        @{ Case = 'a detached HEAD'; Status = '## HEAD (no branch)'; Tag = $false; Expected = '*detached*' },
        @{ Case = 'an existing tag'; Status = '## 0.4.0...origin/0.4.0'; Tag = $true; Expected = '*tag v0.4.0 already exists*' },
        @{ Case = 'a version that is not higher'; Status = '## 0.4.0'; Tag = $false; Expected = '*must be higher*'; Version = '0.2.5' }
    ) {
        param($Case, $Status, $Tag, $Expected, $Version)
        Set-GitState -StatusLine $Status -TagExists $Tag
        $target = if ($Version) { $Version } else { '0.4.0' }
        { Invoke-AdoReleaseStart -Root $repo -Version $target } | Should -Throw $Expected
        (Get-Text $repo 'Directory.Build.props') | Should -Match '0\.3\.0'
    }

    It 'refuses a version that GitHub already released' {
        Mock gh { $global:LASTEXITCODE = 0; '{"tagName":"v0.4.0"}' }
        { Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' } | Should -Throw '*GitHub already has a release v0.4.0*'
    }

    It 'lists the edits with -WhatIf and writes nothing' {
        Add-SampleFragment -Root $repo
        $before = @(Get-ChildItem -LiteralPath $repo -Recurse -File -Force | ForEach-Object { "$($_.FullName)|$($_.Length)|$($_.LastWriteTimeUtc.Ticks)" })
        $result = Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01' -DryRun
        $after = @(Get-ChildItem -LiteralPath $repo -Recurse -File -Force | ForEach-Object { "$($_.FullName)|$($_.Length)|$($_.LastWriteTimeUtc.Ticks)" })
        $after | Should -Be $before
        $result.WhatIf | Should -BeTrue
        $result.Changed | Should -BeNullOrEmpty
        @($result.Bump | Where-Object State -eq 'bump').Count | Should -Be 4
        $result.Archive.From | Should -Be 'docs/release-0.2.0.md'
        ($result.Archive.Relinked | ForEach-Object Path) | Should -Contain 'CHANGELOG.md'
        $result.Fragments.Count | Should -Be 3
        # The bump the run would make leaves no declared field behind.
        ($result.Leftovers.Other | ForEach-Object Path) | Should -Not -Contain 'README.md'
    }

    It 'keeps every carry-over link resolving through two archive cycles' {
        Add-SampleFragment -Root $repo
        $null = Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01'
        Clear-ReleaseMarker -Root $repo -Version '0.4.0'
        Remove-Item -LiteralPath (Join-Path $repo 'docs/unreleased') -Recurse -Force
        Set-GitState -StatusLine '## 0.5.0'
        $null = Invoke-AdoReleaseStart -Root $repo -Version '0.5.0' -Date '2026-03-01'
        Clear-ReleaseMarker -Root $repo -Version '0.5.0'
        Test-Path -LiteralPath (Join-Path $repo 'docs/archive/release-0.3.0.md') | Should -BeTrue
        $notes = Get-Text $repo 'docs/release-0.5.0.md'
        $notes | Should -Match ([regex]::Escape('[0.4.0](release-0.4.0.md#findings-not-fixed), [0.3.0](archive/release-0.3.0.md#findings-not-fixed), [0.2.0](archive/release-0.2.0.md#findings-not-fixed), [0.1.0](archive/release-0.1.0.md#findings-not-fixed) and [0.0.9](archive/release-0.0.9.md#findings-not-fixed)'))
        (Get-Text $repo 'docs/release-0.4.0.md') | Should -Match ([regex]::Escape('[0.3.0](archive/release-0.3.0.md#findings-not-fixed)'))
        $outcome = Get-DocumentationOutcome -ProjectProfile (Get-ProjectProfile -Root $repo)
        $outcome.Failures | Should -BeNullOrEmpty
    }
}

Describe 'Test-AdoToolkitRelease' {
    BeforeAll {
        # A prepared 0.4.0 tree, as Start-AdoToolkitRelease.ps1 leaves it.
        function New-PreparedRepository {
            $root = New-ReleaseRepository
            $props = Join-Path $root 'Directory.Build.props'
            [IO.File]::WriteAllText($props, [IO.File]::ReadAllText($props).Replace('0.3.0', '0.4.0'))
            New-TestFile (Join-Path $root 'docs/release-0.4.0.md') "# Sample 0.4.0 release notes`n"
            New-TestFile (Join-Path $root 'tools/BuildModules.psd1') "@{ Pester = '5.9.1'; PSScriptAnalyzer = '1.25.0'; 'Microsoft.PowerShell.PlatyPS' = '1.0.3' }`n"
            New-TestFile (Join-Path $root '.github/actions/setup/action.yml') "runs:`n  steps:`n    - env:`n        POWERSHELL_VERSION: '7.6.6'`n        POWERSHELL_SHA256: '$('a' * 64)'`n"
            Write-AdoReleaseState -Root $root -Name 'release-prep.json' -State ([ordered]@{ Version = '0.4.0'; Old = '0.3.0'; Fragments = @(); AssemblyComplete = $true })
            return $root
        }
        # The JSON that verify prints for the given stage results.
        function New-VerifyOutput {
            param([hashtable] $Status)
            $stages = @(foreach ($name in $Status.Keys | Sort-Object) {
                    $summary = switch ($name) {
                        'powershell-test' { @('Pester: 12 passed, 0 failed, 0 skipped') }
                        'project-check' { @('build succeeded (Release)', 'Core tests (en-US): 30 passed, 0 failed, 30 executed, 30 discovered', 'Core tests (fr-CA): 25 passed, 0 failed, 25 executed, 25 discovered', 'product Pester: 9 passed, 0 failed, 0 skipped, 9 discovered') }
                        default { @("$name checked") }
                    }
                    [ordered]@{ Name = $name; Status = $Status[$name]; ExitCode = 0; Summary = $summary; Warnings = @(); DurationMs = 1 }
                })
            return (ConvertTo-Json -Compress -Depth 5 -InputObject ([ordered]@{ Status = 'incomplete'; Stages = $stages; Warnings = @('Only the selected stages ran; run verify for the full gate.') }))
        }
    }

    BeforeEach {
        $repo = New-PreparedRepository
        $script:calls = [Collections.Generic.List[object]]::new()
        $script:stageStatus = @{ 'powershell-lint' = 'pass'; 'powershell-test' = 'pass'; 'configuration' = 'pass'; 'project-check' = 'pass' }
        $script:packagingWrites = $null
        Mock Get-ProjectValidationPlan {
            [pscustomobject]@{ Stages = @('powershell-lint', 'powershell-test', 'configuration', 'documentation', 'project-check' | ForEach-Object { [pscustomobject]@{ Name = $_ } }) }
        }
        Mock Get-ProjectDiagnostics { [pscustomobject]@{ Tools = @([pscustomobject]@{ Name = 'gh'; State = 'present' }) } }
        Mock Invoke-AdoReleaseProcess {
            $script:calls.Add([pscustomobject]@{ Arguments = @($ArgumentList); ReleaseBuild = $env:ADOTOOLKIT_RELEASE_BUILD })
            $joined = $ArgumentList -join ' '
            if ($joined -match ' verify -Stage ') { return [pscustomobject]@{ ExitCode = 2; Output = (New-VerifyOutput -Status $script:stageStatus); Errors = '' } }
            if ($joined -match '-getProperty:Version') { return [pscustomobject]@{ ExitCode = 0; Output = "0.4.0`n"; Errors = '' } }
            if ($joined -match 'Publish-AdoToolkitPackage' -and $script:packagingWrites) { New-TestFile (Join-Path $WorkingDirectory $script:packagingWrites) 'changed during the gate' }
            if ($joined -match 'New-AdoToolkitRelease') {
                foreach ($name in @('AdoToolkit-0.4.0.zip', 'AdoToolkit-0.4.0.zip.sha256', 'Install-AdoToolkit.ps1')) { New-TestFile (Join-Path $WorkingDirectory "artifacts/release/$name") 'asset' }
            }
            [pscustomobject]@{ ExitCode = 0; Output = ''; Errors = '' }
        }
        Mock Get-Command { [pscustomobject]@{ Source = 'dotnet.exe' } } -ParameterFilter { $Name -eq 'dotnet' }
    }

    It 'reads each stage from the JSON of verify, which exits 2 under -Stage' {
        $gate = Invoke-AdoReleaseVerify -Root $repo -Stage @('powershell-lint', 'project-check')
        $gate.Status | Should -Be 'pass'
        $script:stageStatus['project-check'] = 'fail'
        (Invoke-AdoReleaseVerify -Root $repo -Stage @('powershell-lint', 'project-check')).Status | Should -Be 'fail'
        $script:stageStatus['project-check'] = 'unavailable'
        (Invoke-AdoReleaseVerify -Root $repo -Stage @('powershell-lint', 'project-check')).Status | Should -Be 'incomplete'
        # A stage that did not run is a failure, whatever the others say.
        (Invoke-AdoReleaseVerify -Root $repo -Stage @('powershell-lint', 'workflow-lint')).Status | Should -Be 'fail'
    }

    It 'runs every stage but documentation and records the result in release-check.json' {
        $result = Invoke-AdoReleaseCheck -Root $repo
        # The runtime archive is not on disk here, so only the portable asset is missing.
        $result.Status | Should -Be 'incomplete'
        ($result.Steps | ForEach-Object { "$($_.Name)=$($_.Status)" }) | Should -Be @('gate=pass', 'version=pass', 'package=pass', 'assets=incomplete', 'content=pass')
        $verify = @($script:calls | Where-Object { $_.Arguments -join ' ' -match ' verify ' })
        $verify.Count | Should -Be 1
        $verify[0].Arguments[-1] | Should -Match "-Stage @\('powershell-lint', 'powershell-test', 'configuration', 'project-check'\)"
        ($result.Tests | ForEach-Object { "$($_.Suite)=$($_.Passed)" }) | Should -Be @('tooling Pester=12', 'Core tests (en-US)=30', 'Core tests (fr-CA)=25', 'product Pester=9')
        $result.Modules['Pester'] | Should -Be '5.9.1'
        ($result.Assets | ForEach-Object Name) | Should -Be @('AdoToolkit-0.4.0.zip', 'AdoToolkit-0.4.0.zip.sha256', 'Install-AdoToolkit.ps1')
        $result.Portable | Should -Be 'not built locally'
        $record = Read-AdoReleaseState -Root $repo -Name 'release-check.json'
        $record['ContentSha256'] | Should -Be $result.ContentSha256
        $record['Content'].Contains('README.md') | Should -BeTrue
        $record['Content'].Contains('CHANGELOG.md') | Should -BeFalse
        $record['Content'].Contains('docs/release-0.4.0.md') | Should -BeFalse
    }

    It 'runs every child with ADOTOOLKIT_RELEASE_BUILD=1 and leaves the variable as it was' {
        $previous = $env:ADOTOOLKIT_RELEASE_BUILD
        $env:ADOTOOLKIT_RELEASE_BUILD = $null
        try {
            $null = Invoke-AdoReleaseCheck -Root $repo
            $script:calls.Count | Should -BeGreaterOrEqual 5
            ($script:calls | ForEach-Object ReleaseBuild | Select-Object -Unique) | Should -Be '1'
            $env:ADOTOOLKIT_RELEASE_BUILD | Should -BeNullOrEmpty
        }
        finally { $env:ADOTOOLKIT_RELEASE_BUILD = $previous }
    }

    It 'fails when a file is <Case> while the gate runs' -TestCases @(
        @{ Case = 'changed'; Relative = 'README.md'; Expected = 'changed: README.md' },
        @{ Case = 'added'; Relative = 'src/New.cs'; Expected = 'added: src/New.cs' }
    ) {
        param($Case, $Relative, $Expected)
        $script:packagingWrites = $Relative
        $result = Invoke-AdoReleaseCheck -Root $repo
        $result.Status | Should -Be 'fail'
        ($result.Steps | Where-Object Name -eq 'content').Detail | Should -Contain $Expected
    }

    It 'runs again after the finish deleted the journaled fragments' {
        Write-AdoReleaseState -Root $repo -Name 'release-prep.json' -State ([ordered]@{ Version = '0.4.0'; Old = '0.3.0'; Fragments = @([ordered]@{ Name = '20260110-120000-retry-fix.json'; Sha256 = 'a' * 64 }); AssemblyComplete = $true })
        (Invoke-AdoReleaseCheck -Root $repo).Status | Should -Be 'incomplete'
    }

    It 'records a gate that ended in an error, and skips the rest' {
        Mock Invoke-AdoReleaseVerify { throw 'verify timed out after 1800 seconds.' }
        $result = Invoke-AdoReleaseCheck -Root $repo
        $result.Status | Should -Be 'fail'
        ($result.Steps | Where-Object Name -eq 'gate').Detail | Should -Be @('verify timed out after 1800 seconds.')
        (Read-AdoReleaseState -Root $repo -Name 'release-check.json')['Status'] | Should -Be 'fail'
    }

    It 'skips the remaining steps after a failed gate' {
        $script:stageStatus['powershell-test'] = 'fail'
        $result = Invoke-AdoReleaseCheck -Root $repo
        $result.Status | Should -Be 'fail'
        ($result.Steps | ForEach-Object Status) | Should -Be @('fail', 'skipped', 'skipped', 'skipped', 'pass')
        @($script:calls | Where-Object { $_.Arguments -join ' ' -match 'Publish-AdoToolkitPackage' }).Count | Should -Be 0
    }
}

Describe 'Complete-AdoToolkitRelease' {
    BeforeEach {
        Set-GitState
        Mock git {
            $global:LASTEXITCODE = 0
            switch ($args[0]) {
                'status' { $script:gitStatus }
                'rev-parse' { $global:LASTEXITCODE = $(if ($script:tagExists) { 0 } else { 1 }) }
                'log' { $script:gitLog }
                default { throw "Unexpected git $($args -join ' ')" }
            }
        }
        Mock gh { $global:LASTEXITCODE = 1; 'release not found' }
        Mock Invoke-AdoReleaseVerify { [pscustomobject]@{ Status = 'pass'; Stages = @([pscustomobject]@{ Name = 'documentation'; Status = 'pass' }); Detail = @() } }
        Mock Invoke-AdoReleaseDocumentTest { [pscustomobject]@{ Status = 'pass'; Detail = @('7 passed') } }
        $repo = New-ReleaseRepository
        Add-SampleFragment -Root $repo
        $null = Invoke-AdoReleaseStart -Root $repo -Version '0.4.0' -Date '2026-02-01'
        Clear-ReleaseMarker -Root $repo -Version '0.4.0'
        $snapshot = Get-AdoContentSnapshot -Root $repo -Version '0.4.0'
        $script:check = [ordered]@{
            Status = 'pass'; Version = '0.4.0'
            Steps = @('gate', 'version', 'package', 'assets', 'content' | ForEach-Object { [ordered]@{ Name = $_; Status = 'pass'; Detail = @() } })
            Stages = @([ordered]@{ Name = 'powershell-lint'; Status = 'pass' }, [ordered]@{ Name = 'project-check'; Status = 'pass' })
            Tests = @([ordered]@{ Suite = 'tooling Pester'; Passed = 12 })
            Modules = [ordered]@{ 'Microsoft.PowerShell.PlatyPS' = '1.0.3'; Pester = '5.9.1'; PSScriptAnalyzer = '1.25.0' }
            Assets = @([ordered]@{ Name = 'AdoToolkit-0.4.0.zip'; Bytes = 876708 })
            Portable = 'passed: AdoToolkit 0.4.0, PowerShell 7.6.6, Windows x64'; Gh = 'present'
            ContentSha256 = $snapshot.Sha256; Files = $snapshot.Count; Content = $snapshot.Files
        }
        Write-AdoReleaseState -Root $repo -Name 'release-check.json' -State $script:check
    }

    It 'writes the Validation section, deletes the journaled fragments and writes the handoff' {
        $result = Invoke-AdoReleaseComplete -Root $repo -Summary 'faster releases'
        $result.Status | Should -Be 'pass'
        $result.Title | Should -Be 'AdoToolkit 0.4.0: faster releases'
        $result.FragmentsDeleted | Should -Be 3
        @(Get-ChildItem -LiteralPath (Join-Path $repo 'docs/unreleased') -Filter '*.json').Count | Should -Be 0
        $notes = Get-Text $repo 'docs/release-0.4.0.md'
        $notes | Should -Match '(?s)<!-- release-check:begin -->.*Pester 5\.9\.1.*tooling Pester 12.*`AdoToolkit-0\.4\.0\.zip` 876,708 bytes.*<!-- release-check:end -->'
        # The section adds no link, so the documentation stage counts the same before and after it.
        $notes.Substring($notes.IndexOf('release-check:begin', [StringComparison]::Ordinal)) | Should -Not -Match '\]\('
        Should -Invoke Invoke-AdoReleaseVerify -Times 1 -Exactly -ParameterFilter { ($Stage -join ',') -eq 'documentation' }
        Should -Invoke Invoke-AdoReleaseDocumentTest -Times 1 -Exactly
        $handoff = Get-Text $repo 'artifacts/release/handoff.md'
        $handoff | Should -Match ([regex]::Escape("`$title = 'AdoToolkit 0.4.0: faster releases'"))
        $handoff | Should -Match ([regex]::Escape('git push --atomic origin 0.4.0 v0.4.0'))
        $handoff | Should -Match ([regex]::Escape('gh pr checks 0.4.0 --watch --fail-fast && gh pr view 0.4.0 --web'))
        $handoff | Should -Match ([regex]::Escape('git switch --create <next> --no-track origin/main'))
    }

    It 'gives the publish command of the release skill, filled, and one that parses' {
        $handoff = New-AdoReleaseHandoff -Version '0.4.0' -Title 'AdoToolkit 0.4.0: faster releases' -Branch 'release/0.4.0' -Remote 'upstream'
        # The command as the release skill wrote it by hand up to 0.11.0, filled for this release:
        # each && keeps a failed commit from tagging and a failed push from opening anything.
        $expected = @'
$title = 'AdoToolkit 0.4.0: faster releases'; $body = 'Release of AdoToolkit 0.4.0, published from tag v0.4.0. Changes: CHANGELOG.md and docs/release-0.4.0.md. Merge with Squash and merge, keeping this title.'; git add --all && git commit -m $title && git tag -a v0.4.0 -m 'AdoToolkit 0.4.0' && git push --atomic upstream release/0.4.0 v0.4.0 && & { $created = $false; if (Get-Command gh -ErrorAction Ignore) { gh pr create --base main --head release/0.4.0 --title $title --body $body; $created = $LASTEXITCODE -eq 0 }; if (-not $created) { $repo = (git remote get-url upstream) -replace '^(?:\w+://)?(?:[^@/]+@)?([^:/]+)[:/]+', 'https://$1/' -replace '(?:\.git)?/?$'; $url = "$repo/compare/main...release/0.4.0?quick_pull=1&title=$([uri]::EscapeDataString($title))&body=$([uri]::EscapeDataString($body))"; $url; Start-Process $url } }
'@
        $handoff.Publish | Should -BeExactly $expected.Trim()
        $handoff.Watch | Should -BeExactly 'gh pr checks release/0.4.0 --watch --fail-fast && gh pr view release/0.4.0 --web'
        $handoff.Next | Should -BeExactly 'git fetch origin && git switch --create <next> --no-track origin/main && git push --set-upstream origin <next>'
        $tokens = $null
        $errors = $null
        $null = [System.Management.Automation.Language.Parser]::ParseInput($handoff.Publish, [ref] $tokens, [ref] $errors)
        $errors | Should -BeNullOrEmpty
        $handoff.Publish | Should -BeLike "`$title = 'AdoToolkit 0.4.0: faster releases'; `$body = 'Release of AdoToolkit 0.4.0, published from tag v0.4.0. Changes: CHANGELOG.md and docs/release-0.4.0.md.*"
        $handoff.Publish | Should -Match ([regex]::Escape('git push --atomic upstream release/0.4.0 v0.4.0'))
        $handoff.Publish | Should -Match ([regex]::Escape('(git remote get-url upstream)'))
        $handoff.Publish | Should -Match ([regex]::Escape('$url = "$repo/compare/main...release/0.4.0?quick_pull=1&title='))
        $handoff.Publish | Should -Not -Match '<(?:new|branch|remote|message)>'
        (New-AdoReleaseHandoff -Version '0.4.0' -Title 'AdoToolkit 0.4.0: x' -Branch 'b' -Remote 'origin' -NoGh).Text | Should -Not -Match 'gh pr checks'
    }

    It 'pushes a branch with no upstream to origin' {
        Set-GitState -StatusLine '## 0.4.0'
        $result = Invoke-AdoReleaseComplete -Root $repo -Summary 'faster releases'
        $result.Remote | Should -Be 'origin'
        (Get-Text $repo 'artifacts/release/handoff.md') | Should -Match ([regex]::Escape('git push --atomic origin 0.4.0 v0.4.0'))
    }

    It 'refuses the summary <Case>' -TestCases @(
        @{ Case = 'with a single quote'; Summary = "the user's update" },
        @{ Case = 'with a double quote'; Summary = 'the "fast" release' },
        @{ Case = 'with a backtick'; Summary = 'the `fast` release' },
        @{ Case = 'with a typographic quote'; Summary = 'the user' + [char] 0x2019 + 's update' },
        @{ Case = 'on two lines'; Summary = "one`ntwo" },
        @{ Case = 'that is empty'; Summary = ' ' }
    ) {
        param($Case, $Summary)
        { Invoke-AdoReleaseComplete -Root $repo -Summary $Summary } | Should -Throw '*no quote*'
        Should -Invoke Invoke-AdoReleaseVerify -Times 0 -Exactly
    }

    It 'refuses while a release:todo marker remains' {
        $notes = Join-Path $repo 'docs/release-0.4.0.md'
        [IO.File]::AppendAllText($notes, "<!-- release:todo: still owed -->`n")
        { Invoke-AdoReleaseComplete -Root $repo -Summary 'faster releases' } | Should -Throw '*Prose is still owed at docs/release-0.4.0.md:*'
        @(Get-ChildItem -LiteralPath (Join-Path $repo 'docs/unreleased') -Filter '*.json').Count | Should -Be 3
    }

    It 'refuses when the tree changed after the check started' {
        [IO.File]::AppendAllText((Join-Path $repo 'docs/tooling.md'), "Edited after the gate.`n")
        { Invoke-AdoReleaseComplete -Root $repo -Summary 'faster releases' } | Should -Throw '*changed: docs/tooling.md*'
        Should -Invoke Invoke-AdoReleaseVerify -Times 0 -Exactly
    }

    It 'refuses a check that <Case>' -TestCases @(
        @{ Case = 'failed'; Status = 'fail'; Step = 'gate' },
        @{ Case = 'is incomplete for more than the portable asset'; Status = 'incomplete'; Step = 'gate' }
    ) {
        param($Case, $Status, $Step)
        $script:check['Status'] = $Status
        $script:check['Steps'][0]['Status'] = $(if ($Status -eq 'fail') { 'fail' } else { 'incomplete' })
        Write-AdoReleaseState -Root $repo -Name 'release-check.json' -State $script:check
        { Invoke-AdoReleaseComplete -Root $repo -Summary 'faster releases' } | Should -Throw "*ended $Status*"
    }

    It 'accepts a check that only could not build the portable asset' {
        $script:check['Status'] = 'incomplete'
        $script:check['Steps'][3]['Status'] = 'incomplete'
        $script:check['Portable'] = 'not built locally'
        Write-AdoReleaseState -Root $repo -Name 'release-check.json' -State $script:check
        $result = Invoke-AdoReleaseComplete -Root $repo -Summary 'faster releases'
        $result.Warnings | Should -Match 'portable asset was not built locally'
    }

    It 'writes the same Validation section when it runs twice' {
        $null = Invoke-AdoReleaseComplete -Root $repo -Summary 'faster releases'
        $first = Get-Text $repo 'docs/release-0.4.0.md'
        $null = Invoke-AdoReleaseComplete -Root $repo -Summary 'faster releases'
        Get-Text $repo 'docs/release-0.4.0.md' | Should -BeExactly $first
        @([regex]::Matches($first, 'release-check:begin')).Count | Should -Be 1
    }

    It 'completes on a second run after an interruption between two deletions' {
        $script:deletions = 0
        Mock Remove-Item {
            $script:deletions++
            if ($script:deletions -eq 2) { throw 'interrupted' }
            [IO.File]::Delete($LiteralPath)
        } -ParameterFilter { $LiteralPath -like '*unreleased*' }
        { Invoke-AdoReleaseComplete -Root $repo -Summary 'faster releases' } | Should -Throw '*interrupted*'
        @(Get-ChildItem -LiteralPath (Join-Path $repo 'docs/unreleased') -Filter '*.json').Count | Should -Be 2
        $script:deletions = 10
        $result = Invoke-AdoReleaseComplete -Root $repo -Summary 'faster releases'
        $result.FragmentsDeleted | Should -Be 2
        @(Get-ChildItem -LiteralPath (Join-Path $repo 'docs/unreleased') -Filter '*.json').Count | Should -Be 0
    }

    It 'refuses a fragment added after the journal' {
        New-Fragment $repo '20260111-090000-late.json' @{ kind = 'internal'; changelog = 'Late.' }
        { Invoke-AdoReleaseComplete -Root $repo -Summary 'faster releases' } | Should -Throw '*added after the journal: 20260111-090000-late.json*'
    }
}

Describe 'The release scripts' {
    BeforeAll {
        # A copy of the tooling in a folder that is no Git repository and holds no preparation, so
        # each script stops at its first refusal and nothing reaches this repository.
        $copy = Join-Path $TestDrive 'scripts'
        foreach ($relative in @('tools/dev.ps1', 'tools/BuildModules.psd1', 'tools/package/Package.Common.ps1', 'tools/package/Release.Common.ps1',
                'tools/package/Start-AdoToolkitRelease.ps1', 'tools/package/Test-AdoToolkitRelease.ps1', 'tools/package/Complete-AdoToolkitRelease.ps1')) {
            New-TestFile (Join-Path $copy $relative) (Get-Text $repositoryRoot $relative)
        }
        foreach ($library in Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'tools/lib') -Filter '*.ps1') {
            New-TestFile (Join-Path $copy "tools/lib/$($library.Name)") ([IO.File]::ReadAllText($library.FullName))
        }
        New-TestFile (Join-Path $copy 'Directory.Build.props') "<Project><PropertyGroup><VersionPrefix>0.3.0</VersionPrefix></PropertyGroup></Project>`n"
    }

    It 'prints one JSON document and exits 1 when <Name> refuses' -TestCases @(
        @{ Name = 'Start-AdoToolkitRelease.ps1'; Arguments = @('-Version', 'next', '-WhatIf'); Expected = "*'next' is not a three-part version*" },
        @{ Name = 'Test-AdoToolkitRelease.ps1'; Arguments = @(); Expected = '*No completed preparation*' },
        @{ Name = 'Complete-AdoToolkitRelease.ps1'; Arguments = @('-Summary', "the user's release"); Expected = '*no quote*' }
    ) {
        param($Name, $Arguments, $Expected)
        $output = @(& (Join-Path $PSHOME 'pwsh.exe') -NoProfile -NonInteractive -File (Join-Path $copy "tools/package/$Name") @Arguments)
        $LASTEXITCODE | Should -Be 1
        $output.Count | Should -Be 1
        $result = $output[0] | ConvertFrom-Json
        $result.Status | Should -Be 'fail'
        $result.Error | Should -BeLike $Expected
    }
}
