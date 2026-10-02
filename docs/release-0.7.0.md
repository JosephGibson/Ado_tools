# AdoToolkit 0.7.0 release notes

Prepared on 2026-10-01 from the uncommitted changes on the branch `0.7.0`, which starts at
`main` after the 0.6.5 release; the files there are those of the tag `v0.6.5`. 0.7.0 changes
the failed-test report and the two commands behind it, `Get-AdoBuildTestFailure` and
`Export-AdoBuildTestFailure`. The Test Case report and the other cmdlets are unchanged. This
is an offline review. No Azure DevOps Server connection or `tests/Live/*.Live.ps1` run was
attempted, so nothing below is confirmed live. In particular, the concurrent requests were
exercised and timed against the synthetic server of the tests only. The areas the README
lists as confirmed are still connections, projects, builds and test runs.

## What changed

0.7.0 adds no cmdlet and no parameter. It adds one configuration key,
`testResults.maximumConcurrentRequests`. The JSON report schema and the seven-file package
layout are unchanged.

### Retrieval with `Get-AdoBuildTestFailure`

| Area | 0.7.0 behavior |
| --- | --- |
| Concurrent requests | The requests of one stage (result listings, result details, attachment lists, bug metadata) are sent together, at most `testResults.maximumConcurrentRequests` at a time: 6 by default, 1 to 16. 0.6.5 sent each request after the previous one was answered. With `1`, 0.7.0 does the same |
| Run history | Read while the main requests run. The earlier builds are read one after another and the test runs of one build together. `testResults.maximumHistoryRequests` limits it as before |
| Result order | Results and diagnostics are combined in input order; history diagnostics come last. Near the history budget, cancelled requests can change the budget left for older builds; see Findings not fixed. When the main requests and history both fail, the main error is reported. Authentication, authorization and cancellation still stop the command |
| Fewer requests | The listing of a test run ends without the request for an empty page when the run is completed, its last page is short and the results read equal its `totalTests`. In every other case the empty page is requested as before. When several builds are piped in, a Test Case, a Bug category and the state categories that were already read are not requested again |
| Closed bugs | `Bugs` holds the open bugs of a test and the bugs that could not be read. A bug whose state is in the Completed or Removed category is left out; 0.6.5 listed it with `IsOpen` false. `AssociatedBugIds` of an attempt stays the server's own list |
| Progress | The progress of the history is shown after the progress of the main requests, as in 0.6.5 |

### Attachments with `Export-AdoBuildTestFailure`

| Area | 0.7.0 behavior |
| --- | --- |
| Default selection | JSON and text files of at most `testResults.maximumInlineJsonBytes` (256 KiB), which are the ones the report can show, are downloaded from every test run inside the attachment window. Larger ones are downloaded from the latest run only, as before. 0.6.5 downloaded from the latest run only, whatever the size |
| A file without a declared size | From a run other than the latest: downloaded up to that limit, and dropped without a warning when it turns out larger |
| `-AllRunAttachments` | Also downloads the larger JSON and text files of every run inside the window |
| Order | The files of the latest run first, then those of the older runs, each in report order, so that a total limit runs out on the older runs. The previews shown in the report are chosen in the same order within `testResults.maximumInlineTotalBytes`. 0.6.5 followed report order |
| Concurrent downloads | Up to `testResults.maximumConcurrentRequests` files are read at the same time. The outcome of each file is still decided in the order above, so the limits, the warnings, the statuses and the files are those of a download of one file at a time. A file that was read ahead and that a limit then excludes is discarded |
| Connection | The default export sends requests whenever a run inside the window has a small JSON or text file, not only when the latest run has one. An export with nothing to download still needs no connection |

### Failed-test report

