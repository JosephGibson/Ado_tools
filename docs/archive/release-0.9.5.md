# AdoToolkit 0.9.5 release notes

Prepared on 2026-10-04 from the uncommitted changes on the branch `0.9.5`, which starts at
`main` after the pull request of 0.9.0 was squash-merged as `820b5e7`; the files there are
those of 0.9.0. 0.9.5 is a correction pass: every change answers a defect found by reading
the repository against the rules of its own `AGENTS.md` files, and each one carries a
regression test that fails without the fix. Two defects reach a user. A line of report text
cut at its length limit could split a surrogate pair, and the lone half reached the sink,
which encoded it as `&#xFFFD;`, so a cell showed the replacement character under a title
that held the whole text. A pipeline object built by hand without its numeric identifier
passed the input guard, reached Core as `0` and ended the pipeline with an untranslated
`ArgumentOutOfRangeException`; it is now reported for that object alone, in the message
culture, and the rest of the pipeline runs. The six others correct the developer tooling,
one live check and the documentation: the product gate kept every test-result folder it ever
wrote, a Pester run could be read as complete although a file discovered nothing or left a
test inconclusive, a malformed `hooks` entry hid itself and silenced the hook-helper check,
`verify -Stage ''` ran the whole gate, the package gate compared the file layout less
strictly than the installer does, and the live connection check printed two verdicts for one
check ID. Retrieval, the content of the reports, the cmdlet surface and the configuration
file are otherwise unchanged. This is an offline review. No Azure DevOps Server connection or
`tests/Live/*.Live.ps1` run was attempted, so nothing below is confirmed live. The areas the
README lists as confirmed are still connections, projects, builds and test runs.

## What changed

0.9.5 adds no cmdlet, parameter or output property, and changes no configuration setting.
The configuration file format, the JSON report schema, the help structure in both cultures
and the seven-file package layout are unchanged. The [0.9.0 notes](archive/release-0.9.0.md) describe
the agent tooling and the test runs; the [0.8.5 notes](archive/release-0.8.5.md) describe the
module.

### Reports

| Area | 0.9.5 behavior |
| --- | --- |
| Error summary | The one-line error that the failed-test report shows in its Error column, in that cell's `title` and in the console table of `AdoTestFailure` is cut at 240 characters without splitting a surrogate pair. An astral character that straddles the cut, an emoji for instance, is dropped whole instead of leaving its high half for the sink to encode as `&#xFFFD;`. `HtmlTestFailureRenderer.Shorten`, which every other cut of the document already used, moved out of the nested `Document` type so that `FirstLine` uses it too |
| Chart caption | The bar captions of the run-history chart are cut at 28 characters by the same rule. The bar's title still carries the whole build number, so the caption can no longer contradict it |
| Everything else | Unchanged. No golden report differs, and no report text, style or layout changed for any input that holds no astral character at a cut |

### Pipeline input

`AdoCmdletBase.EnsureIdentifier` is new, beside `EnsureComplete` and `EnsureInput`. The
`InputGuard` metadata describes reference-typed members only, so an absent integer stayed
invisible to `EnsureComplete`: it bound as `0` and failed inside Core. The guard reports it
with the existing `IncompleteInput` message, as a non-terminating `AdoRequest` error on the
object that carried it, and sends no request. 11 call sites in 9 cmdlets:

| Cmdlet | Input | Members checked |
| --- | --- | --- |
| `Export-AdoBuildTestFailure` | `AdoBuildTestFailureSet` | `Build.Id` |
| `Export-AdoTestCase` | `AdoTestCase` | `Id`, per case |
| `Get-AdoBuild` | `AdoBuildDefinition` | `Id` |
| `Get-AdoBuildFailure`, `Get-AdoBuildTestFailure`, `Get-AdoBuildTimeline`, `Get-AdoTestRun` | `AdoBuild` | `Id` |
| `Get-AdoTestCase` | `AdoWorkItem`, `AdoTestSuite` | `Id`; `PlanId` and `Id` |
| `Get-AdoTestSuite` | `AdoTestPlan` | `Id` |

An object that an AdoToolkit command returned always carries its identifier, so no working
pipeline changes.

### Tooling

