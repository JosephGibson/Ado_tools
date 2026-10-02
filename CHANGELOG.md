# Changelog

What changes for a user of AdoToolkit in each version, newest first, in the style of
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Each version links to its release
notes, which list every change, the validation evidence and the known limitations. The
section of a version also opens the text of its
[GitHub release](https://github.com/JosephGibson/Ado_tools/releases).

## 0.7.10 - 2026-10-02

### Changed

- The module itself is unchanged apart from its version number: the cmdlets, the reports, the help and the configuration file are those of 0.7.5.

Details: [release notes](docs/release-0.7.10.md)

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

Details: [release notes](docs/release-0.7.5.md)

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
