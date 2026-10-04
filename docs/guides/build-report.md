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

That writes `Build-12345-TestFailures.html` to your Downloads folder, downloads the build's
JSON and text attachments into a folder beside it (the small ones of every recent test run,
and the larger ones of the most recent run), and opens the report. Other attachments stay
listed with name, size and a download link. Flaky tests are left out. The rest of this
guide is the same thing with a look at the data first, how to read the report, and the
options worth knowing.

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
| `FailedCount`, `FlakyCount` | `Failed` means the last attempt did not pass in at least one stage, job or named test run; `Flaky` means it failed, then passed later in every one |
| `Runs` | The build's test runs, in attempt order, with `StageName`, `PhaseName` and `JobName` when the server sends them |
| `History` | Run summaries, oldest first, current build last |
| `Status` | `Complete`, or `Partial` when something could not be retrieved |
| `Diagnostics` | Why a `Partial` set is partial |
| `AttachmentsListed` | `False` when `-SkipAttachments` left out the attachment lists |

## Step 3 — Look before you write

```powershell
$set.Failures | Format-Table Ordinal, Classification, ShortName, HasOpenBug
$set.Failures | Where-Object Classification -eq Flaky | Select-Object ShortName, Attempts
$set.Failures | Where-Object HasOpenBug -eq $false | Select-Object ShortName   # no open bug yet
$set.Diagnostics | Format-List Severity, Code, Message
```

