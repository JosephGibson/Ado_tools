# AdoToolkit 0.8.0 release notes

Prepared on 2026-10-03 from the uncommitted changes on the branch `0.8.0`, which starts at
`main` after the pull request of 0.7.10 was squash-merged as `0ece91b`. The work followed the
[0.8.0 plan](archive/plans/0.8.0-test-failure-report.md), phases 0 to 8. Its section 3 holds the
measurements, its section 7 the developer's decisions, and its section 9 the evidence of each
phase. 0.8.0 makes the failed-test report faster to gather and quicker to read: a switch that
leaves out the attachment lists, compressed responses, a planned history read, timing lines,
and a denser report with new and recurring failures marked. It also corrects six defects
that the report has in 0.7.10. This is an offline review. No Azure DevOps Server connection or
`tests/Live/*.Live.ps1` run was attempted, so nothing below is confirmed live. The areas the
README lists as confirmed are still connections, projects, builds and test runs. Every
timing below comes from the synthetic server of the Measure script: it ranks the costs and
says nothing about Server 2020 (V-33).

## What changed

0.8.0 adds one parameter and one output property, and no cmdlet. One configuration default
changes. The configuration file format, the JSON report schema and the seven-file package
layout are unchanged. The IDs B1 to B7 and H1 to H3 point to the tables under Bug fixes.

### Failed-test retrieval

| Area | 0.8.0 behavior |
| --- | --- |
| `-SkipAttachments` | New switch of `Get-AdoBuildTestFailure`, in every parameter set. It leaves out the attachment lists, one request per failed result record and one per attempt or iteration sub-result, and nothing else: every other request is sent in the same order, and attempts have no attachments. The set's new `AttachmentsListed` property is `$false`; it is `$true` otherwise. In the synthetic Default scenario, 150 failing tests in 4 runs whose results each have two rerun sub-results, the switch takes retrieval from 2,586 requests to 786, and from 13.6 to 4.7 s at 20 ms latency |
| History budget | `testResults.maximumHistoryRequests` defaults to 500 instead of 400. Earlier builds are read newest first, one at a time, while the result pages of the newer builds are read. A build's run list counts as it is sent. Its result pages are then counted from the `totalTests` of its runs and set aside at once, all or none. A build whose pages do not fit is unavailable, with every older build and one `HistoryLimitExceeded` diagnostic, before any of its pages is sent. Requests set aside are never given back, even when a page fails. So the builds kept no longer depend on response order or on `testResults.maximumConcurrentRequests`, apart from the requests that no plan foresees (see Known limitations) |
| History failures | When one listing of an earlier build fails, the other listings of that build run to their end instead of being cancelled; requests are cancelled only when the whole retrieval ends. The reads are judged newest first afterwards, so diagnostics keep their order and a fatal error still stops the command |
| History speed | When the history read decides the wait, it is 36 to 40% faster: the run lists and result pages of several builds share the request gate (synthetic, 2 failing tests: 2.67 to 1.60 s with 10 builds, 5.77 to 3.67 s with 30). When the main read fills the gate, as with many failing tests, the wall time is unchanged. The Wide scenario, 8 runs of 5,000 results, keeps all 10 builds at the default of 500 instead of 8 at 400 |
| `-Verbose` | One line per stage, in this order: the build, the test runs, the result listings, the failure details, the attachment lists, the Test Case links, the bugs and the run history, each with its requests and milliseconds. A last line gives the build, the failed or flaky tests found, the requests sent and the elapsed time of the whole command. With `-SkipAttachments`, the attachment line stays, with 0 requests |
| Console table | The default table of a failed test (`AdoTestFailure`) shows `LatestError`, the first line of the latest error as the report's tables show it, instead of `Storage`. The `Storage` property is unchanged |

### Requests of every cmdlet

