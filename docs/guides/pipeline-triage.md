# Pipeline failure triage and failed-test reports

Find out why a build failed, save the relevant logs, and produce an interactive
report of its failed tests and the bugs that track them. The examples assume that you
are [connected](getting-started.md#connect).

If you already have a build ID, [Export a report from a build ID](build-report.md) is
the shortest path to the HTML report.

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
- A profile with `defaultBuildDefinition` and `defaultBranch` supplies `-Definition` and
  `-Branch` when you leave them out, so `Get-AdoBuild -Latest -Result Failed` is enough.
  See [Profiles](configuration.md#profiles).

## See what failed

```powershell
$build | Get-AdoBuildFailure | Format-List Path, Result, ErrorIssues, LogId, LogLineCount
$build | Get-AdoBuildTimeline               # the full record tree, parents first
```

`Get-AdoBuildFailure` returns the deepest failed timeline records, such as tasks,
from the latest attempt of each record. If the build was canceled, it also returns
the deepest canceled records. `Path` shows the stage › job › task chain. Add
`-IncludeWarnings` to also get records that succeeded with issues.

## Save logs

```powershell
[void](New-Item -ItemType Directory -Path .\triage -Force)
$build | Get-AdoBuildFailure | Save-AdoBuildLog -Tail 200 -Path .\triage
```

- Each log is saved as `Build-<id>-Log-<logId>.txt`, byte for byte. An existing
  file is replaced.
- `-Tail` keeps only the last lines. Omit it for the full log. The line range it requests
  is not confirmed on Azure DevOps Server 2020 (V-14). `-Path` must be an existing
  directory and defaults to your Downloads folder.
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

With a default build definition and branch in the profile, `Get-AdoBuildTestFailure`
without a build ID or `-Definition` reports the latest completed build of that
definition and branch.

`Get-AdoTestRun` shows each run's `TotalTests`. Azure DevOps Server 2020 also returns
the run totals `PassedTests`, `NotApplicableTests`, `UnanalyzedTests` and
`IncompleteTests`. These are the server's own categories, not result outcomes:
`UnanalyzedTests` counts results that did not pass and have not been analyzed yet.
`OutcomeCounts` is filled only when the server sends per-outcome statistics.

`Get-AdoBuildTestFailure` reports every test that failed in at least one attempt:

| Classification | Meaning |
| --- | --- |
| `Failed` | The last attempt did not pass, in at least one stage, job or named test run |
| `Flaky` | The test failed, then passed on a later attempt, in every stage, job or named test run |

When the test runs carry different stage, job or run names, attempts are grouped by them, so
a test that fails in every French attempt stays `Failed` even if an English retry passes
last. A run whose name only adds the job retry suffix, such as `UI tests (attempt 2)` on
job attempt 2, stays in the group of the run it retries. The names and the suffix are not
confirmed on Azure DevOps Server 2020 (V-19).

Each failure keeps all its attempts, with error messages, stack traces,
attachment metadata, any linked Test Case and its bugs. The attachment metadata costs one
request per failed result and one more per sub-result, such as a rerun attempt; add
`-SkipAttachments` to leave it out when you do not need the attachments. The set then has `AttachmentsListed` set to
`$false`, its report says that the attachments were not listed, and its export downloads
nothing and needs no connection. Run history covers the current build
and earlier builds of the same definition. It uses 10 builds on the same branch by
default. Change that with `-HistoryCount` (1–50) and `-HistoryScope AllBranches`.
A result set with problems, such as more failing tests than the configured maximum,
has `Status` set to `Partial` and details in `Diagnostics`.

### Bugs

Each reported test lists its open bugs in `Bugs`, ordered by ID: every open work item
associated with one of its test results, and every open work item linked to its Test Case,
by any link type, whose type is in the project's Bug category. Closed bugs are left out:
they no longer explain a failure. `HasOpenBug` is `$true` when at least one bug is open.
The Bug category, state category and Test Case link requests are not confirmed on Azure
DevOps Server 2020 (V-30).

```powershell
$set.Failures | Format-Table Ordinal, ShortName, HasOpenBug
$set.Failures | Where-Object HasOpenBug -eq $false            # failures that no open bug tracks
$set.Failures[0].Bugs                                         # Id, IsOpen, State, WorkItemType, Title
```

A bug is open unless its state is in the Completed or Removed state category of its
project and type, so a `Resolved` bug is still open and stays in the list. Each bug also
has `StateCategory`, `TeamProject`, `WebUrl`, and `IsAssociatedWithResult` and
`IsLinkedToTestCase`, which say where it was found. `IsOpen` is `True`, or empty for a bug
that could not be read; such a bug stays in the list because it may be open. An attempt's
`AssociatedBugIds` is still the server's own list and can name a closed bug. The lookup
reports recoverable lookup problems as warnings. Authentication, authorization and
cancellation still stop the retrieval:

| Warning | What happened |
| --- | --- |
| `BugMetadataUnavailable` | The Bug category or state categories of a project could not be read. Only the type named `Bug` counts, and bugs in the states `Closed`, `Done` and `Removed` count as closed and are left out |
| `UnresolvedBug` | A bug of a test result could not be read. It keeps its ID and link; `IsOpen` is empty |
| `BugLookupFailed` | No bug could be read. The bug IDs of the test results stay as links; linked work items can't be told apart from bugs, so they are left out |

### The HTML report

`Export-AdoBuildTestFailure` writes one dark HTML report per build, by default to your
Downloads folder as `Build-<id>-TestFailures.html`. Its views are an overview table with
cards that sum up the build, the run history with the build's runs, one card per test with
every attempt, the same rows grouped by error, the open bugs with the tests linked to each,
and diagnostics when something could not be retrieved. In the overview and by-error tables,
a test with an open bug shows the lowest-numbered one as a red chip in its **Open bugs**
column, each card lists the test's open bugs, and **Without an open bug** shows only the
tests that still need one; the overview's cards and the last group of the Open bugs view
list them too. Flaky
tests are left out unless you add `-IncludeFlaky`. Times are shown in the time zone of the
computer that exported the report. The command returns the report as a `FileInfo` and
shows its `file:///` address in the console, like `Write-Host`; `-InformationAction Ignore`
hides it.

The report is complete even when scripts are blocked. A small built-in script adds views,
search, filtering, keyboard navigation (`/`, `j`, `k`, `o`) and copying of names, messages
and stack traces.

Only JSON and text attachments are downloaded, into a `<report name>.files-<UTC timestamp>`
folder beside the report: by default the small ones, up to 256 KiB, of every test run that
started within the last 7 days, and the larger ones of the build's latest test run.
Every attachment of a run inside that window is listed with its name and size, and the name
links to the file in Azure DevOps: the browser downloads it with your Windows sign-in (not
confirmed under the work browser policy, V-27).
PNG, HTML and other attachments are never downloaded by the export. A file that fails its
content check is saved as `.bin` and linked without a preview. Size limits and failed
downloads produce warnings.
See [Configuration](configuration.md#settings) for the limits.

| Option | Effect |
| --- | --- |
| `-SkipAttachments` | Downloads nothing; attachments are only listed |
| `-AllRunAttachments` | Also downloads the larger JSON and text files of every run inside the attachment window |
| `-AttachmentWindowDays` | Days, 1–365, in which a run must have started for its attachments to appear. Default 7 |
| `-IncludeFlaky` | Includes flaky tests; by default they are only counted in the header |
| `-Path` | Directory, or an `.html` file path when one build is exported. A missing directory is created when the report is written |
| `-Culture` | Report language, for example `fr-CA` |
| `-NoClobber` | Refuses to replace an existing report |
| `-Open` | Opens the report when it is written. A report that cannot be opened produces a warning and is still returned |
| `-WhatIf` | Names the report and attachment folder without downloading or writing |

[Export a report from a build ID](build-report.md) walks through the views, search and
attachment rules in detail.

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

Full help: [Get-AdoBuildDefinition](../commands/en-US/Get-AdoBuildDefinition.md),
[Get-AdoBuild](../commands/en-US/Get-AdoBuild.md),
[Get-AdoBuildTimeline](../commands/en-US/Get-AdoBuildTimeline.md),
[Get-AdoBuildFailure](../commands/en-US/Get-AdoBuildFailure.md),
[Save-AdoBuildLog](../commands/en-US/Save-AdoBuildLog.md),
[Get-AdoTestRun](../commands/en-US/Get-AdoTestRun.md),
[Get-AdoBuildTestFailure](../commands/en-US/Get-AdoBuildTestFailure.md),
[Export-AdoBuildTestFailure](../commands/en-US/Export-AdoBuildTestFailure.md).
