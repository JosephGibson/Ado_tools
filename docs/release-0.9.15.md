# AdoToolkit 0.9.15 release notes

Prepared on 2026-10-04 from the branch `0.9.15`, which starts at `main` after the pull
request of 0.9.10 was squash-merged as `92ebf8a`; the files there are those of 0.9.10. The
reviewed changes are the one commit already on the branch, `4388117`, and the working tree on
top of it. 0.9.15 answers the first question of triage in a failed-test report: has someone
already filed this bug, and is anyone on it? `AdoTestBug` gains `CreatedDate` and
`AssignedTo`, read by the work item batch the bug lookup already sent, so the feature costs
no request. A bug filed at or after the build went into the queue carries `✦` on its chip, in
a lighter red, wherever that chip appears — the Overview's **Open bugs** column, the By error
facts line, the Details card and the attempt's associated bugs — and the Open bugs view counts
those bugs above its table. Each bug the report read whole also shows the day it was filed and
who it is assigned to, or **Unassigned**, on its line in the Open bugs view and inside the
test's card, which is what the report's search reads: typing an owner's name now leaves the
tests whose bugs that person has. This is the item the developer deferred in the last design
pass of 0.8.5. This is an offline review. No Azure DevOps Server connection or
`tests/Live/*.Live.ps1` run was attempted, so nothing below is confirmed live, and whether
Server 2020 returns the two fields at all is owed to the new check V-37. The areas the README
lists as confirmed are still connections, projects, builds and test runs.

## What changed

0.9.15 adds no cmdlet, no parameter and no configuration setting, and sends no new request.
It adds two properties to `AdoTestBug`, which is public output, and three keys to the string
catalog. The configuration file format, the JSON report schema and the seven-file package
layout are unchanged. The [0.9.10 notes](release-0.9.10.md) describe the last change
to the development tooling, and the [0.9.5 notes](archive/release-0.9.5.md) the last product
fixes.

### The bug's age and its owner

| Area | 0.9.15 behavior |
| --- | --- |
| `AdoTestBug` | Two new properties. `CreatedDate` is a `DateTimeOffset?`, the day the bug was filed as the server records it, converted to UTC; null when the bug could not be read or the server did not send the field. `AssignedTo` is an `AdoIdentityRef?` with `DisplayName`, `UniqueName` and `Id`; null both when nobody is assigned and when the bug could not be read. `IsOpen` tells the two cases apart, because it is `True` only for a bug read whole. Neither property joins the default table, which keeps its width for `Title` |
| The read | `Fields` in `src/AdoToolkit.Core/TestRuns/TestBugResolver.cs` is seven entries instead of five, with `System.CreatedDate` and `System.AssignedTo` appended. It is the same `WorkItemsBatch` call, in batches of up to 200, so no request was added and no stage of `-Verbose` changed |
| `WorkItemFieldValues` | New internal helper, `src/AdoToolkit.Core/WorkItems/WorkItemFieldValues.cs`. `Date` reads a known date field as `FieldValueMapper` does and returns null for anything that is not a date the server wrote. `Identity` never loses a name the payload holds: a non-empty string is that text as `DisplayName`, the Server 2020 `DOMAIN\user` shape; an object is named by the first non-empty of `displayName`, `uniqueName` and `id`, with `Id` and `UniqueName` set from their own properties; an absent field, JSON null, a blank string and an object carrying none of the three are null. Both are bounded reads of one field, neither throws, and a missing optional field produces no diagnostic: the report simply makes no claim |
| `TestCaseDetailService` | Unchanged. Its own identity rule is stricter — it returns null for an object without a usable `displayName` — and sharing it would have produced a false **Unassigned**. Loosening it in place would change `Export-AdoTestCase -IncludeDetail` output, which is not this change |

### The report