| Area | 0.8.0 behavior |
| --- | --- |
| Compression | Every request accepts gzip and deflate. A server that does not compress answers as before; a compressed body is decoded before anything reads it, so the attachment limits count decoded bytes. On the synthetic server, a compressing server sends 92 to 97% fewer bytes, and on a 10 Mbit/s link `Get-AdoBuildTestFailure` takes 16.0 s instead of 31.6 s. Whether IIS on Server 2020 compresses JSON is unknown (V-35) |
| JSON reading | A response in UTF-8, or with no charset, is parsed as it streams in; any other charset, or a UTF-16 or UTF-32 byte order mark, is decoded whole as before. Error mapping is unchanged. The result listings and the history pages read only the eight fields of §15.9 step 3, so a field they do not read, such as `testRun`, no longer fails them; the detail read still checks it. Parsing a listing page of 1,000 results takes 42% less time and allocates 92% less |
| Response size | A JSON response larger than 256 MiB (268,435,456 bytes) once decoded fails with an `AdoResponseFormat` error that names the operation: "The server response is larger than 256 MB." There was no limit below the 2 GiB buffer of .NET before |

### Export

| Area | 0.8.0 behavior |
| --- | --- |
| `-Verbose` | `Export-AdoBuildTestFailure` writes one line per step, in this order whatever is downloaded: the attachment downloads with their files and requests, the rendered report with its bytes, its check, its move into place, then a summary with the build, the downloaded attachments, the requests and the elapsed time. A step with nothing to do keeps its line, with zeros |
| A set gathered with `-SkipAttachments` | Has nothing to download, so the export sends no request and needs no connection, whatever its own switches |
| Attachment files | Each is still flushed to disk before the folder is committed. Dropping the flush saved 1.5 to 12% of the export, below the 20% that the developer set as the bar, so it stays |

### Failed-test report

| Area | 0.8.0 behavior |
| --- | --- |
| Header band | Leads with the counts of failed and flaky tests and of attachments; a zero count is greyed, and with scripts on the others are buttons that filter the tests. Then the pipeline, build, branch, commit, result and finish time on a quieter title line: the branch by its name, `main` for `refs/heads/main`, with the full ref as its title, and the finish time without the word Finished, which becomes its title. The tabs, the search and the filters share one line. The keyboard hint moved to the footer. At a 1280 px window the band is 69 px, two rows, in the Overview in both cultures, instead of 157 to 191 px and four or five rows; the French grouped and Partial reports keep three rows |
| New and in a row | After a test's name in the Overview, By error and Open bugs, **New** when the build before ran the test and it did not fail, or **N in a row** when it failed or was flaky in this build and the N − 1 before it. Nothing is said when the build before could not be read, did not run the test, or had another outcome. A card says the same after its history, with the build where the run of failures started. An attempt's **Failing since** names the build by its number when it is in the history |
| History squares | A card's history is one square per build, told apart by shape as well as colour: filled for failed, half filled for flaky, hollow for passed, dashed for not run, dotted for another outcome, hatched for a build that could not be read. The current build is outlined; the build and its outcome are the square's title and accessible name |
| Type and colour | Five font sizes, 11 to 16 px, instead of 13 from 9.98 to 16.8 px, and no text below 11 px; the body is 14 px. Focus and the current item use the link colour instead of amber, so amber means flaky alone |
| Controls | **Expand all** and **Collapse all** show in Details alone |
| Arrival | Reaching a test from its row, with `j` or `k`, with `Enter` or by its address opens the attempt of its latest error and the group that holds it. The HTML still has every attempt closed |
| Search | Ignores accents as well as case, so `echec` finds « Échec » |
| Stack traces | A trace that has a frame of the test's own code opens with the framework frames hidden, and an error message opens wrapped, each with its button pressed. The script applies both at load, so with scripts blocked every frame shows, and copying and printing always include every frame |
| Runs and history | The history table has the dense cells of the other tables, the chart is 212 px high instead of 272, and the attempt column shows only the levels that the runs have, named in its heading, such as "Attempts (stage / instance)" |
| Unlisted attachments | A set gathered with `-SkipAttachments` shows "not listed" in the header and "Not listed" in every cell of the Runs table instead of counts, a sentence that names `Get-AdoBuildTestFailure -SkipAttachments`, no greyed runs, and no **Has attachments** filter, which could only hide every test |
| Large reports | A card is laid out on screen only when it comes near the view (`content-visibility`); print lays out every card |
| French | The tab is « Séries et historique » instead of « Séries de tests et historique », so that the band fits |
| Size | The synthetic Default report is 9.0 MB instead of 9.1 MB |

B1 to B7, under Bug fixes, also change this report. Only
`src/AdoToolkit.Core/Reporting/Assets/test-failures.css` and
`src/AdoToolkit.Core/Reporting/Assets/test-failures.js` changed among the report assets;
`report-base.css`, shared with the Test Case report, did not. The Test Case report is
unchanged.

