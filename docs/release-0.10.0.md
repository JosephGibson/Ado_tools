# AdoToolkit 0.10.0 release notes

Prepared on 2026-10-05 from the branch `0.10.0`, which starts at `main` after the pull
request of 0.9.15 was squash-merged as `a17e5c7`; the files there are those of 0.9.15. The
reviewed changes are the one commit already on the branch, `8e20e46`, and the working tree on
top of it. 0.10.0 gives the failed-test export a second format. `Export-AdoBuildTestFailure
-Format Csv` writes, instead of the HTML report, one flat CSV file per build with a row per
reported test — its error, its owner, its Test Case, its open bugs and its trend — in fixed
English columns, for a spreadsheet or a script. The CSV export downloads nothing and needs no
connection, and a text cell that a spreadsheet could read as a formula starts with an
apostrophe. A new parameter makes this a minor version. This is an offline review. No Azure
DevOps Server connection, no `tests/Live/*.Live.ps1` run and no spreadsheet application was
used, so nothing below is confirmed live, and how Excel reads the file is owed to the manual
Excel check under [Work-PC Live checks](#work-pc-live-checks). The areas the README lists as
confirmed are still connections, projects, builds and test runs.

## What changed

0.10.0 adds one parameter, `-Format`, to `Export-AdoBuildTestFailure`, with its own public
enum `AdoToolkit.Core.Reporting.TestFailures.TestFailureReportFormat`, one error ID and one
key in the string catalog. It sends no new request and adds no cmdlet and no configuration
setting. The HTML report is unchanged: no report golden changed. `ReportFormat`,
`Export-AdoTestCase`, the configuration file format, the JSON report schema and the
seven-file package layout are unchanged. The [0.9.15 notes](archive/release-0.9.15.md) describe the
last change to the report, and the [0.9.10 notes](archive/release-0.9.10.md) the last change
to the development tooling before it.

### The CSV export

| Area | 0.10.0 behavior |
| --- | --- |
| `-Format` | `Html`, the default, writes the report and downloads its attachments as before. `Csv` writes one CSV file per set instead. The type is `TestFailureReportFormat` (`Html`, `Csv`), kept apart from `ReportFormat`, the formats of `Export-AdoTestCase`, which do not change. Any other value, such as `Json`, fails parameter binding with an `InvalidArgument` error before any input is read |
| Rows | A header, then one record per test of the report, in report order and with the report's `Ordinal`. Flaky tests have a row only with `-IncludeFlaky`, as they have a place in the report only with it |
| Columns | 16, in this order: `Build`, `Ordinal`, `Test`, `Title`, `Classification`, `Attempts`, `Latest error`, `Owner`, `Priority`, `Test case ID`, `Test case state`, `Open bugs`, `Bug IDs`, `Bug states`, `New`, `Since`. `CsvTestFailureRenderer.Columns` holds them; they are never translated or reordered |
| Values | The report's own: `Latest error` is the whole message of the attempt whose first line the report's table shows, line breaks included; `Owner` is the display name, then the unique name in angle brackets; `Test case state` is filled only for a Test Case that was read; `Open bugs` counts the bugs that are open; `Bug IDs` and `Bug states` list every bug of the test's bug list, in ID order, joined by `; `, with an empty state for a bug that could not be read; `New` and `Since` are the report's trend. `Since` is the day the first build of the run of failures finished, as `yyyy-MM-dd` in the export's offset, or that build's number when the day is not known, and is empty unless `New` is `False`. Two helpers of `HtmlTestFailureRenderer`, the finish day and the identity text, are now shared, so the CSV file and the report cannot disagree |
| Culture | None. Header names are English, numbers and `True`/`False` invariant, dates `yyyy-MM-dd`; Azure DevOps text is written as the server sent it. `-Culture` has no effect, and an unsupported culture, given or configured, gives no warning for a CSV file: `TestFailureExportPlan.Warnings` is empty for one, because the file is the same in every culture |
| Encoding | UTF-8 with a byte order mark, a comma between fields, CRLF after every record including the last, and RFC 4180 quoting: a field holding a comma, a quote, a carriage return or a line feed is quoted, and its quotes are doubled. A lone surrogate half, which UTF-8 cannot hold, becomes U+FFFD; an error message over 1 MiB is kept whole |
| Formula safety | A text cell that starts with `=`, `+`, `-`, `@`, a tab or a carriage return gets an apostrophe in front of it, so that a spreadsheet shows it as text instead of evaluating it. This covers every text column, the build number that `Since` can fall back on included. Integers and dates are never changed, a negative priority included |
| Path | An existing directory receives `Build-<id>-TestFailures.csv`; without `-Path` that is the Downloads folder. A path ending in `.csv`, in any case, names the file when exactly one set arrives; each later set gets the per-input `TestFailureReportSingleFile` error, as an `.html` path does. No directory is created. Any other path, a missing directory with or without a trailing separator included, is refused in `BeginProcessing` with the terminating `TestFailureCsvPathInvalid` error, category `InvalidArgument`. A `.csv` path whose directory does not exist, or a name that is only `.csv`, gets the per-input `AdoFileOutput` error instead. Nothing is written in any of these cases |
| Switches | `-SkipAttachments`, `-AllRunAttachments`, `-AttachmentWindowDays` and `-Culture` have no effect, and `-Connection` is not used: the file links no attachment, so the export needs no connection and sends no request. `-NoClobber` refuses an existing file when the export is planned and again when it is written. `-WhatIf` and `-Confirm` name the CSV file, once per set. `-Open` hands the file to its default application, and a launch that fails is a warning |
| Writing | Through `AtomicFileWriter`: rendered into a temporary file beside the target, read back by `CsvTestFailureRenderer.Validate` — the byte order mark, strict UTF-8, the header, and one record of full width per reported test in report order — then moved into place. `-Verbose` writes the same lines as an export that downloads nothing |
| Output | The `FileInfo` of the file, without an `AttachmentDirectory` note property, and its `file:///` address on the information stream with the `PSHOST` tag, as for the report |

### Opening a file named .csv

| Area | Change |
| --- | --- |
| `ShellDocumentLauncher.Open` | Accepts a `.csv` path. The failed-test export writes every text cell formula-safe and calls it directly |
| `ShellDocumentLauncher.CanOpen` | Unchanged: `.csv` is not in its list. `Export-AdoTestCase -Open` asks it first, so a Test Case document written under a `.csv` name, whose text is not formula-safe, is still written and never opened; the warning names it, as before |

### Code

| File | Change |
| --- | --- |
| `src/AdoToolkit.Core/Reporting/TestFailures/TestFailureReportFormat.cs` | New public enum, `Html` and `Csv` |
| `src/AdoToolkit.Core/Reporting/TestFailures/CsvTestFailureRenderer.cs` | New: the columns, the rows, quoting, formula safety and the read-back check |
| `src/AdoToolkit.Core/Reporting/TestFailures/TestFailureExporter.cs` | `PrepareCsv` resolves and checks the path with no generation folder; `ExportCsv` writes, checks and commits the file with the export's Verbose lines |
| `src/AdoToolkit.Core/Reporting/TestFailures/TestFailureExportPlan.cs`, `src/AdoToolkit.Core/Reporting/TestFailures/TestFailureExportOptions.cs` | `Format` on the options; the plan keeps its own `ReportPath`, its generation folder is null for a CSV file, and so are its warnings |
| `src/AdoToolkit.Core/IO/ReportFileNames.cs` | `TestFailures(buildId, format)`, with `Html` as the default, gives `.html` or `.csv` |
| `src/AdoToolkit.Core/Reporting/TestFailures/HtmlTestFailureRenderer.cs` | `FinishedOn` and `Identity` become shared internal helpers; the report's output is unchanged |
| `src/AdoToolkit.Core/IO/ShellDocumentLauncher.cs` | `Open` accepts `.csv`, as above |
| `src/AdoToolkit.PowerShell/Commands/ExportAdoBuildTestFailureCommand.cs` | The `-Format` parameter and the CSV path rule in `BeginProcessing` |
| `src/AdoToolkit.Core/Resources/AdoMessage.cs`, `src/AdoToolkit.Core/Resources/Strings.resx`, `src/AdoToolkit.Core/Resources/Strings.fr.resx` | One key, below |

### Strings

| Key | English | French |
| --- | --- | --- |
| `TestFailureCsvPathInvalid` | The path must be an existing directory or a .csv file: {0} | Le chemin doit désigner un répertoire existant ou un fichier .csv : {0} |

### Documentation

| Area | Change |
| --- | --- |
| `docs/commands/en-US/Export-AdoBuildTestFailure.md`, `docs/commands/fr-CA/Export-AdoBuildTestFailure.md` | The synopsis, the syntax, two `DESCRIPTION` paragraphs on the CSV file — its columns, values, encoding, quoting and formula safety — Example 4 with `Import-Csv`, the `-Format` parameter, and what `-Culture`, `-Path`, `-SkipAttachments`, `-AllRunAttachments`, `-AttachmentWindowDays`, `-IncludeFlaky`, `-NoClobber`, `-Open`, `-Connection`, `-WhatIf`, `-Confirm` and `OUTPUTS` do with a CSV file |
| `docs/guides/build-report.md` | A section, [A CSV file for a spreadsheet](guides/build-report.md#a-csv-file-for-a-spreadsheet): an example, the path rules, a table of the columns, the encoding, formula safety and how to import the file into Excel when the list separator is a semicolon; a row in the options table and two troubleshooting rows |
| `docs/guides/pipeline-triage.md` | A row for `-Format Csv` in the options table |
| `README.md` | The version, the features row, a paragraph on this version and the links to these notes |
| Release preparation | The `-Path` text of both help topics and of the guide said that every refused CSV path gets an `InvalidArgument` error. A `.csv` path whose directory does not exist gets `AdoFileOutput`, as the export's own path check gives it, so the three texts now say which error each path gets. The text was new in this version and never released |
| Guides and installer | `docs/guides/getting-started.md` and the examples of `tools/package/Install-AdoToolkit.ps1` name the 0.10.0 files; the installer is otherwise that of 0.9.15 |
| Archive | The 0.9.10 notes moved to `docs/archive/release-0.9.10.md`, unedited. The archive index lists them, and the changelog and the 0.9.15 notes point at the new place. The 0.9.10 notes define no `V-nn` item, so no citation moved |

### The development tooling

| Area | Change |
| --- | --- |
| `.agents/skills/release/SKILL.md` | `8e20e46`: the last developer step gives the next branch as one command line, run once the pull request has closed: it fetches the merge, creates the branch without tracking and sets its upstream in one go. The 0.9.15 notes were brought into line with it |

### Tests

| File | Change |
| --- | --- |
| `tests/AdoToolkit.Core.Tests/Reporting/TestFailures/CsvTestFailureRendererTests.cs` | New, 12 tests and 22 cases, in both test cultures: the columns and their order; the header alone for an empty set; the byte order mark and CRLF after every record; RFC 4180 quoting of commas, quotes and line breaks; each of the six formula triggers in every text column, with integers left alone; several bugs in list order with an unread one; hostile remote text; a lone surrogate half; an error over 1 MiB; the trend cells against the report's own Since; the same bytes in every report and process culture; and six damaged files that the read-back check refuses |
| `tests/AdoToolkit.Core.Tests/Reporting/TestFailures/TestFailureExporterTests.cs` | Two new tests: a CSV export writes one file, downloads nothing even with every run's attachments asked for, has no culture warning, writes the Verbose lines of an export without downloads and opens the file with `-Open`; and the path rules, with `-NoClobber` refusing at plan and at write |
| `tests/AdoToolkit.Core.Tests/Reporting/TestCaseExporterTests.cs` | One new test: a Test Case document named `.csv` is written and never opened |
| `tests/AdoToolkit.PowerShell.Tests/TestFailureExport.Pester.ps1` | Three new tests against the staged module and the fake server: HTML stays the default and `-Format Csv` writes the file with no request, `-WhatIf` writing nothing; the refused paths and an unknown format, with their error IDs and categories; and `-NoClobber` on an existing CSV file |

### Existing tests changed

| Test | Change |
| --- | --- |
| `tests/AdoToolkit.Core.Tests/IO/ReportFileNamesTests.cs` | `FailedTestReportFolderAndAttachmentNamesAreInvariant` also asserts the `.csv` name, in each culture it already covers |
| `tests/AdoToolkit.Core.Tests/Reporting/TestFailures/TestFailureExporterTests.cs` | The `Options` helper takes a `format`, `Html` by default, so no existing call changed |

No golden changed, no test was deleted, and no acceptance tag changed.

### Visible changes for 0.9.15 users

- `Export-AdoBuildTestFailure` has a new `-Format` parameter. Its default, `Html`, writes the
  report of 0.9.15, whose goldens did not change, so a script written against 0.9.15 needs no
  change.
- `-Format Csv` writes one CSV file per set, with the 16 columns above, and downloads
  nothing: no connection is needed, and `-SkipAttachments`, `-AllRunAttachments`,
  `-AttachmentWindowDays` and `-Culture` have no effect on it.
- The file is UTF-8 with a byte order mark, comma-separated and RFC 4180 quoted. A text cell
  that starts with `=`, `+`, `-`, `@`, a tab or a carriage return carries a leading
  apostrophe, which a script reading the file sees as part of the value.
- With `-Format Csv`, `-Path` names an existing directory or a `.csv` file; no directory is
  created. A path with another ending is refused with `TestFailureCsvPathInvalid` before
  anything is written.
- A CSV export gives no culture warning, even for an unsupported culture.
- `-Open` on a CSV file starts its default application, usually a spreadsheet.
  `Export-AdoTestCase -Open` still never opens a file named `.csv`.
- Configuration files, the JSON report schema and the installation layout are unchanged, and
  the file names of the assets carry 0.10.0.

### Public contract changes

All additive; nothing was removed or renamed.

| Item | Change |
| --- | --- |
| `Export-AdoBuildTestFailure -Format` | New parameter, of type `TestFailureReportFormat`, default `Html`, in every parameter set |
| `AdoToolkit.Core.Reporting.TestFailures.TestFailureReportFormat` | New public enum: `Html`, `Csv` |
| The CSV columns | `Build`, `Ordinal`, `Test`, `Title`, `Classification`, `Attempts`, `Latest error`, `Owner`, `Priority`, `Test case ID`, `Test case state`, `Open bugs`, `Bug IDs`, `Bug states`, `New`, `Since`: the names, their order and their value formats, as above |
| `TestFailureCsvPathInvalid` | New error ID, category `InvalidArgument`, terminating, for a `-Path` that names neither an existing directory nor a `.csv` file |

## Bug fixes

None. 0.10.0 corrects no defect of a cmdlet, a report or the configuration file, so section 3
of the `release` skill had nothing to prove: there is no regression test to fail without a
fix, because there is no fix. The `-Path` wording corrected during release preparation was
text new in this version, never released, and is listed under Documentation.

### Findings not fixed

| Finding | Reason |
| --- | --- |
| The Excel paragraph of `docs/guides/build-report.md` is unconfirmed | It says that French Excel opens a comma file as one column on a double-click, and names the French menu **Données** > **À partir d'un fichier texte/CSV**. Neither has been observed. With a semicolon list separator, a double-click is also expected to split a row at every semicolon, and `Bug IDs` and `Bug states` join several bugs with `; `, so a row with two bugs or more may spread over several columns rather than one, and a quoted line break that no longer opens a field may end its row. The guide stays as it is until the manual Excel check settles these points |
| A spreadsheet may retype values | Expected, not confirmed: a `Since` that falls back on a build number such as `20261002.1`, and a `Bug IDs` cell with a single bug, may be read as numbers, a `Since` day as a date in the regional format, and `True` and `False` as logical values. The file is right for RFC 4180 and for `Import-Csv`; changing the values to steer one spreadsheet would change the public columns. **Data** > **From Text/CSV** lets the reader set each column's type |
| Formula safety covers six leading characters | The decision named `=`, `+`, `-`, `@`, a tab and a carriage return. A full-width sign such as `＝` (U+FF1D) is written as it is. The Excel check carries one such cell, so that whether a spreadsheet evaluates it is observed rather than assumed |
| Two errors for refused CSV paths | A path that does not end in `.csv` is refused by the cmdlet with the terminating `TestFailureCsvPathInvalid`; a `.csv` path whose directory does not exist, or a name that is only `.csv`, reaches the export's own path check and gets the per-input `AdoFileOutput`, category `WriteError`. Nothing is written either way, and the help and the guide now say which error each path gets. One error would need a parent-directory check in `BeginProcessing`, a code change this release did not take |
| `Since` uses the export's current offset | The day is computed in the offset of the computer at export time, not the offset in force when the build finished, so across a daylight-saving change a build that finished within an hour of midnight can show the neighbouring day. The report has done the same since 0.8.5, and the CSV file shares its helper so the two always agree |
| `ShellDocumentLauncher.Open` accepts any `.csv` | The guard against opening a CSV file that is not formula-safe is the caller's `CanOpen`, which `Export-AdoTestCase` asks and which leaves `.csv` out. `OpenLeavesATestCaseDocumentNamedCsvClosed` pins that; a new caller must ask `CanOpen` too |
| The findings of 0.9.15, 0.9.10, 0.9.5, 0.9.0, 0.8.5, 0.8.0, 0.7.10, 0.7.5, 0.7.0, 0.6.5 and 0.6.0 | Unchanged; see the [0.9.15](archive/release-0.9.15.md#findings-not-fixed), [0.9.10](archive/release-0.9.10.md#findings-not-fixed), [0.9.5](archive/release-0.9.5.md#findings-not-fixed), [0.9.0](archive/release-0.9.0.md#findings-not-fixed), [0.8.5](archive/release-0.8.5.md#findings-not-fixed), [0.8.0](archive/release-0.8.0.md#findings-not-fixed), [0.7.10](archive/release-0.7.10.md#findings-not-fixed), [0.7.5](archive/release-0.7.5.md#findings-not-fixed), [0.7.0](archive/release-0.7.0.md#findings-not-fixed), [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes |

### Known limitations

| Limitation | Notes |
| --- | --- |
| How Excel reads the CSV file is not confirmed | Expected, not observed: the apostrophe of a formula-safe cell shows in the cell, no cell is evaluated as a formula, quoted line breaks stay inside their cell except perhaps on a double-click under French regional settings, accents survive both a double-click and **Data** > **From Text/CSV** thanks to the byte order mark, a double-click gives 16 columns under English regional settings and not under French ones, and the import with the delimiter **Comma** and the file origin **65001: Unicode (UTF-8)** gives 16 columns under both. The French label of **Data** > **From Text/CSV** is not confirmed either. The manual Excel check below settles each point; it is not a `V-nn` item, because it concerns a spreadsheet and not the server |
| `-Open` behaves as a double-click | It hands the file to its default application, so on a computer whose list separator is a semicolon, as in French, the file is expected to open in one column. Import it as the guide shows |
| One row per test | The CSV file has no attachment, no attempt of its own and no run history beyond `New` and `Since`. The HTML report remains the place for those |
| V-37: the two bug fields on Server 2020 | Owed since 0.9.15 and unchanged: whether Server 2020 returns `System.CreatedDate` and `System.AssignedTo` in a `WorkItemsBatch` field projection, and which shape the identity takes there. Both properties are null-tolerant, so without them the report makes no claim. `V-37` in `tests/Live/TestFailures.Live.ps1` settles it |
| Limits carried over | The [0.9.15](archive/release-0.9.15.md#known-limitations), [0.9.10](archive/release-0.9.10.md#known-limitations), [0.9.5](archive/release-0.9.5.md#known-limitations), [0.9.0](archive/release-0.9.0.md#known-limitations), [0.8.5](archive/release-0.8.5.md#known-limitations), [0.8.0](archive/release-0.8.0.md#known-limitations), [0.7.10](archive/release-0.7.10.md#known-limitations), [0.7.5](archive/release-0.7.5.md#known-limitations) and [0.7.0](archive/release-0.7.0.md#known-limitations) known limitations still apply, with V-01, V-02, V-03, V-27, V-28 and V-31 to V-36 |

## Work-PC Live checks

Install the 0.10.0 package on the work PC with the installer from the same release, and run
each check in a fresh PowerShell process. The rules from the
[0.2.0 audit](archive/release-0.2.0.md#work-pc-live-checks-least-confirmed-areas-first)
still apply: variables stay on the work PC, raw responses are not sent back, and
`INCONCLUSIVE` is not a pass. 0.10.0 owes no new `V-nn` check; V-37 is still first.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | `tests/Live/TestFailures.Live.ps1` with `ADOTOOLKIT_LIVE_TEST_BUILD_ID`, and `ADOTOOLKIT_LIVE_ASSIGNED_BUG_ID` set to a bug known to have an assignee | V-37, as in check 1 of the [0.9.15 notes](archive/release-0.9.15.md#work-pc-live-checks): `PASS V-37 BUG_FIELDS_PRESENT_AND_SHAPED`. `FAIL V-37 ASSIGNED_TO_SHAPE_UNKNOWN=<n>` means `WorkItemFieldValues.Identity` needs another shape, and `FAIL V-37 CREATED_DATE_ABSENT` that the marker can never appear. Without the assigned bug ID the assignee half is `INCONCLUSIVE`, not a pass |
| 2 | Export a failed-test report for a build whose tests have open bugs, in English and in French, then export the same set with `-Format Csv -Verbose` | The report: checks 2 and 3 of the [0.9.15 notes](archive/release-0.9.15.md#work-pc-live-checks), the `✦` marker, the Open bugs counts, the filing days and assignees, search by owner, and print. The CSV file: one row per test of the Overview, in its order; for each test, `Open bugs`, `Bug IDs`, `New` and `Since` equal to what the report shows; `Owner` in the form Server 2020 sends; `Import-Csv` reads 16 columns; and the Verbose lines count no download and no request |
| 3 | Check 4 of the [0.9.15 notes](archive/release-0.9.15.md#work-pc-live-checks), with the 0.10.0 package | The configuration file untouched by the installation, `Get-Module AdoToolkit -ListAvailable` showing 0.10.0 with seven files, then the earlier checks: the one `S0-9` verdict of `tests/Live/Connection.Live.ps1`, the Error column and the chart captions of a report whose text is cut, V-33, `-SkipAttachments`, the probes of V-28 and V-34 to V-36, and V-31, V-32, V-02, V-01 and V-03. All still pending |

### Manual step: the Excel check

Not a `V-nn` item, and it needs no Azure DevOps connection: run it on any Windows computer
with Excel, under English and then French regional settings. A synthetic file written by
`Export-AdoBuildTestFailure -Format Csv` from a hand-built set, and a checklist, were prepared
for this release under `artifacts/excel-check/`, which Git ignores, so they are not in the
repository.

1. Open the file by double-click, then again in a blank workbook with **Data** >
   **From Text/CSV**, delimiter **Comma**, file origin **65001: Unicode (UTF-8)**.
2. For each test cell, record whether the leading apostrophe shows, whether any cell is
   evaluated as a formula, whether the line breaks of the multi-line error stay in one cell,
   and whether the accented text, `« L’été »` among it, survives.
3. Record the column count of each row under English and under French regional settings,
   and the exact French label of **Data** > **From Text/CSV**.

Every expected result in the checklist is marked expected, not confirmed. Correct the Excel
paragraph of `docs/guides/build-report.md` if the observations differ from it.

## Local validation and developer handoff

Run on 2026-10-05 on the development PC, on the tree that holds these notes.

| Check | Result |
| --- | --- |
| `pwsh -NoProfile -File .\tools\dev.ps1 verify`, the final run after every change | Exit 0 with no warning, the product gate running beside the in-process stages. All seven stages pass: 71 PowerShell files linted, 339 tooling Pester tests, 147 configuration files, 89 Markdown files with 298 links and 366 path references, 4 hook helpers, 5 skills and 3 subagents in the layout, 2 workflow files linted with actionlint, and the product check, which staged and inspected AdoToolkit 0.10.0 |
| The same with `ADOTOOLKIT_RELEASE_BUILD=1`, as both workflows run it | Exit 0 with no warning, with the exact module pins in `tools/BuildModules.psd1` — Pester 5.9.1, PSScriptAnalyzer 1.25.0 and Microsoft.PowerShell.PlatyPS 1.0.3 — and the same counts. The variable was set for that process only and was never set in the shell |
| Core tests in the final gate | 1588 passed under en-US and 1439 under fr-CA, with no failures or skips: 25 more in each culture than 0.9.15, the 22 cases of the CSV renderer, the two CSV export tests and the Test Case open test |
| Product Pester tests against the staged module | 183 passed, none skipped, for 180 in 0.9.15: the three CSV export tests. The tooling tests are 339, as in 0.9.15 |
| `dotnet msbuild src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj -getProperty:Version -nologo` | Prints `0.10.0` |
| `tools/package/Publish-AdoToolkitPackage.ps1 -NoRestore` | Exit 0; builds without restoring and stages `AdoToolkit/0.10.0` with seven files, and 0 warnings |
| `tools/package/New-AdoToolkitRelease.ps1` with `artifacts/runtime-download/PowerShell-7.6.6-win-x64.zip` and its published `hashes.sha256` | Exit 0; writes all five assets to `artifacts/release/`. The archive's SHA-256 was checked first against the published `hashes.sha256` and the setup action's `POWERSHELL_SHA256`: all three agree |
| `tools/package/Test-AdoToolkitPortable.ps1 -ArchivePath ./artifacts/release/AdoToolkit-0.10.0-win-x64.zip -ModuleVersion 0.10.0 -PowerShellVersion 7.6.6` | Passes: AdoToolkit 0.10.0, PowerShell 7.6.6, Windows x64 |
| Archive checks | The module ZIP holds the seven expected files under `AdoToolkit/0.10.0/`. The portable ZIP has the same seven under `module/`, both launchers, `README.txt` and `bundle.json` (module 0.10.0, PowerShell 7.6.6, win-x64, PowerShell SHA-256 `02fe458be20493fbdf43f61ea20610b811ee6c738ab1676c61b9cfcd1a33c860`, equal to the published hash and to the pin of the setup action), 669 entries in all. Each `.sha256` file holds the hash of its ZIP, and the installer is byte-identical to its source |
| `tools/package/Install-AdoToolkit.ps1 -Path ./artifacts/release/AdoToolkit-0.10.0.zip -WhatIf` | Exit 0: the checksum and the layout are accepted and the target is named, `Documents\PowerShell\Modules\AdoToolkit\0.10.0`. Nothing was written, and no module was installed |
| `pwsh -NoProfile -File .\tools\dev.ps1 diagnose` | `ok`, with every required tool present and both recommended tools present: ripgrep 15.2.0 and `gh` 2.102.0. The handoff below therefore opens the pull request with `gh` |
| The Excel check file | The staged 0.10.0 package's `Export-AdoBuildTestFailure -Format Csv -IncludeFlaky`, given a hand-built synthetic set, wrote 13 rows of 16 columns with the byte order mark, which `Import-Csv` reads back. The file and its checklist are under `artifacts/excel-check/`. Excel was not run |

No test failed in either gate run, so nothing is recorded as a flake.

No Live script ran: V-37, the Excel check and every earlier check are the developer's. Not
run: an actual installation, the workflows, which only GitHub runs, a `repo-review` run, and
the squash merge, which happens on GitHub after these notes.

Local assets:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `AdoToolkit-0.10.0-win-x64.zip` | 110432249 | `92c5e8985c11eb8f8ae6eace6f0da1fc5afbb3072f37318f8b261e2cde941206` |
| `AdoToolkit-0.10.0.zip` | 825348 | `20fa5fd32200a68bba260b9edc6b364fb2cb9ca9cbb9159bb9fec54978e5c8b8` |
| `Install-AdoToolkit.ps1` | 13035 | Same file as `tools/package/Install-AdoToolkit.ps1` |

The two `.sha256` files hold these hashes. No tag `v0.10.0` exists. The branch `0.10.0`
tracks `origin/0.10.0`, which already holds `8e20e46`; every other change is uncommitted.
The developer owns publication:

1. Run the first command of the handoff. It commits every change, creates the tag `v0.10.0`
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
5. Run the Excel check from its checklist, and the work-PC checks above with the published
   package, V-37 first, and record any inconclusive coverage.
6. Once the pull request has closed, start the next branch from the updated `main` with the
   command line the handoff gives: it fetches the merge, creates the branch without tracking
   and sets its upstream.
