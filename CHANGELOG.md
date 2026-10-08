# Changelog

What changes for a user of AdoToolkit in each version, newest first, in the style of
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Each version links to its release
notes, which list every change, the validation evidence and the known limitations. The
section of a version also opens the text of its
[GitHub release](https://github.com/JosephGibson/Ado_tools/releases).

## 0.12.5 - 2026-10-08

### Added

- The failed-test report recognizes Playwright's assertions, grouped by kind, expected value, the test's own message and the locator read from the call log, and shows the element or page that each timeout waited for.
- Playwright's action timeout is a generic error: By error lists it under Generic errors as Playwright timeout, with each test's element in Values, so a test's assertion failures stay its primary error.

### Changed

- By error is denser: each group heading and test row takes one line, cut at the width of the table with the whole text on hover, and a group's facts and sample share one row.

Details: [release notes](docs/release-0.12.5.md)

## 0.12.0 - 2026-10-06

### Added

- `reporting.errorRules` in the configuration file: named rules of wildcard patterns, in any language, that name the errors of the failed-test report, merge their wordings and mark the ones that come from the environment as generic.
- `Export-AdoBuildTestFailure -Format Csv` adds four columns at the end of each row: the test's `Primary error`, its `Error kind`, `Specific` or `Generic`, the `Error rule` that named it, and the number of `Distinct errors` of its failed attempts.
- `Get-AdoBuildTestFailure` keeps, for each earlier build of a test's history where it failed, the start of the error messages that its results listed, in `AdoTestHistoryEntry.ErrorMessages`, without a request more: the result listing now reads a ninth field, the error message, beside the eight of §15.9 step 3, and reads it leniently (V-39).
- In the failed-test report, By error says for each test whether its previous failed build ended with the same primary error, read from the start of the messages that the build's results listed, and says nothing when the comparison could be wrong (V-39).

### Changed

- The failed-test report reads the error of every failed attempt: By error makes a group of each error that a test had, lists the test under its primary error and greyed under its other errors, joins the English and French wordings of one error, and sets generic errors, such as a server that does not respond, apart under a heading of their own.
- In the Overview of the failed-test report, Most common errors counts every test that had an error in any failed attempt and leaves generic errors out, and a new Generic errors card counts the tests that had one; in Open bugs, Same error, not linked counts each test once.

Details: [release notes](docs/release-0.12.0.md)

## 0.11.5 - 2026-10-05

### Changed

- The module itself is unchanged apart from its version number: the cmdlets, the reports, the help and the configuration file are those of 0.11.0. This version changes the tools and the procedures that develop and release AdoToolkit.

Details: [release notes](docs/archive/release-0.11.5.md)

## 0.11.0 - 2026-10-05

### Added

- `Update-AdoToolkit` installs the newest AdoToolkit release from GitHub beside the running copy, after matching the download against both SHA-256 checksums that GitHub gives for it. A module installed with `Install-AdoToolkit.ps1` in a folder of `PSModulePath` gets a new version folder; a portable copy gets a new folder next to the old one, which stays as it was. Then open a new PowerShell window, or start the launcher in the new folder. When you already have the newest version, it says so and downloads nothing, and `-WhatIf` names the folder it would install into without downloading anything. The command works from 0.11.0 on: install 0.11.0 itself by hand.

### Changed

- AdoToolkit no longer contacts only your Azure DevOps server. `Update-AdoToolkit`, and no other command, connects to `api.github.com`, `github.com` and `release-assets.githubusercontent.com` over HTTPS through the system proxy, and sends them no Windows credentials. The releases are still unsigned: the checksums catch a damaged or altered download, not who published it.
- The update steps in the README, the getting-started guide, the `README.txt` of the portable bundle and the text of each GitHub release name `Update-AdoToolkit` for 0.11.0 and later.

Details: [release notes](docs/archive/release-0.11.0.md)

## 0.10.5 - 2026-10-05

### Fixed

- A response that is marked compressed but does not decompress no longer ends the command with an untranslated .NET error. An error response keeps the error of its HTTP status, such as `ObjectNotFound`; a successful one is a response format error and is not retried. In a failed-test export it fails that attachment only, where it used to stop the whole export.
- A response found malformed while its body is read is sent once, not retried three times.
- A failed-test report in which a test result and one of its sub-results list the same attachment is written; it used to fail its own check.
- Tab completion of `Get-AdoProfile -Name` escapes `[`, `]`, `*`, `?` and the backtick in the name it inserts, so that the completed name matches that profile only.
- `Install-AdoToolkit.ps1` keeps a backup left by a rollback that failed, the only copy of its version, until that version is installed again. It used to delete it at the start of the next installation.
- The help of `Export-AdoBuildTestFailure` says that **New** means that the build before ran the test and it passed, and the help of `Test-AdoConnection` how many requests its project URL check sends. The guides add `-Project` only to commands that have it, say that a `.csv` path takes one build, name each error by its ID and link every cmdlet they use.

### Security

- The default tables in the console show a control character in text from the server as a space, so that a title, a name or a state can no longer send escape sequences to your terminal. The objects keep the text as the server sent it.
- A work item whose project the server names `.` or `..` is reported as a response format error instead of giving links that leave the collection.

Details: [release notes](docs/archive/release-0.10.5.md)

## 0.10.0 - 2026-10-05

### Added

- `Export-AdoBuildTestFailure -Format Csv` writes one flat CSV file per build instead of the HTML report: a row per reported test with its error, owner, Test Case, open bugs and whether it is new or keeps failing, under fixed English column names, for a spreadsheet or a script. It downloads nothing and needs no connection, and a text cell that a spreadsheet could read as a formula starts with an apostrophe. Without `-Format`, the export writes the HTML report as before.

Details: [release notes](docs/archive/release-0.10.0.md)

## 0.9.15 - 2026-10-04

### Added

- The failed-test report says how old each open bug is and who has it. A bug filed at or after the build went into the queue carries **✦** on its chip, in a paler red, wherever the chip appears, and the Open bugs view counts those bugs above its table. Each bug the report read whole also shows the day it was filed and its assignee, or **Unassigned**, in the Open bugs view and on the test's card, where the report's search can find an owner by name.
- `Get-AdoBuildTestFailure` returns `CreatedDate` and `AssignedTo` on each bug of a reported test, read in the work item batch it already sent. Neither joins the default bug table; select them explicitly.

Details: [release notes](docs/archive/release-0.9.15.md)

## 0.9.10 - 2026-10-04

### Changed

- The module itself is unchanged apart from its version number: the cmdlets, the reports, the help and the configuration file are those of 0.9.5. This version changes the tools and the procedures that develop AdoToolkit.

Details: [release notes](docs/archive/release-0.9.10.md)

## 0.9.5 - 2026-10-04

### Fixed

- The failed-test report no longer shows the replacement character where it cuts a long error line or a long build number inside an emoji: the Error column, its tooltip, the default table of the failed tests and the bar captions of the run-history chart keep whole characters.
- Piping an object you built by hand, without its numeric ID, now reports an error for that object in your language and leaves the rest of the pipeline running; it used to end the pipeline with an untranslated .NET error.
- Four help topics say what their examples do, and four guides link every cmdlet they use, in both English and French.

Details: [release notes](docs/archive/release-0.9.5.md)

## 0.9.0 - 2026-10-04

### Changed

- The module itself is unchanged apart from its version number: the cmdlets, the reports, the help and the configuration file are those of 0.8.5.

Details: [release notes](docs/archive/release-0.9.0.md)

## 0.8.5 - 2026-10-03

### Added

- The failed-test report's Overview ends with cards that sum up the build: new and recurring tests, the most common errors, the tests without an open bug and, in a grouped build, each group.
- By error names each error's exception type and the frame of the tests' own code where they fail, underlines the parts of the message that differ between tests, and shows a sample message for an error that two tests or more share.
- Open bugs says on one line what each bug covers: its linked tests, the tests of the same Test Case or with the same error that it leaves out, how many failed results a link through test results reaches, and in a last group the tests without an open bug.

### Changed

- The failed-test report's views come in the order Overview, Runs and history, Details, By error, Open bugs. Every part of a test has a column of its own, a recurring failure says since which day instead of **N in a row**, bugs are red chips (grey when they could not be read), status colours are brighter, and the legends and ↗ glyphs are gone.
- By error also groups messages that differ only in paths, URLs or hexadecimal IDs, and groups an MSTest failure by its exception instead of its "Test method … threw exception:" line.
- A test's card shows its run history by date, its bugs as chips and its attachments, each file once, before its attempts; the Runs and history view starts with the run history chart.

Details: [release notes](docs/archive/release-0.8.5.md)

## 0.8.0 - 2026-10-03

### Added

- `Get-AdoBuildTestFailure -SkipAttachments` gathers the failed tests without their attachment lists, which saves one request per failed result and per rerun attempt; the set's new `AttachmentsListed` property is then `False`, its report says that the attachments were not listed, and its export needs no connection.
- With `-Verbose`, `Get-AdoBuildTestFailure` and `Export-AdoBuildTestFailure` write the requests and the time of each stage and of the whole command.
- The failed-test report marks a test **New** when it did not fail in the build before, or **N in a row** while it keeps failing, and shows each test's history as one square per build.

### Changed

- `testResults.maximumHistoryRequests` defaults to 500 instead of 400, and the earlier builds kept within it no longer depend on the order of the server's responses; a long history is read faster.
- Every request accepts gzip and deflate responses, and a JSON response larger than 256 MiB once decoded fails with an `AdoResponseFormat` error.
- The default table of failed tests shows the first line of the latest error instead of `Storage`.
- The failed-test report has a shorter header that starts with the counts, which also filter the tests, one type scale, a focus colour that is no longer the amber of flaky tests, **Expand all** and **Collapse all** in Details only, and search that ignores accents; a test opens on its latest error, with its stack trace on its own code.

### Fixed

- In the failed-test report, keyboard focus and a test reached from its row no longer land under the header, a long test name no longer hides its **Open bug** link, Back returns to the test's row, printed status squares keep their colour, error and bug headings name their rows for screen readers, and French labels keep a no-break space before the colon.

Details: [release notes](docs/archive/release-0.8.0.md)

## 0.7.10 - 2026-10-02

### Changed

- The module itself is unchanged apart from its version number: the cmdlets, the reports, the help and the configuration file are those of 0.7.5.

Details: [release notes](docs/archive/release-0.7.10.md)

## 0.7.5 - 2026-10-02

### Changed

- `Get-AdoWorkItem -Field` and `Invoke-AdoWiql -Query` refuse a value of whitespace only before any request is sent.
- The help topics and guides, in English and French, mark server behavior that is not confirmed with its `V-nn` item and correct several statements. French messages and help put a no-break space before colons.

### Fixed

- A request that no server answered, because the network failed or the connection was lost, says that the server could not be reached instead of "The server rejected the request."
- `-Definition` accepts an integer of any type, such as an ID read by `ConvertFrom-Json`.
- A hand-made connection object with a relative or non-HTTP collection URL is refused with a configuration error.
- In the failed-test report, a bug of another project links to its own project, **Expand all** and **Collapse all** work after a history cell has opened the Runs view, and a time at the limit of the calendar no longer fails the report.
- A custom field value that cannot be read no longer fails a failed-test result, a link closed by a table cell no longer breaks the step text of a Test Case report, a server error message cut inside a character no longer escapes as a .NET error, and a temporary file that cannot be deleted is reported as a file output error.
- `Install-AdoToolkit.ps1` keeps the previous copy when the move of the new copy and the rollback both fail, and ignores a blank entry in `PSModulePath`.

Details: [release notes](docs/archive/release-0.7.5.md)

## 0.7.0 - 2026-10-01

### Added

- The failed-test report has an Open bugs view: each open bug once, with the tests linked to it and whether the link comes from a test result or the Test Case.
- The Runs and history view of the failed-test report shows each test run's duration, failed and reported tests and listed and downloaded attachments, and a table with the outcome of every reported test in each build of the history.
- The configuration key `testResults.maximumConcurrentRequests` (1 to 16, default 6) sets how many requests `Get-AdoBuildTestFailure` and the downloads of `Export-AdoBuildTestFailure` have in progress at the same time.

### Changed

- `Get-AdoBuildTestFailure` sends up to six requests at the same time instead of one and combines results in input order; how Azure DevOps Server 2020 answers them is not confirmed yet, and the value `1` restores the earlier behavior. Near the history request limit, a failed history build can leave a different budget for older builds; see the release notes.
- `Get-AdoBuildTestFailure` leaves closed bugs out of `Bugs`: a listed bug is open, or could not be read.
- `Export-AdoBuildTestFailure` downloads by default the JSON and text attachments of up to 256 KiB from every test run inside the attachment window, not only from the latest run, starting with the latest run and several files at a time.
- The failed-test report shows the full error message and stack trace of every attempt instead of a reference to an earlier attempt with the same text, so a report with many retries is larger.
- The failed-test report lists diagnostics one per line with errors first, shows sizes in KB or MB, colors the build result, and puts when and where it was made under every view.

### Removed

- The Open mark on the bugs of a failed-test report card: every listed bug is open, and a bug that could not be read is marked Not read.

### Fixed

- Project and profile names complete correctly from double-quoted prefixes as well as single-quoted ones.

Details: [release notes](docs/archive/release-0.7.0.md)

## 0.6.5 - 2026-10-01

### Added

- A changelog: this file says what changes for users in each version, and each GitHub release opens with the section of its version.

### Changed

- The module itself is unchanged apart from its version number: the cmdlets, the reports, the help and the configuration file are those of 0.6.0.

Details: [release notes](docs/archive/release-0.6.5.md)

## Earlier versions

This file starts with 0.6.5.

| Version | Changes |
| --- | --- |
| 0.6.0 | [Release notes](docs/archive/release-0.6.0.md) |
| 0.5.0 | [Release notes](docs/archive/release-0.5.0.md) |
| 0.2.0 to 0.4.0 | [Archive](docs/archive/README.md) |