### Help and guides

| Area | Change |
| --- | --- |
| Help topics | `Get-AdoBuildTestFailure` and `Export-AdoBuildTestFailure`, in both cultures, with `ms.date` 10-03-2026. `Get-AdoBuildTestFailure`: the syntax and parameter of `-SkipAttachments`, the history budget's semantics in the description, example 6, the columns of the console table under OUTPUTS, and the Verbose lines under NOTES. `Export-AdoBuildTestFailure`: New and N in a row, the history squares and the attempt levels of the views; what the script does (the count shortcuts, the arrival on the latest error, the traces, the search without accents); the export of a set gathered with the switch; and the Verbose lines under NOTES |
| Guides | `docs/guides/build-report.md`: the band, the views, New and N in a row, the squares, arrival, search, traces, `-SkipAttachments` and what the attachment lists cost, and two troubleshooting rows for the Verbose lines. `docs/guides/configuration.md`: the default of 500 and its semantics, gzip and deflate, and the 256 MB limit. `docs/guides/pipeline-triage.md`: the switch. `docs/guides/getting-started.md` names the 0.8.0 files |
| Release notes | The 0.7.5 notes moved to `docs/archive/`, unedited. The index of the archive lists them and resolves `V-34` to `V-36` here; the 0.7.10 notes and the changelog link to the new place |

### Tests, harness and live scripts

| Area | Change |
| --- | --- |
| Synthetic server | `tests/AdoToolkit.PowerShell.Tests/Support/FakeAdoServer.ps1` gzips a body with `-Compression` when the request accepts it, paces writes with `-BytesPerSecond`, and records the status, bytes, decoded bytes and times of each request. Its default behaviour, which every Pester suite shares, is unchanged. Fixes H1 and H2 |
| Measure script | `tests/AdoToolkit.PowerShell.Tests/Support/Measure-TestFailurePipeline.ps1` reports requests and bytes per stage, pads listed results to a realistic size with `-ResultBytes`, takes `-MaximumHistoryRequests`, `-Compression`, `-BytesPerSecond` and `-SkipAttachments`, prints `History` and `AbortedResponses`, documents the scenarios in its help, and waits outside its timing until enough local ports are free. Fix H3 |
| Live probes | New `tests/Live/Probes.Live.ps1`, for the next version and never run here: V-28, V-34, V-35 and V-36 (see Known limitations). Its pure helpers are in `tests/Live/Live.Common.ps1`, tested with synthetic data in `tools/tests/LiveAudit.Tests.ps1`. `docs/tooling.md` describes the script, its inputs and its IDs, and no longer says that V-28 has no probe |
| New tests | 42 Core test methods, among them the new `ResponseJsonTests`, `TestFailureSignalTests`, `UnlistedAttachmentTests` and `RetrievalTimingTests`; with their rows, and the two existing tests that became theories, the gate runs 90 more Core cases in each culture than for 0.7.10. 25 Pester blocks, among them the new `tests/AdoToolkit.PowerShell.Tests/SyntheticServer.Pester.ps1`: 19 more product cases and 16 more tooling cases, the synthetic cases of the probes |

### Visible changes for 0.7.10 users

- Existing configuration files need no change. A file that sets
  `testResults.maximumHistoryRequests` keeps its value; one that does not now gets 500.
- Public contract, parameters: `Get-AdoBuildTestFailure -SkipAttachments`, a switch in every
  parameter set.
- Public contract, output: `AdoBuildTestFailureSet.AttachmentsListed`, a bool, `$true` unless
  the set was gathered with `-SkipAttachments`.
- Public contract, configuration: the default of `testResults.maximumHistoryRequests` is 500
  instead of 400, with the semantics above. Near the budget, which earlier builds are kept
  can differ from 0.7.10: a build whose result pages do not fit sends none of them, and
  0.7.10 could keep one more build at one bound or response order and not at another.
- Requests: every request accepts gzip and deflate, and a JSON response larger than 256 MiB
  once decoded fails with an `AdoResponseFormat` error, from any cmdlet.
- `-Verbose`: `Get-AdoBuildTestFailure` writes one line per stage and a summary;
  `Export-AdoBuildTestFailure` one line per step and a summary. A script that reads Verbose
  output sees new lines.