`Bugs` holds the open bugs associated with the test's results and the open Bug-category work
items linked to its Test Case. A bug is open unless its state is in the Completed or Removed
state category, so a `Resolved` bug is still open; a closed bug is left out of the set and of
the report. See [Pipeline failure triage](pipeline-triage.md#bugs) for the details and the
warnings the lookup can add.

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

The command returns the report as a `FileInfo` and shows the report's `file:///` address in
the console, like `Write-Host`, so a terminal that opens links can open it;
`-InformationAction Ignore` hides the address. When attachments were downloaded, the
`AttachmentDirectory` note property of the `FileInfo` holds the folder:

```powershell
$report = $set | Export-AdoBuildTestFailure -Path .\reports\build-12345
$report.FullName
$report.AttachmentDirectory
```

## Reading the report

The report is built to be scanned with 100–200 failures and up to 14 attempts each. Its
header starts with the counts of failed and flaky tests and of attachments, then names the
pipeline, build, branch, commit, result and finish time. The branch shows by its name, `main`
for `refs/heads/main`, and the time without a label; rest the pointer on either to read the
full ref or the label. A zero count is greyed. With scripts
on, a count is also a filter: **Failed** or **Flaky** shows that kind alone, **Attachments**
the tests that have some, and pressing it again shows every test. The tabs on the second
line switch between views, beside the search and the filters; **Expand all** and **Collapse
all** appear in Details only. With scripts blocked, the views follow one another on a single
page.

| View | What it shows |
| --- | --- |
| Overview | One row per failed test, each part in a column of its own: its status and number, Test Case number, name, class, trend, the lowest-numbered open bug that tracks it with **+N** when there are more, one status column per group of attempts, and the first line of its latest error. Under the table, cards describe the whole build: how many tests are **New**, **Recurring** or have **No comparison**; the **Most common errors** of two tests or more, each opening its group in By error; how many tests are **Without an open bug**, with the three that most need one; and, in a grouped build, per group how many tests failed, were flaky or failed only there. A card that would be empty is left out, and the filters act on the table only |
| Runs and history | First the run history: a chart with the failed count above each bar, and the same numbers as a table of builds where **This run** marks this build. Then the build's test runs with their ID, stage, job, the attempt numbers that the runs have (stage, job or job instance), start, duration, test counts with the passed and failed ones marked `✓` and `✕`, the number of reported tests, and how many attachments are listed and downloaded, or **Not listed** for a set gathered with `-SkipAttachments`. In a grouped build each group of runs has its own heading; the latest run is marked and runs outside the attachment window are greyed. Last, a table with one row per reported test, its outcome in each build and its trend |
| Details | One card per test: its class and name, Test Case number and title, links and full name, then its run history, its open bugs with title and state, and its attachments, then its metadata and its attempts |
| By error | The same rows, grouped under their latest error, largest group first, with the number of distinct errors above the table. Each group names its exception type and the error line of its first test; a part of the line that differs between the tests is underlined, and resting the pointer on it lists the values. A group of two tests or more also says how many tests failed or were flaky, how many are new or recurring, how many have an open bug and which bugs, how many fail in each group of attempts, and the frame of the tests' own code where they fail, when they share one; a closed **Sample message** shows the start of the first test's message. The **Values** column shows each test's own values. Tests without an error message come last |
| Open bugs | One entry per bug, on one line: how many tests are linked to it, its number, title and state, its work item type and project when they are not a Bug of the build's project, then what it covers: **Linked tests**, **Same Test Case, not linked**, **Same error, not linked**, which opens that group in By error, and, in a grouped build, how many of its tests fail in each group. Then a row for each test linked to it, saying whether the link comes from a test result, the Test Case or both; a link through test results that reaches only some of a test's failed results says how many, such as **Test result: 1 of 4 failed results**. Tests that share a Test Case with a linked test but not the bug follow, greyed. The bug with the most tests comes first, and a test with several bugs appears under each. Bugs that could not be read come last, marked **Not read**. A last group lists the tests **Without an open bug**. The tab shows the number of bugs |
| Diagnostics | Shown only when something could not be retrieved: one line per diagnostic, errors first, then warnings, then information, with the number of each in the heading |

In the Overview, By error and Open bugs tables, each group cell reads `✕ 7/7`: `✕` means the
last attempt in that group failed, `≈` that it failed, then passed, and `✓` that it never
failed. The numbers are failed attempts out of all attempts; rest the pointer on the cell to
read them in words, such as "7 of 7 failed". In the overview, each square after them opens
that attempt. A latest error that is too long for its cell is cut, and so are a long test name
and class; rest the pointer on any of them to read it whole.

Colour carries meaning, always with a glyph or text: red for failures and open bugs, green for
passes, amber for flaky tests, violet for **✦ New** and orange for **Since**. A bug that could
not be read is grey wherever it shows: it may be closed, so its test counts as without an
open bug.

The Trend column says **✦ New** when the build before this one ran the test and it did not
fail, and **Since** a date when the test also failed or was flaky in the build before: the
date is the day the first build of that run of failures finished, and the chip opens that
build's test results. Rest the pointer on it to read how many builds in a row, for example
"3 in a row since 20260914.2". A build that is not in the history is named by its number,
such as **Since build 20260901.4**. Nothing is said when the build before this one could not
be read or did not run the test.

A card says the same after its run history: one chip per build, oldest first, with the day the
build finished. The chips are told apart by glyph and shape as well as colour: filled for
failed, tinted for flaky, hollow for passed, dashed for not run, dotted for any other outcome
and hatched for a build that could not be read; the current build is the last chip, its date in
bold, and resting the pointer on a chip gives its build and outcome. An attempt's **Failing
since** names the build by its number when that build is in the history. The card's
**Attachments** row lists each file name once, from the last attempt that has it: the file,
linked to its original in Azure DevOps, its size, and the attempt it comes from, which opens
that attempt at the file with its local copy and preview. **×2** says that two attempts have a
file of that name; rest the pointer on it to read which.

An attachment larger than 1,024 bytes shows its size in KB or MB; rest the pointer on the
size to read the exact number of bytes.

In a card, every group and every attempt starts collapsed; reaching a test from its row, with
`j` or `k`, or with `Enter` opens the attempt of its latest error and the group that holds it.
An attempt's summary line shows its outcome, duration, machine and the first line of its
error, in columns that line up from one attempt to the next. After MSTest's "Test method …
threw exception:", it shows the exception on the line below, as By error does; the
overview's latest error and the console table keep the first line. Opened, every attempt
shows its own full error message and stack trace, even when an earlier attempt failed with the
same text, so a report with many retries of long traces is large. A stack trace that has a
frame of the test's own code opens with the framework frames hidden and **Hide framework
frames** pressed, and an error message opens with **Wrap lines** pressed; press either button
to change it. Copying and printing always include every frame.

Every date and time in the report is shown in the time zone of the computer that exported it,
the same zone as the report's own generation time. When the report was made, with which
toolkit version and from which server, collection and project, is at the foot of every view.

### English and French attempts

When the test runs of the build carry different stage, job or run names, attempts are grouped
by them, so English and French attempts sit in separate labelled groups. The label is the
shortest name that tells the groups apart: the stage name when the languages run in separate
stages, otherwise the job name, otherwise the matrix or parallel instance, otherwise the test
run name. The stage, job and instance names come from the test runs' `pipelineReference`, and
the run name from the run itself; when every run has the same names, attempts stay in one list.

A run is a retry of another run of the same job only when its name is that run's name plus
` (attempt N)` and `N` is its job attempt, for example `UI tests (attempt 2)` on attempt 2.
It then stays in the group of the run it retries. Any other difference, such as `Suite 1`
and `Suite 2` or a change of case, makes a separate group. Whether Azure DevOps Server 2020
sends these names and this suffix is not confirmed at work (V-19).

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
failure type, the days of its run history, and bug titles and states. Case and accents are
ignored, so `echec` finds « Échec ». `12345` and `#12345` both find the tests linked to Test
Case 12345. Attempts that match are marked in their summary line; nothing opens by itself.
**Failing in** narrows the list to tests whose last attempt in a group failed, or failed only
there. **Without an open bug**, shown when at least one test has an open bug, leaves only the
tests that no open bug tracks yet.

Keys, also listed at the foot of the report: `j`/`k` move between tests, `Enter` opens the
selected test's card, `o` opens or closes an attempt or group, and `Esc` in the Search box
clears every filter. Back after opening a card returns to that test's row.

## What lands on disk

Attachments are shown only for test runs that started within the last 7 days; change that
with `-AttachmentWindowDays`. Older runs keep every attempt in the report, but their
attachments are left out, and a run without a start date counts as outside.

Only JSON and text (`.txt`, `.log`) attachments are ever downloaded by the export. Every
attachment name, PNG, HTML and other types included, links to the file in Azure DevOps, which
the browser downloads with your Windows sign-in (not confirmed under the work browser
policy, V-27). By default the export downloads:

- from every run inside the window, the JSON and text files of at most
  `testResults.maximumInlineJsonBytes` (256 KiB), which are the ones the report can show;
- from the most recent run, the larger ones too, up to the per-file limit.

The latest run is the last in attempt order: stage, phase and job attempt, then start date and
run ID. A larger file of an older run stays a link; the export does not fall back to an older
run for those. A file of an older run that declares no size is downloaded and kept only if it
turns out small enough. When no run has a file to download, no attachment folder is created
and the export needs no connection.

To download the larger JSON and text files from every run inside the window as well:

```powershell
$set | Export-AdoBuildTestFailure -Path .\reports\build-12345 -AllRunAttachments
```

Downloaded JSON and text appear in a collapsed preview, which is also what makes their
contents searchable. Files above `testResults.maximumInlineJsonBytes`, or past
`testResults.maximumInlineTotalBytes` for the whole report, are linked but not shown. The
files of the latest run are downloaded and previewed first, then those of the older runs, so
when a total runs out it is the older runs that go without.

| Item | Where |
| --- | --- |
| The report | `<-Path>\Build-<id>-TestFailures.html`, or your Downloads folder with no `-Path` |
| Attachments | `<report name>.files-<UTC stamp>` beside the report, so `Build-<id>-TestFailures.files-<UTC stamp>` by default |
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
| `-Open` | Opens the committed report with the default handler. A report that cannot be opened produces a warning and is still returned |
| `-SkipAttachments` | Downloads nothing; attachments of runs inside the window are still listed with name, size and a link |
| `-AllRunAttachments` | Also downloads the larger JSON and text files from every run inside the window, not only from the most recent run. `-SkipAttachments` takes precedence if both switches are supplied |
| `-AttachmentWindowDays` | Days, 1–365, in which a run must have started for its attachments to appear. Default 7 |
| `-IncludeFlaky` | Includes flaky tests; by default they are left out and only counted in the header |
| `-Path` | A directory, or an `.html` file path for a single build |
| `-Culture fr-CA` | Report language. Defaults to the configured, then the session, culture |
| `-NoClobber` | Refuses to replace an existing report, before any download |
| `-WhatIf` | Names the report and folder without requests or writes |
| `-HistoryCount` (step 2) | Builds of run history, 1–50. Default 10 |
| `-HistoryScope AllBranches` (step 2) | History across branches instead of the build's own branch |
| `-SkipAttachments` (step 2) | Gathers the failures without their attachment lists, which cost one request per failed result and one more per sub-result, such as a rerun attempt. The report then says that the attachments were not listed, and the export downloads nothing and needs no connection |

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
| An `AdoNotFound` error for the build | The ID belongs to another project. Add `-Project`, or check the project segment of the build URL |
| `AdoConnectionMismatch` | The set came from a different collection than the connection used for the export |
| A path error naming the report | `-Path` has an extension other than `.html`, or names an existing file that is not an `.html` file |
| `Status` is `Partial` | Read `Diagnostics`. A common cause is more failing tests than `testResults.maximumReportedFailures` |
| Gathering the failures takes long | Add `-Verbose` to `Get-AdoBuildTestFailure`: each stage, such as the result listings, the failure details or the attachment lists, writes its requests and milliseconds, and a last line gives the requests and the time of the whole command. The attachment lists cost one request per failed result and one more per sub-result, such as a rerun attempt; add `-SkipAttachments` if you do not need the attachments |
| Writing the report takes long | Add `-Verbose` to `Export-AdoBuildTestFailure`: the attachment downloads, the rendering, the check of the report and its move into place each write their milliseconds, the downloads with their files and requests, and a last line gives the time of the whole export |
| Warnings about attachment size | The attachment or the total exceeded the configured limit. Those attachments are listed in the report but not downloaded |
| A flaky test is missing | Flaky tests are left out by default. Add `-IncludeFlaky` |
| A run's attachments are missing | The run started before the attachment window. Raise `-AttachmentWindowDays`; the Runs and history view marks runs outside the window |
| No English and French columns | The runs have no distinct stage, job or run names: the server sent no stage or job names and the runs share one name, or every run has the same names. Check `$set.Runs` as shown above |
| One test shows in two groups of the same job | Its runs have different names, for example `Suite` and `Suite (retry)`. Only a ` (attempt N)` suffix that matches the job attempt joins a retry to its first run |
| A bug linked to the Test Case is missing | It is closed, its type is not in the project's Bug category, or it could not be read. A `BugMetadataUnavailable` warning means only the type named `Bug` was recognized |
| A bug shows as open although it is resolved | Only the Completed and Removed state categories count as closed; `Resolved` is its own category |
| The report opens without filtering or keyboard shortcuts | Scripts are blocked. The report content is complete; only the built-in interactions are lost |

Reports, logs and attachments can contain server names, test output and other internal
data. Handle them like any other work data.

Full help: [Get-AdoBuildTestFailure](../commands/en-US/Get-AdoBuildTestFailure.md),
[Export-AdoBuildTestFailure](../commands/en-US/Export-AdoBuildTestFailure.md),
[Get-AdoBuildFailure](../commands/en-US/Get-AdoBuildFailure.md),
[Save-AdoBuildLog](../commands/en-US/Save-AdoBuildLog.md).
