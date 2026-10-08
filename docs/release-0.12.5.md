# AdoToolkit 0.12.5 release notes

> Playwright errors in the failed-test report: its assertions grouped by what they assert, its action timeout set apart as a generic error, a denser By error with one line per heading and test row, and V-43

Prepared on 2026-10-08 from the branch `0.12.5`, which starts at `09ae930`, the commit of AdoToolkit 0.12.0 on `main`. 0.12.5 teaches the error classifier of 0.12.0 Playwright's messages, as the [0.12.5 plan](plans/0.12.5-playwright-errors-dense-by-error.md) describes: at work, Playwright's action timeout is the error seen most, and as a specific error it became the primary error of many tests and hid their assertion failures. Nothing in it was run against the work server: V-43 is new and pending, and so are V-39 to V-42 and the live checks of 0.11.0. The release session took about 6 minutes from the request to the finishing script, with the release check run once, in about a minute and a half.

## What changed

### Playwright errors

Up to 0.12.0, a Playwright message was keyed on its first line, which does not say what failed:
every action timeout of a build was one error, and so was every `Locator expected to be visible`,
whatever element. From 0.12.5 the report reads the call log that Playwright adds to its message,
and [Reading the report](guides/build-report.md#reading-the-report) in the guide describes the
result. The forms were read from Microsoft.Playwright 1.41.2 and 1.63.0.

| Part | Up to 0.12.0 | From 0.12.5 |
| --- | --- | --- |
| Assertions | One error per first line | One error per kind, such as `Locator expected to have text`, expected value, message of the test's own and locator read from the call log; the received value only varies |
| Page URL assertions | Every expected URL one error | An expected URL counts by its path, so one page under two hosts is one error, and a number or ID in the path is a value |
| Action timeout, `Timeout 30000ms exceeded.` | One specific error, often a test's primary error | The built-in generic rule `PlaywrightTimeout`, named **Playwright timeout** under **Generic errors**, with each test's element or page in **Values**; a test whose attempts mostly timed out and once failed an assertion is listed under the assertion |
| Wait for an event, `… exceeded while waiting for event …` | A specific error | Still specific: it says something about the test |
| Group line | Exception type and first line | Also what a recognized Playwright message expected, waited for and received |

Only a start line whose type is `Microsoft.Playwright.PlaywrightException` is read as a
Playwright assertion, so a test author's message that starts like one keeps its framework's
reading. A configured rule with the pattern `Timeout *ms exceeded.` and `"generic": false`
keeps the timeouts among the specific errors. The configuration guide adds example rules for a
closed browser and a database deadlock, which stay out of the built-in rules because the report
cannot tell when they come from the environment.

### By error layout

| Part | Up to 0.12.0 | From 0.12.5 |
| --- | --- | --- |
| Group heading | Two rows: count, **+N** and rule or exception type, then the error line | One line: count, **+N**, rule or exception type and the line, cut at the width of the table, whole on hover |
| Facts | A row of their own, then the **Sample message** summary | One row ending with the closed **Sample message**, which wraps only when the facts do not fit; opened, the sample shows under it |
| **Errors** cell | Placement, other errors and previous build on separate lines | One line joined by middle dots, cut at the width of the table, whole on hover or when a link in it has the keyboard focus |
| Print | Wrapped, as on screen | The group's line and the **Errors** cell still wrap |

### Visible changes for 0.12.0 users

- In By error, Playwright assertions on different elements, or expecting different values, are separate errors, and the actual value only varies; an expected page URL counts by its path, so one page under two hosts is one error.
- A new built-in rule, PlaywrightTimeout, matches a line that is "Timeout …ms exceeded."; a wait for an event stays specific, and a configured rule with the same pattern and "generic": false keeps the timeout specific.
- A group's count, rule or exception type and line share one row; the Sample message ends the facts row, which wraps only when its facts do not fit, and opens under it; the Errors cell joins its placement, other errors and previous build with middle dots on one line; in print the group's line and the Errors cell wrap.

### Public contract changes

- The CSV column `Error rule` can hold the new built-in rule ID `PlaywrightTimeout`.

## Bug fixes

None: no change fragment of this version records a fix.

### Existing tests changed

- ByErrorViewTests now expect the line's title, the sample inside the facts row and the Errors cell's title and middle dots; the 14 failed-test goldens were regenerated for the stylesheet and markup, and the playwright variant adds two.

### Findings not fixed

| Finding | Reason |
| --- | --- |
| The findings of 0.12.0, 0.11.5, 0.11.0, 0.10.5, 0.10.0, 0.9.15, 0.9.10, 0.9.5, 0.9.0, 0.8.5, 0.8.0, 0.7.10, 0.7.5, 0.7.0, 0.6.5 and 0.6.0 | Unchanged; see the [0.12.0](release-0.12.0.md#findings-not-fixed), [0.11.5](archive/release-0.11.5.md#findings-not-fixed), [0.11.0](archive/release-0.11.0.md#findings-not-fixed), [0.10.5](archive/release-0.10.5.md#findings-not-fixed), [0.10.0](archive/release-0.10.0.md#findings-not-fixed), [0.9.15](archive/release-0.9.15.md#findings-not-fixed), [0.9.10](archive/release-0.9.10.md#findings-not-fixed), [0.9.5](archive/release-0.9.5.md#findings-not-fixed), [0.9.0](archive/release-0.9.0.md#findings-not-fixed), [0.8.5](archive/release-0.8.5.md#findings-not-fixed), [0.8.0](archive/release-0.8.0.md#findings-not-fixed), [0.7.10](archive/release-0.7.10.md#findings-not-fixed), [0.7.5](archive/release-0.7.5.md#findings-not-fixed), [0.7.0](archive/release-0.7.0.md#findings-not-fixed), [0.6.5](archive/release-0.6.5.md#findings-not-fixed) and [0.6.0](archive/release-0.6.0.md#findings-not-fixed) notes |

### Known limitations

| Limitation | Notes |
| --- | --- |
| V-43: Playwright forms on the agents | Whether the Playwright version on the agents writes the forms read from Microsoft.Playwright 1.41.2 and 1.63.0, and whether the stored error message keeps the call log. Without the call log, timeouts still join **Playwright timeout** but show no element in **Values**, and an assertion that names no value is told apart by the frame of the test's own code only |
| A facts row with every kind of fact wraps | At 1,600 pixels, the facts row of a group that shows every kind of fact is wider than the table, about 1,490 pixels of text against 1,446, and wraps onto a second line; other headings, facts rows and test rows of the goldens take one line |
| By error wider than a narrow window | At 1,000 pixels the table is wider than the window, as in 0.12.0 |
| Limits carried over | The [0.12.0](release-0.12.0.md#known-limitations), [0.11.5](archive/release-0.11.5.md#known-limitations), [0.11.0](archive/release-0.11.0.md#known-limitations), [0.10.5](archive/release-0.10.5.md#known-limitations), [0.10.0](archive/release-0.10.0.md#known-limitations), [0.9.15](archive/release-0.9.15.md#known-limitations), [0.9.10](archive/release-0.9.10.md#known-limitations), [0.9.5](archive/release-0.9.5.md#known-limitations), [0.9.0](archive/release-0.9.0.md#known-limitations), [0.8.5](archive/release-0.8.5.md#known-limitations), [0.8.0](archive/release-0.8.0.md#known-limitations), [0.7.10](archive/release-0.7.10.md#known-limitations), [0.7.5](archive/release-0.7.5.md#known-limitations) and [0.7.0](archive/release-0.7.0.md#known-limitations) known limitations still apply |

## Work-PC Live checks

0.12.5 adds V-43, which only the work server and its agents can settle. The rules of the
[0.11.0 notes](archive/release-0.11.0.md#work-pc-live-checks) apply: counts and verdicts only, no
work value copied out.

| Rank | Check | Evidence to seek |
| --- | --- | --- |
| 1 | `Export-AdoBuildTestFailure` on a failed build whose Playwright tests timed out, then By error | V-43: the timeouts under **Generic errors** as **Playwright timeout**, with an element in **Values** for each; the assertions grouped by element, each test listed under its assertion rather than its timeouts. An empty **Values** cell for every timeout means the stored message keeps no call log; count the Playwright groups whose line the report did not read as a kind and expected value |
| 2 | `Update-AdoToolkit -Verbose` in a new window with 0.12.0 installed | V-38 in use: `Status` `Installed`, `LatestVersion` 0.12.5 and a `0.12.5` folder beside the old one |
| 3 | The checks of the [0.12.0 notes](release-0.12.0.md#work-pc-live-checks), with 0.12.5 | V-39 to V-42, still pending |

## Validation

<!-- release-check:begin -->

| Check | Result |
| --- | --- |
| `verify`, every stage but `documentation`, with `ADOTOOLKIT_RELEASE_BUILD=1` | Passed: `powershell-lint`, `powershell-test`, `configuration`, `tooling-layout`, `workflow-lint` and `project-check`, with Microsoft.PowerShell.PlatyPS 1.0.3, Pester 5.9.1 and PSScriptAnalyzer 1.25.0 |
| Tests that passed | tooling Pester 430, Core tests (en-US) 2023, Core tests (fr-CA) 1867 and product Pester 233 |
| The `documentation` stage and the changelog and README tests | Passed on these notes, before the handoff |
| Version and package | MSBuild gives 0.12.5; `AdoToolkit/0.12.5` packaged from the verified build |
| Release assets | `AdoToolkit-0.12.5-win-x64.zip` 110,534,778 bytes, `AdoToolkit-0.12.5-win-x64.zip.sha256` 96 bytes, `AdoToolkit-0.12.5.zip` 927,762 bytes, `AdoToolkit-0.12.5.zip.sha256` 88 bytes and `Install-AdoToolkit.ps1` 13,567 bytes |
| Portable launcher | passed: AdoToolkit 0.12.5, PowerShell 7.6.6, Windows x64 |
| `Install-AdoToolkit.ps1 -WhatIf` | Accepted the checksum and the layout of `AdoToolkit-0.12.5.zip` |
| Content | Every file the gate checked is unchanged, apart from the changelog, these notes and the archive index |

CI builds its own assets: compare the published files with the `.sha256` files of the release.

<!-- release-check:end -->