- Console: the default table of a failed test shows `LatestError` instead of `Storage`. A
  script that needs the storage reads the property, or names it in `Format-Table`.
- The failed-test report: the header band (the counts first, as filter shortcuts; the branch
  by its name; the finish time without its label), New and N in a row, history squares, one
  type scale, focus off amber, **Expand all** and **Collapse all** in Details only, cards
  opening on their latest error, search without accents, and traces opening on the test's
  own code. Its French tab is « Séries et historique ».
- No flush change: attachment files are flushed to disk as in 0.7.10.
- Core types, for code that uses the assembly directly: `TestFailureQuery.SkipAttachments`;
  `TestFailureReportModel.AttachmentsListed`; a `TestFailureRetrievalService.GetAsync`
  overload that reads the build itself; `HtmlTestFailureRenderer.LatestError`, now public;
  the defaults of `TestFailureQuery.MaximumHistoryRequests` and
  `TestResultOptions.MaximumHistoryRequests` are 500. `AdoMessage` loses
  `TestReportAttemptNumbers` and gains 26 members, 11 of them among the report labels, so
  the numeric values of later members change.

## Bug fixes

B1 to B7 are defects of the failed-test report; H1 to H3 are defects of the test harness.
B1 to B6 were found by the review of the report in phase 5 of the plan and exist in 0.7.10.
Each was fixed through the `fix-bug` skill with its test written first, and the output kept
from that run shows 10 failures before the fixes. B7 is a defect of this release's own
restyle, found by the browser checks of phase 8 and never shipped: its test failed before
each half of the fix, and the headless Edge checks failed in 3 and then 2 of the 10 goldens
before it and pass in all 10 after. H1 and H2 were observed on the 0.7.10 baseline (section
3 of the plan, findings 6 and 10); H3 was fixed through the `fix-bug` skill with its test
failing first.

