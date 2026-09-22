# AdoToolkit 0.5.0 release notes

Prepared on 2026-09-22 from the pending changes in `src/`, `tests/`, `tools/` and `docs/`.
This is an offline review. No Azure DevOps Server connection or `tests/Live/*.Live.ps1`
run was attempted, so nothing below is confirmed live. The areas the README lists as
confirmed are still connections, projects, builds and test runs.

## What changed

### Profile defaults for builds and test plans

A connection profile can keep a default branch, build definition, test plan and test
suite, so they are set once in `config.json` instead of on every command.

| Area | 0.5.0 behavior |
| --- | --- |
| Configuration keys | Four optional keys under `profiles.<name>`: `defaultBranch` (text), `defaultBuildDefinition` (a positive ID as a JSON number or an exact name as a JSON string, as for `-Definition`), `defaultTestPlanId` and `defaultTestSuiteId` (positive whole numbers). A missing key or `null` means not set |
| Validation | An empty or blank branch or definition name, an ID of zero, a negative, fractional or larger than 2147483647 ID, a quoted ID and any other JSON type are configuration errors, like an invalid `requestTimeoutSeconds` |
| Order of precedence | A parameter you pass, then the value of the connected profile, then the 0.4.0 behavior: no branch filter, and `-Definition`, `-PlanId` and `-SuiteId` required. No fallback value is built in |
| `Get-AdoBuild` | `-Definition` (still position 0) falls back to `defaultBuildDefinition`, and `-Branch` to `defaultBranch`, also for piped definitions |
| `Get-AdoBuildTestFailure` | `ByDefinition` is the default parameter set, so a call without a build ID, piped build or `-Definition` selects the latest completed build of `defaultBuildDefinition`, on `defaultBranch` when set. A build ID by position and piped builds bind as before |
| `Get-AdoTestCase` | `-PlanId` and `-SuiteId` fall back to `defaultTestPlanId` and `defaultTestSuiteId`. `BySuite` stays the default parameter set and takes no pipeline input, so a call without arguments selects it and piped IDs, suites, WIQL results and work items bind to their own sets as before |
| `Get-AdoTestSuite` | `-PlanId` (still position 0) falls back to `defaultTestPlanId`, and the start suite to `defaultTestSuiteId` |
| Suite and plan | A suite belongs to its plan, so the profile's suite is used only when the plan also comes from the profile. With an explicit `-PlanId` or a piped plan, `Get-AdoTestCase` needs `-SuiteId` and `Get-AdoTestSuite` starts from the plan's root suite |
| Missing values | Without the parameter and the profile value, the command stops with a localized `AdoConfiguration` error that names both, before any request |
| `Set-AdoProfile` | New `-DefaultBranch`, `-DefaultBuildDefinition`, `-DefaultTestPlanId` and `-DefaultTestSuiteId`. `$null` removes a value, and so does an empty or blank string for the branch and definition. An ID below 1 is a parameter validation error; a definition that is neither a positive integer nor a name is refused as for `-Definition`. Nothing is written in either case |
| `Get-AdoProfile` | Profiles print as a list that includes the four defaults and the request timeout |
| Connections | `Connect-Ado -Profile`, and the implicit connection with the default profile, copy the four values to the new `AdoConnection` properties `DefaultBranch`, `DefaultBuildDefinition`, `DefaultTestPlanId` and `DefaultTestSuiteId`. A connection made from `-CollectionUrl` has none |
| Saving | Keys that are not set are removed rather than written as `null`. A file without the new keys loads and saves byte for byte as before, and the keys no longer produce unknown-key warnings inside a profile. The same names at the top level or in another section still warn |

The documented example profile in [Configuration](guides/configuration.md) uses the
default branch `develop` and the default build definition `Test_Plan`.

### Failed-test report and run grouping

