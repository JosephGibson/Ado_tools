# AdoToolkit 0.9.0 release notes

Prepared on 2026-10-04 from the uncommitted changes on the branch `0.9.0`, which starts at
`main` after the pull request of 0.8.5 was squash-merged as `b39567e`; the files there are
those of 0.8.5. 0.9.0 changes how AdoToolkit is developed, not what it does. First, the
agent tooling lets several Claude Code sessions work at once without sharing one working
tree: Git worktrees under `.claude/worktrees/`, three Claude subagents, a saved workflow that
reviews the repository by area, and a `verify` that runs the product gate beside its other
stages. Second, the tests take less time: Core test classes run in parallel, the fr-CA run
leaves out the classes whose assertions the culture cannot change, the two Core runs and
packaging run side by side, and each Pester file, product or tooling, runs in a process of
its own beside the others. It changes no product code: nothing under `src/` differs from
0.8.5, and the module differs by its version number only. This is an offline review. No Azure DevOps Server connection or
`tests/Live/*.Live.ps1` run was attempted, so nothing below is confirmed live. The areas the
README lists as confirmed are still connections, projects, builds and test runs.

## What changed

0.9.0 adds no cmdlet, parameter or output property, and changes no configuration setting.
The configuration file format, the JSON report schema, the help in both cultures and the
seven-file package layout are unchanged. The [0.8.5 notes](release-0.8.5.md) still describe
the module.

### Worktrees

| Area | 0.9.0 behavior |
| --- | --- |
| Where | Claude Code creates a worktree in `.claude/worktrees/<name>/` for `claude --worktree`, the EnterWorktree tool or an agent with `isolation: worktree`. Each is a checkout of its own, so a build, a test run or a staging there leaves the `bin/`, `obj/` and `artifacts/` of the main checkout alone |
| Git | `.gitignore` ignores `.claude/worktrees/`, so `git add --all` in the main checkout does not stage a worktree |
| Discovery | `WorktreeDirectoryPaths` in `tools/dev.ps1` excludes `.claude/worktrees/` by its path from the root, in the ripgrep listing, in the filesystem walk and in `find`, so `inspect`, `find`, `powershell-lint` and `documentation` no longer read the copies of a worktree. ripgrep now runs from the root it searches with `.` as its search path, which anchors the exclusion at that root: inside a worktree, discovery lists the worktree's own files |
| Edit hook | `tools/validate-edit.ps1` checks a file edited in a worktree against that worktree, whether `CLAUDE_PROJECT_DIR` names the worktree or the main checkout (`Get-WorktreeRoot` in `tools/lib/discovery.ps1`) |
| Restore | A worktree starts without restored packages, so `project-check` exits `2` there until `dotnet restore AdoToolkit.slnx --locked-mode` runs in it. `.claude/settings.json` allows that command without a prompt, and `AGENTS.md` makes it routine inside a worktree only |
| Boundary | `AGENTS.md`: the agent may create a worktree, and never commits in, merges or removes one; the developer does |
| Plans | `docs/AGENTS.md` and `docs/tooling.md`: each phase of a plan in `docs/plans/` lists the files it touches and the phases it depends on, so phases with disjoint files can run as parallel worktree sessions |

### Subagents and the review workflow

| File | Change |
| --- | --- |
| `.claude/agents/area-reviewer.md` | New subagent: a read-only review of one of nine areas against the nested `AGENTS.md` rules of its paths, or an adversarial check of another review's findings. Each finding has a file and line, the rule or decision it breaks and a failure scenario |
| `.claude/agents/docs-sync.md` | New subagent, read-only: given a diff or a behavior change, the guides, the README and the help topics of both cultures that no longer match, `yaml` blocks that differ from the compiled cmdlet, missing `ms.date` updates and breaches of the French rules |
| `.claude/agents/fr-translator.md` | New subagent: writes the fr-CA prose of help topics and entries of `src/AdoToolkit.Core/Resources/Strings.fr.resx` from their English text, has no shell, and names the checks that prove its output for the caller to run |
| `tools/guard-readonly.ps1` | New hook, run before every shell command of `area-reviewer` and `docs-sync` from their front matter: it allows one plain `git diff` command, without the options that `tools/guard-git.ps1` refuses or `--no-index`, or one `tools/dev.ps1 find` command, and refuses chaining, pipes, redirection and substitution. Like `tools/guard-git.ps1`, it reads the command text and is not a sandbox |
| `.claude/workflows/repo-review.js` | New saved workflow, `repo-review`: a scope step for `changes`, one reviewer per area, a verifier per area as soon as its review returns, and in `fix` mode one fixer per area with confirmed findings and a final gate that runs `verify`. Arguments `scope` (`repo` or `changes`), `mode` (`report` or `fix`) and `base` (`origin/main`); at most 29 agents |
| `tooling-layout` stage | Also checks that every `.claude/agents/<name>.md` has a description and the name `<name>`, and that the hook scripts in a subagent's front matter live under `tools/`, as those of `.claude/settings.json` must. Its summary adds "n subagent(s) named after their files" |