| ID | Finding, cause and fix | Regression test | Files |
| --- | --- | --- | --- |
| B1 | Keyboard focus could land under the sticky header after `k` or Shift+Tab (WCAG 2.2 SC 2.4.11). `scroll-padding-top` on `html`, from the band's height, replaces the `scroll-margin-top` list, so the two offsets no longer add up, and the script no longer sets `--report-header-offset` | `StylingTests.KeyboardFocusStaysClearOfTheStickyHeader` | `src/AdoToolkit.Core/Reporting/Assets/test-failures.css`, `src/AdoToolkit.Core/Reporting/Assets/test-failures.js` |
| B2 | After a long test name the Open bug marker was cut off: the cell's `max-width` was ignored. The name is a `test-name` span cut at 26rem with the whole name as its title, so the marker after it stays in view | `ReportRefinementTests.ALongNameIsCutBeforeTheOpenBugMarkerAndKeepsItsWholeTextAsTitle` | `src/AdoToolkit.Core/Reporting/TestFailures/HtmlTestFailureRenderer.cs`, `src/AdoToolkit.Core/Reporting/Assets/test-failures.css` |
| B3 | French text got ": " with an ordinary space where code joined a label and a value: the accessible name of a status strip, the title of a history cell and the title of a chart bar. The new catalog string `TestReportLabelValue` joins them in both cultures | `ReportRefinementTests.LabelsAndValuesAreJoinedByTheCatalogSeparator`, en-US and fr-CA | `src/AdoToolkit.Core/Reporting/TestFailures/HtmlTestFailureRenderer.cs`, `src/AdoToolkit.Core/Reporting/Charts/HistoryStrip.cs`, `src/AdoToolkit.Core/Reporting/Charts/RunHistoryChart.cs`, `src/AdoToolkit.Core/Resources/AdoMessage.cs`, `src/AdoToolkit.Core/Resources/Strings.resx`, `src/AdoToolkit.Core/Resources/Strings.fr.resx` |
| B4 | The error and bug group headings had `scope="colgroup"` with no `colgroup`, so they named no cells for a screen reader. They use `scope="rowgroup"` | `ReportRefinementTests.ErrorAndBugHeadingsNameTheirRowGroup` | `src/AdoToolkit.Core/Reporting/TestFailures/HtmlTestFailureRenderer.cs` |
| B5 | Back after opening a card returned to the top of the table. `fragment()` scrolls the current test's row to the centre and focuses it | `ReportRefinementTests.ReturningToATableOfTestsBringsTheCurrentTestsRowBack`. It reads the script text; the browser checks of phase 8 ran it | `src/AdoToolkit.Core/Reporting/Assets/test-failures.js` |
| B6 | Printed status squares lost their fill. `.sq` and the history squares print their colours exactly (`print-color-adjust: exact`) | `StylingTests.PrintedStatusSquaresKeepTheirFill` | `src/AdoToolkit.Core/Reporting/Assets/test-failures.css` |
| B7 | A card reached from an Overview row, by `j` or by its address landed up to a line, 7 to 15 px, under the band, in two ways. Details shows **Expand all** and **Collapse all**, so its band can be a line taller (69 to 101 px), and the scroll came before the band's new height was measured. And the scroll lays out cards that `content-visibility` skipped, so a short report grows a scrollbar, the band rewraps (101 to 125 px in the French Partial report) and the position no longer clears it. The script measures the band when the view changes and repeats a scroll to the top once when the band's height changed | `StylingTests.ATargetScrolledToTheTopClearsTheBandEvenWhenItsHeightChanges` | `src/AdoToolkit.Core/Reporting/Assets/test-failures.js` |
| H1 | The synthetic server reported 7 requests in flight with a bound of 6. They were history listings that the client had cancelled when a sibling was refused by the budget, and that the server still answered; recorded request by request, the client never sent more than 6. The server leaves the requests it sees abandoned out of its peak, and the Measure script reports them as `AbortedResponses` | `tests/AdoToolkit.PowerShell.Tests/SyntheticServer.Pester.ps1`: "leaves out of its peak a request whose client left" and "stops counting a request whose client closed its connection, and keeps serving" | `tests/AdoToolkit.PowerShell.Tests/Support/FakeAdoServer.ps1`, `tests/AdoToolkit.PowerShell.Tests/Support/Measure-TestFailurePipeline.ps1` |
| H2 | A measurement ended with exit code 1: the write to a connection that the client had closed stopped a server worker, and stopping the server rethrew it. A client that leaves no longer fails the server; any other error still does | The same file: "is not failed by a client that resets its connection before the response" and "is not failed by a client that leaves during the response" | `tests/AdoToolkit.PowerShell.Tests/Support/FakeAdoServer.ps1` |
| H3 | Under strict mode the Measure script stopped when a cmdlet sent no request: `Measure-Object -Property` returns no object for empty input, so there was no `Sum` to read. A run with `-AttachmentsPerAttempt 0` already hit it. The script adds the bytes itself | The same file: "reports an export that sends no request", without attachments and with `-SkipAttachments` | `tests/AdoToolkit.PowerShell.Tests/Support/Measure-TestFailurePipeline.ps1` |

Also in the harness: a paced response, which 0.8.0 added, no longer counts as in flight
during the wait after its last chunk; "stops counting a paced response once its last byte is
sent" covers it.

### Existing tests changed