| Area | 0.7.0 behavior |
| --- | --- |
| Error text | Every attempt shows its own full error message and stack trace. 0.6.5 replaced a text that an earlier attempt already had with a reference to that attempt, which could not be searched or copied there |
| Open bugs view | New, after By error: one entry per bug with its number, title and state, then a row for each test linked to it and whether the link comes from a test result, the Test Case or both. The bug with the most tests comes first; a bug that could not be read comes last, marked Not read. The tab shows the number of bugs |
| Bugs in a card | The open bugs with title and state, without the Open mark: every listed bug is open. A bug that could not be read is marked Not read. An attempt links an associated bug only when it is among the bugs of the test |
| Test runs | The table of the Runs and history view shows, per run: duration as `h:mm:ss`, tests, passed, failed (from the per-outcome statistics, empty without them), the number of reported tests, and the attachments as "n listed, m downloaded" or "Outside the window". The state column appears only when a run is not completed. The attempt numbers always have three places. The latest run is marked, runs outside the window are greyed, and the note on the window is under the table |
| History chart | Drawn at its natural size with the build number under each bar, the failed count above it and a legend. A small count keeps a visible segment. The table of the same numbers is always shown below it; 0.6.5 kept it collapsed |
| History by test | New table: one row per reported test, its outcome in each build of the history, and the number of builds in a row, ending with this one, in which it failed or was flaky |
| Footer | Generation time, toolkit version, server, collection and project are under every view; 0.6.5 showed them in the Runs and history view |
| Tables | By error and Open bugs have no stripes, which restarted in every group. Each table of tests has the legend of its marks above it. Test numbers end at the same place. The latest error has its whole first line as a tooltip |
| Links | A link inside the report keeps its color after a visit, and an attempt square keeps the color of its status. The mark of a link to Azure DevOps is smaller and muted |
| Header | The build result is a badge. Its color comes from a fixed list (`succeeded`, `partiallySucceeded`, `failed`, `canceled`, in any case); the text is what the server sent |
| Cards | Each label and value pair starts with a thin rule. In an attempt summary the status, the duration and the machine each have their own column, so that attempts line up. The title of a card is one heading level below the view heading, and the headings inside a card follow, down to level six; the look is the same |
| Diagnostics | One line per diagnostic: severity, code and message, errors first, then warnings, then information, each in the order of arrival. The heading shows the number of each severity |
| Sizes | A size above 1,024 bytes is shown in KB or MB with one decimal, and the exact number of bytes is its tooltip |
| Keyboard | `j` and `k` move row by row in the Open bugs view, where a test can have a row under several bugs |

Report labels, English and French:

| Change | English | French |
| --- | --- | --- |
| New | Open bugs | Bogues ouverts |
| New | No open bug is linked to a test in this report. | Aucun bogue ouvert n’est lié à un test de ce rapport. |
| New | Linked through | Lié par |
| New | Not read | Non lu |
| New | Test runs | Séries de tests |
| New | Reported tests | Tests signalés |
| New | {0} listed, {1} downloaded | {0} répertoriées, {1} téléchargées |
| New | Latest run | Dernière série de tests |
| New | History by test | Historique par test |
| New | Consecutive failures | Échecs consécutifs |
| New | {0:N1} KB, {0:N1} MB | {0:N1} Ko, {0:N1} Mo |
| Reworded | Attachments are listed for runs started on or after {0}. Left out from older runs: {1}. | Les pièces jointes sont répertoriées pour les séries de tests commencées le {0} ou après. Omises des séries plus anciennes : {1}. |
| Removed | The three "Same … as attempt {0}" notes, Downloaded, Listed, Open | Their French forms |

### Configuration

| Key | 0.7.0 behavior |
| --- | --- |
| `testResults.maximumConcurrentRequests` | New. `6` by default, 1 to 16; a value outside that range is a configuration error. Saving the configuration writes the key. A file without it uses the default |
| `testResults.maximumInlineJsonBytes` | Also the largest JSON or text file that the export downloads from test runs other than the latest |

### Help and guides

| Area | Change |
| --- | --- |
| Help topics | `Get-AdoBuildTestFailure` and `Export-AdoBuildTestFailure`, in both cultures, describe the concurrent requests, the closed bugs, the Open bugs view, the default downloads, the download order and the reworked Runs and history view |
| Guides | `build-report.md`, `pipeline-triage.md` and `configuration.md` describe the same changes; `getting-started.md` names the 0.7.0 files |
| Release notes | The 0.6.0 notes moved to `docs/archive/`, whose index now also resolves `V-33` |

