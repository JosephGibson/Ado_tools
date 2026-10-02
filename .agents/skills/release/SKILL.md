---
name: release
description: Prepare an AdoToolkit version bump, release notes, changelog and validated local packages, then give the developer commit/tag/push and pull-request commands. Use only for a requested release.
---

# Prepare a release

`<new>`, `<old>` and `<older>` are the next, current and previous versions.
Prepare the release from the checked-out `<branch>`; its pull request targets `main`.
Run no Git writes. Give handoff commands only after validation passes.

The developer is the author. Add no agent/tool attribution or co-author trailer to
commits, tags, pull requests, notes, changelog or release text.

## 1. Update the version

| File | Update |
| --- | --- |
| `Directory.Build.props` | `VersionPrefix`, the sole declaration; assemblies and manifest follow it |
| `README.md` | Version, feature paragraph and release-note link |
| `docs/guides/getting-started.md` | Module and portable zip names |
| `tools/package/Install-AdoToolkit.ps1` | Zip names in examples |

Run `pwsh -NoProfile -File .\tools\dev.ps1 find -Query '<old>' -Limit 100`.
Remaining occurrences belong only to history: old notes, changelog, archive or historical
comparisons in the new notes.

Keep only the current and previous notes under `docs/`. Archive
`docs/release-<older>.md` using the Archiving procedure in `docs/AGENTS.md`;
repoint the old notes' and changelog's links.

## 2. Write the notes and changelog

Use the final `git diff` and `git status`, following the previous notes' structure:

| Section | Content |
| --- | --- |
| Opening | Date, source of the reviewed changes, live-validation status |
| What changed | Tables by area, describing final behavior |
| Visible changes for previous users | Every observable change, including public contract changes |
| Bug fixes | Finding, cause/fix, regression test, files and pre-fix failure evidence |
| Existing tests changed | Edited tests/goldens and reasons |
| Findings not fixed | Finding and reason |
| Known limitations | Remaining limits and `V-nn` items |
| Work-PC Live checks | Ranked checks and required evidence |
| Local validation and developer handoff | Actual commands/results, local assets with size/SHA-256 and developer steps |

Write the changelog from the same diff after the notes. Put the new section first:

```markdown
## <new> - <yyyy-MM-dd>

### Fixed

- <One line describing what a user sees.>

Details: [release notes](docs/release-<new>.md)
```

Use only populated groups, in order: Added, Changed, Deprecated, Removed, Fixed, Security.
Keep tests, tooling and internal details in the notes; if no user behavior changes, say
so in one entry. The date matches the notes. `verify` requires the exact version/date
heading, at least one bullet and the note link; the release workflow publishes this section.

## 3. Validate the final tree

| Command | Required result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify` | Exit `0`; exit `2` is incomplete |
| Same with `ADOTOOLKIT_RELEASE_BUILD=1` | Exit `0` with exact module versions from `tools/BuildModules.psd1` |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | `<new>` |
| `pwsh -NoProfile -File .\tools\package\Publish-AdoToolkitPackage.ps1 -NoRestore` | Valid seven-file package under `artifacts/AdoToolkit/<new>/` |
| `pwsh -NoProfile -File .\tools\package\New-AdoToolkitRelease.ps1` | Module zip, checksum and installer under `artifacts/release/` |

Restore the previous release-build environment value after its check. Counts come from
the final gate. Any later edit, including validation notes, requires another `verify`.

The portable asset requires the official PowerShell archive and published checksum on
disk. Use the arguments and validation command in `docs/tooling.md`, Packaging and
releases. Fetch nothing without authorization; if absent, report the portable asset as
not built locally. If any required row fails or exits `2`, report command, code and
named failures, then stop without handoff commands.

## 4. Prepare the developer handoff

Read `git status --short --branch`: take branch and remote from its first line; an
unpublished branch uses `origin`. Stop on detached HEAD or `main`.
Run `git show --no-patch v<new>`: success means the version was already released, so stop.
List unrelated working-tree paths above the commands; command 1 stages everything.

Set `<message>` to one line, `AdoToolkit <new>: <summary>`, following
`git log --oneline`. The summary contains no quote; the message has no body or trailer.

Report the gate, built assets with size/SHA-256, public contract changes and unfixed
findings. Fill every placeholder in these templates and give each on one line in its
own code block. State that command 1 publishes immediately when it pushes the tag,
before pull-request review.

```powershell
git add --all && git commit -m '<message>' && git tag -a v<new> -m 'AdoToolkit <new>' && git push --atomic <remote> <branch> v<new>
```

Command 2 prints and opens the compare page, encoding the title/body when run; it uses
no `gh`.

```powershell
$title = '<message>'; $body = 'Release of AdoToolkit <new>, published from tag v<new>. Changes: CHANGELOG.md and docs/release-<new>.md.'; $repo = (git remote get-url <remote>) -replace '^(?:\w+://)?(?:[^@/]+@)?([^:/]+)[:/]+', 'https://$1/' -replace '(?:\.git)?/?$'; $url = "$repo/compare/main...<branch>?quick_pull=1&title=$([uri]::EscapeDataString($title))&body=$([uri]::EscapeDataString($body))"; $url; Start-Process $url
```

Developer steps after running them:

1. Create the pull request on the opened page; merge after the verify workflow passes.
2. Watch the release workflow and compare its five assets/checksums. For failures, use
   Release workflow in `docs/tooling.md`.
3. Run work-PC live checks with the published package.
