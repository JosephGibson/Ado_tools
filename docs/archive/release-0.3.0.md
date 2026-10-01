# AdoToolkit 0.3.0 release notes

Prepared on 2026-09-21 from the pending changes in `src/`, `tests/`, `tools/`, `docs/` and
`.github/workflows/`. This is an offline review. No Azure DevOps Server connection or
`tests/Live/*.Live.ps1` run was attempted, so nothing below is confirmed live. The areas
the README lists as confirmed are still connections, projects, builds and test runs.

## What changed

### Failed-test report

The report produced by `Export-AdoBuildTestFailure` was rebuilt for builds with 100–200
failed tests and up to 14 attempts each.

| Area | 0.3.0 behavior |
| --- | --- |
| Layout | Tabs for Overview (one table row per test), By error (rows grouped by their latest error, ignoring numbers and GUIDs), Details (one card per test), Runs and history, and Diagnostics when something is missing. With scripts blocked, the views are stacked on one page |
| Attempts | Every stage or job group, attempt and attachment preview starts collapsed. When an error message or stack trace repeats an earlier attempt's, the attempt links to that attempt instead of repeating the text |
| English and French | Attempts are grouped by the stage, job and job-instance names in each test run's `pipelineReference`. The overview has one status column per group, such as `✕ 7/7` followed by a square per attempt. Runs without distinct names stay in one list |
| Test Case | The Test Case number links to the work item from the overview row and from the card heading |
| Search | Matches every word against the whole card, including collapsed attempts, attachment previews, stage and job names and run fields. `12345` and `#12345` both find Test Case 12345. **Failing in** narrows the list to tests that failed in a group, or only in that group |
| Flaky tests | Left out unless `-IncludeFlaky` is used; the header still counts them |
| Attachments | `-AttachmentWindowDays` (1–365, default 7) keeps attachments only for runs started inside the window. Every attempt stays |
| Downloads | Only JSON and text are downloaded: from the latest run by default, from every run in the window with `-AllRunAttachments`, and nothing with `-SkipAttachments`. `.txt` and `.log` files are a new text kind and are saved as `.txt`. PNG, HTML and other files stay links to their Azure DevOps result |
| Size | A synthetic build with 200 failures × 14 attempts, each attempt with 4 attachments, went from 91.5 MB to 13.2 MB. With 100 × 14 it went from 45.8 MB to 6.6 MB, and a test keeps that case under 7 MB |

### Other changes

| Area | Change |
| --- | --- |
| Classification | The deciding outcome takes each pipeline group's last attempt. A test that fails in every French attempt stays `Failed` even if an English retry passes last; it is `Flaky` only when every group ends with a pass. This changes `FailedCount`, `FlakyCount`, `Classification` and the current build's history cell from `Get-AdoBuildTestFailure` |
| `Get-AdoTestRun` | Runs carry `StageName`, `PhaseName` (a YAML job) and `JobName` (a matrix or parallel instance, `__default` when there is none) |
| Test names | `ShortName` no longer breaks data-driven names at dots inside the arguments. `Tests.Login("a.b")` is now `Login("a.b")`, not `b")` |
| Configuration | New `testResults.maximumInlineTotalBytes` (8 MiB) caps inlined attachment text per report. `maximumInlineJsonBytes` now applies to text too |
| Portable release | New `AdoToolkit-<version>-win-x64.zip` asset that bundles the pinned PowerShell 7.6.6 runtime with a `Start-AdoToolkit.cmd` launcher. The release workflow checks the finished bundle offline with `Test-AdoToolkitPortable.ps1` and publishes five assets |

### Visible changes for 0.2.0 users

- Reports leave flaky tests out by default. Add `-IncludeFlaky` for the 0.2.0 behavior.
- Runs that started more than 7 days before the export lose their attachments in the report.
- PNG screenshots and HTML attachments are no longer downloaded or shown as thumbnails.
  They are listed with a link to their Azure DevOps result.
- A `.log` attachment is saved as `.txt`, so a browser shows it instead of downloading it.
- When runs carry stage or job names, classification is per group, as described above.

## Final review findings and disposition

| Finding | Disposition | Files |
| --- | --- | --- |
| Short names and the overview's class name were cut at the last dot, including dots inside a data-driven test's arguments | Fixed. Separators inside the argument list are ignored; the class name uses the same rule | `src/AdoToolkit.Core/TestRuns/AttemptGrouper.cs`, `HtmlTestFailureRenderer.cs`; `tests/AdoToolkit.Core.Tests/TestRuns/AttemptClassificationTests.cs` |
| Sticky table headers had no effect inside their horizontal scroll box | Fixed. On wide screens the tables no longer scroll horizontally, and their headers stay below the top bar | `src/AdoToolkit.Core/Reporting/Assets/test-failures.css`, `test-failures.js` |
| The runs table said "Downloaded" for runs whose downloads had all failed or been refused | Fixed. The label now requires at least one saved local file | `HtmlTestFailureRenderer.cs` |
| French attempt summaries repeated the server outcome after the badge ("Échec Failed") | Fixed. A plain `Passed` or `Failed` is shown by the badge alone; other outcomes such as `Blocked` still appear | `HtmlTestFailureRenderer.cs`; `StructuralCompletenessTests.cs` |
| The runs table's Group column repeated the stage and job columns, and each card ended with a redundant link back to the overview | Removed. Runs of one group are now listed together, in attempt order. The unused `TestReportGroup` resource was removed | `HtmlTestFailureRenderer.cs`, `test-failures.css`, `Strings.resx`, `Strings.fr.resx`, `AdoMessage.cs` |
| Durations always showed three decimals ("31.200 s") | Durations now show at most three decimals ("31.2 s") | `Strings.resx`, `Strings.fr.resx` |
| The header's "Finished: …" had no space before the colon in French | Dropped the colon in both languages | `HtmlTestFailureRenderer.cs` |
| A print rule still named a class that no longer exists | Removed | `test-failures.css` |
| Current-release metadata and examples still said 0.2.0 | Updated to 0.3.0. Synthetic test versions in the tooling tests are unchanged | `Directory.Build.props`, `README.md`, `docs/guides/getting-started.md`, `docs/tooling.md`, `tools/package/Install-AdoToolkit.ps1` |