| Area | 0.9.5 behavior |
| --- | --- |
| Gate test results | Each Core run reads its TRX counters and then gives its folder under `artifacts/verify/` back, in the gate's `finally`, so a failing or interrupted gate cleans up too. `$coreRuns` is declared before the `try` that fills it, which the `finally` needs under strict mode |
| Pester completeness | `Invoke-PesterProcess` sums `InconclusiveCount` and adds `Incomplete`, true when any single file discovered no test or ended with a test that neither passed nor failed. `Get-PesterOutcome` and the gate read that flag and compare `PassedCount + FailedCount` with `TotalCount`, in place of testing `SkippedCount` and `NotRunCount` in the aggregate. `powershell-test` and the product Pester step of the gate are therefore incomplete, exit `2`, when a file discovers no test or a test is skipped, not run or inconclusive |
| `tooling-layout` hooks | Every member of a `hooks` entry in `.claude/settings.json` is read through `PSObject.Properties`, as the rest of the stage does. A group without a `hooks` array is named; an entry that is not an object, and a command hook with a blank `command`, are named as before |
| `verify -Stage` | Every supplied name is validated against the plan, an empty name included. A selection that names no stage is refused as an unknown stage instead of running the whole gate and reporting a pass |
| Package layout | `Assert-AdoPackage` compares the seven file names case-sensitively, as `$layout` in `tools/package/Install-AdoToolkit.ps1` is compared, so a case-only difference fails in the gate instead of on a user's machine after the release is published |

### Live checks

`tests/Live/Connection.Live.ps1` gives `S0-9` one verdict, as `tests/Live/AGENTS.md`
requires. The signature of the installed release is an observation that precedes the verdict,
so it prints as `NOTE S0-9 SIGNATURE_VALID` or `NOTE S0-9 UNSIGNED_RELEASE_INSTALLED`; the
single verdict is `PASS S0-9 CONNECT_TEST_PROJECTS`, printed once the connection, the test
and `Get-AdoProject` have succeeded. A failure raised after that point no longer prints a
second `FAIL S0-9`. The `V-07`, `V-08`, `V-14` and `V-16` lines are unchanged.

### Documentation

| Area | Change |
| --- | --- |
| Help examples | The text under the examples of `Connect-Ado`, `Get-AdoProfile`, `Get-AdoProject` and `Set-AdoProfile` says what the example's code does, as `docs/commands/AGENTS.md` requires: the profile it names, the filter it applies, the limit it sets and the default it makes. Both cultures changed in the same edit, and each file's `ms.date` is `10-04-2026` |
| Guides | The Full help list of `docs/guides/getting-started.md`, `docs/guides/build-report.md` and `docs/guides/pipeline-triage.md` names every cmdlet the guide uses, and `docs/guides/configuration.md` adds the cmdlets that read the defaults and limits it documents |
| `docs/tooling.md` | The tooling changes above: `powershell-test` incompleteness, the gate removing each TRX folder, product Pester completeness read per file, the empty stage name and the case-sensitive layout comparison |
| 0.9.0 notes | An empty table header stood above Existing tests changed, left over from the heading of the table that follows it. Removed |
| Archive | The 0.8.5 notes moved to `docs/archive/release-0.8.5.md`, unedited, as this release procedure requires; `docs/archive/README.md` lists them, and the changelog and the 0.9.0 notes point at the new place |
| README | The version, the paragraph on this version and the link to these notes |

### Tests

