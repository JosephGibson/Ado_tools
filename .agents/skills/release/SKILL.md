---
name: release
description: Prepare an AdoToolkit version bump, release notes, changelog and validated local packages, then give the developer two commands that publish the release and open its pull request. Use only for a requested release.
---

# Prepare a release

`<new>`, `<old>` and `<older>` are the next, current and previous versions.
Prepare the release from the checked-out `<branch>`; its pull request targets `main`.
Run no Git writes and publish nothing: the developer runs the handoff commands, and they are
given only after validation passes.

The developer is the author. Add no agent/tool attribution or co-author trailer to
commits, tags, pull requests, notes, changelog or release text.

## 1. Update the version

| File | Update |
| --- | --- |
| `Directory.Build.props` | `VersionPrefix`, the sole declaration; assemblies and manifest follow it |
| `README.md` | Version, feature paragraph and release-note link |
| `docs/guides/getting-started.md` | Module and portable zip names |
| `tools/package/Install-AdoToolkit.ps1` | Zip names in examples |

`pwsh -NoProfile -File .\tools\dev.ps1 find -Query '<old>' -Limit 100` names every file that
still holds `<old>`. It reports one line per file, so read each file's own matches before
deciding. Remaining occurrences belong only to history: old notes, the changelog, the archive,
or historical comparisons in the new notes.

Keep only the current and the previous notes under `docs/`. Archive
`docs/release-<older>.md` by the Archiving procedure in `docs/AGENTS.md`: move the file, add
its row to `docs/archive/README.md`, and repoint every link that named the old location.
Markdown under `docs/archive/` is frozen and unchecked, so only live documents need
repointing; the changelog and the `<old>` notes are the usual two. If the archived notes
define a `V-nn` item, keep its row in `docs/archive/README.md` resolving to the new place.

## 2. Write the notes and changelog

Use the final `git diff` and `git status`, following the previous notes' structure:

| Section | Content |
| --- | --- |
| Opening | Date, source of the reviewed changes, live-validation status |
| What changed | Tables by area, describing final behavior |
| Visible changes for previous users | Every observable change, including public contract changes |
| Bug fixes | One row per defect: an ID, the finding with its cause and fix, the regression test, the files. A Pre-fix failures table follows it, from section 3 |
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

## 3. Prove each fix failed before it

A Bug fixes row needs evidence that its regression test fails without its fix. Produce it one
fix at a time, by restoring released source rather than editing the fix away:

1. Copy the file as it stands into the session scratch folder, outside the repository, so no
   copy can be committed.
2. Write the released content beside it: `git show <base>:<path>`, where `<base>` is the
   commit `<branch>` starts at.
3. Copy the released content over the target and run only the covering test. Record the
   message, and the run's totals.
4. Copy the saved file back, then set its `LastWriteTime` to now. `Copy-Item` restores the
   old timestamp, so MSBuild would skip the rebuild and every later run would test the
   assembly built from the released source.
5. Confirm the restore with `git diff --stat -- <path>`: the counts must be those of the
   branch's own change.

Revert all the files of one fix together; a partial revert may not compile. How to run one
test:

| Suite | Run |
| --- | --- |
| Core | `dotnet test tests/AdoToolkit.Core.Tests --configuration Release --no-restore --filter "FullyQualifiedName~<Name>"` |
| Product Pester | `tools/check.ps1 -SkipTests` stages the module, then run the one file with `ADOTOOLKIT_MODULE_MANIFEST` naming the staged `AdoToolkit.psd1` and `ADOTOOLKIT_CONFIG_PATH` naming a file that does not exist |
| Tooling | No build. Import Pester 5 explicitly, because a bare `Invoke-Pester` may load Pester 6, and run the one file |

Record each failure in the Pre-fix failures table, with the totals of every pre-fix run, and
say that each test passes on the final tree. A fix whose failure cannot be reproduced is
reported as such: do not claim evidence that was not observed.

## 4. Validate the final tree

