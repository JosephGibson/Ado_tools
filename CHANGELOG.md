# Changelog

What changes for a user of AdoToolkit in each version, newest first, in the style of
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Each version links to its release
notes, which list every change, the validation evidence and the known limitations. The
section of a version also opens the text of its
[GitHub release](https://github.com/JosephGibson/Ado_tools/releases).

## 0.9.5 - 2026-10-04

### Fixed

- The failed-test report no longer shows the replacement character where it cuts a long error line or a long build number inside an emoji: the Error column, its tooltip, the default table of the failed tests and the bar captions of the run-history chart keep whole characters.
- Piping an object you built by hand, without its numeric ID, now reports an error for that object in your language and leaves the rest of the pipeline running; it used to end the pipeline with an untranslated .NET error.
- Four help topics say what their examples do, and four guides link every cmdlet they use, in both English and French.

Details: [release notes](docs/release-0.9.5.md)

## 0.9.0 - 2026-10-04

### Changed

- The module itself is unchanged apart from its version number: the cmdlets, the reports, the help and the configuration file are those of 0.8.5.

Details: [release notes](docs/release-0.9.0.md)

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
