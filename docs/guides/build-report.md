# Export a report from a build ID

You have a build ID — the number in a build URL such as
`https://ado.example.test/tfs/DefaultCollection/Web/_build/results?buildId=12345` — and
you want its failed-test report. This guide is the shortest path from that number to an
HTML file you can open or send. For the wider triage workflow, starting from a
definition and a branch, see [Pipeline failure triage](pipeline-triage.md).

## Before you begin

- The module is installed and a profile is saved. See
  [Getting started](getting-started.md).
- You know which project the build belongs to. If it is not the default project of
  your profile, add `-Project '<name>'` to every command below.
- AdoToolkit only reads from Azure DevOps. Nothing in this guide changes a build.

## The short version

```powershell
Get-AdoBuildTestFailure -BuildId 12345 | Export-AdoBuildTestFailure -Open
```

That writes `Build-12345-TestFailures.html` to your Downloads folder, downloads the JSON
and text attachments of the build's most recent test run into a folder beside it, and
opens the report. Other attachments stay listed with name, size and a link. Flaky tests
are left out. The rest of this guide is the same thing with a look at the data first,
how to read the report, and the options worth knowing.

## Step 1 — Connect

```powershell
Connect-Ado            # default profile
Get-AdoConnection      # collection and project currently in use
```

`Connect-Ado` does not contact the server. You can skip it: the first command that
needs a connection connects with the default profile and says so with `-Verbose`.

## Step 2 — Gather the failures

```powershell
$set = Get-AdoBuildTestFailure -BuildId 12345 -HistoryCount 7
$set                   # BuildId, BuildNumber, FailedCount, FlakyCount, Runs, Status
```

`Get-AdoBuild` searches by definition and has no `-Id` parameter, so a bare build ID
goes directly into `Get-AdoBuildTestFailure`. The build record comes back with the set:

```powershell
$set.Build | Format-List Id, BuildNumber, Definition, SourceBranch, Result, FinishTime, WebUrl
```

Check that `BuildNumber` and `Definition` are the build you meant before exporting.

| Property | What it holds |
| --- | --- |
| `Build` | The build the ID resolved to |
| `Failures` | One entry per test that failed in at least one attempt, with its attempts, Test Case, `Bugs` and `HasOpenBug` |
| `FailedCount`, `FlakyCount` | `Failed` means the last attempt did not pass in at least one stage or job; `Flaky` means it failed, then passed later in every stage or job |
| `Runs` | The build's test runs, in attempt order, with `StageName`, `PhaseName` and `JobName` when the server sends them |
| `History` | Run summaries, oldest first, current build last |
| `Status` | `Complete`, or `Partial` when something could not be retrieved |
| `Diagnostics` | Why a `Partial` set is partial |

## Step 3 — Look before you write

```powershell
$set.Failures | Format-Table Ordinal, Classification, ShortName, HasOpenBug
$set.Failures | Where-Object Classification -eq Flaky | Select-Object ShortName, Attempts
$set.Failures | Where-Object HasOpenBug -eq $false | Select-Object ShortName   # no open bug yet
$set.Diagnostics | Format-List Severity, Code, Message
```

