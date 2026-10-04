# AdoToolkit 0.8.5 release notes

Prepared on 2026-10-03 from the uncommitted changes on the branch `0.8.5`, which starts at
`main` after the pull request of 0.8.0 was squash-merged as `4e62fba`. The work followed the
[0.8.5 plan](archive/plans/0.8.5-report-styling.md): its section 4 is the design, its section 10 the
developer's decisions over three rounds of previews, and its section 11 the evidence of each
step. 0.8.5 is a styling pass on the HTML report that `Export-AdoBuildTestFailure` writes:
the views come in a new order, every part of a test has a column of its own, colour carries
meaning, the Overview gains cards that sum up the build, By error clusters messages that
differ only in their variable parts, Open bugs says what each bug covers, and a card shows
its dated run history, its bugs and its attachments before its attempts. Retrieval, the
cmdlets and every other report are unchanged. This is an offline review. No Azure DevOps
Server connection or `tests/Live/*.Live.ps1` run was attempted, so nothing below is
confirmed live. The areas the README lists as confirmed are still connections, projects,
builds and test runs.

## What changed

0.8.5 adds no cmdlet, parameter or output property, and changes no configuration setting.
The configuration file format, the JSON report schema and the seven-file package layout are
unchanged. The report reads only what `Get-AdoBuildTestFailure` already gathered: no
request, endpoint or server assumption is new. B1, under Bug fixes, is a defect of this
release's own restyle, fixed before it shipped.

### Failed-test report: views and columns

| Area | 0.8.5 behavior |
| --- | --- |
| Order | Overview, Runs and history, Details, By error, Open bugs, then Diagnostics when present, in the tabs and in the page, so print and a page with scripts blocked read the same. Overview stays the default view |
| Columns | Every table of tests reads `# · Test Case · Test · Class · Trend · Open bugs · groups or Attempts · last`. The last column is Latest error in Overview, Values in By error when a cluster has values that differ, and Linked through in Open bugs; Open bugs shows in Overview and By error only. `#` holds the status glyph and the ordinal close together, the ordinal right-aligned in a span as wide as the largest one (three digits from 100 tests). Test holds the name alone, Class the class name; both are cut by the stylesheet with the whole text as title. Every Overview row is one line, its latest error cut by the cell with the whole line as title |
| Status cells | Each group or Attempts cell reads `✕ 7/7` as before and now has "7 of 7 failed" as its title. The legend line above each table is gone |
| Trend | **✦ New**, the only filled chip, when the build before ran the test and it did not fail; **Since** and the day the first build of the run of failures finished, an orange pill that opens that build's test results, with "N in a row since <build>" as its title, when it failed or was flaky in the build before too. When the day of that build is not known, the chip names the build instead, "Since build 20260901.4". The trend column is centred in every table. The streak length shows nowhere as text |
| Open bugs column | The lowest-numbered open bug as a red chip, `#801`, with `+N` and the other numbers as its title when there are more; a bug that could not be read shows the same chip in grey, titled "Not read"; otherwise the cell is empty |
| Density | Overview, Runs and history, By error and Open bugs have cells of 4 × 12 px with a line height of 1.4, rows of about 28 px; Details keeps the density of 0.8.0 |
| Links | No ↗ after a link or a chart caption: every link goes to Azure DevOps or within the page |

### Failed-test report: colour

| Meaning | Colour, always with a glyph or text |
| --- | --- |
| Failed, flaky, passed | Brighter red #ff6b6b, amber #ffc53d and green #3ddc84, in this report's stylesheet only. They keep at least 5.4:1 on every surface, and their print variants 6.6:1 on paper; `ThemeContrastTests.TheFailedTestReportsOwnColoursKeepTextContrastOnEverySurface` holds each of this report's colours to 4.5:1 |
| New | Violet #b38cff, filled, ✦ New |
| Recurring | Orange #ff953e, a pill with orange text on an orange tint: the Since chips and the Recurring counts |
| An open bug | Red, as a failure, wherever it shows: the Open bugs column, bug headings, the card's bugs, Associated bugs, the With an open bug fact |
| A bug that could not be read | The same chip in grey, wherever it shows: it may be closed, so its test counts as without an open bug |
| Print | Darker variants on white, and chips as outlines |

