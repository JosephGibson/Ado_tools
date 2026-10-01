---
name: release
description: Prepares an AdoToolkit release for the developer - bumps the version everywhere it is repeated, archives old release notes, writes docs/release-<version>.md, and validates the gate and the local package. Use only when the user asks to prepare, bump or write up a release.
---

# Prepare a release

`<new>` is the version being released, `<old>` the one before it, `<older>` the one before
that. The developer commits, tags and pushes; this skill never does.

## 1. Bump the version

| File | Change |
| --- | --- |
| `Directory.Build.props` | `VersionPrefix`: the only declaration; the manifest and assemblies follow it |
| `README.md` | The version line, the paragraph on what the version brings, the release notes link |
| `docs/guides/getting-started.md` | The file names `AdoToolkit-<old>.zip` and `AdoToolkit-<old>-win-x64.zip` |
| `tools/package/Install-AdoToolkit.ps1` | The file names in its `.EXAMPLE` blocks |

Check: `pwsh -NoProfile -File .\tools\dev.ps1 find -Query '<old>' -Limit 100` lists only
`docs/release-<old>.md`, files under `docs/archive/`, and historical mentions in
`docs/release-<new>.md`.

## 2. Archive the notes of `<older>`

`docs/` keeps the notes of `<new>` and `<old>` only. Move `docs/release-<older>.md` as
`docs/AGENTS.md` describes under Archiving, and repoint the links in `docs/release-<old>.md`.

## 3. Write `docs/release-<new>.md`

Write from the final diff (`git diff`, `git status`), not from memory. Keep the sections
and tables of `docs/release-<old>.md`:

| Section | Holds |
| --- | --- |
| Opening paragraph | The date, what the notes were prepared from, and whether anything was confirmed live |
| What changed | One table per area: `Area`, `<new> behavior` |
| Visible changes for `<old>` users | Every change a user or script can observe, with each public contract change |
| Bug fixes | `Area`, `Finding`, `Fix`, `Regression test`, `Files`; then how the tests were shown to fail before the fix |
| Existing tests changed | Tests and goldens that were edited, and why |
| Findings not fixed | The finding and the reason |
| Known limitations | Limits that remain, with their `V-nn` item |
| Work-PC Live checks | Ranked checks for the work PC, each with the evidence to seek |
| Local validation and developer handoff | The commands run and their results, the local assets with size and SHA-256, the steps left to the developer |

Report only what was run. Counts come from the final `verify` output.

## 4. Validate

| Command | Expect |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify` | Exit `0`. Exit `2` names a missing prerequisite and is not a pass |
| The same with `$env:ADOTOOLKIT_RELEASE_BUILD = '1'` | Exit `0` with the exact module versions of `tools/BuildModules.psd1`; exit `2` when one is not installed |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `<new>` |
| `pwsh -NoProfile -File .\tools\package\Publish-AdoToolkitPackage.ps1 -NoRestore` | Stages `artifacts/AdoToolkit/<new>/` with the seven package files |
| `pwsh -NoProfile -File .\tools\package\New-AdoToolkitRelease.ps1` | Writes the module zip, its `.sha256` and the installer to `artifacts/release/` |

The portable zip needs the pinned PowerShell archive and its published `hashes.sha256`
on disk: pass `-PowerShellArchivePath`, `-PowerShellChecksumPath` and `-PowerShellVersion`
to `New-AdoToolkitRelease.ps1`, then run `tools/package/Test-AdoToolkitPortable.ps1`. The
scripts never download; ask before fetching anything. Without the archive, say that the
portable asset was not built locally.

## 5. Hand off

State, in this order: the gate result, the assets built with size and SHA-256, each public
contract change, the findings not fixed, and the steps that remain with the developer:

1. Review the diff and commit.
2. Create the tag `v<new>` on that commit and push the commit and the tag.
3. Watch `.github/workflows/release.yml` and compare its five assets and checksums.
4. Run the work-PC live checks with the published package.