| Area | 0.9.15 behavior |
| --- | --- |
| The marker | A bug is marked when it was read whole (`IsOpen` is `True`), the build has a queue time, and the bug's creation date is at or after it. Both sides are instants, so no offset or daylight-saving rule enters the comparison. `BugChip` is one method, so the marker reaches every place a bug chip appears with no drift: the chip gains `bug-new`, prepends `<span aria-hidden="true">✦</span>`, and extends its `title` and its screen-reader text with **Opened after this build was queued** |
| The bug's line | `BugText` — the line the Open bugs heading and the Details card's bug list both render — gains, after the title and the state and only for a bug read whole: **Created on** and the day in the report's offset; the **✦ New** text chip when the bug is marked; then **Assigned To** and the name, or **Unassigned**. A bug marked **Not read** still says only that, so no line pairs a date with **Not read** |
| The Open bugs summary | A line above the table: `Open bugs: 1 · Opened after this build was queued: 1`, « Bogues ouverts : 1 · Ouverts après la mise en file de ce build : 1 ». The second count is left out when the build has no queue time, rather than printed as zero. The precedent is By error's own summary line |
| Search | The day and the name render inside the `.failure-card` that `apply` in `src/AdoToolkit.Core/Reporting/Assets/test-failures.js` matches, so an owner's name is searchable and the matching tests' table rows and bug groups follow their card's verdict. The search is unchanged; what it reads grew |
| The colour | A new token `--fail-new`, `#ff9d9d` on screen and `#7a1414` in print, in both palettes of `src/AdoToolkit.Core/Reporting/Assets/test-failures.css`. The chip keeps its border and its 12 % tint, so its box and the column's width do not change. Red because red means bug in this report, lighter red because the filled violet **✦ New** chip stays the test's alone; the glyph carries the meaning, so colour never carries it alone |
| What no bug gets | A bug the report could not read takes no marker, no date and no assignee: `IsOpen` is null for a bug that never came back **and** for one that came back without a `System.State`, and the second has a creation date. A read, open bug with no creation date takes no marker and no date, because **New** is a claim about a date, and still shows its assignee. A build with no queue time marks no bug and omits the count |
| What is unchanged | No Overview card was added: the cards row answers *what kind of build is this*, and a bug-filing count answers a question about the bug list. The default console tables of `AdoTestBug` are unchanged. Closed bugs, a bug's history and `docs/schemas/testcase.v1.schema.json` are untouched |

### Strings

| Key | English | French |
| --- | --- | --- |
| `TestReportBugOpenedAfterQueued` | Opened after this build was queued | Ouvert après la mise en file de ce build |
| `TestReportBugsOpenedAfterQueued` | Opened after this build was queued | Ouverts après la mise en file de ce build |
| `TestReportUnassigned` | Unassigned | Non assigné |

Two keys share their English text because the French differs in number, as `TestReportNew`
and `TestReportNewTests` already do. `build` is masculine, so « ce build » and the singular
« Ouvert » agree with « bogue ». No new key was needed for the labels: `ReportCreatedDate`
(Created on / Créé le), `ReportAssignedTo` (Assigned To / Assigné à), `TestReportNew`,
`TestReportOpenBugs` and `TestReportLabelValue` are reused.

### The live check V-37

| File | Change |
| --- | --- |
| `tests/Live/TestFailures.Live.ps1` | `V-37` in `$items`, with its own projected work item batch. The script's existing batch reads a Test Case with relations and no field projection, and the bug lookup runs inside the cmdlet where the raw response is not visible, so V-37 sends its own request for the bug IDs the V-30 block collected, with the resolver's seven-field list and `errorPolicy: omit` |
| `tests/Live/Live.Common.ps1` | `Get-AdoLiveIdentityShape` classifies a `System.AssignedTo` value `IDENTITY_OBJECT`, `STRING`, `EMPTY` or `OTHER`; `Get-AdoLiveBugFieldEvidence` counts field presence and compares it with the module's own non-null counts. `EMPTY` is beside the three names the plan listed, so a bug whose field comes back JSON null or blank is the handled unassigned case and not an unknown shape |
| `tools/tests/LiveAudit.Tests.ps1` | 17 synthetic cases for both helpers and for the block's validation |
| `docs/tooling.md` | The row for the script names V-37 and what it compares, and the optional `ADOTOOLKIT_LIVE_ASSIGNED_BUG_ID`, a bug the developer knows has an assignee. Omission cannot be told from "nobody is assigned" without one, so without it the assignee half is `INCONCLUSIVE`, never a pass |