| File | Change |
| --- | --- |
| `tests/AdoToolkit.Core.Tests/Architecture/FixtureCatalogTests.cs` | New `EveryReportGoldenIsComparedByAGoldenTheory`: the names the three golden theories expect are exactly the 52 files in `tests/Fixtures/Reports/`. The catalog test beside it keeps a row and a file in step, so without this one a golden dropped from a theory would stay cataloged, stay reviewed and never be compared again, which `tests/AGENTS.md` forbids. It passes on this tree: no golden is orphaned |
| `tests/AdoToolkit.Core.Tests/Reporting/Charts/RunHistoryChartTests.cs` | New `BarCaptionCutAtItsLimitKeepsTheAstralCharacterWhole` |
| `tests/AdoToolkit.Core.Tests/Reporting/TestFailures/ErrorTextTests.cs` | New `SummaryCutAtItsLimitKeepsTheAstralCharacterWhole` |
| `tests/AdoToolkit.PowerShell.Tests/InputGuard.Pester.ps1` | New case, two inputs: an `AdoTestPlan` piped to `Get-AdoTestSuite` and an `AdoBuildDefinition` piped to `Get-AdoBuild`, each without its `Id`, give one error per input and send no request |
| `tools/tests/Gate.Tests.ps1` | The passing and the failing gate each leave no `core-*` folder under `artifacts/verify/` |
| `tools/tests/Stages.Tests.ps1` | New case, two runs: a Pester run where one file discovers no test, and one where a test is inconclusive, are both incomplete with no failure |
| `tools/tests/Workflow.Tests.ps1` | New test for a hook group that carries no `hooks` array, and `-Stage ''` must be refused |
| `tools/tests/Package.Tests.ps1` | New case, two layouts: `en-us` for `en-US` and `Adotoolkit.psd1` for `AdoToolkit.psd1` are each refused |
| `tools/tests/LiveAudit.Tests.ps1` | New `Live connection check with synthetic data only`: the validation block of `tests/Live/Connection.Live.ps1` prints the signature note first and exactly one `S0-9` verdict, signed and unsigned |

No fixture or golden was added, changed or removed.

### Visible changes for 0.9.0 users

- A failed-test report no longer shows the replacement character where an error line or a
  chart caption was cut inside an astral character. The cut text is otherwise identical, so a
  report of the same build is byte-identical to a 0.9.0 report unless such a character fell
  on a cut.
- Piping an object you built by hand, without its numeric identifier, now writes one
  `AdoRequest` error for that object in the message culture and leaves the rest of the
  pipeline running. 0.9.0 ended the pipeline with an `ArgumentOutOfRangeException` whose text
  was never translated, for example "planId ('0') must be a non-negative and non-zero value."
  A script that relied on that terminating error must read the error stream instead.
- Public contract: unchanged. No cmdlet, parameter, output type, configuration setting or
  schema differs. Objects returned by AdoToolkit commands always carry their identifiers, so
  a pipeline that uses them is untouched.
- Help and guides: four help topics describe their examples more precisely, in both cultures,
  and four guides list every cmdlet they use. No parameter description, syntax block or
  `yaml` block changed.
- The file names of the assets carry 0.9.5. Installation is as before, with the installer
  that ships with the zip.
- For developers: `verify` and the product gate are stricter. A Pester file that discovers no
  test, or a test left skipped, not run or inconclusive, makes the run incomplete and exits
  `2`; `verify -Stage ''` is refused; a case-only difference in the staged package layout
  fails the gate. The gate no longer leaves its test-result folders under `artifacts/verify/`.

## Bug fixes

