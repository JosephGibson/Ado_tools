# AdoToolkit 0.9.10 release notes

Prepared on 2026-10-04 from the uncommitted changes on the branch `0.9.10`, which starts at
`main` after the pull request of 0.9.5 was squash-merged as `c23264a`; the files there are
those of 0.9.5. 0.9.10 changes how AdoToolkit is developed, not what it does. A plan now
reaches the developer with a critique from another model family: the new `critique-plan`
skill sends it to an OpenAI model through the Codex CLI, triages each finding against the
decisions, the goldens and the public contract of this repository, and records every verdict
in the plan as applied, rejected or owed, while `tools/guard-plan.ps1` blocks a plan that
records none. The Git boundary gains `rev-parse`, the one read the critic needs to resolve
the repository root. `diagnose` lists the GitHub CLI as recommended where the remote is
GitHub, and `bootstrap -Install` installs it, because the release handoff now opens its own
pull request: its first command commits, tags, pushes and runs `gh pr create` with the final
title, and its second waits for the Verify check before it opens the page. The `release`
skill also states how a bug fix proves that its test failed before it, and what the final
validation must record. 0.9.10 changes no product code: nothing under `src/` or `tests/`
differs from 0.9.5, and the module differs by its version number only. This is an offline
review. No Azure DevOps Server connection or `tests/Live/*.Live.ps1` run was attempted, so
nothing below is confirmed live. The areas the README lists as confirmed are still
connections, projects, builds and test runs.

## What changed

0.9.10 adds no cmdlet, parameter or output property, and changes no configuration setting.
The configuration file format, the JSON report schema, the help in both cultures and the
seven-file package layout are unchanged. The [0.9.5 notes](release-0.9.5.md) describe the
last product changes, and the [0.8.5 notes](archive/release-0.8.5.md) describe the module.

### Plan critique

| Area | 0.9.10 behavior |
| --- | --- |
| Skill | `.agents/skills/critique-plan/SKILL.md` is new, with its `.claude/` wrapper: the final step of planning. It names the artifact, the command, the triage and the record. The critic is the developer's personal `critique` skill, which runs an OpenAI model through `codex exec`, so the second opinion comes from another model family; the session never writes the critique itself, not even when the command fails |
| Artifact | A saved plan is the file under `docs/plans/`; a plan held in the session, a plan mode plan included, goes in through a quoted heredoc. The critic reads one plan at a time, and over 10,000 characters it stops with exit `2` until the developer confirms the size |
| Command | `bash ~/.claude/skills/critique/critique --raw --repo --out "$OUT" <plan>`, run from the repository root, in the background, with the raw critique written to the session scratchpad and never into the repository. `--repo` lets the critic read this tree read-only, which is what grounds a finding in `path:line` |
| Triage | A recorded decision (`§n`, `DD-0nn`, `Q-nn`, `V-nn`, `S0-n`, `Fnn`), a pinned golden under `tests/Fixtures/Reports/` or the public contract names what a finding would change; none of them refutes it, because the contract changes when the change is the fix and a golden records bytes, not correctness. A finding is rejected only on evidence that the behavior it questions is still intended, and a finding about unconfirmed server behavior is owed to a `V-nn` check in `tests/Live/`, which only the developer runs |
| Record | The plan carries a `## Critique` section: the critic and the date, then one line per finding with `applied` and the change made, `rejected` and the evidence, or `owed` and the check that would settle it. When the critic cannot run, for a missing `codex`, a usage limit or a Codex session, where the critic would be the same model family as the author, the plan records `Critique: skipped` with the reason, and the pass stays owed |
| Hooks | `tools/guard-plan.ps1` is new, and `.claude/settings.json` registers it twice. Before an `ExitPlanMode` call it blocks a plan whose text records no pass and returns the reason to Claude on exit `2`; after a `Write` of a plan under `docs/plans/`, other than its `README.md`, it returns a reminder as context without blocking. A record is a `Critique` heading, or a line that opens with the word, matched at the start of a line so that a plan discussing a critique in prose does not pass for one. The hook reads the payload of the call and nothing on disk, and it checks the record, not the run; another tool, an empty plan and an unreadable payload are left to Claude Code. Dot-sourced, it defines its functions and stops, as the other hooks do |
| Rules | `AGENTS.md`: a plan is critiqued before it is presented or saved, and it records which findings the pass applied, rejected and left owed. `docs/AGENTS.md` and `docs/plans/README.md`: a plan carries the section, and one presented without it is unfinished |