It prints structure only: per returned bug whether each field is present, each present assignee
as a shape name, and the counts. No name, no date and no identity is printed. An agent never
runs it.

### Documentation

| Area | Change |
| --- | --- |
| `docs/commands/en-US/Get-AdoBuildTestFailure.md`, `docs/commands/fr-CA/Get-AdoBuildTestFailure.md` | `DESCRIPTION` names the two fields in what the bug batch reads; `OUTPUTS` lists the two properties, says that neither is in the default table and what each empty value means; `NOTES` owes the server question to V-37 |
| `docs/commands/en-US/Export-AdoBuildTestFailure.md`, `docs/commands/fr-CA/Export-AdoBuildTestFailure.md` | The `DESCRIPTION` of the Overview, Details and Open bugs views names the marker, the day, the assignee and the summary count; `NOTES` names V-37. The long `DESCRIPTION` was also rewrapped, which is most of its diff |
| `docs/guides/build-report.md` | The Details and Open bugs rows of the view table; the colour paragraph, including what print does; and a paragraph on what `✦` on a bug chip claims and does not claim, the cases that take no marker, and V-37. The search section says that bug filing days and assignees are matched |
| `docs/guides/pipeline-triage.md` | A `Format-Table` example that selects the two properties, what each empty value means and how `IsOpen` tells them apart, V-37, and the report paragraph on the marker, the count and the searchable name |
| `README.md` | The version, a paragraph on this version, and the link to these notes |
| Guides and installer | `docs/guides/getting-started.md` and the examples of `tools/package/Install-AdoToolkit.ps1` name the 0.9.15 files; the installer is otherwise that of 0.9.10 |
| Archive | The 0.9.5 notes moved to `docs/archive/release-0.9.5.md`, unedited. The archive index lists them, the changelog and the 0.9.10 notes point at the new place, and the index's citation table resolves `V-37` to the Known limitations of these notes. The 0.9.5 notes define no `V-nn` item, so no citation moved |

### The development tooling

| Area | Change |
| --- | --- |
| `.agents/skills/release/SKILL.md` | The handoff takes the pull request as far as it can from one template: `gh pr create` where that works, and otherwise the compare page, printed and opened with the title and the body encoded. The commit message and the pull-request title are one variable, so neither can drift from the other, and the block runs only after the push succeeded, so a chain that stops earlier publishes nothing. A `gh pr create` refused for the token ends on the compare page instead of leaving the developer with nothing. The developer steps also give the next branch its commands, `git switch --create <next> --no-track origin/main` and a first push with `--set-upstream`: a branch created from `origin/main` tracks `main`, and a bare `git push` would then write to `main` |
| `.agents/skills/critique-plan/SKILL.md`, `docs/tooling.md` | The critic's size gate is 50,000 characters instead of 10,000. A plan of this repository's size reaches the critic whole, which is what lets a finding weigh one phase against another |

### Tests

| File | Change |
| --- | --- |
| `tests/AdoToolkit.Core.Tests/Reporting/TestFailures/BugsViewTests.cs` | Five new tests, seven cases. The marker is taken at the queue time itself and not one minute before it; a bug that came back without a state stays grey whatever it carries; without a queue time no bug is marked and the summary leaves out the count; the summary line counts the marked bugs in both cultures; and the marker reaches every place a chip appears while the day and the name land inside the card that search reads, in both cultures |
| `tests/AdoToolkit.Core.Tests/TestRuns/TestBugResolutionTests.cs` | One new test over every identity shape — an object with `displayName`, one named only by `uniqueName`, one named only by `id`, one carrying none of the three, a bare `DOMAIN\user` string, a blank string and JSON null — and an unparseable creation date, which makes no claim and no diagnostic |
| `tools/tests/LiveAudit.Tests.ps1` | 17 new cases for the two live helpers and the V-37 validation |

### Existing tests changed