B1 to B3 are defects of the product, T1 to T6 of the tooling and of the live checks. Each was
found by reading the repository against the rules of the `AGENTS.md` file that governs its
path. Each regression test was run against the 0.9.0 source of the file it covers, restored
from the commit `820b5e7`, before the fix was put back; the failures are in
[Pre-fix failures](#pre-fix-failures) below, and every test passes on this tree.

| ID | Finding, cause and fix | Regression test | Files |
| --- | --- | --- | --- |
| B1 | The one-line error summary was cut with a raw range of 240 characters, which splits a surrogate pair. The lone high half reached the sink, where the encoder writes `&#xFFFD;`, so the Error cell of every table, its `title` and the console table of `AdoTestFailure` showed the replacement character under text that was correct elsewhere. `FirstLine` now cuts with `Shorten`, the helper that every other cut of the document used; it moved from the nested `Document` type to the enclosing class | `ErrorTextTests.SummaryCutAtItsLimitKeepsTheAstralCharacterWhole`: a message whose pair straddles character 240 keeps no surrogate in the line and renders no `&#xFFFD;` | `src/AdoToolkit.Core/Reporting/TestFailures/HtmlTestFailureRenderer.cs` |
| B2 | `RunHistoryChart.Label` cut a bar caption the same way, to 27 characters and an ellipsis at a limit of 28, so a build number with an astral character at the cut gave a caption of `&#xFFFD;` under a title that held the number whole. The cut steps back one character when it would split a pair | `RunHistoryChartTests.BarCaptionCutAtItsLimitKeepsTheAstralCharacterWhole`: a build number whose pair straddles the cut renders no `&#xFFFD;` | `src/AdoToolkit.Core/Reporting/Charts/RunHistoryChart.cs` |
| B3 | `EnsureComplete` reads the `InputGuard` metadata, which describes reference-typed members only, so a hand-made input without its numeric identifier passed every guard, reached Core as `0` and threw `ArgumentOutOfRangeException`. `RunLocal` catches `AdoException` alone, so the exception left `ProcessRecord`, ended the whole pipeline and carried text that is never translated. The new `EnsureIdentifier` reports the object and the member with `IncompleteInput`, like a null member, at 11 call sites in 9 cmdlets | `InputGuard.Pester.ps1`, the case that reports an input whose identifier is absent as a per-input error, with `Get-AdoTestSuite` and `AdoTestPlan` and with `Get-AdoBuild` and `AdoBuildDefinition`: two inputs give two `AdoRequest` errors, no output and no request | `src/AdoToolkit.PowerShell/Commands/AdoCmdletBase.cs` and the nine command files listed under [Pipeline input](#pipeline-input) |
| T1 | Each Core run of the gate wrote its TRX results to a new folder under `artifacts/verify/` and nothing removed it, so every gate run in a checkout left two folders behind. This checkout held 323 of them with 322 TRX files, 572 MB, from the runs of every version up to 0.9.0. The gate now removes each folder in its `finally`, after its counters have been read, so an interrupted or failing gate cleans up too; `$coreRuns` moved above the `try` because the `finally` reads it under strict mode | `tools/tests/Gate.Tests.ps1`: the passing gate and the gate with a failing Core run each leave no `core-*` folder | `tools/check.ps1` |
| T2 | A Pester run was read as complete when a file discovered no test, because its zero counts vanish in the merged totals, and when a test was inconclusive, because only `SkippedCount` and `NotRunCount` were tested. `verify` and the gate could then report a pass although a whole file had run nothing, which contradicts the rule that `2` is incomplete and never a pass. `Invoke-PesterProcess` sums `InconclusiveCount` and reports per-file `Incomplete`; `Get-PesterOutcome` and the gate test the flag and whether `PassedCount + FailedCount` equals `TotalCount` | `tools/tests/Stages.Tests.ps1`, the case that reports a Pester run as incomplete, for a file that discovers no test and for an inconclusive test | `tools/lib/processes.ps1`, `tools/lib/validation.ps1`, `tools/check.ps1` |
| T3 | The `hooks` check of `tooling-layout` read `$group.hooks` and `$hook.type` directly. Under strict mode a bare read of an absent property raises `PropertyNotFound`, which the surrounding catch reported in place of the shape problem and left the list of hook scripts empty, so the stage checked no hook helper at all and still passed. Every member is read through `PSObject.Properties`, and a group without a `hooks` array, an entry that is not an object and a blank command are each named | `tools/tests/Workflow.Tests.ps1`, the test that names a hook group carrying no `hooks` array | `tools/lib/validation.ps1` |
| T4 | `if ($Stage)` is false for `@('')`, so `verify -Stage ''` skipped the name check, ran every stage and could report `pass`, which `-Stage` must never do. The selection is taken whenever `-Stage` is bound, and the empty name is rejected as unknown | `tools/tests/Workflow.Tests.ps1`, the selected-check test, with a new assertion for `-Stage ''` | `tools/lib/validation.ps1` |
| T5 | `Assert-AdoPackage` compared the staged file names with `Compare-Object` without `-CaseSensitive`, while `tools/package/Install-AdoToolkit.ps1` compares `$layout` with it. A case-only difference, `en-us` for `en-US`, passed the gate and both workflows and would then be refused by the installer on every user's machine, after the release was published. The comparison is case-sensitive | `tools/tests/Package.Tests.ps1`, the case that rejects a layout differing only in case, for `en-us` and `Adotoolkit.psd1` | `tools/package/Package.Common.ps1` |
| T6 | `tests/Live/Connection.Live.ps1` printed `PASS S0-9` for the signature of the installed release and a second `PASS S0-9` for the connection, and a failure after the second one printed `FAIL S0-9` as a third. `tests/Live/AGENTS.md` allows one verdict per check ID, and `S0-9` is one criterion met by the connection. The signature prints as `NOTE`, and a settled flag suppresses a `FAIL` raised after the verdict | `tools/tests/LiveAudit.Tests.ps1`, the test that gives `S0-9` one verdict after the signature note, signed and unsigned | `tests/Live/Connection.Live.ps1` |

### Pre-fix failures

Each regression test was run with the file it covers replaced by its 0.9.0 content from
`820b5e7`, the rest of this tree unchanged, and then with the fix restored.

| Test | Failure without the fix |
| --- | --- |
| `ErrorTextTests.SummaryCutAtItsLimitKeepsTheAstralCharacterWhole` | `Assert.DoesNotContain() Failure: Filter matched in collection` at `ErrorTextTests.cs:48`: the line keeps a surrogate |
| `RunHistoryChartTests.BarCaptionCutAtItsLimitKeepsTheAstralCharacterWhole` | `Assert.DoesNotContain() Failure: Sub-string found`: the rendered chart holds `&#xFFFD;` |
| `InputGuard.Pester.ps1`, the absent-identifier case | Both inputs raise `ArgumentOutOfRangeException`: "planId ('0') must be a non-negative and non-zero value. (Parameter 'planId')" for `Get-AdoTestSuite`, and the same for `resolvedDefinition.Value` for `Get-AdoBuild` |
| `Gate.Tests.ps1`, both gate tests | A `core-*` folder is left under `artifacts/verify/` of the fixture |
| `Stages.Tests.ps1`, both cases | `Unavailable` is false and no warning is given: the run reads as complete |
| `Workflow.Tests.ps1`, the hook-group test | The failure reported is the strict-mode `PropertyNotFound`, not the shape of the group |
| `Workflow.Tests.ps1`, the selected-check test | `-Stage ''` does not throw: it runs the whole gate |
| `Package.Tests.ps1`, both layouts | `Assert-AdoPackage` accepts `en-us` and `Adotoolkit.psd1` |
| `LiveAudit.Tests.ps1`, both signature cases | Two `S0-9` verdict lines, the first `PASS S0-9 SIGNATURE_VALID` or `PASS S0-9 UNSIGNED_RELEASE_INSTALLED` |

Totals of the pre-fix runs: both Core tests failed, 2 of the 16 tests of
`InputGuard.Pester.ps1` failed, and 10 tooling tests failed across `Gate.Tests.ps1` (2 of 5),
`Stages.Tests.ps1` (2 of 20), `Workflow.Tests.ps1` (2 of 26), `Package.Tests.ps1` (2 of 37)
and `LiveAudit.Tests.ps1` (2 of 86). No other test failed in those runs.

### Existing tests changed

| Test | Change |
| --- | --- |
| `tools/tests/Gate.Tests.ps1`, the two tests that run the gate to completion | Each gained one assertion: no `core-*` folder remains under `artifacts/verify/` of the fixture |
| `tools/tests/Workflow.Tests.ps1`, the selected-check test | Gained one assertion: `-Stage ''` is refused as an unknown stage |

No test was deleted, and no golden report changed. The mocked Pester results of
`tools/tests/Workflow.Tests.ps1` need no `Incomplete` member: `Get-PesterOutcome` reads it
through `PSObject.Properties`, so a result without it is read on its totals alone.

### Findings not fixed

| Finding | Reason |
| --- | --- |
| `Invoke-PesterProcess` reports per-file completeness, but `Get-PesterOutcome` cannot name which file was incomplete | The merged result carries one flag, not a list. The warning says what to look for, and each file's own run prints its counts; naming the file needs a per-file result in the merged object, which the stage contract does not carry |
| An astral character can still be dropped from a cut line | A cut at a character limit loses text by design; the fix only stops it from losing half a character. The cell's title and the attempt's own text carry the whole message |
| `EnsureIdentifier` is called where a cmdlet reads an identifier, not derived from the `InputGuard` metadata | The metadata describes reference-typed members, and extending it to value types would change the meaning of a public attribute for a defect that only a hand-made object can reach. A new cmdlet that reads an integer identifier from pipeline input must call the guard; no test enforces it |
| `tests/AdoToolkit.PowerShell.Tests/TestRuns.Pester.ps1`, the test that sends the same requests with 6 at a time, asserts an exact concurrency peak and failed once during this validation | The assertion reads the peak the fake server observed and requires exactly 2; under a loaded gate a third request overlapped, "Expected 2, but got 3". It passed in every other run, and nothing in 0.9.5 touches retrieval, the HTTP pipeline or the fake server, so the flake predates this release. Replacing the exact peak with the bound, or separating the stages whose request counts the test relies on, changes a product test and belongs to a pass of its own |
| Tag a bug opened in this run as New, in a lighter red, as the developer asked in the last design pass of 0.8.5 | Still open: it needs each bug's creation date (`System.CreatedDate`), a new property of `AdoTestBug`, which is public output, and a live check on Server 2020 |
| 94 `pester-*.txt` files left in `artifacts/verify/` by the reporting child of the gate | Not a defect of this tree: 0.9.0 deleted the code that wrote them, and the newest is dated before that change. They were removed by hand before the final validation, with the 323 TRX folders of T1 |
| The findings of 0.9.0, 0.8.5, 0.8.0, 0.7.10, 0.7.5, 0.7.0, 0.6.5 and 0.6.0 | Unchanged; see the [0.9.0](archive/release-0.9.0.md#findings-not-fixed), [0.8.5](archive/release-0.8.5.md#findings-not-fixed), [0.8.0](archive/release-0.8.0.md#findings-not-fixed), [0.7.10](archive/release-0.7.10.md#findings-not-fixed), [0.7.5](archive/release-0.7.5.md#findings-not-fixed), [0.7.0](archive/release-0.7.0.md#findings-not-fixed), [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes |

### Known limitations

| Limitation | Notes |
| --- | --- |
| The surrogate rule is applied where the code cuts text | `HtmlTestFailureRenderer` and `RunHistoryChart` now cut through one helper each. A new cut written with a raw range operator would reintroduce the defect; no test reads the sources for one |
| A hand-made input is guarded member by member | `EnsureComplete` covers every reference-typed member that `InputGuard` describes; integers are covered where a cmdlet reads them. An object with a plausible but wrong identifier is a valid request, and the server answers it |
| Completeness is read from Pester's counters | A test that Pester reports as passed is counted as run. A file that fails during discovery is already a failure, not an incomplete run |
| The case-sensitive layout check compares names | The gate and the installer compare file names, not the filesystem. Windows refuses a direct case-only rename, which is why the regression test renames through a staging name |
| Limits carried over | The [0.9.0](archive/release-0.9.0.md#known-limitations), [0.8.5](archive/release-0.8.5.md#known-limitations), [0.8.0](archive/release-0.8.0.md#known-limitations), [0.7.10](archive/release-0.7.10.md#known-limitations), [0.7.5](archive/release-0.7.5.md#known-limitations) and [0.7.0](archive/release-0.7.0.md#known-limitations) known limitations still apply, with V-27, V-28, V-33 to V-36, V-31, V-32 and V-02 |

## Work-PC Live checks

Install the 0.9.5 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass. No check gates 0.9.5; checks 1 to 3 confirm the two fixes that
a user can see and the one live check that changed.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | Record `Get-FileHash` of `%APPDATA%\AdoToolkit\config.json`, install 0.9.5, then run `Get-AdoProfile` and `Connect-Ado` | The hash is unchanged, no warning appears, and `Get-Module AdoToolkit -ListAvailable` shows 0.9.5 with seven files in its folder |
| 2 | Run `tests/Live/Connection.Live.ps1` with `ADOTOOLKIT_LIVE_PROFILE` set | The first line is `NOTE S0-9 SIGNATURE_VALID` or `NOTE S0-9 UNSIGNED_RELEASE_INSTALLED`, and exactly one `S0-9` verdict follows, `PASS S0-9 CONNECT_TEST_PROJECTS`. Exit `2` for the `V-07`, `V-08`, `V-14` and `V-16` lines is expected |
| 3 | Export a failed-test report for a build whose tests carry an error message longer than 240 characters, and read the Error column | Each cell ends in an ellipsis and holds no replacement character; the cell's tooltip holds the whole line. A build number longer than 28 characters gives the same result in the chart's bar captions. Only a message or build number with an astral character at the cut can show the defect, so a clean report is consistent with the fix and does not prove it |
| 4 | Checks 1 and 2 of the [0.9.0 notes](archive/release-0.9.0.md#work-pc-live-checks), with the 0.9.5 package | As listed there: the restyled failed-test report in English and French, then the 0.8.0 checks with V-33, `-SkipAttachments`, the probes of V-28 and V-34 to V-36, and the 0.7.5 checks with V-31, V-32, V-02, V-01 and V-03. All still pending |

## Local validation and developer handoff

Run on 2026-10-04 on the development PC, on the tree that holds these notes.

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0 in about 45 s. All seven stages pass without a warning: 70 PowerShell files linted, 317 tooling Pester tests, 147 configuration files, 86 Markdown files with 268 links and 288 path references, 3 hook helpers, 4 skills and 3 subagents in the layout, 2 workflow files linted with actionlint, and the product check, which staged and inspected AdoToolkit 0.9.5 |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as both workflows run it | Exit 0 with the exact module pins in `tools/BuildModules.psd1`. The variable was set for that process only and was never set in the shell |
| Core tests in the final gate | 1555 passed under en-US and 1406 under fr-CA, with no failures or skips. 0.9.0 ran 1552 and 1404: three new tests, of which the culture-invariant golden-coverage test runs under en-US only |
| Product Pester tests against the staged module | 180 passed, none skipped, for 178 in 0.9.0: the new case has two inputs. The tooling tests are 317, for 310 |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `0.9.5` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.9.5` with seven files, and 0 warnings |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.9.5-win-x64.zip -ModuleVersion 0.9.5 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.9.5, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.9.5/`. The portable ZIP has the same seven files under `module/`, both launchers, `README.txt` and `bundle.json` (module 0.9.5, PowerShell 7.6.6, win-x64, PowerShell SHA-256 equal to the published hash and to the pin of the setup action), 669 entries in all. Each `.sha256` file holds the hash of its ZIP. The installer is byte-identical to its source |
| `tools/package/Install-AdoToolkit.ps1 -Path ./artifacts/release/AdoToolkit-0.9.5.zip -WhatIf` | Exit 0: the checksum and the layout are accepted and the target is named. Nothing was written, and no module was installed |
| The fix of T1, on this checkout | `artifacts/verify/` held 323 `core-*` folders with 322 TRX files, 572 MB, and 94 `pester-*.txt` files written by the reporting child that 0.9.0 deleted. Both sets were removed by hand before validation. Every gate run since, the passing runs and the one that failed, left no `core-*` folder behind |

One product Pester test failed once during validation and passed in every other run: the
concurrency-peak test recorded under [Findings not fixed](#findings-not-fixed). The run that
failed is the first `ADOTOOLKIT_RELEASE_BUILD=1` gate; the repeat of the same command passed,
and the failing run also showed that the gate removes its TRX folder when a step fails. The
verify workflow may need a re-run if it meets the same flake.

The pre-fix runs recorded in [Pre-fix failures](#pre-fix-failures) replaced single files with
their content from `820b5e7` and restored them afterwards; the restored files are
byte-identical to the branch's own, which `git diff` confirms file by file.

No Live script ran. Not run: an actual installation, the workflows, which only GitHub runs, a
`repo-review` run, and the squash merge, which happens on GitHub after these notes.

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.9.5-win-x64.zip` | 110421242 | `d0d21943c242b577b98d60de85d6d4777c006751349d7d3fbfa328db72b62862` |
| `AdoToolkit-0.9.5.zip` | 814327 | `ec723bb1e81636d218f02fc0801d8592fac447aa888c73d7c4b1d37c20bafe46` |
| `Install-AdoToolkit.ps1` | 13032 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes. No tag or GitHub release exists for 0.9.5. Every
change is uncommitted on the branch `0.9.5`, which has no upstream yet, so its first push
sets `origin`. The developer owns publication:

1. Run the first command of the handoff. It commits every change, creates the tag `v0.9.5`
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
6. Start the next branch from the updated `main`: the squash leaves the commits of `0.9.5`,
   and the tag `v0.9.5`, out of its history.