### The Git boundary

`git rev-parse` is a read, so `tools/guard-git.ps1` accepts it beside `status`, `diff`,
`log`, `show` and `blame`, and `AGENTS.md` lists it with the reason: `critique --repo`
resolves the repository root with `git rev-parse --show-toplevel`. The option rules are
unchanged, so `git rev-parse --output=root.txt --show-toplevel` is blocked, as is any
`rev-parse` behind a prefix, a wrapper or a shell. The refusal now names the allowed
subcommands from the list the guard tests against, instead of repeating five of them in its
text, so the message cannot fall behind the rule again.

### GitHub CLI

| Area | 0.9.10 behavior |
| --- | --- |
| `diagnose` | Lists `gh` as recommended in a repository that has workflow files, as it lists actionlint. It gates no stage: `verify` passes without `gh`, and the `release` skill then hands over the compare page instead of `gh pr create` |
| `bootstrap -Install` | Installs `GitHub.cli` with winget, beside ripgrep and actionlint. winget extends `PATH` for new processes only, so `gh` reads as missing until a new terminal opens, and `gh auth login` stays the developer's step |

### The release procedure

| Area | Change |
| --- | --- |
| Pre-fix evidence | A new section 3: a Bug fixes row proves that its regression test fails without its fix by restoring the released source of one file, from `git show <base>:<path>`, with the working copy saved outside the repository. The files of one fix are reverted together, because a partial revert may not compile. Afterwards the file is copied back and its `LastWriteTime` set to now, because `Copy-Item` restores the old timestamp and MSBuild would then skip the rebuild and test the released assembly in every later run, and `git diff --stat` confirms the restore. The section names how to run one test of each suite, and requires that a fix whose failure cannot be reproduced is reported as such |
| Validation | The table adds the portable check, the hash of each zip against its own `.sha256` file, the installer with `-WhatIf`, the archive entries and `diagnose`. Writing the validation section is itself an edit, so the gate runs, the section is written from that run, and the gate runs again; the counts in the notes are those of the last run. A test that fails once and passes on a repeat of the same command is recorded as a flake, with a row under Findings not fixed; a failure that repeats stops the release |
| Handoff | Command 1 ends with `gh pr create --base main --head <branch> --title '<message>'`, so the pull request opens with its final title and nothing about it is typed by hand; if only that step fails, the release is already published and the branch and the tag are pushed, so the `gh pr create` part runs alone. Command 2 is `gh pr checks <branch> --watch --fail-fast && gh pr view <branch> --web`, so a browser that opens is the signal that the Verify check passed. Without `gh`, command 2 is the prefilled compare page, as it was in 0.9.5 |
| Archiving | Section 1 names the three steps for the notes of the previous version, says that Markdown under `docs/archive/` is frozen so that only live documents need repointing, and keeps a `V-nn` row of the archive index resolving to the new place |

### Documentation

| Area | Change |
| --- | --- |
| `AGENTS.md` | `critique-plan` in the list of skills, the rule that a plan is critiqued before it is presented or saved, and `rev-parse` in the Git boundary with its reason |
| `docs/AGENTS.md`, `docs/plans/README.md` | A plan carries the `## Critique` section, with a verdict for each finding; a plan presented without one is unfinished |
| `tools/AGENTS.md` | The row for `tools/guard-plan.ps1` |
| `docs/tooling.md` | A new Plan critique section with the artifact, the command, the triage and the record; the row of the hooks table; the Codex CLI and `gh` in the prerequisites, neither needed by a stage; `bootstrap -Install`; the two handoff commands of the `release` skill; `critique-plan` among the skills that may be selected automatically; and `tools/guard-plan.ps1` named with the hooks that define their functions when dot-sourced |
| README | The version, the paragraph on this version and the link to these notes |
| Guides and installer | `docs/guides/getting-started.md` and the examples of `tools/package/Install-AdoToolkit.ps1` name the 0.9.10 files; the installer is otherwise that of 0.9.5 |
| Archive | The 0.9.0 notes moved to `docs/archive/release-0.9.0.md`, unedited. The archive index lists them, and the changelog and the 0.9.5 notes point at the new place. The 0.9.0 notes define no `V-nn` item, so no citation moved |

