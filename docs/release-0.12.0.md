# AdoToolkit 0.12.0 release notes

> the error classifier of the failed-test report: every failed attempt grouped by error, a primary error per test, generic errors set apart, English and French wordings joined, `reporting.errorRules`, four CSV columns, the previous build's error and V-39 to V-42

Prepared on 2026-10-06 from the branch `0.12.0`, which starts at `f494b12`, the commit of AdoToolkit 0.11.5 on `main`. 0.12.0 reads the error of every failed attempt in the failed-test report, as the [0.12.0 plan](plans/0.12.0-error-classifier.md) describes, so that By error sorts failures by their cause: at work, generic failures formed its largest groups and the English and French stages split one cause into several. Nothing in it was run against the work server: V-39 to V-42 are new and pending, and so are the live checks of 0.11.0. The release session took about 8 minutes from the request to the finishing script, with the release check run twice, because a typo in `docs/guides/build-report.md` was fixed after the first check had passed.

## What changed

### By error

Up to 0.11.5, By error grouped each test under the error of its latest attempt only, and keyed
a group on the literal text of that error. From 0.12.0 it reads the error of every failed
attempt, and [Reading the report](guides/build-report.md#reading-the-report) in the guide
describes the result.

| Part | Up to 0.11.5 | From 0.12.0 |
| --- | --- | --- |
| Groups | One per latest error; a test is in one group | One per error that any failed attempt had; a test is listed under its primary error and greyed under each of its other errors |
| Order | Largest group first, tests without an error message last | Specific errors, the primary error of the most tests first; then **Generic errors**, under a heading of their own; then the tests without an error message |
| Above the table | The number of distinct errors | The numbers of distinct and of generic errors |
| Group heading | Count, exception type and the first test's line | The tests whose primary error it is, **+N** for the others, the exception type or the name of the rule that named it, and the line, with the Expected and Actual lines of a recognized xUnit or NUnit message |
| Facts | Outcomes, trend, bugs, groups of attempts and the shared frame | Also **Forms**, with the test that joined two wordings, and **Classes**, the number of classes its tests are in; also shown for one test whose error has several wordings, and the tests whose previous failed build had the same error |
| Errors column | None | Where the error comes from, such as **3 of 4 failed attempts** or **Every failed attempt**, the test's other errors, each linked to its group, and **Same error in build N** or **Other error in build N** |
| Sample message | The first test's latest error | The latest attempt of the first test that had the group's error |

A test's **primary error** is its most frequent error among its failed attempts, generic errors
aside, and the latest one on a tie; a test with generic errors only takes the most frequent of
them. The 200 × 14 classifier test, 200 tests of 14 attempts with distinct English and French
messages, took 820 ms run alone in a fresh process on the development PC. The 200 × 14 report
measures 52,929,561 bytes with one error per test and 53,603,239 with three, under its budget
of 54,100,000 bytes; it measured 52,911,987 in 0.11.5.

### English and French wordings

The report recognizes the assertion messages of MSTest 2, 3 and 4 in English and French, and
those of NUnit 3 and 4, xUnit 2 and 3 and the wait timeout of Selenium, which exist in English
only. Each form was read from the framework's package and cites it in the code. A recognized
message is keyed by what identifies it, such as its expected value, so the English and French
forms of one assertion are one error when that text is the same. Two other wordings become one
error when one test failed with both at the same place, the same exception type and the same
frames of its own code with their line numbers, in an English group and a French group of
attempts. A group's language comes from the MSTest texts in it; a group with none, or with
both, pairs with no other, and two groups of one language never pair. The facts name the test
and the groups that joined the wordings, such as **Paired by SubmitOrder (Tests_EN, Tests_FR)**.
Whether the agents produce these texts is V-40.

### Generic errors and error rules

Five built-in rules, all generic, recognize a refused connection, a host name that does not
resolve, the HTTP statuses 502 to 504, a lost or never-created WebDriver session and a page
that did not load, in the English messages of Windows, .NET, Chrome, chromedriver and Selenium.
`reporting.errorRules` in the configuration file adds named rules of `*` and `?` patterns, in
any language, tried in file order before the built-in ones: a rule names the errors it matches
and merges them, and marks them generic unless it says `"generic": false`. Patterns are not
regular expressions and match in time linear in the line. A rule that breaks a limit is a
configuration error that names its place, and stops every command that reads the file, as any
invalid setting does. [Error rules](guides/configuration.md#error-rules) in the configuration
guide gives the limits, the matching and the table of the built-in rules.

### Overview and Open bugs

- **Most common errors** lists the three specific errors that the most tests had in any failed
  attempt, two tests at least; generic errors are left out.
- A new **Generic errors** card counts the tests **With a generic error** and those with **Only
  generic errors**, and opens them in By error. It is left out when no test had one.
- **Same error, not linked** starts from the primary error of each linked test and counts each
  other test that had it once, where it summed the tests of each group.
- The Overview table keeps **Latest error**, and the details keep opening on a test's latest
  error.

### CSV

`Export-AdoBuildTestFailure -Format Csv` appends four columns after `Since`: `Primary error`,
the whole message of the primary error's latest attempt; `Error kind`, `Specific` or
`Generic`; `Error rule`, a configured rule by its name or a built-in rule by its ID, such as
`ConnectionRefused`; and `Distinct errors`. The earlier columns keep their names and order, and
`Primary error` is formula-safe like the other text columns.

### History of a test

The result listing that pass 1 and the history read already send now also reads the error
message, a ninth field beside the eight of §15.9 step 3, leniently: a value that is not a
string reads as no message. `Get-AdoBuildTestFailure` keeps the starts of those messages, for
each earlier build where the test failed, in `AdoTestHistoryEntry.ErrorMessages`, without a
request more. By error compares the previous failed build's starts with the test's primary
error. It says **Other error** only when the error is not recognized by the frame of the test's
own code, which a listing does not carry, the differing message was not cut at 4,000
characters, it came from a stage or job where the test had its primary error this time, and
the build listed no more than five different messages for the test; otherwise, and when that
build listed no message, it says nothing. Whether Server 2020 lists the message is V-39.

### Unchanged

No cmdlet parameter changes. `Get-AdoBuildTestFailure`, `AdoTestFailure` and the console
table read no error rule and keep each test's latest error.

### Internal changes

- `tests/Live/Probes.Live.ps1` probes V-39: whether a test result listed without details carries the start of its error message, which the comparison of errors across builds relies on. It fails when the listing cuts a message before 4,000 characters, prints counts only and can run at work against 0.11.5; V-34, V-35 and V-39 now find a run with failures by the unanalyzed tests that Server 2020 sends.

### Visible changes for 0.11.5 users

- By error gains an Errors column, a Generic errors section and a test can show in several groups; the Overview gains a Generic errors card; Most common errors counts every test that had an error and leaves generic ones out; Same error, not linked counts each test once; an xUnit or NUnit heading shows its Expected and Actual lines.
- The Errors column of By error adds Same error in build N or Other error in build N, and the facts of a group count the tests whose previous error was the same.
- English and French wordings of one error share a group in By error, and the facts name the test that joined them.
- A CSV file of `Export-AdoBuildTestFailure -Format Csv` has four more columns at the end of each row: `Primary error`, `Error kind`, `Error rule` and `Distinct errors`.
- The `AdoTestHistoryEntry` objects in the `History` of `Get-AdoBuildTestFailure` output have an `ErrorMessages` property.
- A configuration file with an invalid `reporting.errorRules` stops every command that reads it with a configuration error, as any invalid setting does.

### Public contract changes

- Configuration file: `reporting.errorRules`, an array of at most 100 rules, each with `name`, `patterns` and `generic`, which defaults to `true`. An invalid rule is a configuration error that names its place, such as `reporting.errorRules[2].patterns[0]`.
- CSV file of `Export-AdoBuildTestFailure -Format Csv`: `Primary error`, `Error kind`, `Error rule` and `Distinct errors` are appended after `Since`; the earlier columns keep their names and order.
- Output type: `AdoTestHistoryEntry.ErrorMessages`, the distinct starts of the error messages that an earlier build's failed results of the test listed, at most five, each its first 20 lines and at most 8,192 characters; empty for the current build and for the tests read after a build's starts reach 2 million characters.
- Unchanged: the cmdlet names and parameters, `AdoTestFailure`, its console table and `docs/schemas/testcase.v1.schema.json`.

## Bug fixes

None: no change fragment of this version records a fix.

### Existing tests changed

- `ConfigurationStoreTests.CompleteConfiguration` gains three error rules, with an unknown key and an explicit `generic`, so the byte-for-byte save and the profile round trip cover them.
- ByErrorViewTests: the Errors column takes the colspans of the large and failed fixtures to 10 and 8; ErrorClustersTests reads the clusters of the model's classification; ErrorTextTests renders the 200 × 14 report with one and with three errors per test, 52,929,561 and 53,603,239 bytes against the 54,100,000 budget; the 12 failed-test goldens were regenerated, and the bilingual pair added, through update-goldens.
- `CsvTestFailureRendererTests`: records are 20 fields wide, the header and the RFC 4180 case carry the new columns, the formula case covers `Primary error`, and the trend case reads its two cells only; `TestFailureExport.Pester.ps1` checks the new header and a rule of the configuration in `Error rule`.
- `ServerWireShapeTests` reads nine listing fields, the error message among them, and is renamed `ResultListingsReadOnlyThePass1FieldsAndTheErrorMessage`; the history fixtures `results-history-399.json` and `results-history-400.json` list a message for each failed result.
- The bilingual fixture gives CheckTitle and AddToCart a previous failed build, so `OverviewCardsTests` sees New and recurring first and `ByErrorViewTests` checks AddToCart's cell with its comparison; the 14 failed-test goldens were regenerated through `update-goldens` for the new rule of the stylesheet.

### Findings not fixed

| Finding | Reason |
| --- | --- |
| Selenium's stale and non-interactable element messages are not recognized | The plan named them among the messages keyed by the frame of the test's own code, but they were not added to the catalog; the review of phase 5 found it, and the guides name only Selenium's wait timeout. Such an error is keyed by its line, as in 0.11.5, so the same message from different tests and places makes one group; a configured rule names it |
| The findings of 0.11.5, 0.11.0, 0.10.5, 0.10.0, 0.9.15, 0.9.10, 0.9.5, 0.9.0, 0.8.5, 0.8.0, 0.7.10, 0.7.5, 0.7.0, 0.6.5 and 0.6.0 | Unchanged; see the [0.11.5](archive/release-0.11.5.md#findings-not-fixed), [0.11.0](archive/release-0.11.0.md#findings-not-fixed), [0.10.5](archive/release-0.10.5.md#findings-not-fixed), [0.10.0](archive/release-0.10.0.md#findings-not-fixed), [0.9.15](archive/release-0.9.15.md#findings-not-fixed), [0.9.10](archive/release-0.9.10.md#findings-not-fixed), [0.9.5](archive/release-0.9.5.md#findings-not-fixed), [0.9.0](archive/release-0.9.0.md#findings-not-fixed), [0.8.5](archive/release-0.8.5.md#findings-not-fixed), [0.8.0](archive/release-0.8.0.md#findings-not-fixed), [0.7.10](archive/release-0.7.10.md#findings-not-fixed), [0.7.5](archive/release-0.7.5.md#findings-not-fixed), [0.7.0](archive/release-0.7.0.md#findings-not-fixed), [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes |

### Known limitations

| Limitation | Notes |
| --- | --- |
| V-39: the error message in result listings | Whether Server 2020 lists failed results with `detailsToInclude=None` with their `errorMessage`, starting as the result read alone does, and where it cuts a long one. The comparison with the previous failed build and `AdoTestHistoryEntry.ErrorMessages` rely on it; without it they stay empty and By error says nothing. `tests/Live/Probes.Live.ps1` checks it |
| V-40: MSTest texts on the agents | Whether the agents produce the MSTest 2, 3 and 4 forms of the catalog, in English and in French. A group's language comes from them, so a French group whose forms the catalog misses joins no English group; error rules still apply |
| V-41: French Windows and .NET Framework texts | The French forms of the Windows socket messages, of the .NET Framework network messages and of the default messages of `NullReferenceException`, `IndexOutOfRangeException`, `DivideByZeroException`, `OperationCanceledException` and `TaskCanceledException`, which the report ignores in English, are not confirmed. Until they are, a French refused connection or unknown host is generic only through a configured rule, and a French default message joins its English group only by pairing |
| V-42: browser driver texts | The chromedriver texts of a lost session and of a page that did not load are not confirmed, so such failures may not land under Generic errors; a configured rule covers them |
| No comparison for a test rerun inside its task | Its failed attempts are sub-results, which a listing does not include, and the parent carries no message of its own |
| A data-driven parent is classified by its own message | The messages of its sub-results stay in the card, and are not grouped |
| Pairing needs pipeline groups | A build without groups of attempts, or with groups whose language is unknown, joins no wordings; an error rule still merges them |
| Limits carried over | The [0.11.5](archive/release-0.11.5.md#known-limitations), [0.11.0](archive/release-0.11.0.md#known-limitations), [0.10.5](archive/release-0.10.5.md#known-limitations), [0.10.0](archive/release-0.10.0.md#known-limitations), [0.9.15](archive/release-0.9.15.md#known-limitations), [0.9.10](archive/release-0.9.10.md#known-limitations), [0.9.5](archive/release-0.9.5.md#known-limitations), [0.9.0](archive/release-0.9.0.md#known-limitations), [0.8.5](archive/release-0.8.5.md#known-limitations), [0.8.0](archive/release-0.8.0.md#known-limitations), [0.7.10](archive/release-0.7.10.md#known-limitations), [0.7.5](archive/release-0.7.5.md#known-limitations) and [0.7.0](archive/release-0.7.0.md#known-limitations) known limitations still apply |

## Work-PC Live checks

0.12.0 adds V-39 to V-42, which only the work server and its agents can settle. The rules of the
[0.11.0 notes](archive/release-0.11.0.md#work-pc-live-checks) apply: counts and verdicts only, no
work value copied out. V-39 can run before 0.12.0 is installed; the others read a report of 0.12.0.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | `tests/Live/Probes.Live.ps1` with `ADOTOOLKIT_LIVE_TEST_BUILD_ID` set to a build with failed tests | V-39: `PASS V-39 LISTED_MESSAGES_AGREE`. `LISTED_MESSAGES_CUT_SHORT`, `LISTED_MESSAGES_DIFFER` or `NO_MESSAGE_LISTED` is a `FAIL` that makes the comparison with the previous failed build unreliable or empty, and a finding for the next version. The V-34 and V-35 probes now also find a run with failures on Server 2020 |
| 2 | `Update-AdoToolkit -Verbose` in a new window with 0.11.5 or 0.11.0 installed, then the same in the portable console | V-38 in use: `Status` `Installed`, `LatestVersion` 0.12.0 and a `0.12.0` folder beside the old one; in a new window the loaded module is 0.12.0. An error is a finding to report by its error ID |
| 3 | `Export-AdoBuildTestFailure` on a build whose English and French stages failed, then By error | V-40: the English and French attempts of one MSTest assertion share a group, its facts show **Forms: 2**, and no group's line is the French "threw exception" wrapper. Count the groups that hold one form only while the same test failed in both stages |
| 4 | The same report, on a build where a French stage met a refused connection, an unknown host or a `NullReferenceException` | V-41: the network failures land under **Generic errors**, and the French `NullReferenceException` joins its English group. A failure outside them is the text a rule must cover; note its kind, not its text |
| 5 | The same report, on a build where a browser session was lost or a page did not load | V-42: those failures land under **Generic errors**, as **WebDriver session failed** or **Page load timed out** |
| 6 | The other checks of the 0.11.0 notes, with 0.12.0 | All still pending |

## Validation

<!-- release-check:begin -->

| Check | Result |
| --- | --- |
| `verify`, every stage but `documentation`, with `ADOTOOLKIT_RELEASE_BUILD=1` | Passed: `powershell-lint`, `powershell-test`, `configuration`, `tooling-layout`, `workflow-lint` and `project-check`, with Microsoft.PowerShell.PlatyPS 1.0.3, Pester 5.9.1 and PSScriptAnalyzer 1.25.0 |
| Tests that passed | tooling Pester 430, Core tests (en-US) 1987, Core tests (fr-CA) 1831 and product Pester 233 |
| The `documentation` stage and the changelog and README tests | Passed on these notes, before the handoff |
| Version and package | MSBuild gives 0.12.0; `AdoToolkit/0.12.0` packaged from the verified build |
| Release assets | `AdoToolkit-0.12.0-win-x64.zip` 110,530,076 bytes, `AdoToolkit-0.12.0-win-x64.zip.sha256` 96 bytes, `AdoToolkit-0.12.0.zip` 923,060 bytes, `AdoToolkit-0.12.0.zip.sha256` 88 bytes and `Install-AdoToolkit.ps1` 13,567 bytes |
| Portable launcher | passed: AdoToolkit 0.12.0, PowerShell 7.6.6, Windows x64 |
| `Install-AdoToolkit.ps1 -WhatIf` | Accepted the checksum and the layout of `AdoToolkit-0.12.0.zip` |
| Content | Every file the gate checked is unchanged, apart from the changelog, these notes and the archive index |

CI builds its own assets: compare the published files with the `.sha256` files of the release.

<!-- release-check:end -->