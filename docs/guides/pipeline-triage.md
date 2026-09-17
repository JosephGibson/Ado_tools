# Pipeline failure triage and failed-test reports

Find out why a build failed, save the relevant logs, and produce an interactive
report of its failed and flaky tests. The examples assume that you are
[connected](getting-started.md#connect).

## Find builds

```powershell
Get-AdoBuildDefinition -Name 'Web*'
Get-AdoBuild -Definition 'Web CI' -Branch main -Top 10
$build = Get-AdoBuild -Definition 42 -Branch main -Latest -Result Failed   # used below
```

- `-Definition` takes an integer ID or an exact name. A name that matches no
  definition, or several, is an error. Use the ID in that case.
- `main` becomes `refs/heads/main`. Full `refs/...` names are used as given.
- `-Latest` returns the most recently finished build. It defaults `-Status` to
  `Completed`.

## See what failed

```powershell
$build | Get-AdoBuildFailure | Format-List Path, Result, ErrorIssues, LogId, LogLineCount
$build | Get-AdoBuildTimeline               # the full record tree, parents first
```

`Get-AdoBuildFailure` returns the deepest failed timeline records, such as tasks,
from the latest attempt of each record. If the build was canceled, it returns
canceled records instead. `Path` shows the stage › job › task chain. Add
`-IncludeWarnings` to also get records that succeeded with issues.

## Save logs

```powershell
$build | Get-AdoBuildFailure | Save-AdoBuildLog -Tail 200 -Path .\triage
```

- Each log is saved as `Build-<id>-Log-<logId>.txt`, byte for byte. An existing
  file is replaced.
- `-Tail` keeps only the last lines. Omit it for the full log. `-Path` must be an
  existing directory and defaults to your Downloads folder.
- A failure without a log writes an error for that record, and the pipeline
  continues.
- A download fails after 10 minutes in total or 60 seconds without data.

## Report failed and flaky tests

```powershell
$build | Get-AdoTestRun                      # test runs of the build, in attempt order
$set = $build | Get-AdoBuildTestFailure -HistoryCount 15
$set.Failures | Where-Object Classification -eq Flaky | Select-Object ShortName, Attempts
$set | Export-AdoBuildTestFailure -Open
```

To report the latest completed build of a definition in one step, name the
definition instead of piping a build. `-Branch` and `-Result` narrow the choice as
they do for `Get-AdoBuild -Latest`:

```powershell
Get-AdoBuildTestFailure -Definition 'Web CI' -Branch main -Result Failed |
    Export-AdoBuildTestFailure -Path .\reports\nightly -Open
```

`Get-AdoTestRun` shows each run's `TotalTests`. Azure DevOps Server 2020 also returns
the run totals `PassedTests`, `NotApplicableTests`, `UnanalyzedTests` and
`IncompleteTests`. These are the server's own categories, not result outcomes:
`UnanalyzedTests` counts results that did not pass and have not been analyzed yet.
`OutcomeCounts` is filled only when the server sends per-outcome statistics.

`Get-AdoBuildTestFailure` reports every test that failed in at least one attempt:

| Classification | Meaning |
| --- | --- |
| `Failed` | The last attempt did not pass |
| `Flaky` | The test failed, then passed on a later attempt |

Each failure keeps all its attempts, with error messages, stack traces,
attachment metadata and any linked Test Case. Run history covers the current build
and earlier builds of the same definition. It uses 10 runs on the same branch by
default. Change that with `-HistoryCount` (1–50) and `-HistoryScope AllBranches`.
A result set with problems, such as more failing tests than the configured maximum,
has `Status` set to `Partial` and details in `Diagnostics`.

### The HTML report

`Export-AdoBuildTestFailure` writes one dark HTML report per build. By default, the
report is saved to your Downloads folder as `Build-<id>-TestFailures.html`. It
contains:

- a run history chart and table, and an index of reported tests
- one card per test, with every attempt, highlighted messages and stack traces,
  and Test Case links
- diagnostics about anything that could not be retrieved

The report is complete even when scripts are blocked. A small built-in script adds
filtering, keyboard navigation (`/`, `j`, `k`, `o`) and copying of names, messages
and stack traces.

Attachments of the reported results are downloaded into a
`<report name>.files-<UTC timestamp>` folder beside the report. PNG files are shown
as thumbnails, and small JSON files are highlighted inline. A file that fails its
content check is saved as `.bin` and linked without a preview. Size limits and
failed downloads produce warnings. The affected attachments are still listed, with
their name, size and a link to the result in Azure DevOps. See
[Configuration](configuration.md#settings) for the limits.

| Option | Effect |
| --- | --- |
| `-SkipAttachments` | Downloads nothing; attachments are only listed |
| `-Path` | Directory, or an `.html` file path when one build is exported. A missing directory is created when the report is written |
| `-Culture` | Report language, for example `fr-CA` |
| `-NoClobber` | Refuses to replace an existing report |
| `-WhatIf` | Names the report and attachment folder without downloading or writing |

An existing report is replaced only after the new attachments are downloaded and
the new report is validated. If the export fails before that point, the previous
report and folder are unchanged. Older attachment folders of the same report are
removed after the replacement.

Reports, logs and attachments can contain server names, test output and other
internal data. Handle them like any other work data.

### When a response cannot be read

If the server returns data in an unexpected shape, the error names the request and
the location in the response, for example:

```text
The server response has an invalid format. Operation: TestResultsList. JSON path: $.value[0].testRun.id.
```

The location contains only field names and positions, never response values, so
you can share it when reporting the problem.

Full help: [Get-AdoBuild](../commands/en-US/Get-AdoBuild.md),
[Get-AdoBuildFailure](../commands/en-US/Get-AdoBuildFailure.md),
[Save-AdoBuildLog](../commands/en-US/Save-AdoBuildLog.md),
[Get-AdoBuildTestFailure](../commands/en-US/Get-AdoBuildTestFailure.md),
[Export-AdoBuildTestFailure](../commands/en-US/Export-AdoBuildTestFailure.md).