### Failed-test report: each view

| View | 0.8.5 behavior |
| --- | --- |
| Overview | The table first, then one row of cards, each left out when it would be empty. **New and recurring**: how many tests are New, Recurring or have No comparison, one count per line, and a bar of the three shares. **Most common errors**: the three largest clusters of two tests or more, with their exception type and line, each opening the cluster in By error. **Without an open bug**: "n of m tests", then the three that most need a bug, longest failing first, then new ones, and a link to the Without an open bug group of Open bugs; shown only when some test has an open bug. **By group**, in a grouped build: per group, the tests that failed, were flaky or failed only there. The cards describe the whole build; the filters act on the table |
| Runs and history | The run history chart first, centred in its panel, with its History data table, where "This run" is a pill. Then the runs: the run name, then its ID in a column of its own; "Latest run" at the end of the run cell; Passed and Failed read `✓ 38` and `✕ 2` in status colour, muted at zero; one row group per pipeline group in a grouped build, with a heading; an accent on the leading edge of the latest run's row. Then History by test: `# · Test · Class · one cell per build · Trend`, each cell a small box tinted by its status with its glyph |
| Details | The card's title shows the class, muted, before the name; the full name with Copy follows. Then the facts, with the metadata, on one label column, 7.5rem or 10rem in French: Run history, one chip per build with its glyph and the day it finished in the report's culture, the current build last with its date in bold, then the trend; Bugs, each as the Overview's chip with its title and state; Attachments, each file name once, from the last attempt that has it, linked to its original, with its size and a link to that attempt's listing, and `×N` titled "Attempts: 1, 3" when several attempts have a file of that name. The dates join the card's search text. The current card and a card focused from the keyboard have rings offset by 2 px; no coloured top edge |
| Attempts | Summaries keep the native disclosure marker. Title, status, duration and machine have fixed widths, so they line up, a missing duration or machine keeps its place, and the server's own outcome, such as `Timeout`, follows the machine. After MSTest's "Test method X threw exception:", the summary shows the exception on the line below. Metadata pairs sit on a grid; a comment or the JSON of custom fields takes a whole row. Inside an attachment row, the name and size on the left, the local copy on the right. A failed attempt's message has a red edge; the stack trace tints the first frame of the test's own code and dims framework frames further. The Copy, Wrap and Framework buttons are quiet until hovered or focused |
| By error | Clusters merge lines whose URLs, GUIDs, Windows, UNC and Unix paths, hexadecimal IDs (eight digits or more, mixing digits and letters, or `0x…`) and numbers differ, instead of numbers and GUIDs alone. The key line is the whole first line of the latest error, not its first 240 characters, or the exception line after MSTest's first line. Above the table, "Distinct errors: n · Tests: m". Each cluster is a row group with an anchor, `e-1`, `e-2`, …, and a heading: count, exception type by its short name with the full name as title, and the first test's line without its `Type:` prefix, cut at 240 characters; each value that differs between the tests is underlined, titled "Differs between tests: a, b, c (+n)". A cluster of two tests or more also has a facts row (failed and flaky counts, New and Recurring counts, With an open bug with the bug chips and Without an open bug, a count per pipeline group, and the common frame of the tests' own namespace when two tests or more share it) and a closed "Sample message", the first test's message cut at 12 lines or 1,000 characters. The Values column shows each test's own values. Tests without a message come last |
| Open bugs | A bug's heading is one line, wrapping only when it must: count, chip, title in bold, state, "Not read", its work item type when it is not Bug and its project when it is not the build's, then Linked tests, Same Test Case, not linked, Same error, not linked (a link to the largest such cluster), and in a grouped build how many of its tests fail in each group. Linked through says how far a link through test results reaches, "Test result: 2 of 3 failed results", counted by result since reruns share their parent result's bugs, and only when the bug is not also linked through the Test Case. Tests that share a Test Case with a linked test but not the bug follow its tests, muted. A last group, Without an open bug (`no-bug`), lists every test that no open bug tracks, when some test has one and some other has none |
| Diagnostics | Last, unchanged |

Unchanged: the script `src/AdoToolkit.Core/Reporting/Assets/test-failures.js` (13,349 bytes),
`report-base.css`, the Test Case report, the console table's `LatestError` (the first line),
how a test is classified, the size budgets of `CompactReportTests` and `ErrorTextTests`, and
the report with scripts blocked, in print and under its Content Security Policy. The
stylesheet `src/AdoToolkit.Core/Reporting/Assets/test-failures.css` grows from 23,267 to
37,281 bytes, so each report is about 14 KB larger; a golden of one test grows from 64 to
81 KB.

### Code

| Area | Change |
| --- | --- |
| `src/AdoToolkit.Core/Reporting/TestFailures/HtmlTestFailureRenderer.cs` | The order, the columns, the Overview cards, By error, Open bugs, the Runs view and the card |
| `src/AdoToolkit.Core/Reporting/TestFailures/ErrorClusters.cs` | New, internal: the key line, the placeholders, the clusters, the exception type, the common frame. The Overview and Open bugs use the clusters of By error; `HtmlTestFailureRenderer.ErrorKey` returns the same key |
| `src/AdoToolkit.Core/Reporting/Highlighting/StackTraceLexer.cs` | `FirstFrame`, internal: the first frame of a namespace among the first 64 lines of a trace |
| `src/AdoToolkit.Core/Reporting/Charts/HistoryStrip.cs` | A glyph in each cell, and a `Write` overload whose label, such as the day a build finished, shows in the cell |
| `src/AdoToolkit.Core/Reporting/Charts/RunHistoryChart.cs` | "This run" is a pill; no ↗ after the captions and the build links |
| `src/AdoToolkit.Core/Reporting/Assets/test-failures.css` | The styles above. The rule for the ↗ glyph, unused since the links lost it, is removed |
| Strings | 22 new catalog strings in both cultures, the table of the plan's section 5, from "Class" to "Since build {0}"; `TestReportGroupLegend`, `TestReportConsecutiveFailures` and `TestReportInARow` are removed with the legend and the streak counts. `TestReportInARowSince` stays as the Since chip's title. French follows `src/AGENTS.md`, and `ResourceParityTests`, `FrenchTerminologyTests` and `HardCodedStringTests` pass |

### Help, guides and documents

| Area | Change |
| --- | --- |
| Help topics | `Export-AdoBuildTestFailure` in both cultures, with `ms.date` 10-03-2026: the views in their order, the columns, the trend, the cards, the clusters, the bug coverage, the card's history, bugs and attachments, and what search reads |
| Guides | `docs/guides/build-report.md`: the views table, the status cells, colour, the trend, the card, the attempt summary and search. `docs/guides/pipeline-triage.md`: the report's views and the Open bugs column. `docs/guides/getting-started.md` names the 0.8.5 files |
| Release notes and plans | The 0.7.10 notes and the 0.8.0 plan moved to `docs/archive/`, unedited, as the 0.8.0 handoff asked; the archive indexes list them, and the 0.8.0 notes, the changelog and the help of `tests/AdoToolkit.PowerShell.Tests/Support/Measure-TestFailurePipeline.ps1` point to the new places. The 0.8.5 plan stays in `docs/plans/` |
| Skill | `.agents/skills/update-goldens/SKILL.md` counts 52 goldens, 12 of them failed-test reports |

### Tests

| Area | Change |
| --- | --- |
| A larger fixture | `TestFailureReportFixture` gains the `large` variant, ported from the plan's synthetic preview set: 16 tests in two stages of two runs, ten builds of history, clusters that differ in numbers, paths and GUIDs, an MSTest first line, a shared helper frame, new, recurring and flaky tests, a bug that covers one of four failed results, a test of the same Test Case left out, an unread bug, a `Timeout` outcome and attachments on several attempts, one too large. Its English and French goldens are new, and `tests/Fixtures/README.md` lists them |
| New tests | 30 Core test methods. New classes: `ErrorClustersTests` (keys for numbers, GUIDs, URLs, paths, hex IDs; kinds and text kept apart; the MSTest key line; the exception type; short types and frames; `FirstFrame`; cluster order, values and common frame), `ByErrorViewTests` (headings and marked values, facts and sample only for two tests or more, the sample's cut, the line's cut, the Values column, the attempt summary after MSTest's line), `OverviewCardsTests` (each card, its counts, order and links, and the empty cases), `CardFactsTests` (the card's order and label width, the dated history, attachments once per name with `×N`). In existing classes: `BugsViewTests` (coverage by result, Same Test Case and Same error, the Without an open bug group), `TestFailureSignalTests` (the Since chip's build fallback), `StylingTests` (no ↗ in any variant, one Overview row per test for the ordinal width), `ThemeContrastTests` (this report's own colours, on screen and in print, which the shared palette's test did not read). With the two new goldens, the Core suite runs 70 more cases in each culture than for 0.8.0 |

### Visible changes for 0.8.0 users

- The failed-test report looks and reads differently, as above: the views in a new order with
  Overview still first, new columns, cards under the Overview, a dated Since chip instead of
  "N in a row", red bugs, brighter status colours, no legend lines, no ↗, a dated run
  history, and the card's attachments. A bookmark to a view (`#details`, `#by-error`,
  `#bugs`) or to a card (`#f-3`) still works; `#e-1` and `#no-bug` are new anchors.
- By error groups more tests together than 0.8.0 did, since paths, URLs and hexadecimal IDs
  no longer keep their lines apart, and an MSTest test now groups by its exception.
- A test whose only bug could not be read counts as without an open bug in the cards and in
  the last group of Open bugs, as the Without an open bug filter already counted it.
- The overview's Latest error and the console table still show the first line of the latest
  error; only the attempt summaries and By error read the exception line after MSTest's.
- No public contract changes: no cmdlet, parameter, output type, configuration setting or
  schema changed.
- Core types, for code that uses the assembly directly: `HistoryStrip.Write` gains an overload
  with a label function. `AdoMessage` loses three members and gains 22, so the numeric values
  of later members change, and the report model's labels change with them.

## Bug fixes

| ID | Finding, cause and fix | Regression test | Files |
| --- | --- | --- | --- |
| B1 | A cluster line longer than 240 characters was cut inside a value that differs between the tests, which the plan and the code's own comment rule out: the cut shortened whichever part reached the limit, marked value or not, so a reader saw part of a number or a path, marked and titled as if whole. A marked value that does not fit is now left out whole, and the line ends with "…" before it. Found while writing the By error tests of this release, in code new in 0.8.5; never shipped | `ByErrorViewTests.ALongClusterLineIsCutNeverInsideAMarkedValue`. Before the fix it failed alone among the new and changed tests (1 of 131 in its run): `Assert.Contains() Failure: Sub-string not found`, the expected line ending before the value not found, because the heading held the value's first two characters in its marked span | `src/AdoToolkit.Core/Reporting/TestFailures/HtmlTestFailureRenderer.cs` |

### Existing tests changed

The 0.8.5 plan, section 8, listed the 67 failing cases in 36 methods that the restyle would
break, all of them markup or rule pins; the run of the Core suite on the approved preview
build reproduced exactly those. The first gate run found three more, in the product Pester
suite, that the plan had not listed. Each was changed to pin the new design, and the new pins
are at least as strict.

| Test | Change |
| --- | --- |
| `AttachmentRenderingTests.AttachmentsWithoutAFileShowTheirStatusAndKeepTheAdoLink`, `LatestRunAttachmentTests.DownloadScopePreservesEveryAttemptAndRemoteAttachment`, `LatestRunAttachmentTests.OnlyJsonAndTextAreDownloadedAndHtmlAndPngNeverAre`, `TestFailureExporterTests.SkipAttachmentsDownloadsNothingAndCreatesNoFolder`, `TestCaseLinkRenderingTests.ResolvedAndUnresolvedReferencesKeepOnlyTheCorrectTitleAndState` | An attachment name or Test Case number ends its link, without the ↗ glyph |
| `tests/AdoToolkit.PowerShell.Tests/TestFailureExport.Pester.ps1`: "SkipAttachments downloads nothing and creates no folder", "lists large older attachments without a connection when the latest run has none" and "leaves out attachments of runs older than AttachmentWindowDays" | The attachment name ends its link, without the ↗ glyph |
| `StylingTests.ExternalLinkGlyphIsSmallAndMuted` | Became `NoLinkCarriesAnArrow`, a theory over the five fixtures: no ↗, no image inside a link, no rule for one |
| `StylingTests.OneTypeScaleOfFiveSizes` | Only the brand uses 11 px, now that the ↗ rule is gone |
| `StylingTests.LegendIsAboveEveryTableOfTests` | Became `NoLegendAboveATableOfTests`: no legend, its catalog string gone, and status cells titled instead |
| `StylingTests.AttemptSummaryHasADurationColumnAndAMachineColumn` | The machine has its name as title; the title's widths, the machine's cut, and a missing duration and machine keeping their places before a `Timeout` outcome |
| `StylingTests.HistoryDataTableIsAsDenseAsTheOthers` | 4 × 12 px cells, those of the other tables |
| `StylingTests.OrdinalHasItsOwnRightAlignedSpanInEveryTable` | Eight rows with the Without an open bug group; the span is 2ch, 3ch from 100 tests and 4ch from 1,000 |
| `CompactReportTests.AttemptsFromDifferentPipelineNamesRenderAsSeparateGroupsWithTheirStatusInTheOverview`, `CompactReportTests.RunsWithoutDistinctPipelineNamesRenderOneUngroupedList` | A status cell carries "n of m failed" as its title |
| `BugsViewTests.EachBugIsListedOnceWithEveryTestLinkedToIt`, `BugsViewTests.EntryShowsTheBugInItsOwnProjectAndMarksAnUnreadBug`, `BugsViewTests.GroupedBuildKeepsOneStatusColumnPerGroup` | The heading holds the bug's chip, red or grey, and spans the new columns; the Without an open bug group closes the view; a grouped heading counts per group |
| `BugsViewTests.ViewSitsAfterByErrorWithItsEntryCountInTheTab` | The tabs and the sections share the new order |
| `ReportRefinementTests.OverviewLinksTheLowestOpenBugInItsOwningProjectAndTheCardMarksOnlyTheUnreadBug` | The Open bugs column with `+1`, the chip's rules, and the card's bugs as chips, the unread one grey |
| `ReportRefinementTests.ALongNameIsCutBeforeTheOpenBugMarkerAndKeepsItsWholeTextAsTitle` | Became `ALongNameIsCutInItsOwnColumnAndKeepsItsWholeTextAsTitle`: the class and the bug in columns of their own, the class cut like the name |
| `TestBugResolutionTests.BugsInTheCompletedAndRemovedCategoriesAreLeftOutWhateverTheStateName`, `FailedBugLookupAddsAWarningAndTheReportStillRenders`, `UnreadableAssociatedBugKeepsItsLinkWithAWarning`, `UnusableBugProjectFallsBackToTheBuildProject`, `ClosedAssociatedAndLinkedBugsAreAbsentFromTheSetAndTheCard` | A card's bugs and an attempt's Associated bugs are chips; the Open bugs column instead of the marker after the name |
| `TestFailureSignalTests.RowsAndTheCardSayHowLongATestHasFailed` | Became `RowsAndTheCardSaySinceWhenATestHasFailed`: the Since chip in every table and the card, its link and title, no streak length as text |
| `TestFailureSignalTests.ANewFailureSaysNewAndAnUnreadPreviousBuildSaysNothing` | The New chip in the Trend column and the card |
| `RunsViewTests.RunsTableHasItsOwnHeadingBeforeTheHistoryHeading` | Became `HistoryComesFirstThenTheRunsTableUnderItsOwnHeading` |
| `RunsViewTests.RunsTableShowsFailedDurationAndReportedTests`, `NumericColumnsAreRightAligned` | The ID column; passed and failed counts with their glyphs, a zero muted |
| `RunsViewTests.LatestRunIsMarkedAndRunsOutsideTheWindowAreMuted`, `WindowNoteIsASentenceUnderTheRunsTable` | Latest run ends the run cell, with the accent rule; History by test follows the note |
| `RunsViewTests.HistoryByTestHasOneRowPerTestOneCellPerBuildAndTheFailureStreak`, `HistoryByTestShowsDashesWithoutCellsAndStopsTheStreakAtAnyOtherOutcome`, `BarsAreLabelledWithTheBuildNumberAndTheSlotFitsTheLongestOne` | Became `HistoryByTestHasOneRowPerTestOneCellPerBuildAndTheTrend` and `HistoryByTestShowsDashesWithoutCellsAndANewFailureAfterAPass`: the Class and Trend columns; the chart's captions without ↗ |
| `UnlistedAttachmentTests.HeaderAndRunsTableSayTheAttachmentsWereNotListed` | Reads the whole Runs view, which now holds the history panel first |
| `StripTests.HistoryKeepsAllSixStatesTitlesCurrentMarkerAndToolkitLinksWithoutALegend` | Each cell holds its glyph, and with a label its encoded text |
| `RunHistoryChartTests.CurrentOutlineUnavailableHatchingAndEquivalentTableArePresent` | Also pins the This run pill and no ↗ |
| `GoldenTestFailureReportTests` | Gains the `large` variant |
| Goldens | The 10 failed-test goldens in `tests/Fixtures/Reports/` changed, and the 2 `large` ones were written, through the `update-goldens` skill; the comparison of their text with 0.8.0 shows only the changes above. The 40 Test Case goldens did not change |

No existing test was deleted; the renamed ones replace their earlier versions.

### Findings not fixed

| Finding | Reason |
| --- | --- |
| Tag a bug opened in this run as New, in a lighter red, as the developer asked in the last design pass | Moved to 0.9: it needs each bug's creation date (`System.CreatedDate`), a new property of `AdoTestBug`, which is public output, and a live check on Server 2020. The plan's section 9 keeps it with the other ideas that need the server |
| The overview's Latest error and the console table keep MSTest's "Test method X threw exception:" | A decision of the plan (section 10): switching them changes what `Get-AdoBuildTestFailure` shows. The attempt summaries and By error read the exception |
| The Test Case report has `scope="colgroup"` without a `colgroup`; `RichTextImageAlt` and `PartialTestCase` keep an ordinary space before a colon in French | Carried from 0.8.0 and 0.7.5: 0.8.5 changes only the failed-test report, and the fixes change the Test Case goldens |
| The findings of 0.8.0, 0.7.10, 0.7.5, 0.7.0, 0.6.5 and 0.6.0 | Unchanged; see the [0.8.0](archive/release-0.8.0.md#findings-not-fixed), [0.7.10](archive/release-0.7.10.md#findings-not-fixed), [0.7.5](archive/release-0.7.5.md#findings-not-fixed), [0.7.0](archive/release-0.7.0.md#findings-not-fixed), [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes |

### Known limitations

| Limitation | Notes |
| --- | --- |
| Clusters are heuristic | Two errors whose text differs only in numbers, paths, GUIDs, URLs or hexadecimal IDs share a cluster, whatever those values mean; text in another language, or a different wording of the same failure, stays apart. The facts row, the sample and the Values column show what each cluster merged |
| The trend reads the history window | New and Since come from the builds that `-HistoryCount` read; the day is the one that build finished, in the export's time zone, so a run of failures that started before the window shows the day of the window's first build |
| Browser features | The ordinal's width from 100 tests uses `:has()`; a browser without it keeps two digits' width, and three-digit ordinals then end one digit further. How the work browser treats a report opened from a file is still V-27 |
| Browser checks | Headless Edge 154 ran 36 checks of the previews in the design rounds (plan, section 11: tab order, `j`, `k`, `o`, search, the Without an open bug filter, jumps to a cluster, to `no-bug`, to a far card and from a card's attachment to its attempt, no horizontal scroll at 1440 px, print emulation). They were not repeated on the final build, which differs from the reviewed previews only by B1 and the removed ↗ rule |
| Report size | Each report is about 14 KB larger, the stylesheet's growth. Both size budgets hold |
| Limits carried over | The [0.8.0](archive/release-0.8.0.md#known-limitations), [0.7.10](archive/release-0.7.10.md#known-limitations), [0.7.5](archive/release-0.7.5.md#known-limitations) and [0.7.0](archive/release-0.7.0.md#known-limitations) known limitations still apply, with V-28, V-33 to V-36, V-31, V-32 and V-02 |

## Work-PC Live checks

Install the 0.8.5 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass. No check gates 0.8.5.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | Record `Get-FileHash` of `%APPDATA%\AdoToolkit\config.json`, install 0.8.5, then run `Get-AdoProfile` and `Connect-Ado` | The hash is unchanged, no warning appears, and `Get-Module AdoToolkit -ListAvailable` shows 0.8.5 with seven files in its folder |
| 2 | `Export-AdoBuildTestFailure -Open` for a failed build with many failures, a history of 10 builds or more and bugs, in the browser used at work, with scripts allowed and then blocked | The views in their new order; one line per Overview row at the usual window width, without horizontal scroll; the cards' counts plausible against the table; By error merging the messages you would merge by hand, and no cluster that merges different failures (report the counts only); Since dates matching the builds; bug chips red, an unread bug grey; the card's run history, bugs and attachments; Shift+Tab, `j`, `k`, search and the print dialog as in 0.8.0. No Content Security Policy error in the console |
| 3 | The same report in French (`-Culture fr-CA`) | The band, the cards and the bug headings fit, and the attempt titles line up |
| 4 | Checks 2 to 6 of the [0.8.0 notes](archive/release-0.8.0.md#work-pc-live-checks), with the 0.8.5 package | As listed there: V-33 and the stage timing, `-SkipAttachments`, the probes of V-28 and V-34 to V-36, the browser checks, and the 0.7.5 checks with V-31, V-32, V-02, V-01 and V-03. All still pending |

## Local validation and developer handoff

Run on 2026-10-03 on the development PC, on the tree that holds these notes.

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0. All seven stages pass without a warning: 63 PowerShell files linted, 292 tooling Pester tests, 147 configuration files, 83 Markdown files with 237 links and 237 path references, the hook and skill layout, 2 workflow files linted with actionlint, and the product check, which staged and inspected AdoToolkit 0.8.5 |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as both workflows run it | Exit 0 with the exact module pins in `tools/BuildModules.psd1`. The variable was unset before and was unset again afterwards |
| Core tests in the final gate | 1552 passed under en-US and 1552 under fr-CA, with no failures or skips: 70 more cases than 0.8.0 |
| Product Pester tests against the staged module | 179 passed, none skipped, as for 0.8.0; three of them now pin the new markup. The tooling tests are 292, as for 0.8.0 |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `0.8.5` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.8.5` with seven files, and 0 warnings |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/` |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.8.5-win-x64.zip -ModuleVersion 0.8.5 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.8.5, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.8.5/`. The portable ZIP has the same seven files under `module/`, the launcher, `README.txt` and `bundle.json` (module 0.8.5, PowerShell 7.6.6, win-x64, PowerShell SHA-256 equal to the published hash and to the pin of the setup action), 669 files in all. Each `.sha256` file holds the hash of its ZIP. The installer is byte-identical to its source |

The test of B1 failed before its fix in this session, as Bug fixes records. No browser opened
the final report, and no Live script ran. Not run: the installer against the 0.8.5 ZIP, the
workflows, which only GitHub runs, and the squash merge, which happens on GitHub after these
notes.

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.8.5-win-x64.zip` | 110420793 | `685ea22c70f9aa444b319bfe203631524ae99a5bca8ea210a24038a461246b49` |
| `AdoToolkit-0.8.5.zip` | 813879 | `c818b888cc0ea1b3c64a087fad418a56dba58bdb5979aa1292971d20a6a35edb` |
| `Install-AdoToolkit.ps1` | 13032 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes. No tag or GitHub release exists for 0.8.5. Every
change is uncommitted on the branch `0.8.5`, which tracks `origin/0.8.5`. The developer owns
publication:

1. Run the first command of the handoff. It commits every change, creates the tag `v0.8.5`
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
6. Start the next branch from the updated `main`, and archive the 0.8.5 plan there as
   `docs/AGENTS.md` describes.