### Tests and tooling

| Area | Change |
| --- | --- |
| Synthetic server | `tests/AdoToolkit.PowerShell.Tests/Support/FakeAdoServer.ps1` can answer by route instead of in a fixed order, with a latency and several workers, and records the highest number of requests it answered at once. Its default mode is unchanged |
| Measurement | `tests/AdoToolkit.PowerShell.Tests/Support/Measure-TestFailurePipeline.ps1` is new. It serves a generated build from the synthetic server and times the two cmdlets. It is run by hand; no runner collects it |
| Live check | `tests/Live/TestFailures.Live.ps1`: a comment only. Its bug count for V-30 now holds open and unread bugs, because the module returns no others |

### Measurement

The script above, against the synthetic server on the development PC, with its defaults:
one group of four test runs of 3,000 results each, 150 failing tests with two rerun
sub-results and one attachment per attempt, ten builds of history, 20 ms of latency per
request and 16 server workers. Each figure is the median of three runs. "0.6.5" is the
unchanged module, measured again in the same session.

| Cmdlet | Version and value | Requests | Wall time | Most requests at once |
| --- | --- | ---: | ---: | ---: |
| `Get-AdoBuildTestFailure` | 0.6.5 | 2,586 | 80,639 ms | 1 |
| | 0.7.0, 6 | 2,586 | 13,580 ms | 6 |
| | 0.7.0, 1 | 2,586 | 78,522 ms | 1 |
| `Export-AdoBuildTestFailure` | 0.6.5 | 300 | 10,206 ms | 1 |
| | 0.7.0, 6 | 1,200 | 6,844 ms | 6 |
| | 0.7.0, 1 | 1,200 | 37,846 ms | 1 |
| `Export-AdoBuildTestFailure -AllRunAttachments` | 0.6.5 | 1,200 | 39,395 ms | 1 |
| | 0.7.0, 6 | 1,200 | 6,889 ms | 6 |
| | 0.7.0, 1 | 1,200 | 39,343 ms | 1 |

Every attachment of the generated build is small, so the default export of 0.7.0 downloads
those of all four runs: four times the files of 0.6.5. With 2,500 results per run, the
last page of each run is short and 0.7.0 sends 2,546 requests instead of 2,586. The
report of this build is 9,587,562 bytes; 0.6.5 wrote 4,778,084 bytes, or 6,869,300 with
`-AllRunAttachments`. These figures say nothing about Server 2020; see V-33 below.

### Visible changes for 0.6.5 users

- Existing configuration files need no change.
- Public contract, output: `Bugs` of a failure from `Get-AdoBuildTestFailure` no longer
  contains closed bugs. `IsOpen` of a listed bug is `True`, or empty for a bug that could
  not be read; it is never `False`. A script that counted closed bugs through `Bugs` must
  read `AssociatedBugIds` and look the items up. `HasOpenBug` means what it meant.
- Public contract, configuration: the new key `testResults.maximumConcurrentRequests`, and
  the second meaning of `testResults.maximumInlineJsonBytes`.
- `Get-AdoBuildTestFailure` opens up to six connections to the server at once. Set the key
  to `1` to get the request order of 0.6.5.
- Diagnostics of the run history come after the other diagnostics of a set. With several
  builds piped in, an `UnresolvedTestCase` diagnostic is repeated in every set that has the
  Test Case, although the Test Case is requested once.
- The default export downloads more files and therefore needs a connection more often. With
  the key at `1` it can take longer than 0.6.5 did, because it downloads more.
- Attachments are downloaded latest run first. When the total limit is reached, the files
  that go without are those of older runs, not those that come later in the report.
- The report is larger when attempts repeat long error text: the fixture of 200 failures
  with 14 identical attempts each is 46.9 MB.
- The markup of the report changed: the views, the headings inside a card (one level
  lower), the diagnostics, the Runs and history view and the size text. The validation
  markers are unchanged (`data-failure-count`, one `article` per failure, `f-<n>` and
  `f-<n>-a<m>` anchors, `data-diagnostic`). Anything else that read the 0.6.5 markup must be
  revisited.