### Tests

| File | Change |
| --- | --- |
| `tools/tests/Hooks.Tests.ps1` | New `Describe 'tools/guard-plan.ps1'`, four tests. Three plans that record no pass are blocked, among them two that discuss a critique in prose, and three that record one, among them `Critique: skipped`, are let through. Another tool, an empty plan and an unreadable payload are left to Claude Code. A plan file written without the record is reminded, by its relative and by its absolute path, while a recorded section, `docs/plans/README.md`, another document, a source file and an `Edit` are silent. The last test runs the hook as Claude Code does: exit `2` with the reason for a plan, and exit `0` with the `PostToolUse` context for a write |
| `tools/tests/Dev.Tests.ps1` | New test: `gh` is listed, as recommended, in a repository that has a workflow file, and is absent from the diagnostics of one that has none |

### Visible changes for 0.9.5 users

- The module is the 0.9.5 module with another version number. Existing configuration files
  need no change, and no script that worked with 0.9.5 needs one.
- Public contract: unchanged. No cmdlet, parameter, output type, configuration setting or
  schema differs.
- The file names of the assets carry 0.9.10. Installation is as before, with the installer
  that ships with the zip.
- For developers: a plan is critiqued before it is presented or saved, and records a verdict
  for each finding. `tools/guard-plan.ps1` blocks an `ExitPlanMode` call whose plan records
  no pass. Claude Code reads `.claude/settings.json` at session start, so restart the
  sessions open in a checkout after pulling 0.9.10, and the pass needs the developer's
  `critique` skill with `codex` and `jq` on `PATH`.
- For developers: `git rev-parse` is allowed by the Git boundary and by its hook. Every Git
  write stays with the developer, and the forbidden options are refused as before.
- For developers: `diagnose` lists `gh` as recommended where workflow files exist, and
  `bootstrap -Install` installs it. It gates no stage, and `verify` passes without it.
- For developers: the release handoff opens its own pull request with `gh pr create` and
  waits for the Verify check with `gh pr checks --watch --fail-fast`; the prefilled compare
  page remains the path without `gh`.

## Bug fixes

None. 0.9.10 corrects no defect of the module or of the tooling, so no pre-fix evidence was
produced: section 3 of the `release` skill, which this release adds, applies to a release
that fixes one. Two texts of this release's own change were corrected while these notes were
written: the refusal of `tools/guard-git.ps1`, which still named five allowed subcommands
after `rev-parse` joined them, and the sentence of `docs/tooling.md` that lists the hooks
which define their functions when dot-sourced, which left out `tools/guard-plan.ps1`.

### Existing tests changed

| Test | Change |
| --- | --- |
| `tools/tests/Hooks.Tests.ps1`, the Git guard cases | `git rev-parse --abbrev-ref HEAD` moved from the blocked commands to the allowed ones, as `root=$(git rev-parse --show-toplevel)`, the form the critique uses, and `git rev-parse --output=root.txt --show-toplevel` joined the blocked ones. The two tests themselves are unchanged |
| `tools/tests/Stages.Tests.ps1`, "keeps configured Claude hook helpers under tools and every skill paired" | The summary it reads from this repository is now 4 hook helpers, with `tools/guard-plan.ps1`, and 5 skills paired between `.agents` and `.claude`, with `critique-plan`. The three subagents are unchanged |

No test was deleted, no product test changed, and no fixture or golden was added, changed or
removed.

### Findings not fixed