| Test | Change |
| --- | --- |
| `BugsViewTests.EachBugIsListedOnceWithEveryTestLinkedToIt`, `BugsViewTests.GroupedBuildKeepsOneStatusColumnPerGroup` | Expect `scope="rowgroup"` (B4) |
| `TestBugResolutionTests.BugsInTheCompletedAndRemovedCategoriesAreLeftOutWhateverTheStateName` | The marker follows the name's span (B2) |
| `RunsViewTests.AttemptNumbersAlwaysShowThreePlaces` | Renamed `AttemptNumbersShowOnlyTheLevelsThatExist`, a theory in both cultures: the column shows and names only the levels that the runs have, and is absent when no run has an attempt number |
| `RunsViewTests.ExpandAllShowsTheDetailsViewWhenTheHashAlreadyNamesIt` | Renamed `ExpandAllAndCollapseAllShowInDetailsAlone`: both buttons show in Details alone, so the path of 0.7.5 that switched to Details from another view (0.7.5 B5) is gone |
| `RunsViewTests.HistoryByTestHasOneRowPerTestOneCellPerBuildAndTheFailureStreak` | The test cell holds the name in its `test-name` span |
| `RunsViewTests.FailedSegmentStaysVisibleItsCountStandsAboveTheBarAndTheChartHasALegend` | The full bar is 120 units instead of 180, for the lower chart |
| `StripTests.HistoryKeepsAllSixStatesTitlesCurrentMarkerAndToolkitLinksWithoutALegend` | History cells are empty squares with a status class, six of them distinct, whose title and accessible name join the build and status with the catalog separator (B3) |
| `ReportRefinementTests.DetailsKeepHistoryCellsAndTitlesWithoutALegend` | A history cell needs its `status-*` class |
| `StylingTests.ExternalLinkGlyphIsSmallAndMuted`, `StylingTests.HeadingsInsideACardGoDownOneLevelAndStopAtSix` | Read the type-scale tokens instead of `em` and `rem` sizes; the history-cell glyph rule they compared with is gone, and history cells hold no text |
| `ScriptAssetTokenScanTests.AssetContainsNoForbiddenApiOrVisibleStringAssignment` | The size limit of the failed-test script goes from 12 to 16 KiB; the script is 12.8 KB. The Test Case report's script keeps 12 KiB |
| `TestFailureExporterTests.LaunchFailureAfterCommitIsAWarningAndStillReturnsTheResult` | Reads the warnings alone, through the new `CapturingLog.Warnings`, since the export's Verbose lines now share its log |
| `RetrievalRequestBoundTests.RequestCountMatchesTheDocumentedBoundForTheRerunFixture` | A theory with and without `-SkipAttachments`: 18 requests with 11 attachment lists, 7 with none, and `AttachmentsListed` |
| `ServerWireShapeTests.NonNumericReferenceIdsStillFail`, `ServerWireShapeTests.InvalidReferenceIdNamesTheOperationAndJsonPath`, `ServerWireShapeTests.StringTestRunIdsInResultListsParse` (renamed `StringTestRunIdsInResultDetailsParse`) | Check `testRun` on the detail read (`TestResultGet`, `$.testRun.id`) instead of the listing, which no longer reads it |
| `tests/AdoToolkit.PowerShell.Tests/TestRuns.Pester.ps1`: "emits one set with failures, counts, history and Test Case links" | Also checks that `AttachmentsListed` is `$true`. Its helper `Get-TwoRunResponses`, and the one of `tests/AdoToolkit.PowerShell.Tests/TestFailureExport.Pester.ps1`, take `-SkipAttachments` |
| Goldens | The 10 failed-test goldens in `tests/Fixtures/Reports/` changed through the `update-goldens` skill, reviewed in both cultures, in phase 5 and again for the band. The 40 Test Case goldens did not change. `tests/Fixtures/README.md` is unchanged |

No existing test was deleted; the four renamed ones replace their earlier versions.

### Earlier findings closed

| Finding | Closed by |
| --- | --- |
| Near the history budget, cancelled requests can change the budget left for older builds (0.7.0, carried in 0.7.5 and 0.7.10) | The planned history read. `RetrievalConcurrencyTests.NearTheHistoryBudgetTheBuildsKeptAndTheRequestsSentDependOnNeitherTheBoundNorTheResponseOrder`, 11 budgets from 5 to 25 at bounds 1 and 8 with three response orders, failed on the 0.7.10 history read in 11 of 12 cases. What remains is under Known limitations |
| JSON responses are read without a size cap below the 2 GiB buffer of .NET (0.4.0) | The 256 MiB limit |
| **Expand all** from a history cell was not run in a browser (0.7.5) | Headless Edge ran **Expand all** and **Collapse all** in Details, in all 10 goldens; they no longer show in other views |

### Findings not fixed

