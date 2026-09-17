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

That writes `Build-12345-TestFailures.html` to your Downloads folder, downloads the
attachments into a folder beside it, and opens the report. The rest of this guide is
the same thing with a look at the data first, and the options worth knowing.

## Step 1 — Connect

```powershell
Connect-Ado            # default profile
Get-AdoConnection      # collection and project currently in use
```

`Connect-Ado` does not contact the server. You can skip it: the first command that
needs a connection connects with the default profile and says so with `-Verbose`.

## Step 2 — Gather the failures

```powershell
$set = Get-AdoBuildTestFailure -BuildId 12345 -HistoryCount 15
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
| `Failures` | One entry per test that failed in at least one attempt |
| `FailedCount`, `FlakyCount` | `Failed` means the last attempt did not pass; `Flaky` means it failed, then passed later |
| `Runs` | The build's test runs, in attempt order |
| `History` | Run summaries, oldest first, current build last |
| `Status` | `Complete`, or `Partial` when something could not be retrieved |
| `Diagnostics` | Why a `Partial` set is partial |

## Step 3 — Look before you write

```powershell
$set.Failures | Format-Table Ordinal, Classification, ShortName, Attempts
$set.Failures | Where-Object Classification -eq Flaky | Select-Object ShortName, Attempts
$set.Diagnostics | Format-List Severity, Code, Message
```

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

## What lands on disk

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
| `-SkipAttachments` | Downloads nothing; attachments are still listed with name, size and a link |
| `-Path` | A directory, or an `.html` file path for a single build |
| `-Culture fr-CA` | Report language. Defaults to the configured, then the session, culture |
| `-NoClobber` | Refuses to replace an existing report, before any download |
| `-WhatIf` | Names the report and folder without requests or writes |
| `-HistoryCount` (step 2) | Builds of run history, 1–50. Default 10 |
| `-HistoryScope AllBranches` (step 2) | History across branches instead of the build's own branch |

Defaults for history, attachment size limits and report culture live in the
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
| The report opens without filtering or keyboard shortcuts | Scripts are blocked. The report content is complete; only the built-in interactions are lost |

Reports, logs and attachments can contain server names, test output and other internal
data. Handle them like any other work data.

Full help: [Get-AdoBuildTestFailure](../commands/en-US/Get-AdoBuildTestFailure.md),
[Export-AdoBuildTestFailure](../commands/en-US/Export-AdoBuildTestFailure.md),
[Get-AdoBuildFailure](../commands/en-US/Get-AdoBuildFailure.md),
[Save-AdoBuildLog](../commands/en-US/Save-AdoBuildLog.md).
