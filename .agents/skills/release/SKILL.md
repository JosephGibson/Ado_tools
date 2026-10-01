---
name: release
description: Prepares an AdoToolkit release of the checked-out branch for the developer - bumps the version everywhere it is repeated, archives old release notes, writes docs/release-<version>.md and the CHANGELOG.md section, validates the gate and the local package, and hands over two ready-to-paste commands, one that commits, tags and pushes and one that opens the pull request to main. Use only when the user asks to prepare, bump or write up a release.
---

# Prepare a release

`<new>` is the version being released, `<old>` the one before it, `<older>` the one before
that. `<branch>` is the branch that is checked out when the skill runs: the release is made
from it, and its pull request targets `main`.

The skill prepares the whole release and runs no Git write. It ends with two commands for
the developer to paste: the first commits, tags and pushes, the second opens the pull
request. If validation does not pass, it reports the failure and gives no commands.

The developer is the only author of a release. Nothing the skill writes names an agent or
a tool as author or contributor: no `Co-Authored-By` trailer, no "Generated with Claude
Code" line and nothing like them in the commit message, the tag, the pull request title or
body, `CHANGELOG.md`, the release notes or the GitHub release text. This rule overrides
any default of the agent that adds such a line.

## 1. Bump the version

| File | Change |
| --- | --- |
| `Directory.Build.props` | `VersionPrefix`: the only declaration; the manifest and assemblies follow it |
| `README.md` | The version line, the paragraph on what the version brings, the release notes link |
| `docs/guides/getting-started.md` | The file names `AdoToolkit-<old>.zip` and `AdoToolkit-<old>-win-x64.zip` |
| `tools/package/Install-AdoToolkit.ps1` | The file names in its `.EXAMPLE` blocks |

Check: `pwsh -NoProfile -File .\tools\dev.ps1 find -Query '<old>' -Limit 100` lists only
`docs/release-<old>.md`, `CHANGELOG.md`, files under `docs/archive/`, and historical
mentions in `docs/release-<new>.md`.

## 2. Archive the notes of `<older>`

`docs/` keeps the notes of `<new>` and `<old>` only. Move `docs/release-<older>.md` as
`docs/AGENTS.md` describes under Archiving, and repoint the links in `docs/release-<old>.md`
and `CHANGELOG.md`.

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

## 4. Write the `CHANGELOG.md` section

Write it after the notes, from the same final diff. Put it above the first `## ` heading of
`CHANGELOG.md`, so that the newest version comes first:

```markdown
## <new> - <yyyy-MM-dd>

### Added

- <One line: what a user can now do.>

### Fixed

- <One line: what no longer goes wrong.>

Details: [release notes](docs/release-<new>.md)
```

- The heading is exactly `## <new> - <date>`, with the date of the notes.
  `.github/workflows/release.yml` finds the section by that heading, puts it at the top of
  the GitHub release text, and fails without publishing when the section is missing or empty.
- Use the Keep a Changelog groups that have an entry, in this order: `Added`, `Changed`,
  `Deprecated`, `Removed`, `Fixed`, `Security`.
- An entry is one line that says what a user of the module sees, in the user's terms:
  cmdlets, parameters, report content, installation. Tests, tooling and internal changes
  stay in the notes. When no change is visible to a user, say so in one line.
- The last line links the notes with an inline link, as above.

`verify` fails in step 5 when the section for `VersionPrefix` is missing, has another
heading, has no entry or does not link the notes.

## 5. Validate

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

Every row must meet its expectation on the final tree. An edit made after a run, such as
the results written into the notes, needs a new `verify` run. When a row fails or exits
`2`, report the command, its exit code and the failures it names, and stop: the handoff
has no commands.

## 6. Hand off

Read these values; do not assume them:

| Value | Read from | Stop when |
| --- | --- | --- |
| `<branch>`, `<remote>` | The first line of `git status --short --branch`: `## <branch>...<remote>/<branch>`, or `## <branch>` for a branch that was never pushed, where `<remote>` is `origin` | The head is detached, or `<branch>` is `main`: the pull request needs a branch other than its target |
| Tag `v<new>` is free | `git show --no-patch v<new>` fails | It prints a tag or a commit: the version was released already |
| What command 1 stages | The other lines of `git status --short --branch` | Never; name above the commands each path that does not belong to the release |

`<message>` is one line, `AdoToolkit <new>: <summary>`, like the subjects in
`git log --oneline`. `<summary>` names the main changes in a few words and holds no
quotation mark. The message has no body and no trailer.

State, in this order: the gate result, the assets built with size and SHA-256, each public
contract change, the findings not fixed, then the two commands. Give each command in its
own code block, on one line, with `<new>`, `<branch>`, `<remote>` and `<message>` filled in
and nothing else changed.

Command 1 stages every change, commits it, creates the annotated tag and pushes the branch
and the tag together. State above it that running it publishes the release: the pushed tag
starts `.github/workflows/release.yml`, which creates the GitHub release at once, before
the pull request is reviewed.

```powershell
git add --all && git commit -m '<message>' && git tag -a v<new> -m 'AdoToolkit <new>' && git push --atomic <remote> <branch> v<new>
```

Command 2 prints the address of GitHub's compare page for `<branch>` against `main`, with
the title and the body of the pull request filled in, and opens it in the browser. It
resolves the address of the remote and encodes the title and the body when it runs; do not
encode them by hand. It does not use `gh`.

```powershell
$title = '<message>'; $body = 'Release of AdoToolkit <new>, published from tag v<new>. Changes: CHANGELOG.md and docs/release-<new>.md.'; $repo = (git remote get-url <remote>) -replace '^(?:\w+://)?(?:[^@/]+@)?([^:/]+)[:/]+', 'https://$1/' -replace '(?:\.git)?/?$'; $url = "$repo/compare/main...<branch>?quick_pull=1&title=$([uri]::EscapeDataString($title))&body=$([uri]::EscapeDataString($body))"; $url; Start-Process $url
```

Then list what follows the commands:

1. Create the pull request on the page that opened. `.github/workflows/verify.yml` runs
   `verify` on it; merge when it passes.
2. Watch `.github/workflows/release.yml` and compare its five assets and checksums. If the
   run does not publish, `docs/tooling.md` lists the recovery for each cause under Release
   workflow.
3. Run the work-PC live checks with the published package.