| Test | Change |
| --- | --- |
| `tests/AdoToolkit.Core.Tests/Reporting/TestFailures/TestFailureReportFixture.cs` | The builds gain a `QueueTime`, and the bugs gain creation dates and assignees chosen to sit on each side of it: after it, at it, one minute before it, long before it, one bug read with no date at all, and one whose assignee is the hostile string, because a name is remote text too. Without this the goldens would not show the feature at all |
| `tests/AdoToolkit.Core.Tests/Reporting/TestFailures/ByErrorViewTests.cs` | The expected facts line of bug 5101 now carries `open-bug-marker bug-new` and the glyph, because that bug is filed after the build was queued |
| `tests/AdoToolkit.Core.Tests/Reporting/TestFailures/StylingTests.cs` | The unread-bug card case also asserts the assignee of the read bug beside it |
| `tests/AdoToolkit.Core.Tests/Reporting/TestFailures/TestFailureSignalTests.cs` | The assertion that an unread test has no trend of its own now excludes `class="trend trend-` rather than `class="trend `: the bug's own **New** chip shares the chip shape and is not a trend |
| `tests/AdoToolkit.Core.Tests/Reporting/ThemeContrastTests.cs` | `--fail-new` joins the `colours` array, so it is required at 4.5:1 on `--bg`, `--surface`, `--surface-2` and `--code-bg`, on screen and in print |
| `tests/AdoToolkit.PowerShell.Tests/TestRuns.Pester.ps1` | The failed-test retrieval test pins both properties on the bug reached through PowerShell: the exact `DateTimeOffset`, the `AdoIdentityRef` type, its `DisplayName` and its `UniqueName`. The pin on the default table's row is unchanged, which keeps the "neither joins the table" claim honest |
| `tests/Fixtures/TestRuns/workitems-bugs.json`, `tests/Fixtures/TestRuns/workitems-linked.json` | The responses carry `System.CreatedDate` and `System.AssignedTo`: bug 2001 and bug 3001 assigned, one through an identity object and one through a bare string, and bugs 2002 and 3080 created and unassigned. `tests/Fixtures/README.md` describes both files as they now read |
| `tests/Fixtures/Reports/testfailures-*.html` | The 12 failed-test goldens, six variants in two cultures, regenerated through the `update-goldens` skill and reviewed in both cultures: the `--fail-new` token and the three new rules in the inlined stylesheet, the marked chips, the bug lines and the Open bugs summary. No other golden changed |

No test was deleted, and no acceptance tag changed.

### Visible changes for 0.9.10 users

- Public contract, additive: each `AdoTestBug` in the `Bugs` of a reported test now carries
  `CreatedDate` and `AssignedTo`. Nothing was removed or renamed, no existing property
  changed, and the default console tables are the same, so a script written against 0.9.10
  needs no change. A script that enumerates a bug's properties, or serializes one, sees two
  more.
- In a failed-test report, a bug filed at or after the build's queue time carries `✦` on its
  chip in a lighter red, wherever the chip appears. A report of a build whose queue time the
  server did not give marks no bug.
- The Open bugs view has a summary line above its table with the number of bugs it lists and,
  when the build has a queue time, how many of them were filed after it.
- Each bug the report read whole shows the day it was filed and its assignee, or
  **Unassigned**, in the Open bugs view and on the test's card. A bug marked **Not read** is
  unchanged.
- The report's search now also matches a bug's filing day and its assignee, so a word you
  typed before may leave more tests than it did in 0.9.10.
- The report is one request-for-request identical retrieval: the two fields ride in the work
  item batch the bug lookup already sent, so `-Verbose` shows the same stages and the same
  request counts as 0.9.10 for the same build.
- Configuration files, the JSON report schema and the installation layout are unchanged, and
  the file names of the assets carry 0.9.15.
- For developers: the release handoff falls back to the prefilled compare page when
  `gh pr create` cannot open the pull request, and the plan critique accepts a plan of up to
  50,000 characters.

## Bug fixes

None in the module. 0.9.15 corrects no defect of a cmdlet, a report or the configuration
file, so section 3 of the `release` skill produced no pre-fix evidence: there is no regression
test to fail without a fix, because there is no fix. Two defects of the release procedure
itself were corrected, both in prose that no test covers, so neither carries reproduced
evidence and neither is claimed as a fixed defect of the product:

| Defect | Correction |
| --- | --- |
| The 0.9.10 handoff ended at `gh pr create`. A fine-grained token that may read a repository, its checks and its runs still refuses `createPullRequest`, and `gh pr create --dry-run` exits `0` without exercising the permission, so the refusal could only appear after the tag had been pushed and the release published — leaving the developer with a published release and no pull request and nothing in the command to fall back on | `4388117`: one template takes the pull request as far as it can, `gh pr create` where that works and the printed and opened compare page otherwise, with the title and the body encoded. The fallback is in the command, not in a probe |
| The developer steps told the developer to start the next branch from the updated `main` without saying how. A branch created from `origin/main` tracks `main`, and then a bare `git push` writes to `main` | The steps now give `git switch --create <next> --no-track origin/main` and a first push with `--set-upstream`, with the reason |

### Findings not fixed

| Finding | Reason |
| --- | --- |
| A long wait in the queue marks an unrelated bug **New** | Accepted, with the day the bug was filed beside the marker. The queue time is one value and a property of the build, so two reports of the same build cannot disagree; a first run's start depends on which runs were retrieved and on rerun and stage ordering. A false **New** is the cheaper error, because the marker says "this may have been filed for this run" and the date settles it, while missing a bug that was filed for the run is the costly miss |
| A report generated today for an older build can mark a bug that was filed for a later build | No upper bound is available: the bug read has no `asOf`, `RunHistoryService.ReadEarlierAsync` reads the current build and earlier ones only, and `GeneratedAt` is always later than any bug. Reading the next build of the definition would buy a true bound for one more request on every report, to fix a case that arises only when an old report is regenerated. The claim is narrowed to a true one instead — *opened after this build was queued* — and `docs/guides/build-report.md` states the case |
| The Overview has no card counting the bugs filed for this run | The cards row holds up to four panels and each answers what kind of build this is; a fifth panel for one integer is what crowds that row at 1280 px. The Open bugs view's own summary line carries the count, one key away from the dates and the assignees |
| `CreatedDate` and `AssignedTo` are not in the default bug table | `AdoTestBug`'s table is five columns and `Title` takes what is left; an identity renders as `Name <unique.name>` and a date-time is about 20 characters, which would leave `Title` roughly 30. Both are one `Select-Object` away, `OUTPUTS` lists them and `docs/guides/pipeline-triage.md` shows the `Format-Table` |
| `TestCaseDetailService` keeps its stricter identity rule, so two rules now read an identity | Sharing the strict rule would have produced a false **Unassigned**, and loosening it in place would change `Export-AdoTestCase -IncludeDetail`, which is not this change. The bug read's rule is a superset, so the two can disagree only by showing a name the Test Case detail omits — cosmetic, not a false claim |
| A closed bug still carries no age or owner | Closed bugs are left out of `Bugs` entirely, as before. Nothing here changes which bugs the report lists |
| The findings of 0.9.10, 0.9.5, 0.9.0, 0.8.5, 0.8.0, 0.7.10, 0.7.5, 0.7.0, 0.6.5 and 0.6.0 | Unchanged; see the [0.9.10](release-0.9.10.md#findings-not-fixed), [0.9.5](archive/release-0.9.5.md#findings-not-fixed), [0.9.0](archive/release-0.9.0.md#findings-not-fixed), [0.8.5](archive/release-0.8.5.md#findings-not-fixed), [0.8.0](archive/release-0.8.0.md#findings-not-fixed), [0.7.10](archive/release-0.7.10.md#findings-not-fixed), [0.7.5](archive/release-0.7.5.md#findings-not-fixed), [0.7.0](archive/release-0.7.0.md#findings-not-fixed), [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes. The one the 0.9.10 notes left open — tag a bug opened in this run as **New**, in a lighter red — is what this version implements |

### Known limitations

| Limitation | Notes |
| --- | --- |
| V-37: the two bug fields on Server 2020 | Whether Server 2020 returns `System.CreatedDate` and `System.AssignedTo` in a `WorkItemsBatch` field projection, and which shape the identity takes there — an identity object or a bare `DOMAIN\user` string — has not been observed at work. Both properties are null-tolerant end to end: without them there is no marker, no date, no assignee and no diagnostic, and the report makes no claim. The code handles both shapes. `V-37` in `tests/Live/TestFailures.Live.ps1` settles it; an `OTHER` shape is a `FAIL`, and the assignee half is `INCONCLUSIVE` without a bug known to be assigned |
| The marker has no upper bound | `✦` means the bug was filed at or after the build's queue time, not that it was filed for this build, and a report regenerated today for an older build can mark a bug filed for a later one. The day the bug was filed is on its line beside the marker, and `docs/guides/build-report.md` states the case |
| The marker depends on the build's queue time | A build whose `QueueTime` the server did not give marks no bug and omits the Open bugs count. The dates and the assignees still show |
| An identity carrying no name at all reads as unassigned | `AssignedTo` is null for an identity object with no `displayName`, `uniqueName` or `id`. Such a payload holds no name that could be shown, so the line says **Unassigned**; any payload that holds a name yields one |
| Limits carried over | The [0.9.10](release-0.9.10.md#known-limitations), [0.9.5](archive/release-0.9.5.md#known-limitations), [0.9.0](archive/release-0.9.0.md#known-limitations), [0.8.5](archive/release-0.8.5.md#known-limitations), [0.8.0](archive/release-0.8.0.md#known-limitations), [0.7.10](archive/release-0.7.10.md#known-limitations), [0.7.5](archive/release-0.7.5.md#known-limitations) and [0.7.0](archive/release-0.7.0.md#known-limitations) known limitations still apply, with V-27, V-28, V-33 to V-36, V-31, V-32 and V-02 |

## Work-PC Live checks

Install the 0.9.15 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass. V-37 is the check this version owes.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | `tests/Live/TestFailures.Live.ps1` with `ADOTOOLKIT_LIVE_TEST_BUILD_ID`, and `ADOTOOLKIT_LIVE_ASSIGNED_BUG_ID` set to a bug known to have an assignee | `PASS V-37 BUG_FIELDS_PRESENT_AND_SHAPED`: every returned bug carries `System.CreatedDate`, every present `System.AssignedTo` is `IDENTITY_OBJECT`, `STRING` or `EMPTY`, the module's non-null counts equal the raw presence counts, and the named assigned bug came back with the field. `FAIL V-37 ASSIGNED_TO_SHAPE_UNKNOWN=<n>` is the evidence that `WorkItemFieldValues.Identity` needs another shape; `FAIL V-37 CREATED_DATE_ABSENT` means the field is not in a projection and the marker can never appear. Without the assigned bug ID the assignee half is `INCONCLUSIVE`, not a pass |
| 2 | Export a failed-test report for a build whose tests have open bugs, in English and in French, and read the Open bugs view | A bug filed after the build was queued carries `✦` in the lighter red on every chip, the summary line gives both counts, each bug read whole shows its filing day and its assignee or **Unassigned**, and typing an owner's name in the search leaves that owner's tests. Compare the filing days against the work items: a wrongly marked **New** corrects itself on sight |
| 3 | Print the same report, or save it as PDF | `--fail-new` darkens with the rest of the palette and the `✦` chips stay legible beside the red of the open bugs |
| 4 | Checks 1 and 2 of the [0.9.10 notes](release-0.9.10.md#work-pc-live-checks), with the 0.9.15 package | As listed there: the configuration file untouched by the installation, `Get-Module AdoToolkit -ListAvailable` showing 0.9.15 with seven files, then the earlier checks — the one `S0-9` verdict of `tests/Live/Connection.Live.ps1`, the Error column and the chart captions of a report whose text is cut, V-33, `-SkipAttachments`, the probes of V-28 and V-34 to V-36, and V-31, V-32, V-02, V-01 and V-03. All still pending |

## Local validation and developer handoff

Run on 2026-10-04 on the development PC, on the tree that holds these notes.

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0, the product gate running beside the in-process stages. All seven stages pass without a warning: 71 PowerShell files linted, 339 tooling Pester tests, 147 configuration files, 89 Markdown files with 284 links and 366 path references, 4 hook helpers, 5 skills and 3 subagents in the layout, 2 workflow files linted with actionlint, and the product check, which staged and inspected AdoToolkit 0.9.15 |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as both workflows run it | Exit 0 with the exact module pins in `tools/BuildModules.psd1`, and the same counts. The variable was set for that process only and was never set in the shell |
| Core tests in the final gate | 1563 passed under en-US and 1414 under fr-CA, with no failures or skips: 8 more in each culture than 0.9.10, the five tests of the marker and the summary with their culture cases, and the identity-shape test |
| Product Pester tests against the staged module | 180 passed, none skipped, as in 0.9.10: the new pins join an existing test. The tooling tests are 339, for 322 in 0.9.10: the 17 cases of the live helpers and the V-37 validation |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `0.9.15` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.9.15` with seven files, and 0 warnings |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.9.15-win-x64.zip -ModuleVersion 0.9.15 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.9.15, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.9.15/`. The portable ZIP has the same seven under `module/`, both launchers, `README.txt` and `bundle.json` (module 0.9.15, PowerShell 7.6.6, win-x64, PowerShell SHA-256 `02fe458be20493fbdf43f61ea20610b811ee6c738ab1676c61b9cfcd1a33c860`, equal to the published hash and to the pin of the setup action), 669 entries in all. Each `.sha256` file holds the hash of its ZIP, and the installer is byte-identical to its source |
| `tools/package/Install-AdoToolkit.ps1 -Path ./artifacts/release/AdoToolkit-0.9.15.zip -WhatIf` | Exit 0: the checksum and the layout are accepted and the target is named, `Documents\PowerShell\Modules\AdoToolkit\0.9.15`. Nothing was written, and no module was installed |
| `pwsh -NoProfile -File .\tools\dev.ps1 diagnose` | `ok`, with every required tool present and both recommended tools present: ripgrep 15.2.0 and `gh` 2.102.0. The handoff below therefore opens the pull request with `gh` |

No test failed in either gate run, so nothing is recorded as a flake.

The 12 failed-test goldens were regenerated through the `update-goldens` skill and reviewed in
both cultures. `ThemeContrastTests` was the first failing test of the report work, so
`--fail-new` was chosen against the 4.5:1 requirement before any markup was written.

No Live script ran: V-37 and every earlier check are the developer's, at work. Not run: an
actual installation, the workflows, which only GitHub runs, a `repo-review` run, and the squash
merge, which happens on GitHub after these notes. The plan critique ran before the work began,
with five findings, all applied; `docs/plans/0.9.15-bug-age-and-assignee.md` records each one.

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.9.15-win-x64.zip` | 110424860 | `a099368a801f2e377460d28a71b18914e3700abc0c597f36cde996f545c234b5` |
| `AdoToolkit-0.9.15.zip` | 817959 | `0e88eda96aa413dd9e051f40f49e13ccfd5c784aae28566af2ed7121f1db5c42` |
| `Install-AdoToolkit.ps1` | 13035 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes. No tag or GitHub release exists for 0.9.15. The
branch `0.9.15` tracks `origin/0.9.15`, which already holds `4388117`; every other change is
uncommitted. The developer owns publication:

1. Run the first command of the handoff. It commits every change, creates the tag `v0.9.15`
   and pushes the branch and the tag together, which publishes the release at once, and then
   opens the pull request with its final title, falling back to the prefilled compare page if
   `gh pr create` cannot.
2. Run the second command. It waits for the Verify check of the pull request and opens the
   page in a browser once that check passes.
3. Merge with Squash and merge and keep the commit title that GitHub proposes: the title of
   the pull request, then its number.
4. Watch the release workflow and check its five uploaded assets, version and checksums. CI
   builds its own assets, so compare each checksum with CI's own `.sha256` files; archive
   metadata can differ from a local build. If the run does not publish, `docs/tooling.md`
   lists the recovery for each cause.
5. Run the work-PC checks above with the published package, V-37 first, and record any
   inconclusive coverage.
6. Start the next branch from the updated `main`, without tracking it.