| Finding | Reason |
| --- | --- |
| The plan guard reads the record, not the run, so a plan could claim a pass that never happened | By design, as `tools/guard-git.ps1` is: the hook reads the text of the call and is a speed bump, not a sandbox. Tying a plan to the raw critique in the session scratchpad is not something the hook can check from the call it is given |
| An `Edit` that revises a plan under `docs/plans/` is not inspected | Only a `Write` carries the whole file, so the reminder arrives once, when the plan appears. The `ExitPlanMode` gate reads the final text, and a saved plan is the developer's to check |
| The `critique` skill itself is not in this repository | It is the developer's personal skill under `~/.claude/skills/critique/`, shared by every project; `docs/tooling.md` names what it needs and where it writes. A copy here would be a second version to keep in step |
| Under Codex the critique is skipped | The critic would be the same model family as the author of the plan, which is no second opinion. The skill records the skip with its reason, and the pass stays owed to a Claude session |
| Nothing checks that a plan under `docs/plans/` carries its `## Critique` section | `verify` reads links, anchors and paths in Markdown, not the shape of a plan, and a plan is a working document until the developer archives it. The hook, the skill and the rules in `AGENTS.md` and `docs/plans/README.md` carry the requirement |
| Tag a bug opened in this run as New, in a lighter red, as the developer asked in the last design pass of 0.8.5 | Still open: it needs each bug's creation date (`System.CreatedDate`), a new property of `AdoTestBug`, which is public output, and a live check on Server 2020. 0.9.10 changes no product code |
| The findings of 0.9.5, 0.9.0, 0.8.5, 0.8.0, 0.7.10, 0.7.5, 0.7.0, 0.6.5 and 0.6.0 | Unchanged; see the [0.9.5](release-0.9.5.md#findings-not-fixed), [0.9.0](archive/release-0.9.0.md#findings-not-fixed), [0.8.5](archive/release-0.8.5.md#findings-not-fixed), [0.8.0](archive/release-0.8.0.md#findings-not-fixed), [0.7.10](archive/release-0.7.10.md#findings-not-fixed), [0.7.5](archive/release-0.7.5.md#findings-not-fixed), [0.7.0](archive/release-0.7.0.md#findings-not-fixed), [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes |

### Known limitations

| Limitation | Notes |
| --- | --- |
| Hooks are not a sandbox | `tools/guard-plan.ps1`, `tools/guard-git.ps1` and `tools/guard-readonly.ps1` read the text of a call, and Codex runs none of them. The boundaries of `AGENTS.md` hold without them |
| A hook change needs a new session | Claude Code reads `.claude/settings.json` at session start, so the plan guard takes effect in the next session, as a new file in `.claude/agents/` does |
| The critique needs the Codex CLI | `codex` and `jq` on `PATH` and the developer's ChatGPT plan, with a plan of at most 10,000 characters unless the developer confirms the size. No stage needs them, and `verify` never runs the critic |
| `gh` is recommended, not required | A release prepared without it hands over the prefilled compare page, and the developer creates the pull request there and watches the check on it |
| Limits carried over | The [0.9.5](release-0.9.5.md#known-limitations), [0.9.0](archive/release-0.9.0.md#known-limitations), [0.8.5](archive/release-0.8.5.md#known-limitations), [0.8.0](archive/release-0.8.0.md#known-limitations), [0.7.10](archive/release-0.7.10.md#known-limitations), [0.7.5](archive/release-0.7.5.md#known-limitations) and [0.7.0](archive/release-0.7.0.md#known-limitations) known limitations still apply, with V-27, V-28, V-33 to V-36, V-31, V-32 and V-02 |

## Work-PC Live checks

Install the 0.9.10 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass. No check gates 0.9.10, whose module code is that of 0.9.5.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | Record `Get-FileHash` of `%APPDATA%\AdoToolkit\config.json`, install 0.9.10, then run `Get-AdoProfile` and `Connect-Ado` | The hash is unchanged, no warning appears, and `Get-Module AdoToolkit -ListAvailable` shows 0.9.10 with seven files in its folder |
| 2 | Checks 2 to 4 of the [0.9.5 notes](release-0.9.5.md#work-pc-live-checks), with the 0.9.10 package | As listed there: the one `S0-9` verdict of `tests/Live/Connection.Live.ps1` after its signature note, the Error column and the chart captions of a report whose text is cut, and then the earlier checks with V-33, `-SkipAttachments`, the probes of V-28 and V-34 to V-36, and V-31, V-32, V-02, V-01 and V-03. All still pending |

## Local validation and developer handoff

Run on 2026-10-04 on the development PC, on the tree that holds these notes.

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0 in about 46 s, the product gate running beside the in-process stages. All seven stages pass without a warning: 71 PowerShell files linted, 322 tooling Pester tests, 147 configuration files, 88 Markdown files with 275 links and 308 path references, 4 hook helpers, 5 skills and 3 subagents in the layout, 2 workflow files linted with actionlint, and the product check, which staged and inspected AdoToolkit 0.9.10 |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as both workflows run it | Exit 0 with the exact module pins in `tools/BuildModules.psd1`, and the same counts. The variable was set for that process only and was never set in the shell |
| Core tests in the final gate | 1555 passed under en-US and 1406 under fr-CA, with no failures or skips: the counts of 0.9.5, since no product test changed |
| Product Pester tests against the staged module | 180 passed, none skipped, as in 0.9.5. The tooling tests are 322, for 317 in 0.9.5: four tests of the plan guard and one of `gh` |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `0.9.10` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.9.10` with seven files, and 0 warnings |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.9.10-win-x64.zip -ModuleVersion 0.9.10 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.9.10, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.9.10/`. The portable ZIP has the same seven under `module/`, both launchers, `README.txt` and `bundle.json` (module 0.9.10, PowerShell 7.6.6, win-x64, PowerShell SHA-256 `02fe458be20493fbdf43f61ea20610b811ee6c738ab1676c61b9cfcd1a33c860`, equal to the published hash and to the pin of the setup action), 669 entries in all. Each `.sha256` file holds the hash of its ZIP, and the installer is byte-identical to its source |
| `tools/package/Install-AdoToolkit.ps1 -Path ./artifacts/release/AdoToolkit-0.9.10.zip -WhatIf` | Exit 0: the checksum and the layout are accepted and the target is named, `Documents\PowerShell\Modules\AdoToolkit\0.9.10`. Nothing was written, and no module was installed |
| `pwsh -NoProfile -File .\tools\dev.ps1 diagnose` | `ok`, with every required tool present and both recommended tools present: ripgrep 15.2.0 and `gh` 2.102.0. The handoff below therefore uses the `gh` commands |

No test failed in either gate run, so nothing is recorded as a flake. The concurrency-peak
test that failed once during the validation of 0.9.5 passed in both runs here.

The plan guard was proved by its own tests, which call it as Claude Code does, for the exit
code and the context it returns. Claude Code reads `.claude/settings.json` at session start,
so the hook of this release first runs in a session opened after it.

No Live script ran. Not run: an actual installation, the workflows, which only GitHub runs, a
`repo-review` run, a critique pass, since 0.9.10 adds the procedure and no plan was written
for it, and the squash merge, which happens on GitHub after these notes.

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.9.10-win-x64.zip` | 110421130 | `f99d2c4684f7ce0e70d643d43a74ab0afe4f55fa214cf40f62388bb06e5056e5` |
| `AdoToolkit-0.9.10.zip` | 814229 | `348065b67c9df066f4459b5985d447770e135b568cf5cce025542b1d481faeaf` |
| `Install-AdoToolkit.ps1` | 13035 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes. No tag or GitHub release exists for 0.9.10. Every
change is uncommitted on the branch `0.9.10`, which tracks `origin/main`, so its first push
creates `origin/0.9.10`. The developer owns publication:

1. Run the first command of the handoff. It commits every change, creates the tag `v0.9.10`
   and pushes the branch and the tag together, which publishes the release at once, and then
   opens the pull request with its final title.
2. Run the second command. It waits for the Verify check of the pull request and opens the
   page in a browser once that check passes.
3. Merge with Squash and merge and keep the commit title that GitHub proposes: the title of
   the pull request, then its number.
4. Watch the release workflow and check its five uploaded assets, version and checksums. CI
   builds its own assets, so compare each checksum with CI's own `.sha256` files; archive
   metadata can differ from a local build. If the run does not publish, `docs/tooling.md`
   lists the recovery for each cause.
5. Run the work-PC checks above with the published package and record any inconclusive
   coverage.
6. Start the next branch from the updated `main`, and restart any Claude Code session open in
   a checkout so that it loads the plan guard.