| Area | 0.5.0 behavior |
| --- | --- |
| Named runs | Attempts are grouped by the test run name as well as the stage, phase and job names. Runs of one job with different names, such as one run per language, get their own groups, labels, overview columns and classification: a test that fails in every attempt of one named run is `Failed` even when another named run passes. The name is trimmed, and case counts. The label uses the shortest names that tell the groups apart, the run name last |
| Job retries | A run named like another run of the same stage, phase and job plus ` (attempt N)`, where `N` is its job attempt, is a retry of that run and stays in its group. Any other difference keeps runs apart, and different run IDs alone never merge them |
| History | Earlier builds in the run history are grouped the same way when their cells are classified |
| Attachment links | Every attachment name links to the attachment content on the server, `{project}/_apis/test/Runs/{run}/Results/{result}/attachments/{id}?api-version=6.0-preview.1`, with `&testSubResultId=` for a sub-result: the route the downloader already uses. The browser downloads the original with Windows authentication, for every file type. In 0.4.0 the name linked to the result page |
| Open bug | In the overview and by-error tables, **Open bug** is now a link to the test's lowest-numbered open bug, in that bug's project, drawn in the failure color. The card's **Open** badge is unchanged |
| History strip | Each cell has a tooltip with the build number and status. The status legend under each strip is gone |
| Header | The failed, flaky and attachment counts and the partial-result link sit in the title line, which wraps; the separate count line is gone |
| Report address | `Export-AdoBuildTestFailure` writes the report's `file:///` address to the information stream with the `PSHOST` tag, so it shows in the console like `Write-Host` output. The success stream still carries only the `FileInfo`. Nothing is written with `-WhatIf` or when the export fails, and `-InformationAction Ignore` hides it |

### Other changes

| Area | Change |
| --- | --- |
| Documentation | `configuration.md` has the new keys, the example and the precedence rules. English and French help for `Set-AdoProfile`, `Get-AdoProfile`, `Connect-Ado`, `Get-AdoBuild`, `Get-AdoBuildTestFailure`, `Get-AdoTestCase` and `Get-AdoTestSuite` describe the defaults, with new examples. `getting-started.md`, `pipeline-triage.md` and `test-case-reports.md` point to them. The report changes are described in the English and French help for `Export-AdoBuildTestFailure` and `Get-AdoBuildTestFailure`, `build-report.md`, `pipeline-triage.md` and the README. Example file names in `getting-started.md`, `tooling.md` and `Install-AdoToolkit.ps1` use 0.5.0 |
| Messages | Four new English and French messages: a missing build definition, test plan or test suite, and the ID range for `Set-AdoProfile` |

### Visible changes for 0.4.0 users

- Existing configuration files need no change and are not rewritten with new keys.
  0.4.0 reads a file that has the new keys, with an unknown-key warning for each, and keeps
  them when it saves.
- `-Definition` on `Get-AdoBuild` and `Get-AdoBuildTestFailure`, `-PlanId` and `-SuiteId` on
  `Get-AdoTestCase`, and `-PlanId` on `Get-AdoTestSuite` are no longer mandatory in the
  parameter metadata. When a value is missing from both the call and the profile,
  PowerShell no longer prompts for it; the command stops with an `AdoConfiguration` error
  instead.
- `Get-AdoBuildTestFailure` without arguments now means the latest build of the profile's
  definition. Before, it asked for `-BuildId`.
- `-PlanId` and `-SuiteId` of `Get-AdoTestCase` and `-PlanId` of `Get-AdoTestSuite` are
  `Nullable[int]` in `Get-Command` output.
- `Get-AdoProfile` prints a list instead of a three-column table.
- `AdoConnection` objects have four more properties. The `Connect-Ado` and
  `Get-AdoConnection` table view is unchanged; use `Format-List` to see them.
- A build whose test runs of one job carry different names now shows one group per name.
  A test that 0.4.0 classified as `Flaky` because a differently named run passed last is
  now `Failed`, and `FailedCount` and `FlakyCount` change with it.
- Attachment names in reports download the attachment instead of opening the result page.
  The history strip has no legend; hover a cell for its status.
- `Export-AdoBuildTestFailure` prints one `file:///` line per report. Scripts that merge
  every stream, for example with `*>&1`, now also receive one `InformationRecord` per report.