| Command | Required result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify` | Exit `0`; exit `2` is incomplete |
| Same with `ADOTOOLKIT_RELEASE_BUILD=1` | Exit `0` with the exact module versions from `tools/BuildModules.psd1`. Set it for that one process; never set it in the shell |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | `<new>` |
| `pwsh -NoProfile -File .\tools\package\Publish-AdoToolkitPackage.ps1 -NoRestore` | Valid seven-file package under `artifacts/AdoToolkit/<new>/` |
| `pwsh -NoProfile -File .\tools\package\New-AdoToolkitRelease.ps1` | Module zip, checksum and installer under `artifacts/release/` |
| `pwsh -NoProfile -File .\tools\package\Test-AdoToolkitPortable.ps1` | The module version, the PowerShell version and the architecture of the portable zip |
| `Get-FileHash` of each zip | Equal to the hash in its own `.sha256` file, and the shipped installer byte-identical to `tools/package/Install-AdoToolkit.ps1` |
| `tools/package/Install-AdoToolkit.ps1 -Path <module zip> -WhatIf` | Accepts the checksum and the layout and names the target. It writes nothing and installs nothing |
| The archive entries | The module zip holds the seven files under `AdoToolkit/<new>/`. The portable zip holds the same seven under `module/`, both launchers, `README.txt` and `bundle.json`, whose PowerShell hash equals the published hash and the pin of the setup action |
| `pwsh -NoProfile -File .\tools\dev.ps1 diagnose` | Records whether `gh` is present, which decides the handoff commands |

The validation section records the final gate, and writing it is itself an edit. So: run the
gate, write the section from that run, run the gate again, and compare. Adding links to the
notes raises the documentation stage's link count, so correct the number and run once more.
The counts in the notes must be those of the last run.

A test that fails once and passes on a repeat of the same command is a flake, not a blocker,
when nothing in the release touches its subject. Record the command, the failure and the
passing repeat in the validation section, and add a row to Findings not fixed saying why the
release does not cause it. A failure that repeats stops the release.

The portable asset requires the official PowerShell archive and published checksum on
disk. Use the arguments and validation command in `docs/tooling.md`, Packaging and
releases. Fetch nothing without authorization; if absent, report the portable asset as
not built locally. If any required row fails or exits `2`, report command, code and
named failures, then stop without handoff commands.

## 5. Prepare the developer handoff

Read `git status --short --branch`: take branch and remote from its first line; a branch with
no upstream pushes to `origin`. Stop on detached HEAD or `main`.
Run `git show --no-patch v<new>`: success means the version was already released, so stop.
List unrelated working-tree paths above the commands; command 1 stages everything.

Report `gh`'s state from `diagnose`, but do not pick the commands from it: command 1 falls
back on its own. winget puts a tool on the PATH of new terminals only, so `missing` just
after an install means "open a new terminal", not "unavailable". Creating a pull request also
needs a token allowed to write one: a fine-grained PAT without pull-request write reads the
repository, the checks and the runs and still refuses `createPullRequest`. Nothing short of
the create settles that, because `gh pr create --dry-run` exits `0` without exercising the
permission, so the fallback belongs in the command and not in a probe.

Set `<message>` to one line, `AdoToolkit <new>: <summary>`, following
`git log --oneline`. The summary contains no quote; the message has no body or trailer.
Leave out the ` (#<n>)` that ends a subject on `main`: the squash merge appends it.

Report the gate, built assets with size/SHA-256, public contract changes and unfixed
findings. Fill every placeholder in these templates and give each on one line in its own
code block. State that command 1 publishes the release the moment it pushes the tag, before
any pull-request review.

Command 1 commits, tags, pushes both together, and then takes the pull request as far as it
can with its final title already set: `gh pr create` where that works, and otherwise the
compare page, printed and opened with the title and the body encoded, where the developer
creates it and keeps the title. One template serves both, and the commit message and the
pull-request title are one variable, so neither can drift from the other.

```powershell
$title = '<message>'; $body = 'Release of AdoToolkit <new>, published from tag v<new>. Changes: CHANGELOG.md and docs/release-<new>.md. Merge with Squash and merge, keeping this title.'; git add --all && git commit -m $title && git tag -a v<new> -m 'AdoToolkit <new>' && git push --atomic <remote> <branch> v<new> && & { $created = $false; if (Get-Command gh -ErrorAction Ignore) { gh pr create --base main --head <branch> --title $title --body $body; $created = $LASTEXITCODE -eq 0 }; if (-not $created) { $repo = (git remote get-url <remote>) -replace '^(?:\w+://)?(?:[^@/]+@)?([^:/]+)[:/]+', 'https://$1/' -replace '(?:\.git)?/?$'; $url = "$repo/compare/main...<branch>?quick_pull=1&title=$([uri]::EscapeDataString($title))&body=$([uri]::EscapeDataString($body))"; $url; Start-Process $url } }
```

The block runs only when the push succeeded, so a chain that stops earlier publishes nothing
and opens nothing. A missing `gh`, and a `gh pr create` that exits non-zero, both end on the
compare page; nothing is pushed twice. When the create was refused for the token, the release
is already published and the branch and the tag are pushed, so `gh pr create` alone is enough
once that token may write a pull request.

Command 2 waits for the Verify check of `.github/workflows/verify.yml` and then opens the
pull request, so a browser that opens is the signal that the check passed. It needs the pull
request to exist, by command 1 or by the compare page. If it reports no check yet, the run
has not registered; repeat it.

```powershell
gh pr checks <branch> --watch --fail-fast && gh pr view <branch> --web
```

Without `gh` there is no command 2: the developer watches the Verify check on the pull
request page and merges from there.

Developer steps after running them:

1. On the pull request that command 1 opened, merge with Squash and merge, keeping the
   commit title GitHub proposes: `<message> (#<n>)`, the pull-request title then its number.
2. Watch the release workflow and compare its five assets and checksums with the table in the
   notes. For failures, use Release workflow in `docs/tooling.md`.
3. Run the work-PC live checks with the published package.
4. Start the next branch from the updated `main`, once the pull request has closed: the
   squash leaves the commits of `<branch>`, and the tag `v<new>`, out of its history. Give
   this as one line in its own code block, with `<next>` left for the developer, who names
   the version after `<new>`. It fetches the merge, creates the branch without tracking and
   sets its upstream in one go, because a branch created from `origin/main` tracks `main`,
   and then a bare `git push` writes to `main` instead of the branch.

   ```powershell
   git fetch origin && git switch --create <next> --no-track origin/main && git push --set-upstream origin <next>
   ```