| Finding | Reason |
| --- | --- |
| The Test Case report has `scope="colgroup"` without a `colgroup`, as the failed-test report had (B4) | 0.8.0 changes only the failed-test report; the fix changes the Test Case goldens |
| `RichTextImageAlt` and `PartialTestCase` keep an ordinary space before a colon in French | Carried from 0.7.5. The Test Case goldens hold them; they change only through the `update-goldens` skill, with the French wording reviewed |
| A printed failed-test report can leave a card's header alone above a page break | Each attempt avoids a break inside it, as in 0.7.10, so that an attempt prints on one page |
| Shift+Tab, the real clipboard, the print dialog and native tooltips were not checked in a browser | Headless Edge cannot drive them; check 5 below does. The titles of the band are pinned by a test |
| Reading the Test Cases and bugs beside the attachment lists (phase 2, optional) | Dropped: each stage line must count its own requests, which needs a second counter and clock for that branch, to save at most about 0.1 s at 20 ms, and nothing with `-SkipAttachments` |
| The findings of 0.7.10, 0.7.5, 0.7.0, 0.6.5 and 0.6.0 | Unchanged apart from those closed above; see the [0.7.10](archive/release-0.7.10.md#findings-not-fixed), [0.7.5](archive/release-0.7.5.md#findings-not-fixed), [0.7.0](archive/release-0.7.0.md#findings-not-fixed), [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes |

### Known limitations

V-34 to V-36 are defined here; `docs/archive/README.md` resolves them. V-28 is in the
verification ledger of the specification. `tests/Live/Probes.Live.ps1` probes all four for
the next version. 0.8.0 relies on none of them, and a `FAIL` is evidence for that version's
plan, not a regression.

| Limitation | Notes |
| --- | --- |
| V-28 | Whether Server 2020 exposes the result-summary, results-by-build, results-query and test-history routes, which could replace per-build listings. The probe reads the resource locations of the test area with one `OPTIONS` request. This is its first probe |
| V-34 | Whether a result list with `detailsToInclude=Iterations,WorkItems,SubResults` and `$top=200` returns what the single-result read returns: the fields the toolkit reads, the sub-results, the iterations and the full text. A probe on Azure DevOps Services found messages cut at 4,000 characters in lists. If it holds, most detail requests could go |
| V-35 | Whether Server 2020 compresses JSON responses when the client asks. 0.8.0 asks on every request; IIS does not list `application/json` among its default compressed types, so it may save nothing at work. It breaks nothing either way |
| V-36 | Whether a result's attachment list also holds the attachments of its sub-results. 0.8.0 lists both, as before; if it holds, the sub-result lists could go |
| Concurrent requests (V-33) | Still unconfirmed at work, as in the [0.7.0 notes](archive/release-0.7.0.md#known-limitations). If Server 2020 refuses or delays several requests, set `testResults.maximumConcurrentRequests` to `1` |
| Requests that no plan foresees | A retry, or a result page beyond what a run's `totalTests` predicts, takes from what is left of the history budget when it is sent, so near the budget such a case can still depend on response order. A run without `totalTests`, or not completed, is read to its end before any older build is planned, so it cannot. The help and `docs/guides/configuration.md` say both |
| The history read is faster only where it is the long pole | When many tests fail, the main read fills the request gate and the planned history read changes only the order of the same requests: the wall time moved by −3 to +6%, inside the spread of the runs. It is 36 to 40% faster when few tests fail and the history is long. For a build with many failures, `-SkipAttachments` lowers the wait, and so would compression if the server gives it (V-35) |
| Decisions that stand | §15.9 step 3 (a listing reads eight fields; the new listing type holds exactly them), §17 (no cache across invocations), Q-30 (dark on screen, light in print; no light theme) and the 0.7.0 rule that every attempt keeps its full error message and stack trace are unchanged |
| Synthetic timings | Every figure in these notes comes from the synthetic server on the development PC. They rank the costs; the stage lines of `-Verbose` give the real ones at work |
| Limits carried over | The [0.7.10](archive/release-0.7.10.md#known-limitations), [0.7.5](archive/release-0.7.5.md#known-limitations), [0.7.0](archive/release-0.7.0.md#known-limitations), [0.6.5](archive/release-0.6.5.md#known-limitations) and [0.6.0](archive/release-0.6.0.md#known-limitations) known limitations still apply, with V-31, V-32 and V-02 |

## Work-PC Live checks

Install the 0.8.0 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass. No check gates 0.8.0.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | Record `Get-FileHash` of `%APPDATA%\AdoToolkit\config.json`, install 0.8.0, then run `Get-AdoProfile` and `Connect-Ado` | The hash is unchanged, no warning appears, and `Get-Module AdoToolkit -ListAvailable` shows 0.8.0 with seven files in its folder |
| 2 | V-33 and the stage timing: `Get-AdoBuildTestFailure -Verbose` on a large build with the default configuration, then with `testResults.maximumConcurrentRequests` set to `1`, `10` and `16` | Equal counts, `Status` and diagnostic codes at every value, and no refused or throttled request. Report the error ID, the counts, the requests and milliseconds of each stage line and the summary only. If requests at `1` take about twice the network round trip, Windows authentication may be repeated on every request: that is an IIS setting for the server's administrators (`authPersistNonNTLM` for Kerberos), not a toolkit change |
| 3 | The same build with `-SkipAttachments`, then `Disconnect-Ado` and `Export-AdoBuildTestFailure -Open` of that set | The same failures, attempts and diagnostics as without the switch, the attachment line at 0 requests, `AttachmentsListed` `False`; the export sends nothing without a connection, and the report says the attachments were not listed |
| 4 | `tests/Live/Probes.Live.ps1` with `ADOTOOLKIT_LIVE_TEST_BUILD_ID`, a build with a failed test, and `ADOTOOLKIT_LIVE_RERUN_BUILD_ID`, a build whose in-task reruns have attachments | One verdict each for V-28, V-34, V-35 and V-36, with the route notes of V-28. They decide the routes of the next version; record them, `INCONCLUSIVE` included |
| 5 | A report of a build whose tests link a bug of another project, opened with `-Open` in the browser used at work | Shift+Tab never leaves focus under the band; a name, a message and a trace with hidden frames copy to the clipboard, the trace with every frame; the print dialog from Details with every attempt open shows every card, every frame and filled squares; the branch and finish time show their full text on hover; the bug link opens the bug in its own project; **Expand all** and **Collapse all** show and work in Details alone. No Content Security Policy error in the console |
| 6 | Checks 4 to 7 of the [0.7.5 notes](archive/release-0.7.5.md#work-pc-live-checks), with the 0.8.0 package | As listed there: `TestFailures.Live.ps1` and `TestCase.Live.ps1`, completion, a request with the PC off the network, and the 0.6.0 checks with V-31, V-32, V-02, V-01 and V-03. All still pending since 0.7.5; checks 2 and 3 there are ranks 2 and 5 here |

## Local validation and developer handoff

Run on 2026-10-03 on the development PC, on the tree that holds these notes.

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0. All seven stages pass without a warning: 63 PowerShell files linted, 292 tooling Pester tests, 147 configuration files, 83 Markdown files with 232 links and 287 path references, the hook and skill layout, 2 workflow files linted with actionlint, and the product check, which staged and inspected AdoToolkit 0.8.0 |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as both workflows run it | Exit 0 with the exact module pins in `tools/BuildModules.psd1`. The variable was unset before and was unset again afterwards |
| Core tests in the final gate | 1482 passed under en-US and 1482 under fr-CA, with no failures or skips: 90 more cases than 0.7.10 |
| Product Pester tests against the staged module | 179 passed, none skipped: 19 more than 0.7.10. The tooling tests have 16 more, the probe cases |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `0.8.0` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.8.0` with seven files, and 0 warnings |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.8.0-win-x64.zip -ModuleVersion 0.8.0 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.8.0, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.8.0/`. The portable ZIP has the same seven files under `module/`, the launcher, `README.txt` and `bundle.json` (module 0.8.0, PowerShell 7.6.6, win-x64, PowerShell SHA-256 equal to the published hash and to the pin of the setup action), 669 files in all. Each `.sha256` file holds the hash of its ZIP. The installer is byte-identical to its source |

The tests of B1 to B7 and H3 failed before their fixes in the sessions of the plan's phases,
as Bug fixes records; they were not run again against the 0.7.10 source for these notes.
Headless Edge 154 ran 224 checks of the report's controls on all 10 failed-test goldens in
phase 8, on the development PC; no browser at work opened a report, and no Live script ran.
Not run: the installer against the 0.8.0 ZIP, the workflows, which only GitHub runs, and the
squash merge, which happens on GitHub after these notes.

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.8.0-win-x64.zip` | 110380854 | `cdbf1b6a43983f05cdd82ae81b4f8a3263b45bae2613eff399a9180189940749` |
| `AdoToolkit-0.8.0.zip` | 773940 | `a01b971951cd3b12be84badd345c485625b15582ecee479c68bd279f21064cd6` |
| `Install-AdoToolkit.ps1` | 13032 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes. No tag or GitHub release exists for 0.8.0. Every
change is uncommitted on the branch `0.8.0`, which has no upstream yet. The developer owns
publication:

1. Run the first command of the handoff. It commits every change, creates the tag `v0.8.0`
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
6. Start the next branch from the updated `main`, and archive the 0.8.0 plan there as
   `docs/AGENTS.md` describes.