Subagents and workflows are Claude-only; Codex reads `AGENTS.md` and `.agents/skills/` as
before.

### verify

| Area | 0.9.0 behavior |
| --- | --- |
| Order of work | `project-check`, the product gate, starts first in a runspace of its own, which loads `tools/dev.ps1`, and runs beside the in-process stages, which still run one after another in the `verify` process. A run lasts about as long as the longer of the two |
| Result | Unchanged in shape: the stages in the order of the plan, each with its own `DurationMs`. Since the stages overlap, their durations no longer add up to the run's |
| Failures | A failure or a timeout of `project-check` is reported the same while an in-process stage still runs. Stopping the runspace runs the cleanup of the bounded process, which stops the process tree of a stage still running; a stage that returned nothing fails with the reason the runspace stopped |
| Rule | `tools/AGENTS.md`: an in-process stage reads no build output and writes nothing to `bin/`, `obj/` or `artifacts/` of the repository |
| Duration | When the change was made, a full run took 176.8 s before it and about 107 s after it on the development PC. With the faster test runs below, the final runs of this release took about 48 s |

### Test runs

| Area | 0.9.0 behavior |
| --- | --- |
| Core tests in parallel | The test assembly no longer turns parallelization off, so xUnit runs test classes in parallel. A class that changes process-wide state (an environment variable, the default thread culture, the current directory, the console) joins `[Collection(ProcessEnvironment.Name)]`, which runs alone after the others; `ProcessStateTests` reads the test sources and fails on such a class outside the collection. A plain `TestDirectory` no longer points `ADOTOOLKIT_CONFIG_PATH` at its folder; `TestDirectory.WithConfigurationPath()` does, and only `ConfigurationStoreTests` calls it |
| fr-CA run | The gate runs every test under `en-US`, and under `fr-CA` it runs `--filter Culture!=Invariant`: 17 classes marked `[Trait("Culture", "Invariant")]`, 148 test cases, run once. `CultureSelectionTests` refuses the trait on a class in `Localization/`, `Reporting/` or `RichText/`, on one that reads the process culture, catalog text, French typography, a French culture or message text, and on one that uses a fixture that reads the process culture. `tests/AGENTS.md` states the rule, so a new class runs in both cultures |
| Product gate | `tools/check.ps1` builds once, then starts both Core runs as processes of their own (`Start-AdoGateProcess` in `tools/lib/test-results.ps1`), stages and inspects the package meanwhile, then runs product Pester. It reports build, Core en-US, Core fr-CA, package and product Pester in that order whichever ends first, and exits `1` when a step failed, else `2` when one was incomplete, so a later failure is never hidden behind an earlier incomplete step. A packaging failure stops the Core runs |
| Pester files | `Invoke-PesterProcess`, in the new `tools/lib/processes.ps1`, runs each test file in a pwsh process of its own, as many at once as there are logical processors and at most 600 s each, and merges their counts and failures. Nothing a process prints reaches the output. `powershell-test` uses it for `tools/tests/`, and the gate for `tests/AdoToolkit.PowerShell.Tests/` in place of the reporting child `Invoke-AdoReportingChild`, which is gone. `tests/AGENTS.md` and `tools/AGENTS.md`: each file may change `$env:` for itself and depends on no other |
| Hooks | `tools/guard-git.ps1` and `tools/guard-readonly.ps1`, dot-sourced, define their functions and stop, so their rules are tested in one process; `Get-GitGuardReason` gives the answer to one payload. Run by Claude Code, both behave as before |
| Waits | Tests that waited on purpose wait less, and timing margins that a parallel run could break are wider; see [Existing tests changed](#existing-tests-changed) |

### Documentation

| Area | Change |
| --- | --- |
| Instructions | `AGENTS.md`: the subagents and the workflow, and the worktree boundary. `tools/AGENTS.md`: the new hook, `tools/lib/processes.ps1`, ripgrep's working directory, `project-check` beside the in-process stages, the fixed order of the gate's lines, and tooling test files that run side by side. `tests/AGENTS.md`: how each suite runs, the `ProcessEnvironment` collection, product Pester files that run side by side, and a new section Cultures for the culture-invariant trait. `docs/AGENTS.md`: the row for `docs/plans/` |
| `docs/tooling.md` | The worktree exclusion, the stages running side by side, `powershell-test` with a process per file and its 600 s limit, the product gate's new order, the new layout rows, the hooks table with `tools/guard-readonly.ps1`, the restore rule, and new sections Subagents, Review workflow and Worktrees |
| README | The version, the paragraph on this version and the link to these notes |
| Guides and installer | `docs/guides/getting-started.md` and the examples of `tools/package/Install-AdoToolkit.ps1` name the 0.9.0 files; the installer is otherwise that of 0.8.5 |
| Release notes and plans | The 0.8.0 notes and the 0.8.5 plan moved to `docs/archive/`, unedited, as the 0.8.5 handoff asked. The archive indexes list them and resolve `V-34` to `V-36` in the new place; the 0.8.5 notes and the changelog link to it. `docs/plans/README.md` is new: it says what a plan holds and where a finished one goes, and keeps `docs/plans/`, which the instructions name, present in a clone while no plan is open |

### Tests

| File | Change |
| --- | --- |
| `tests/AdoToolkit.Core.Tests/Architecture/ProcessStateTests.cs` | New: every class that sets an environment variable, the default thread culture, the current directory or a console stream, or calls `TestDirectory.WithConfigurationPath()`, is in the `ProcessEnvironment` collection |
| `tests/AdoToolkit.Core.Tests/Architecture/CultureSelectionTests.cs` | New: a culture-invariant class lies outside the culture folders, reads no culture or message text, and uses no fixture that reads the process culture |
| `tests/AdoToolkit.Core.Tests/Support/ProcessEnvironment.cs`, `tests/AdoToolkit.Core.Tests/Support/TestSources.cs` | New support: the collection that runs alone, and the test sources that the two architecture tests read |
| `tools/tests/Dev.Tests.ps1` | Split by subject, so that the parts run side by side. It keeps discovery, `find`, `deps`, `diagnose`, `bootstrap` and `context`, with a new test: discovery neither lists nor searches the files of a worktree, with ripgrep and without, while inside the worktree it sees them |
| `tools/tests/Stages.Tests.ps1` | New file, the plan and the stages from `tools/tests/Dev.Tests.ps1`, with three new tests: each test file runs in a process of its own and their results merge, with nothing printed; a test process that runs past its limit is stopped and reported as timed out; analyzer findings are reported by file and line in the order of the files |
| `tools/tests/Verify.Tests.ps1` | New file, the `verify` results from `tools/tests/Dev.Tests.ps1`, with two new tests: the product gate failing while an in-process stage still runs is reported; a stage that times out while an in-process stage runs has its process tree stopped |
| `tools/tests/Invocation.Tests.ps1` | New file: `tools/dev.ps1` called with `&` runs its in-process stages, apart from the rest because its child `verify` is one of the longest tests |
| `tools/tests/Hooks.Tests.ps1` | New file for the three hooks. `tools/guard-git.ps1` and `tools/guard-readonly.ps1`: the commands each blocks and allows, its answer to each payload, and one call of each as Claude Code makes it. `tools/validate-edit.ps1`: the edit-hook tests from `tools/tests/Dev.Tests.ps1`, and a new one: a file edited in a worktree is checked against it, with `CLAUDE_PROJECT_DIR` naming either checkout |
| `tools/tests/Gate.Tests.ps1` | New file, `tools/check.ps1` with stubbed steps: the steps report in a fixed order whichever ends first and the fr-CA run is filtered; a failing Core run, a failure after an incomplete step, an incomplete step alone and a failing product Pester test each give the right exit code |
| `tools/tests/Check.Tests.ps1` | `Start-AdoGateProcess` gives a step its own environment and literal arguments and returns its exit code and lines, and `Stop-AdoGateProcess` stops a step that still runs |
| `tools/tests/Documentation.Tests.ps1` | New `Subagent layout`: a subagent named after its file, with a description and a hook helper under `tools/`, passes; a name that differs from the file, a missing description and a hook helper outside `tools/` are each reported |

### Visible changes for 0.8.5 users

- The module is the 0.8.5 module with another version number. Existing configuration files
  need no change, and no script that worked with 0.8.5 needs one.
- Public contract: unchanged. No cmdlet, parameter, output type, configuration setting or
  schema differs.
- The file names of the assets carry 0.9.0. Installation is as before, with the installer
  that ships with the zip.
- For developers: Claude Code sessions can work in worktrees under `.claude/worktrees/`,
  `verify` runs `project-check` beside the other stages, and the subagents and the
  `repo-review` workflow are available. A Claude Code session notices `.claude/agents/` only
  when that folder existed at its start, so restart open sessions after pulling 0.9.0.
- For developers: Core test classes run in parallel, so a new class that changes process-wide
  state joins `[Collection(ProcessEnvironment.Name)]`. A new class runs in both cultures
  unless it is marked culture-invariant under the rules of `tests/AGENTS.md`. A Pester file,
  product or tooling, runs in a process of its own and must not depend on another file.

## Bug fixes

None. 0.9.0 corrects no defect of the module or of the tooling.

### Existing tests changed

| Test | Change |
| --- | --- |
Core tests:

| Test | Change |
| --- | --- |
| 17 classes in `Architecture/`, `Connections/`, `Http/` and `TestRuns/` | Marked `[Trait("Culture", "Invariant")]`, their bodies unchanged: they run under `en-US` only |
| `ConfigurationStoreTests` | Joins the `ProcessEnvironment` collection, and its seven tests that write the configuration file use `TestDirectory.WithConfigurationPath()`. The store without a path reads `ADOTOOLKIT_CONFIG_PATH`, which the whole process shares |
| `CancellationTests.DownloadInactivityBudgetResetsAfterEachRead` | Reads of 100 ms against an inactivity window of 1 s, for reads of 20 ms against 200 ms: the same proof, twelve reads longer than one window, with room for a busy machine |
| `RequestGateTests.TimeSpentWaitingForASlotIsNotRequestTimeAndAFailedRequestReleasesItsSlot` | A request timeout of 500 ms and replies after 50 ms, for 1 s and 200 ms: the same proportions in less time |
| `ParallelDownloadTests.BodiesBeingReadNeverExceedTheBoundAndReachIt` | At a bound of one, 3 files instead of 20: one at a time is shown by three. Bounds three and six still read 20 files |
| `RetrievalConcurrencyTests` | At a bound of one, the requests, the set and the report do not depend on the delays, so the bound-one side of `SetAndRenderedReportAreIdenticalAtABoundOfOneAndOfEightUnderRandomDelays` keeps its random delays for seed 1 only, and that of `HistoryBudgetCountsOnlyHistoryRequestsAndGivesTheSameBuildsAtEveryBound` runs without them |
| `SinkEncodingTests.FrameworkEncoderEscapesTheSpacesThatSinkEncodingKeepsLiteral`, deleted with its file | It asserted what the framework's `HtmlEncoder` does, not what AdoToolkit does. `FrenchTypographyTests.RenderedFrenchContentPreservesSpacesQuotesAndDecomposedAccents` asserts that the rendered reports keep U+00A0 and U+202F literal |
| `CompactReportTests.NoAttemptGroupOrPreviewStartsOpen`, deleted | The failed-test goldens of each variant, in both cultures, hold the markup it searched, so an `open` attribute changes a golden; the large-report test of the same class still asserts that no `details` element starts open |

Product Pester tests:

| Test | Change |
| --- | --- |
| `tests/AdoToolkit.PowerShell.Tests/SyntheticServer.Pester.ps1`: "stops counting a request whose client closed its connection, and keeps serving" | The first request now blocks until its client leaves, and the second goes to a route that answers at once, for a latency of 1.5 s on both |
| `tests/AdoToolkit.PowerShell.Tests/SyntheticServer.Pester.ps1`: "writes a response no faster than -BytesPerSecond" | 3,999 bytes at 10,240 bytes a second, more than 250 ms, for 40,000 bytes at 40,000 a second, more than 900 ms: the last chunk is still due after the first |
| `tests/AdoToolkit.PowerShell.Tests/InputGuard.Pester.ps1`: "complete hand-made objects are still accepted", deleted | `tests/AdoToolkit.PowerShell.Tests/BulkTestCase.Pester.ps1` pipes complete hand-made `AdoTestSuite`, `AdoWorkItem` and `AdoWiqlResult` objects of the connected collection and checks what they bind and request |

Tooling tests:

| Test | Change |
| --- | --- |
| "runs the built-in tooling stages before the product gate" | Moved to `tools/tests/Stages.Tests.ps1` and renamed "plans the built-in tooling stages before the product gate", its body unchanged: it reads the plan's order, and the stages no longer run in that order |
| "keeps configured Claude hook helpers under tools and every skill paired" | Moved to `tools/tests/Stages.Tests.ps1`. It reads the repository itself, so it now counts three hook helpers, with `tools/guard-readonly.ps1`, and three subagents named after their files |
| "runs its in-process stages when called, with product check exit `<CheckExit>`" | Moved to `tools/tests/Invocation.Tests.ps1` with the passing product check only; `tools/tests/Verify.Tests.ps1` already reads a check that exits `2` as incomplete |
| "blocks mutating Git commands in the Claude Code guard", "blocks Git commands that hide behind a prefix, a wrapper, or a shell" and "blocks Git execution and output overrides after a read subcommand" | Merged into one test of `tools/tests/Hooks.Tests.ps1` that holds all their commands and calls `Test-IsBlockedGitCommand` in its own process; one more test runs the hook as Claude Code does |
| The three tests of `Invoke-AdoReportingChild` in `tools/tests/Check.Tests.ps1` | Deleted with the function. The tests of `Start-AdoGateProcess`, `Stop-AdoGateProcess` and `Invoke-PesterProcess` cover what replaced it |
| The three tests of `Get-PesterOutcome` in `tools/tests/Workflow.Tests.ps1` | Mock `Invoke-PesterProcess`, whose results carry messages instead of error records |

No fixture or golden was added, changed or removed.

### Findings not fixed

| Finding | Reason |
| --- | --- |
| Tag a bug opened in this run as New, in a lighter red, as the developer asked in the last design pass of 0.8.5 | Still open: it needs each bug's creation date (`System.CreatedDate`), a new property of `AdoTestBug`, which is public output, and a live check on Server 2020. 0.9.0 changes no product code |
| The restore rule of `.claude/settings.json` cannot tell a worktree from the main checkout | A permission rule matches the command text only. `AGENTS.md` limits the routine use to a worktree, and the restore runs in locked mode against the lock files |
| Two `verify` runs in the same checkout still collide on `bin/`, `obj/` and `artifacts/` | By design: each session that builds works in a worktree of its own. `verify` does not lock the tree |
| The findings of 0.8.5, 0.8.0, 0.7.10, 0.7.5, 0.7.0, 0.6.5 and 0.6.0 | Unchanged; see the [0.8.5](release-0.8.5.md#findings-not-fixed), [0.8.0](archive/release-0.8.0.md#findings-not-fixed), [0.7.10](archive/release-0.7.10.md#findings-not-fixed), [0.7.5](archive/release-0.7.5.md#findings-not-fixed), [0.7.0](archive/release-0.7.0.md#findings-not-fixed), [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes |

### Known limitations

| Limitation | Notes |
| --- | --- |
| Hooks are not a sandbox | `tools/guard-readonly.ps1` and `tools/guard-git.ps1` read command text; Codex runs neither. The boundaries of `AGENTS.md` hold without them |
| Path-scoped rules in subagents | Subagents load `CLAUDE.md`, and through it `AGENTS.md`. Whether they load `.claude/rules/*.md` is not documented, so each definition names the nested `AGENTS.md` files its agent reads first |
| A new `.claude/agents/` folder | Claude Code watches it only when it existed at session start; a session started before needs a restart |
| A worktree builds on its own | It needs its own restore before `project-check` passes, and its files are not those of the main checkout until the developer merges its branch |
| The two architecture tests read source text | `ProcessStateTests` and `CultureSelectionTests` find a change of process state, or a read of the culture, by the patterns they search. A change made through a helper in another file, other than the fixtures that `CultureSelectionTests` follows, is not seen |
| Limits carried over | The [0.8.5](release-0.8.5.md#known-limitations), [0.8.0](archive/release-0.8.0.md#known-limitations), [0.7.10](archive/release-0.7.10.md#known-limitations), [0.7.5](archive/release-0.7.5.md#known-limitations) and [0.7.0](archive/release-0.7.0.md#known-limitations) known limitations still apply, with V-27, V-28, V-33 to V-36, V-31, V-32 and V-02 |

## Work-PC Live checks

Install the 0.9.0 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass. No check gates 0.9.0, whose module code is that of 0.8.5.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | Record `Get-FileHash` of `%APPDATA%\AdoToolkit\config.json`, install 0.9.0, then run `Get-AdoProfile` and `Connect-Ado` | The hash is unchanged, no warning appears, and `Get-Module AdoToolkit -ListAvailable` shows 0.9.0 with seven files in its folder |
| 2 | Checks 2 to 4 of the [0.8.5 notes](release-0.8.5.md#work-pc-live-checks), with the 0.9.0 package | As listed there: the restyled failed-test report in English and French, then the 0.8.0 checks with V-33, `-SkipAttachments`, the probes of V-28 and V-34 to V-36, and the 0.7.5 checks with V-31, V-32, V-02, V-01 and V-03. All still pending |

## Local validation and developer handoff

Run on 2026-10-04 on the development PC, on the tree that holds these notes. Three other
Claude Code sessions were open in this checkout, idle, during the runs.

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0 in about 48 s. All seven stages pass without a warning: 70 PowerShell files linted, 310 tooling Pester tests, 147 configuration files, 86 Markdown files with 243 links and 288 path references, 3 hook helpers, 4 skills and 3 subagents in the layout, 2 workflow files linted with actionlint, and the product check, which staged and inspected AdoToolkit 0.9.0 |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as both workflows run it | Exit 0 in 47 s with the exact module pins in `tools/BuildModules.psd1`. The variable was unset before and was unset again afterwards |
| Core tests in the final gate | 1552 passed under en-US and 1404 under fr-CA, with no failures or skips. 0.8.5 ran 1552 in each culture: the fr-CA run leaves out the 148 culture-invariant cases, and en-US has two new architecture tests for the two deleted ones |
| Product Pester tests against the staged module | 178 passed, none skipped: 179 in 0.8.5, less the deleted one. The tooling tests are 310, for 292 |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `0.9.0` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.9.0` with seven files, and 0 warnings |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.9.0-win-x64.zip -ModuleVersion 0.9.0 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.9.0, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.9.0/`. The portable ZIP has the same seven files under `module/`, the launcher, `README.txt` and `bundle.json` (module 0.9.0, PowerShell 7.6.6, win-x64, PowerShell SHA-256 equal to the published hash and to the pin of the setup action), 669 files in all. Each `.sha256` file holds the hash of its ZIP. The installer is byte-identical to its source |

No Live script ran. Not run: the installer against the 0.9.0 ZIP, the workflows, which only
GitHub runs, a `repo-review` run, and the squash merge, which happens on GitHub after these
notes.

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.9.0-win-x64.zip` | 110420713 | `b7e4212342c8be52f6d01307c80356fb482237d5e19644b09cf83332c264a31d` |
| `AdoToolkit-0.9.0.zip` | 813799 | `8877cd966b9603f3ac85f8ab024167329f92758e58bb65f0e0c017c93a995c37` |
| `Install-AdoToolkit.ps1` | 13032 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes. No tag or GitHub release exists for 0.9.0. Every
change is uncommitted on the branch `0.9.0`, which tracks `origin/0.9.0`. The developer owns
publication:

1. Run the first command of the handoff. It commits every change, creates the tag `v0.9.0`
   and pushes the branch and the tag together, which publishes the release at once.
2. Run the second command and create the pull request on the page it opens, keeping its
   title.
3. When the Verify check passes, merge with Squash and merge and keep the commit title that
   GitHub proposes: the title of the pull request, then its number.
4. Watch the release workflow and check its five uploaded assets, version and checksums. CI
   builds its own assets, so compare each checksum with CI's own `.sha256` files; archive
   metadata can differ from a local build. If the run does not publish, `docs/tooling.md`
   lists the recovery for each cause.
5. Run the work-PC checks above with the published package and record any inconclusive
   coverage.
6. Start the next branch from the updated `main`, and restart any open Claude Code session
   there so that it finds `.claude/agents/`.