- Core types, for code that uses the assembly directly: `TestFailureQuery`,
  `TestResultOptions` and `TestFailureExportOptions` have the new properties
  `MaximumConcurrentRequests` (the first two) and `MaximumInlineJsonBytes` (the third). No
  cmdlet returns them.
- French report labels changed as listed above. Scripts must not match label text; error
  IDs and diagnostic codes are unchanged.

## Bug fixes

None. 0.7.0 changes behavior by decision; it corrects no defect that was reported against
0.6.5. The flaws of display that it removes, such as stripes that restarted in every group
and attempt squares that lost their color after a visit, are listed under "Failed-test
report" above.

The tests of the new behavior were written with each change. They were not run against the
0.6.5 source, with one exception: the measurement script, the routed synthetic server and
the figures of 0.6.5 above come from the unchanged module.

### Structural changes

| Change | Reason |
| --- | --- |
| `RequestGate` | One limit for all requests of an invocation. A request takes its place before its timeout starts and keeps it through its retries |
| `OrderedParallel` | Runs the requests of a stage within the limit and returns their results in input order. The failure with the lowest index is the one reported, and a cancellation by the caller wins. This is what keeps the result independent of the order of the answers |
| `RequestCounter` | The history spends from its own share of the request budget, so that requests in flight elsewhere cannot use it up |
| `TestFailureInvocationCache` | Also holds the Test Cases, the Bug category and the state categories, for builds piped into one command |
| `AttachmentSelection` | Says which runs give every JSON and text file and which give the small ones only. The export plan carries it in place of a list of runs |
| `AttachmentDownloader` | Reads files ahead within the limit and settles them strictly in order with the rules of the sequential download |
| Styles in `test-failures.css` only | `report-base.css` and the markup of the Azure DevOps link mark are shared with the Test Case report, which this release does not change. Every change of style is a rule of the failed-test report |
| Decisions of the archived specification superseded | By the owner's direction: §1.4 and §15.9 (every request sequential), §6.4 (the listing always ends on an empty page), §15.13 (download in report order), the latest-run-only default of 0.2.0 and 0.3.0, the closed bugs listed since 0.4.0, and the reference to an earlier attempt for repeated error text |

### Existing tests changed

| Test | Change |
| --- | --- |
| `TestBugResolutionTests` | Closed bugs are expected to be absent from the set and the card. One scenario routes only the bug it asks for, and an open bug in a project named `..` keeps the coverage of dot segments in links. The card assertions expect no Open mark and the Not read mark |
| `TestFailureRetrievalTests`, `RetrievalRequestBoundTests` | Request counts and positions follow the skipped empty page: 11 requests where there were 13, and bounds of 18 and 11 |
| `RetrievalCancellationTests`, `AttachmentDownloaderTests` | Tests that depend on the order of requests set the value to 1 |
| `ConfigurationStoreTests` | The complete configuration has the new key |
| `LatestRunAttachmentTests` | A small-file limit of 2 bytes keeps its files "large", so that the test still covers the latest-run rule; expected download orders and the window note follow the new rules |
| `CompactReportTests` | The size budget of the report of 100 failures is 27 MB instead of 7 MB: the report is 26.4 MB now that every attempt holds its text |
| `StructuralCompletenessTests` | No exception for a referenced text; the exact size is expected as the text or as its tooltip |
| `TestFailureReportModelTests`, `ReportRefinementTests`, `TestFailureExporterTests`, `RunHistoryChartTests`, `ThemeContrastTests` | Assertions follow the new markup: no link to a closed bug, the Not read mark, the size text, the history table without `details`, the classes of the attempt summary |
| `TestFailureReportFixture` | The closed bug is gone from the fixture, because retrieval can no longer produce it |
| `FakeHttpMessageHandler`, `CapturingLog`, `AttachmentFixture` (test doubles) | Safe for concurrent use; the handler records the highest number of requests in flight |
| `TestRuns.Pester.ps1`, `TestFailureExport.Pester.ps1`, `Usability.Pester.ps1` | Tests that pin the order of requests write a configuration with the value 1. Request counts follow the skipped empty page and the cache: 11, 12 and 21 where there were 13, 14 and 27. Two export tests were renamed for the large-file rule, and the test of an export without a connection now uses large files |
| Report goldens | The ten `testfailures-*.html` files were regenerated at each of the five changes of the report. No other golden changed |