## Bug fixes

Found in this review of the pending 0.5.0 changes.

| Area | Finding | Fix | Regression test | Files |
| --- | --- | --- | --- | --- |
| Report address | `new Uri(path)` reads `%` followed by the hexadecimal code of a letter, digit or `-._~` as an escape, so exporting to `x%41y.html` printed the address of `xAy.html`, another file. It also left `[` and `]` unescaped, which RFC 3986 does not allow in a path | Each path segment after the drive or server is escaped with `Uri.EscapeDataString`; device paths (`\\?\`, `\\.\`) keep the framework conversion | `TestFailureExport.Pester.ps1` "prints the report URI … (en-US, x%41y %20 100%.html)" checks that the address converts back to the exact path; the two `rapport [été] #1.html` cases now require `%5B` and no raw space, `[`, `]` or `#` | `Commands/Infrastructure/FileUris.cs` (new), `ExportAdoBuildTestFailureCommand.cs` |
| Documentation | The pending diff changed the report without its documentation. The English and French help for `Export-AdoBuildTestFailure` still grouped by stage or job only, linked attachments to their result, described an Open bug mark and did not mention the report address; the help for `Get-AdoBuildTestFailure` left run names out of the grouping; `build-report.md`, `pipeline-triage.md` and the README said the same | Rewritten for 0.5.0, in both languages for help | — | `docs/commands/*/Export-AdoBuildTestFailure.md`, `docs/commands/*/Get-AdoBuildTestFailure.md`, `docs/guides/build-report.md`, `docs/guides/pipeline-triage.md`, `README.md` |
| Release notes | These notes covered only the profile defaults, reported test counts from before the report changes, and said no existing test had changed | Rewritten from the final diff and the final `verify` run | — | `docs/release-0.5.0.md` |

The regression test was run against the module staged by the pre-fix `verify` and failed in
all three cases, then passed against the fixed module.

The rest of the diff was reviewed without finding a defect: the configuration keys and
their round trip, the four fallbacks, the parameter set changes, `Set-AdoProfile`,
`Get-AdoProfile` and the connection properties, the named-run grouping, the report markup and
styles, the English and French messages, and the English and French help, which have the
same sections and code blocks in every changed file. A probe of the staged module also
confirmed that piped builds, definitions, plans and suites, piped IDs, `$null` and empty
input still reach the same parameter sets or errors as in 0.4.0.

### Existing tests changed

| Test | Change |
| --- | --- |
| `StripTests.HistoryKeepsAllSixStatesTitlesCurrentMarkerAndToolkitLinksWithoutALegend` | Renamed from `…LegendCurrentMarker…`. It requires no legend and one tooltip per cell |
| `CompactReportTests.RunsWithoutDistinctPipelineNamesRenderOneUngroupedList` | Its runs now share one run name, since distinct run names now make groups |
| `TestBugResolutionTests` | The overview marker is matched as a link to bug 4003, and the card pattern allows attributes after `data-open-bug` |
| `TestFailureExport.Pester.ps1` | The `-WhatIf` and `-NoClobber` cases also check that no address is printed; the attachment link checks use the download route |
| Report goldens | The ten `tests/Fixtures/Reports/testfailures-*.html` files are regenerated for the new header, markers, links and history cells |

### Findings not fixed

| Finding | Reason |
| --- | --- |
| `[pscustomobject]@{ Id = 44 } \| Get-AdoBuild` and the same object piped to `Get-AdoTestSuite` stop with a raw `System.ArgumentNullException`. PowerShell converts the object to an `AdoBuildDefinition` or `AdoTestPlan` whose `CollectionUri` is null | Present in 0.4.0: the `InputObject` binding and its collection check are not part of this release's changes |
| The advisory `What if:` line in the `verify` output | Unchanged from the [0.4.0 findings](release-0.4.0.md#findings-not-fixed) |

### Known limitations

| Limitation | Notes |
| --- | --- |
| All branches | With `defaultBranch` set, no parameter asks `Get-AdoBuild` or `Get-AdoBuildTestFailure` for builds of every branch. Use a profile without `defaultBranch`, or a connection made with `Connect-Ado -CollectionUrl` |
| Connection snapshot | The values are copied when the connection is made. `Set-AdoProfile` does not change the current connection; run `Connect-Ado` again, or `Disconnect-Ado` to let the next command reconnect with the default profile |
| Other projects | The defaults do not depend on the project. With `-Project`, the profile's definition name, branch, plan and suite are used in that project, and a definition piped into `Get-AdoBuild` from another project still gets the profile's branch |
| Numeric definition names | `Get-AdoProfile` shows the definition name `'45'` and the ID `45` the same way. `DefaultBuildDefinition.Name` and `.Id` tell them apart, and the file keeps a name as a JSON string |
| Run names on Server 2020 | The run names and the ` (attempt N)` retry suffix are unverified (V-19). If a job retry publishes its run under another name without that suffix, the retry gets its own group, and a test that passes only on the retry is reported `Failed` instead of `Flaky` |
| Attachment downloads | The content route is the one the downloader already uses (V-23). Opening it from a local report under the work browser policy is unverified (V-27) |
| Live scripts and defaults | `Triage.Live.ps1` and `Smoke.Live.ps1` resolve `ADOTOOLKIT_LIVE_DEFINITION` with `Get-AdoBuild -Latest`, which now applies the profile's `defaultBranch` |
| Live behavior | Parameter binding and requests are covered by the fake server only. The build and test plan routes are unchanged from 0.4.0; the 0.4.0 bug routes are still unconfirmed at work (V-30) |
| Pipeline names on Server 2020 | Unchanged from the previous release: stage, phase and job names are unverified (V-19) |

## Work-PC Live checks

Install the candidate 0.5.0 package on the work PC and run each check in a fresh
PowerShell process. The rules from the [0.2.0 audit](release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass. Checks 1 to 4 and 9 use the profile without the new defaults.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | Before changing anything, record `Get-FileHash` of `%APPDATA%\AdoToolkit\config.json`, then run `Get-AdoProfile` and `Connect-Ado` | The hash is unchanged and no unknown-key warning appears: 0.5.0 reads the 0.4.0 file without rewriting it |
| 2 | Pick a build whose test runs of one job have different names, for example one per language, and a build with a retried test job if one exists. Run `$set = Get-AdoBuildTestFailure -BuildId <id>`, `$set.Runs \| Format-Table Id, Name, StageName, PhaseName, JobName, PipelineAttempt`, then export it in English and French | One group and overview column per run name, labelled with the run name; a retry run named `… (attempt N)` stays with its first run. A test that failed in every attempt of one run is `Failed`. Note any retry whose name differs in another way |
| 3 | In that report, open an attachment link of a PNG or HTML file, of a sub-result if one exists, and an **Open bug** link | The browser downloads the original file with your Windows sign-in under the work browser policy (V-23, V-27), and the bug opens in its own project |
| 4 | Export once more from the console | The console shows one `file:///` line; opening it opens the same report |
| 5 | `Set-AdoProfile -Name <profile> -DefaultBranch develop -DefaultBuildDefinition Test_Plan`, then `Get-AdoProfile` | The list shows both values. The file differs from the saved copy only by the two new keys |
| 6 | `Connect-Ado`, then `Get-AdoBuild -Latest` and `Get-AdoBuild -Latest -Branch main` | The first returns the latest completed `Test_Plan` build on `develop`, the second on `main`, matching the build pages. `Test_Plan` resolves to exactly one definition |
| 7 | `Get-AdoBuildTestFailure -Result Failed \| Export-AdoBuildTestFailure -Open` | The report is for the latest failed `Test_Plan` build on `develop` |
| 8 | Add `-DefaultTestPlanId` and `-DefaultTestSuiteId` for a plan you use, reconnect, then run `Get-AdoTestSuite -Recurse` and `Get-AdoTestCase -Recurse \| Measure-Object` | The suite tree and case count match the suite in Azure DevOps. `Get-AdoTestSuite -PlanId <other plan>` starts from that plan's root suite |
| 9 | The [previous release's checks](release-0.4.0.md#work-pc-live-checks), including V-30, with `ADOTOOLKIT_LIVE_PROFILE` naming a profile without `defaultBranch` | Still pending |

## Local validation and developer handoff

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0. All five stages pass: 55 PowerShell files linted, 174 tooling Pester tests, 142 configuration files, hook layout and the product check. Its only warning is the advisory `What if:` line described in the [0.4.0 findings](release-0.4.0.md#findings-not-fixed) |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as the release workflow runs it | Exit 0 with the exact module pins in `tools/BuildModules.psd1` |
| Core tests in the final gate | 978 passed under en-US and 978 under fr-CA, with no failures or skips in either TRX report. 24 are new with the report changes: 14 in `NamedRunGroupingTests` and 10 in `ReportRefinementTests`. 25 are new in `ConfigurationStoreTests`: valid, absent and `null` defaults, 19 invalid values, unknown-key warnings, a byte-for-byte save of a file without the new keys, and a round trip that sets, changes and clears the defaults while every other setting stays |
| Product Pester tests against the staged module | 124, all passed: the gate fails on any failed, skipped or not-run test, and Pester discovery of the same tree finds 124. 12 are new in `Profiles.Pester.ps1`: `Set-AdoProfile` and `Get-AdoProfile`, profile and URL connections, an invalid value in the file, and for each of `Get-AdoBuild`, `Get-AdoBuildTestFailure`, `Get-AdoTestCase` and `Get-AdoTestSuite` an explicit parameter over the profile, the profile value when it is omitted, and the 0.4.0 behavior without it, checked on the request lines of the fake server. 3 are the report address cases in `TestFailureExport.Pester.ps1` |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version` | Prints `0.5.0`; the staged manifests in `artifacts/verify/AdoToolkit/0.5.0/` and `artifacts/AdoToolkit/0.5.0/` report `ModuleVersion` 0.5.0 |
| `tools/package/Publish-AdoToolkitPackage.ps1` | Exit 0; restores, builds and stages `AdoToolkit/0.5.0` |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.5.0-win-x64.zip -ModuleVersion 0.5.0 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.5.0, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the eight expected files under `AdoToolkit/0.5.0/`, and its English and French help include this review's help changes. The portable ZIP has the same eight files under `module/`, the launcher, `README.txt`, `bundle.json` (module 0.5.0, PowerShell 7.6.6, win-x64, PowerShell SHA-256 equal to the pin) and 658 runtime files, 670 files in all. Each `.sha256` file matches its ZIP. The installer is byte-identical to its source |

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.5.0-win-x64.zip` | 110276695 | `b2c54905752c82478acec7c7cec9087da9c6bc528b2d640e8a9f7ba50e9e3f90` |
| `AdoToolkit-0.5.0.zip` | 669801 | `3c6bc5e08eae788a7df0954ad1297c50a9351b810b10efdbf9d02bf380521825` |
| `Install-AdoToolkit.ps1` | 12926 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes.

No Live script, commit, tag, push or GitHub release publication was performed. The
developer owns the rest of the release:

1. Review the final diff: the profile keys in `ConfigurationStore`, the fallbacks in
   `AdoCmdletBase` and the four commands, the parameter set changes, the new
   `Set-AdoProfile` parameters and `Get-AdoProfile` list view, the named-run grouping in
   `PipelineGrouping`, the report changes and regenerated goldens, `FileUris`, and the new
   tests and help.
2. Commit the reviewed 0.5.0 changes, including the four new files and `FileUris.cs`.
3. Create `v0.5.0` on that commit and push the commit and tag when ready to publish.
4. Watch the release workflow and check its five uploaded assets, version and checksums.
   CI builds its own assets, so compare each checksum with CI's own `.sha256` files;
   archive metadata can differ from a local build.
5. Run the work-PC checks above with the published package and record any inconclusive
   coverage.