The golden reports in `tests/Fixtures/Reports/` were regenerated after these changes. The two
`testfailures-grouped` goldens are new in this release.

### Known limitations

| Limitation | Notes |
| --- | --- |
| Pipeline names on Server 2020 | The stage, phase and job names are defined in the REST 6.0 contract, but their presence in Server 2020 responses is unverified (V-19). Without them the report falls back to one list and classification matches 0.2.0 |
| Default download with separate language stages | By default only the build's latest run is downloaded, so only that run's language gets downloaded files. Use `-AllRunAttachments` to download both |
| Size of the 200 × 14 case | At 13.2 MB it is above the 10 MB planned. About 4 MB is the 11,200 per-attachment result links, which stay because attachments are required to be links |
| Report script | By decision, the script's views, search and keyboard handling have no automated test. The tests check that the searchable text is inside each card, and headless Edge screenshots were reviewed by hand |

## Azure DevOps Server 2020 contract

No route, api-version or paging change. `TestRunsList` (`{project}/_apis/test/runs`,
`6.0`, `includeRunDetails=true`) now also reads `pipelineReference.stageReference.stageName`,
`phaseReference.phaseName` and `jobReference.jobName`. All three are optional. Empty values
and `__default` count as absent, and missing names produce no diagnostic. Attachment content
requests are the same route as in 0.2.0, but are now limited to JSON and text attachments.

## Work-PC Live checks

Install the candidate 0.3.0 package on the work PC and run each script in a fresh
PowerShell process. The rules from the [0.2.0 audit](release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | `TestFailures.Live.ps1` with `ADOTOOLKIT_LIVE_TEST_BUILD_ID` set to a build that runs English and French in separate stages or jobs | The `NOTE V-19 PIPELINE_NAMES` line: `STAGES` or `PHASES` of 2 or more confirms grouping. Counts are printed, never names |
| 2 | Export that build four times, to separate folders: with defaults, with `-IncludeFlaky`, with `-AllRunAttachments`, and with `-AttachmentWindowDays 30` | Groups and their status columns match the pipeline. Flaky tests appear only with the switch. Downloads contain only `.json` and `.txt` files. Older runs show as outside the window. Test both languages, search (a Test Case ID, a message fragment, a machine name), the **Failing in** filter, printing, and links under the work browser policy |
| 3 | The portable ZIP: unblock it, extract it and start `Start-AdoToolkit.cmd` | The console opens with AdoToolkit 0.3.0 loaded under the work PC's execution policy and application control. `Test-AdoConnection` passes |
| 4 | The remaining 0.2.0 checks: `TestCase`, `Bulk`, `Triage`, `Shape`, `Smoke` and `Connection` | Unchanged by this release; still pending |

## Local validation and developer handoff

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify` with `ADOTOOLKIT_RELEASE_BUILD=1` | Exit 0. All five stages pass: 55 PowerShell files linted, 166 tooling and product Pester tests, 137 configuration files, hook layout and the product check |
| Core tests in the full gate | 900 passed under en-US and 900 under fr-CA, with no failures or skips in either TRX report |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version` | Prints `0.3.0` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoBuild` | Exit 0; stages `AdoToolkit/0.3.0` from the verified build |
| `tools/package/New-AdoToolkitRelease.ps1` with the pinned PowerShell 7.6.6 archive and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1` | Passes: AdoToolkit 0.3.0, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the eight expected files under `AdoToolkit/0.3.0/`. The portable ZIP has the same module files under `module/`, the launcher, `README.txt`, `bundle.json` and the runtime. The installer is byte-identical to its source |

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.3.0-win-x64.zip` | 110247109 | `9b3908f4dba44b0be406703bb2c02b3dbbd5b39851e38b54e3758957442fa8eb` |
| `AdoToolkit-0.3.0.zip` | 640221 | `59830fd1d6d3e9fd5bc74fb09d9b1a221bff72bc739d02fd8b48b7724349f491` |
| `Install-AdoToolkit.ps1` | 12926 | Same file as `tools/package/Install-AdoToolkit.ps1` |

No Live script, commit, tag, push or GitHub release publication was performed. The
developer owns the rest of the release:

1. Review the final diff, including the report rework, the portable release, the
   regenerated goldens and the new tests. Run the work-PC checks above and record any
   inconclusive coverage.
2. Commit the reviewed 0.3.0 changes.
3. Create `v0.3.0` on that commit and push the commit and tag when ready to publish.
4. Watch the release workflow and check its five uploaded assets, version and checksums.
   CI builds its own assets, so compare each checksum with CI's own files; archive
   metadata can differ from a local build.