### Findings not fixed

| Finding | Reason |
| --- | --- |
| How Server 2020 answers six requests at once is unknown | V-33. Only the work PC can show it. The value `1` restores the behavior of 0.6.5 |
| The speed was measured against the synthetic server only | Same reason. The figures show that the requests overlap, not what the server gains |
| With the value `1`, the default export is slower than in 0.6.5 when older runs have small files | It downloads them, by decision. `-SkipAttachments` downloads nothing |
| A report of many retries with long error text is large | By decision: every attempt holds its text. The size guard pins 46.9 MB for 200 failures of 14 attempts |
| When a history build fails for a reason other than the budget, the result listings of its other runs are cancelled, so the budget that build used can differ from one run to the next when the budget is nearly spent | The result is the same in every case where the budget is not reached. Reading the runs of a build one after another would remove it and the gain |
| The lines of the Diagnostics view use the CSS subgrid | Without it the three parts of a line are stacked, and still readable |
| The findings of 0.6.5 and 0.6.0 | Unchanged; see the [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes |

### Known limitations

| Limitation | Notes |
| --- | --- |
| Concurrent requests (V-33) | Unconfirmed at work: that Azure DevOps Server 2020 answers up to six requests of one Windows-authenticated user at the same time without refusing or delaying them, and that each connection signs in once. If it does not, set `testResults.maximumConcurrentRequests` to `1`. No Live script covers it; the check is by hand, below |
| Empty page | The request is skipped only when the run reports `totalTests` and the results read equal it. A run without that number, or still in progress, costs one more request, as in 0.6.5 |
| Open bugs view | Lists the bugs of the tests in the report. Flaky tests, and their bugs, are in it only with `-IncludeFlaky` |
| Sizes | KB and MB are multiples of 1,024 bytes. The tooltip with the exact size needs a pointer |
| Files without a declared size | From an older run they are read up to the small-file limit before they are dropped, so a large one costs that much transfer |
| The script of the report | Loaded in Microsoft Edge only, from the rendered fixtures. The filters, the keys and copying were not exercised again for this release |
| Limits carried over | The [0.6.5](archive/release-0.6.5.md#known-limitations) and [0.6.0](archive/release-0.6.0.md#known-limitations) known limitations still apply, with V-31, V-32 and V-02 |

## Work-PC Live checks

Install the 0.7.0 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | Record `Get-FileHash` of `%APPDATA%\AdoToolkit\config.json`, install 0.7.0, then run `Get-AdoProfile` and `Connect-Ado` | The hash is unchanged, no warning appears, and `Get-Module AdoToolkit -ListAvailable` shows 0.7.0 with seven files in its folder |
| 2 | V-33: `Measure-Command { $set = Get-AdoBuildTestFailure -BuildId <id> }` for a build with several test runs and failed tests, with the default configuration. Then add `"maximumConcurrentRequests": 1` under `testResults` in the configuration file and repeat in a new process. Remove the key afterwards | Both runs end without an error or a warning that names a request. `FailedCount`, `FlakyCount`, the number of failures, `Status` and the diagnostic codes are equal. An authentication error, a refused or throttled request, or different counts decide V-33 against the default. Report the error ID if there is one, the counts and the two durations, nothing else |
| 3 | `$set \| Export-AdoBuildTestFailure -Open` for the same build, with runs inside the attachment window | The attachment folder holds the small JSON and text files of every such run. The Runs and history view shows "n listed, m downloaded" for each run and marks the latest one. No warning other than a size limit |
| 4 | The same report in the browser used at work, in English and French; then the print preview | The Open bugs view lists the bugs that Azure DevOps shows for these tests and no closed one. The lines of the Diagnostics view have three aligned columns. The tooltips of a long error and of a size appear. The console shows no Content Security Policy error. The print preview shows every view on a light page |
| 5 | `TestFailures.Live.ps1` with the 0.7.0 package | The verdicts of V-19 to V-25 and V-30 as before. The bug count of V-30 holds open and unread bugs only |
| 6 | Checks 2 to 9 of the [0.6.0 notes](archive/release-0.6.0.md#work-pc-live-checks), with the 0.7.0 package | As listed there: V-31 and V-32, V-02, V-01, V-03, the `-Open` warning, custom fields, Markdown line breaks, and the 0.5.0 checks. All still pending |

## Local validation and developer handoff

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0. All seven stages pass without a warning: 61 PowerShell files linted, 254 tooling Pester tests, 147 configuration files, 82 Markdown files, the hook and skill layout, 2 workflow files linted with actionlint, and the product check. The session that ran it had started before actionlint was installed, so the folder of actionlint was put on its `PATH` first |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as both workflows run it | Exit 0 with the exact module pins in `tools/BuildModules.psd1` |
| Core tests in the final gate | 1360 passed under en-US and 1360 under fr-CA, with no failures or skips. 151 are new: 38 in `StylingTests`, 23 in `RunsViewTests`, 20 in `RetrievalConcurrencyTests`, 19 in `ParallelDownloadTests`, 14 in `SmallAttachmentTests`, 10 in `OrderedParallelTests`, 7 in `BugsViewTests`, 6 in `RequestGateTests`, 2 in `ErrorTextTests` and 12 in existing classes |
| Product Pester tests against the staged module | 149, all passed. 4 are new: two cases of the concurrent retrieval and download, at the values 6 and 1, and two of the default downloads |
| Tooling Pester tests | 254, all passed. None is new |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `0.7.0` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.7.0` with seven files |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.7.0-win-x64.zip -ModuleVersion 0.7.0 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.7.0, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.7.0/`. The portable ZIP has the same seven files under `module/`, the launcher, `README.txt` and `bundle.json` (module 0.7.0, PowerShell 7.6.6, win-x64, PowerShell SHA-256 equal to the pin), 669 files in all. Each `.sha256` file holds the hash of its ZIP. The installer is byte-identical to its source |
| The rendered reports | After each change of the report, the ten fixtures were rendered in English and French. Headless Microsoft Edge took pictures of the Overview, Open bugs, Details, Runs and history and Diagnostics views; the script ran in them, as the selected view and an attempt opened from its address show |
| The measurement above | Run three times per row, on the development PC, with nothing else running |

Not run: the installer against the 0.7.0 ZIP, and the workflows, which only GitHub runs.

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.7.0-win-x64.zip` | 110356907 | `8097aa1d0b4355b5ae19af564eadc46be5d8a6475e849c1104c8888fdcb7313c` |
| `AdoToolkit-0.7.0.zip` | 749993 | `1eaa20e17fedb17121eb91e982a85d6576ac4543936365db18f36f8a9c589e95` |
| `Install-AdoToolkit.ps1` | 12880 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes.

No Live script was run, and no tag or GitHub release exists for 0.7.0. Every change is
uncommitted on the branch `0.7.0`. The developer owns the rest of the release:

1. Review the rendered failed-test reports in English and French, and the French labels
   listed above, which are proposals: `tests/Fixtures/Reports/testfailures-*.html`. A golden
   holds a placeholder in place of the script; to see a report as a browser shows it, export
   one with `Get-AdoBuildTestFailure … | Export-AdoBuildTestFailure -Open`.
2. Run the first command of the handoff. It commits every change, creates the tag `v0.7.0`
   and pushes the branch and the tag together, which publishes the release.
3. Run the second command and create the pull request on the page it opens. Merge it when
   the Verify check passes.
4. Watch the release workflow and check its five uploaded assets, version and checksums. CI
   builds its own assets, so compare each checksum with CI's own `.sha256` files; archive
   metadata can differ from a local build. If the run does not publish, `docs/tooling.md`
   lists the recovery for each cause.
5. Run the work-PC checks above with the published package and record any inconclusive
   coverage.
