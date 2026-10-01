BeforeAll {
    . (Join-Path $PSScriptRoot '../dev.ps1')
    function New-Fixture {
        param([string] $Path, [string] $Content = '')
        [void][System.IO.Directory]::CreateDirectory((Split-Path -Parent $Path))
        [System.IO.File]::WriteAllText($Path, $Content.Replace("`r`n", "`n"), [System.Text.UTF8Encoding]::new($false))
    }
    function Get-Outcome {
        param([string] $Root)
        Get-DocumentationOutcome -ProjectProfile (Get-ProjectProfile -Root $Root)
    }
    function New-Skill {
        param([string] $Root, [string] $Agent, [string] $Name, [string] $Description, [string] $Body = '# Skill')
        New-Fixture (Join-Path $Root "$Agent/skills/$Name/SKILL.md") "---`nname: $Name`ndescription: $Description`n---`n`n$Body`n"
    }
    function Get-Layout {
        param([string] $Root)
        Get-ToolingLayoutOutcome -ProjectProfile (Get-ProjectProfile -Root $Root)
    }
}

Describe 'Documentation stage' {
    It 'passes when every link, anchor and repository path resolves' {
        $root = Join-Path $TestDrive 'clean'
        New-Fixture (Join-Path $root 'README.md') @'
# Sample

See the [guide](docs/guide.md#step-1--connect), [the second heading](docs/guide.md#twice-1),
[this file](#sample), the [folder](docs/), <https://example.test/a> and `docs/guide.md`.

[reference]: docs/guide.md "Guide"
[^1]: A footnote is text, not a link target.
'@
        New-Fixture (Join-Path $root 'docs/guide.md') @'
# Guide

## Step 1 — Connect

## Twice

## Twice

<a id="Custom"></a>
Back to the [README](../README.md) and to [here](#Custom).
'@
        $outcome = Get-Outcome $root
        $outcome.Failures | Should -BeNullOrEmpty
        $outcome.Summary | Should -Be @('2 Markdown file(s), 7 link(s) and 1 path reference(s) checked')
    }

    It 'reports a missing file, a missing anchor and a link that leaves the repository' {
        $root = Join-Path $TestDrive 'broken'
        New-Fixture (Join-Path $root 'README.md') @'
# Sample

[gone](docs/gone.md)
[anchor](docs/guide.md#no-such-heading)
[outside](../outside.md)
[self](#missing)
'@
        New-Fixture (Join-Path $root 'docs/guide.md') '# Guide'
        New-Fixture (Join-Path $TestDrive 'outside.md') '# Outside'
        (Get-Outcome $root).Failures | Should -Be @(
            "README.md:3: link 'docs/gone.md' does not resolve.",
            "README.md:4: link 'docs/guide.md#no-such-heading' names no heading of its target.",
            "README.md:5: link '../outside.md' does not resolve.",
            "README.md:6: link '#missing' names no heading of its target.")
    }

    It 'reports a repository path that does not exist and leaves placeholders and other text alone' {
        $root = Join-Path $TestDrive 'paths'
        New-Fixture (Join-Path $root 'src/App.cs') ''
        New-Fixture (Join-Path $root 'AGENTS.md') @'
# Rules

- `src/App.cs`, `src\App.cs`, `./src/App.cs` and `src/` exist.
- `src/Gone.cs` and `tools/` do not.
- `src/<name>.cs`, `tests/**/*.cs`, `application/json`, `fr/Help.xml` and
  `pwsh -File .\tools\dev.ps1` are not paths to check.
'@
        (Get-Outcome $root).Failures | Should -Be @(
            "AGENTS.md:4: path 'src/Gone.cs' does not exist.",
            "AGENTS.md:4: path 'tools/' does not exist.")
    }

    It 'ignores links and paths inside code blocks, and links inside code spans' {
        $root = Join-Path $TestDrive 'code'
        New-Fixture (Join-Path $root 'README.md') @'
# Sample

A span: `[text](missing.md)`.

```text
[text](missing.md) and `src/Gone.cs`
```

~~~~markdown
```
[text](missing.md)
```
~~~~
'@
        $outcome = Get-Outcome $root
        $outcome.Failures | Should -BeNullOrEmpty
        $outcome.Summary | Should -Be @('1 Markdown file(s), 0 link(s) and 0 path reference(s) checked')
    }

    It 'checks links but not paths in a dated document and skips archived documents and fixtures' {
        $root = Join-Path $TestDrive 'history'
        # An entry of the changelog, like the notes of a release, describes the repository of its day.
        New-Fixture (Join-Path $root 'CHANGELOG.md') "# Changelog`n`n## 1.0.0 - 2026-01-15`n`n- ``src/Removed.cs`` was deleted. [Notes](docs/release-1.0.0.md) and [gone](docs/gone.md)."
        New-Fixture (Join-Path $root 'docs/CHANGELOG.md') "``src/Removed.cs``"
        New-Fixture (Join-Path $root 'docs/release-1.0.0.md') "# Notes`n`n``src/Removed.cs`` was deleted. See [gone](gone.md)."
        New-Fixture (Join-Path $root 'docs/archive/old.md') "[gone](gone.md) and ``src/Removed.cs``"
        New-Fixture (Join-Path $root 'docs/archive/README.md') "[old](old.md), [gone](gone.md) and ``src/Removed.cs``"
        # A golden report is test data, not documentation.
        New-Fixture (Join-Path $root 'tests/Fixtures/Reports/golden.md') '[gone](gone.md)'
        (Get-Outcome $root).Failures | Should -Be @(
            "CHANGELOG.md:5: link 'docs/gone.md' does not resolve.",
            "docs/CHANGELOG.md:1: path 'src/Removed.cs' does not exist.",
            "docs/archive/README.md:1: link 'gone.md' does not resolve.",
            "docs/archive/README.md:1: path 'src/Removed.cs' does not exist.",
            "docs/release-1.0.0.md:3: link 'gone.md' does not resolve.")
    }

    It 'is part of the plan only when the repository has Markdown' {
        $with = Join-Path $TestDrive 'plan-with'
        $without = Join-Path $TestDrive 'plan-without'
        New-Fixture (Join-Path $with 'README.md') '# Sample'
        New-Fixture (Join-Path $without 'app.json') '{}'
        (Get-ProjectValidationPlan -Root $with).Stages.Name | Should -Contain 'documentation'
        (Get-ProjectValidationPlan -Root $without).Stages.Name | Should -Not -Contain 'documentation'
    }
}

Describe 'Skill layout' {
    It 'accepts a skill whose Claude wrapper names its body and repeats its description' {
        $root = Join-Path $TestDrive 'skills-paired'
        New-Skill $root '.agents' 'sample' 'Does the sample task.'
        New-Skill $root '.claude' 'sample' 'Does the sample task.' 'Read `.agents/skills/sample/SKILL.md` and follow it.'
        $outcome = Get-Layout $root
        $outcome.Failures | Should -BeNullOrEmpty
        $outcome.Summary | Should -Contain '1 skill(s) paired between .agents and .claude'
    }

    It 'reports a missing body, a missing wrapper, a different description and a wrong name' {
        $root = Join-Path $TestDrive 'skills-broken'
        New-Skill $root '.claude' 'orphan' 'Wrapper only.' 'Read `.agents/skills/orphan/SKILL.md`.'
        New-Skill $root '.agents' 'codex-only' 'Body only.'
        New-Skill $root '.agents' 'drift' 'The body description.'
        New-Skill $root '.claude' 'drift' 'Another description.' 'Read `.agents/skills/drift/SKILL.md`.'
        New-Skill $root '.agents' 'unlinked' 'Same.'
        New-Skill $root '.claude' 'unlinked' 'Same.' 'No pointer to the body.'
        New-Fixture (Join-Path $root '.agents/skills/renamed/SKILL.md') "---`nname: other`ndescription: Same.`n---`n"
        New-Fixture (Join-Path $root '.claude/skills/renamed/SKILL.md') "---`nname: renamed`ndescription: Same.`n---`n``.agents/skills/renamed/SKILL.md```n"
        (Get-Layout $root).Failures | Sort-Object | Should -Be @(
            ".agents/skills/renamed/SKILL.md: the front matter name must be 'renamed'.",
            ".claude/skills/unlinked/SKILL.md: the wrapper must name .agents/skills/unlinked/SKILL.md.",
            "Skill 'codex-only' has no Claude wrapper at .claude/skills/codex-only/SKILL.md.",
            "Skill 'drift' has different descriptions in .agents and .claude.",
            "Skill 'orphan' has no body at .agents/skills/orphan/SKILL.md.")
    }
}