`Bugs` holds the bugs associated with the test's results and the Bug-category work items
linked to its Test Case. A bug is open unless its state is in the Completed or Removed state
category, so a `Resolved` bug is still open. See
[Pipeline failure triage](pipeline-triage.md#bugs) for the details and the warnings the
lookup can add.

A `Partial` status also raises a warning naming the counts. The export still runs; the
report lists the diagnostics in its own section.

An empty set is not an error. If no test failed, the report is written with a "no
failures" note, which is a useful thing to send when a build failed for a reason other
than its tests.

## Step 4 — Name the output without writing anything

```powershell
$set | Export-AdoBuildTestFailure -Path .\reports\build-12345 -WhatIf
```

`-WhatIf` names the report and the attachment folder, makes no attachment requests and
writes no files. The data was already retrieved in step 2.

## Step 5 — Export

```powershell
$set | Export-AdoBuildTestFailure -Path .\reports\build-12345 -Open
```

The command returns the report as a `FileInfo`. When attachments were downloaded, its
`AttachmentDirectory` note property holds the folder:

```powershell
$report = $set | Export-AdoBuildTestFailure -Path .\reports\build-12345
$report.FullName
$report.AttachmentDirectory
```

## Reading the report

The report is built to be scanned with 100–200 failures and up to 14 attempts each. The
tabs under the header switch between views; with scripts blocked, the views follow one
another on a single page.

| View | What it shows |
| --- | --- |
| Overview | One row per failed test: its number, name and class, an **Open bug** mark when a bug still tracks it, Test Case number, one status column per stage or job, and the first line of its latest error |
| By error | The same rows, grouped under their latest error, largest group first. Numbers and GUIDs are ignored when grouping, so "after 30012 ms" and "after 30020 ms" group together |
| Details | One card per test: Test Case number and title, links, full name, its bugs with title, state and an **Open** mark, and history, then its attempts |
| Runs and history | The build's test runs with their stage, job, attempts and attachment status, the run history chart, and when and where the report was made |
| Diagnostics | Shown only when something could not be retrieved |

In the overview, each stage or job cell reads `✕ 7/7`: `✕` means the last attempt in that
group failed, `≈` that it failed, then passed, and `✓` that it never failed. The numbers are
failed attempts out of all attempts, and each square after them opens that attempt.

In a card, every group and every attempt starts collapsed. An attempt's summary line shows its
outcome, duration, machine and the first line of its error. When a later attempt has the same
error message or stack trace as an earlier one, it shows a link to that attempt instead of
another copy.

Every date and time in the report is shown in the time zone of the computer that exported it,
the same zone as the report's own generation time.

### English and French attempts

When the test runs of the build carry different stage or job names, attempts are grouped by
them, so English and French attempts sit in separate labelled groups. The label is the
shortest name that tells the groups apart: the stage name when the languages run in separate
stages, otherwise the job name, otherwise the matrix or parallel instance. The names come
from the test runs' `pipelineReference`; when the server sends none, or every run has the same
names, attempts stay in one list.

Grouping also decides the classification. A test that fails in every French attempt stays
`Failed` even when an English retry passes last. It is `Flaky` only when every group ends
with a pass.

To check what names your server sends, look at `Runs` before exporting:

```powershell
$set.Runs | Format-Table Id, Name, StageName, PhaseName, JobName, PipelineAttempt
```

`PhaseName` is what a YAML pipeline calls the job; `JobName` is the matrix or parallel
instance, `__default` when there is none.

### Search

Press `/` or use the Search box. Every word you type must appear somewhere in a test's card,
collapsed attempts included: error messages, stack traces, JSON and text attachments shown in
the report, stage and job names, run and attempt fields such as run name, machine or
failure type, and bug titles and states. `12345` and `#12345` both find the tests linked to
Test Case 12345. Attempts that match are marked in their summary line; nothing opens by
itself. **Failing in** narrows the list to tests whose last attempt in a group failed, or
failed only there. **Without an open bug**, shown when at least one test has an open bug,
leaves only the tests that no open bug tracks yet.

Keys: `j`/`k` move between tests, `Enter` opens the selected test's card, `o` opens or closes
an attempt or group, and `Esc` in the Search box clears every filter.

## What lands on disk

Attachments are shown only for test runs that started within the last 7 days; change that
with `-AttachmentWindowDays`. Older runs keep every attempt in the report, but their
attachments are left out, and a run without a start date counts as outside.

Only JSON and text (`.txt`, `.log`) attachments are ever downloaded. PNG, HTML and other
attachments stay links to their Azure DevOps result. By default, only the most recent run is
downloaded, and only when it is inside the window. The latest run is the last in attempt
order: stage, phase and job attempt, then start date and run ID. If that run has no JSON or
text attachments, no attachment folder is created; the export does not fall back to an
older run.

To download JSON and text from every run inside the window:

```powershell
$set | Export-AdoBuildTestFailure -Path .\reports\build-12345 -AllRunAttachments
```

Downloaded JSON and text appear in a collapsed preview, which is also what makes their
contents searchable. Files above `testResults.maximumInlineJsonBytes`, or past
`testResults.maximumInlineTotalBytes` for the whole report, are linked but not shown.

| Item | Where |
| --- | --- |
| The report | `<-Path>\Build-<id>-TestFailures.html`, or your Downloads folder with no `-Path` |
| Attachments | `Build-<id>-TestFailures.files-<UTC stamp>` beside the report |
| Nothing else | No temporary files survive a failed export |

A missing `-Path` directory is created when the report is written. Pass an `.html` file
path instead to choose the file name; that works for one build per command, and any
later set in the same pipeline gets its own error.

An existing report is replaced only after the new attachments are downloaded and the
new report is validated, so a failed export leaves the previous report and its folder
intact. Older attachment folders of the same report are removed after the replacement.

## Options worth knowing

| Option | Effect |
| --- | --- |
| `-Open` | Opens the committed report with the default handler |
| `-SkipAttachments` | Downloads nothing; attachments of runs inside the window are still listed with name, size and a link |
| `-AllRunAttachments` | Downloads JSON and text from every run inside the window. `-SkipAttachments` takes precedence if both switches are supplied |
| `-AttachmentWindowDays` | Days, 1–365, in which a run must have started for its attachments to appear. Default 7 |
| `-IncludeFlaky` | Includes flaky tests; by default they are left out and only counted in the header |
| `-Path` | A directory, or an `.html` file path for a single build |
| `-Culture fr-CA` | Report language. Defaults to the configured, then the session, culture |
| `-NoClobber` | Refuses to replace an existing report, before any download |
| `-WhatIf` | Names the report and folder without requests or writes |
| `-HistoryCount` (step 2) | Builds of run history, 1–50. Default 10 |
| `-HistoryScope AllBranches` (step 2) | History across branches instead of the build's own branch |

Defaults for history, attachment size and inline limits, and report culture live in the
[configuration file](configuration.md#settings).

## The same build ID, other questions

```powershell
Get-AdoBuildFailure -BuildId 12345 | Format-List Path, Result, ErrorIssues, LogId
Get-AdoBuildFailure -BuildId 12345 | Save-AdoBuildLog -Tail 200 -Path .\triage
Get-AdoBuildTimeline -BuildId 12345
Get-AdoTestRun -BuildId 12345
```

`Get-AdoBuildFailure` answers "which task failed"; the test report answers "which tests
failed". `Save-AdoBuildLog` needs an existing directory, unlike the export.

## When something looks wrong

| Symptom | Cause |
| --- | --- |
| `Build 12345 was not found.` | The ID belongs to another project. Add `-Project`, or check the project segment of the build URL |
| `ConnectionMismatch` | The set came from a different collection than the connection used for the export |
| A path error naming the report | `-Path` has an extension other than `.html`, or names an existing file |
| `Status` is `Partial` | Read `Diagnostics`. A common cause is more failing tests than `testResults.maximumReportedFailures` |
| Warnings about attachment size | The attachment or the total exceeded the configured limit. Those attachments are listed in the report but not downloaded |
| A flaky test is missing | Flaky tests are left out by default. Add `-IncludeFlaky` |
| A run's attachments are missing | The run started before the attachment window. Raise `-AttachmentWindowDays`; the Runs and history view marks runs outside the window |
| No English and French columns | The server sent no stage or job names, or every run has the same names. Check `$set.Runs` as shown above |
| A bug linked to the Test Case is missing | Its type is not in the project's Bug category, or it could not be read. A `BugMetadataUnavailable` warning means only the type named `Bug` was recognized |
| A bug shows as open although it is resolved | Only the Completed and Removed state categories count as closed; `Resolved` is its own category |
| The report opens without filtering or keyboard shortcuts | Scripts are blocked. The report content is complete; only the built-in interactions are lost |

Reports, logs and attachments can contain server names, test output and other internal
data. Handle them like any other work data.

Full help: [Get-AdoBuildTestFailure](../commands/en-US/Get-AdoBuildTestFailure.md),
[Export-AdoBuildTestFailure](../commands/en-US/Export-AdoBuildTestFailure.md),
[Get-AdoBuildFailure](../commands/en-US/Get-AdoBuildFailure.md),
[Save-AdoBuildLog](../commands/en-US/Save-AdoBuildLog.md).
