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
  your profile, add `-Project '<name>'` to the commands below that read from the
  server, such as `Get-AdoBuildTestFailure`, or connect with
  `Connect-Ado -Project '<name>'` once. `Export-AdoBuildTestFailure` and
  `Get-AdoConnection` take no `-Project`: the export takes the project from the set.
- AdoToolkit never changes anything in Azure DevOps. Nothing in this guide changes a build.

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
| Overview | One row per failed test, each part in a column of its own: its status and number, Test Case number, name, class, trend, the lowest-numbered open bug that tracks it with **+N** when there are more, one status column per group of attempts, and the first line of its latest error. Under the table, cards describe the whole build: how many tests are **New**, **Recurring** or have **No comparison**; the **Most common errors**, the three errors that the most tests had in any failed attempt, two tests at least, each opening its group in By error; **Generic errors**, how many tests had one and how many had nothing else, opening them in By error; how many tests are **Without an open bug**, with the three that most need one; and, in a grouped build, per group how many tests failed, were flaky or failed only there. Generic errors are left out of the most common ones. A card that would be empty is left out, and the filters act on the table only |
| Runs and history | First the run history: a chart with the failed count above each bar, and the same numbers as a table of builds where **This run** marks this build. Then the build's test runs with their ID, stage, job, the attempt numbers that the runs have (stage, job or job instance), start, duration, test counts with the passed and failed ones marked `✓` and `✕`, the number of reported tests, and how many attachments are listed and downloaded, or **Not listed** for a set gathered with `-SkipAttachments`. In a grouped build each group of runs has its own heading; the latest run is marked and runs outside the attachment window are greyed. Last, a table with one row per reported test, its outcome in each build and its trend |
| Details | One card per test: its class and name, Test Case number and title, links and full name, then its run history, its open bugs with title and state and, for a bug that was read whole, the day it was filed, **✦ New** and its assignee, and its attachments, then its metadata and its attempts |
| By error | The same rows, grouped by error. Every error that a test had in a failed attempt is a group: first the tests whose primary error it is, then, greyed, the other tests that had it. A test's primary error is its most frequent error among its failed attempts, generic errors aside, and the latest one on a tie; a test with generic errors only takes the most frequent of them. Specific errors come first, the error that is the primary error of the most tests first; then the **Generic errors**, under a heading of their own; then the tests without an error message. Above the table are the numbers of distinct and of generic errors. Each group's heading is one line: how many tests have it as their primary error, **+N** for the others, its exception type or the rule that named it, and the error line of its first test, with the Expected and Actual lines of a recognized xUnit or NUnit message, what a recognized Playwright message expected, waited for and received, and the test author's own lines. The line is cut at the width of the table; rest the pointer on it to read it whole. A part of the line that differs between the tests is underlined, and resting the pointer on it lists the values. A group of two tests or more, or of an error with several wordings, also says on one row, which wraps only when it does not fit, how many tests failed or were flaky, how many are new or recurring, how many have an open bug and which bugs, how many fail in each group of attempts, how many **Forms** the error has and which test joined them, the frame of the tests' own code where they fail, when they share one, and in how many **Classes** its tests are; a closed **Sample message** ends that row and, opened, shows under it the start of the latest attempt of its first test that had the error, at most 12 lines and 1,000 characters. The **Values** column shows each test's own values, or its own line when the tests' lines differ in more than values. The **Errors** column says on one line where the error comes from, such as **3 of 4 failed attempts** or **Every failed attempt**, then lists the test's other errors, each linked to its group, and the previous failed build, between middle dots; a greyed row links the test's primary error. That line is cut at the width of the table too: rest the pointer on it to read it whole, or move to one of its links with the keyboard to show the whole cell. In print, the group's line and the **Errors** cell wrap |
| Open bugs | A summary line counts the bugs this view lists, as the tab does, and how many of them were opened after the build was queued; the second count is left out when the build has no queue time. Then one entry per bug, on one line: how many tests are linked to it, its number, title and state, the day it was filed, **✦ New** when it was filed after the build was queued, who it is assigned to or **Unassigned**, its work item type and project when they are not a Bug of the build's project, then what it covers: **Linked tests**, **Same Test Case, not linked**, **Same error, not linked**, the other tests that had the primary error of a linked test in any failed attempt, each counted once, which opens the group that holds the most of them in By error, and, in a grouped build, how many of its tests fail in each group. Then a row for each test linked to it, saying whether the link comes from a test result, the Test Case or both; a link through test results that reaches only some of a test's failed results says how many, such as **Test result: 1 of 4 failed results**. Tests that share a Test Case with a linked test but not the bug follow, greyed. The bug with the most tests comes first, and a test with several bugs appears under each. Bugs that could not be read come last, marked **Not read**. A last group lists the tests **Without an open bug**. The tab shows the number of bugs |
| Diagnostics | Shown only when something could not be retrieved: one line per diagnostic, errors first, then warnings, then information, with the number of each in the heading |

In the Overview, By error and Open bugs tables, each group cell reads `✕ 7/7`: `✕` means the
last attempt in that group failed, `≈` that it failed, then passed, and `✓` that it never
failed. The numbers are failed attempts out of all attempts; rest the pointer on the cell to
read them in words, such as "7 of 7 failed". In the overview, each square after them opens
that attempt. A latest error that is too long for its cell is cut, and so are a long test name
and class; rest the pointer on any of them to read it whole.

Colour carries meaning, always with a glyph or text: red for failures and open bugs, a paler red
on screen with **✦** for a bug opened after the build was queued, green for passes, amber for
flaky tests, violet for the test's **✦ New** chip and orange for **Since**. In print every
colour of the report is darkened instead, the bug marker with them. A bug that could not be read
is grey wherever it shows: it may be closed, so its test counts as without an open bug.

**✦** on a bug chip means the bug was filed at or after the build's queue time — not that it was
filed for this build. There is no upper bound on the window, so a report generated today for an
older build can mark a bug that was filed for a later one; the day the bug was filed is on its
line, beside the marker. The marker, the day and the assignee are shown only for a bug the
report read whole: a bug marked **Not read** says nothing more, and a build with no queue time
marks no bug at all. Whether Server 2020 sends the creation date and the assignee at all awaits
confirmation at work (V-37); without them the line simply shows neither.

The Trend column says **✦ New** when the build before this one ran the test and it passed,
and **Since** a date when the test also failed or was flaky in the build before: the
date is the day the first build of that run of failures finished, and the chip opens that
build's test results. Rest the pointer on it to read how many builds in a row, for example
"3 in a row since 20260914.2". A build that is not in the history is named by its number,
such as **Since build 20260901.4**. Nothing is said when the build before this one could not
be read, did not run the test or ended it with another outcome, such as not executed.

In By error, the **Errors** column of a test's row also compares its primary error with the
errors of its previous failed build, the latest earlier build of the history where it failed or
was flaky. The comparison reads the start of the error messages that the results of that build
listed, up to five different ones, without a request more; whether Azure DevOps Server 2020
lists them is not confirmed at work (V-39). **Same error in build 20260915.2** says that one of
them is a wording of the test's primary error. **Other error in build 20260915.2** says that
none is, and is said only when it can be trusted: the error is not recognized by the frame of
the test's own code, which a listing does not carry; a message that differs was listed whole,
not cut at 4,000 characters as Azure DevOps Services cuts listed messages, and came from a stage
or job where the test had its primary error this time; and the build listed no more than five
different messages for the test. Otherwise, and when that build listed no message, nothing is
said. The facts of a group count the tests whose previous error was the same. A test rerun
inside its task gets no comparison: its failed attempts are sub-results, which a listing does
not include.

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

In By error, the English and the French wording of one error can make one group. The report
recognizes the messages of MSTest 2, 3 and 4 in both languages, and those of NUnit, xUnit, the
wait timeout of Selenium and the timeouts and assertions of Playwright, which are written in
English only. A recognized message is read
by what identifies it, such as its expected value, so the English and French forms of one
assertion are one error when that text is the same, as with `Assert.IsTrue` and one message.
When it differs, as with `Expected:<Welcome>` and `Attendu : <Bienvenue>`, and for any other
two wordings, they become one error when one test failed with them at the same place in an
English group and in a French group: the same exception type and the same frames of the test's
own code, with their line numbers. A group is
English or French by the MSTest texts in it; a group with none, or with both, pairs with no
other. The error's facts then name the test and the groups that joined the wordings, such as
**Paired by SubmitOrder (Tests_EN, Tests_FR)**. Two wordings that one group shows are never
joined, and neither are two groups of the same language. Whether the agents produce these
MSTest texts in each language is not confirmed at work (V-40). An error rule joins wordings in
any case: see [Error rules](configuration.md#error-rules).

The first line of a Playwright message does not say what failed: `Timeout 30000ms exceeded.`
reads the same whatever element the action waited for. The report reads the element from the
call log that Playwright adds to its message: the locator that the action or the assertion
waited for, or the page it was navigating to. Two assertions are one error when they are of one
kind, such as `Locator expected to have text`, and expect the same value of the same element,
with the same message of the test's own, whatever value they received; numbers and IDs aside,
as everywhere in By error. An expected page URL counts by its path, since its host changes between environments,
and a part of the path that is a number or an ID counts as a value, so `/orders/4411` and
`/orders/4412` are one page. An action timeout whose message kept no call log still joins
**Playwright timeout**, with no element in its **Values**. An assertion that names no expected
value, no element and no message of the test's own, as when its message kept no call log, is told
apart by the frame of the test's own code, as an `Assert.IsTrue` without a message is. The forms
were read from Playwright for .NET 1.41.2 and
1.63.0; whether the version on the agents writes them, and whether the result keeps the call
log in its error message, is not confirmed at work (V-43).

### Generic errors

Some failures come from the environment rather than from the test: a page or a server that
does not respond, a browser session that is lost. The report sets them apart as generic
errors, so that they do not hide what the tests themselves report. Built-in rules recognize
a refused connection, a host name that does not resolve, the HTTP statuses 502, 503 and 504,
a WebDriver session that was lost or never created, a page that did not load, and a Playwright
action that ran out of time, in the English messages of Windows, .NET, Chrome, Selenium and
Playwright; `reporting.errorRules` adds others, in any language, as
[Error rules](configuration.md#error-rules) describes. A test that had a
generic error and another one is listed under the other one, whichever was more frequent, and
appears greyed under the generic error. Playwright's timeouts make one group, **Playwright
timeout**, whatever element each action waited for: its **Values** column shows each test's
element, read from the call log. A test whose attempts mostly timed out and once failed an
assertion is listed under the assertion. A wait for an event that never came, such as a
download, is not part of it: it says something about the test. The French messages of Windows
and .NET Framework and the texts of the browser drivers are not confirmed at work (V-41,
V-42), and neither are the Playwright forms on the agents (V-43); a configured rule covers them
meanwhile.

### Search

Press `/` or use the Search box. Every word you type must appear somewhere in a test's card,
collapsed attempts included: error messages, stack traces, JSON and text attachments shown in
the report, stage and job names, run and attempt fields such as run name, machine or
failure type, the days of its run history, and bug titles, states, filing days and assignees,
so typing an owner's name leaves the tests whose bugs that person has. Case and accents are
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

## A CSV file for a spreadsheet

To sort, filter or count the failed tests in a spreadsheet or a script, export a flat CSV
file instead of the report with the `-Format` parameter of
[Export-AdoBuildTestFailure](../commands/en-US/Export-AdoBuildTestFailure.md):

```powershell
$set | Export-AdoBuildTestFailure -Format Csv -Path .\reports
Import-Csv -LiteralPath .\reports\Build-12345-TestFailures.csv | Where-Object 'Open bugs' -eq 0
```

The file has one row per test of the report, in report order, so flaky tests are in it only
with `-IncludeFlaky`. It links no attachment: the export downloads nothing and needs no
connection, and `-SkipAttachments`, `-AllRunAttachments`, `-AttachmentWindowDays` and
`-Culture` have no effect on it. `-Path` names an existing directory, which receives
`Build-<id>-TestFailures.csv`, or a file in an existing directory whose name ends in `.csv`;
any other path, a directory that does not exist included, fails before anything is written:
with an `InvalidArgument` error when it does not end in `.csv`, and otherwise with an
`AdoFileOutput` error. A `.csv` file takes one build, as an `.html` file does: each set
after the first gets the `TestFailureReportSingleFile` error and is not written, so send
several builds to a directory. `-NoClobber`, `-Open` and `-WhatIf` work as they do for the
report.

The columns, in this order:

| Column | Holds |
| --- | --- |
| `Build` | The build ID |
| `Ordinal` | The test's number in the report |
| `Test` | The full test name |
| `Title` | The Test Case title that the test results carry |
| `Classification` | `Failed`, or `Flaky` with `-IncludeFlaky` |
| `Attempts` | How many attempts the test has |
| `Latest error` | The whole message of the latest error, with its line breaks; the report's table shows its first line |
| `Owner` | The owner's display name, then the unique name in angle brackets |
| `Priority` | The test's priority |
| `Test case ID` | The ID of the test's Test Case |
| `Test case state` | The Test Case's state, when the Test Case was read |
| `Open bugs` | How many open bugs the test has |
| `Bug IDs` | Every bug of the test's bug list, in ID order, separated by a semicolon and a space |
| `Bug states` | Their states, in the same order; a bug that could not be read has an empty state |
| `New` | `True` when the build before this one ran the test and it passed, `False` when the test also failed or was flaky in the build before, empty otherwise: when that build could not be read, did not run the test or ended it with another outcome |
| `Since` | When `New` is `False`, the day, as `yyyy-MM-dd`, that the first build of that run of failures finished, in your computer's time zone; that build's number when the day is not known |
| `Primary error` | The whole message of the test's primary error, the error that By error lists the test under, from the latest attempt that had it; empty for a test without an error message |
| `Error kind` | `Specific`, or `Generic` for an error that a generic rule names; empty without an error message |
| `Error rule` | The rule that named the primary error: a rule of `reporting.errorRules` by its name, a built-in rule by its ID, such as `ConnectionRefused`; empty when no rule matched |
| `Distinct errors` | How many errors the test's failed attempts had, the English and French wordings of one error counting once |

`New` and `Since` are the trend of the report, and `Open bugs` the count the report uses to
tell tracked tests from the others. `Primary error` and `Latest error` differ when the test's
latest attempt failed with another error than most of its attempts, or with a generic one. The
IDs of the built-in rules are `ConnectionRefused`, `NameResolution`, `ServerUnavailable`,
`WebDriverSession`, `PageLoadTimeout` and `PlaywrightTimeout`;
[Error rules](configuration.md#error-rules) says what each recognizes.

The file is made for machines. The header names are English whatever the culture; numbers,
`True` and `False` use the invariant culture, and dates are `yyyy-MM-dd`. Azure DevOps text
is written as the server sent it. The file is UTF-8 with a byte order mark, with a comma
between fields and CRLF after every row. Fields follow RFC 4180: a field that holds a comma,
a quote or a line break is enclosed in quotes, and its quotes are doubled.

A text field that starts with `=`, `+`, `-`, `@`, a tab or a carriage return gets an
apostrophe in front of it, so that the spreadsheet shows a test name or an error message as
text instead of reading it as a formula. Numbers and dates are never changed. Remove the
apostrophe only if you trust the text.

French Excel reads a comma file as one column when you double-click it: Excel splits a CSV
file at the list separator of the Windows regional settings, which is a semicolon in French.
Import the file instead: in Excel, choose **Data** > **From Text/CSV** (**Données** >
**À partir d'un fichier texte/CSV**), pick the file, check that the delimiter is **Comma**
and the file origin is **65001: Unicode (UTF-8)**, then choose **Load**.

## Options worth knowing

| Option | Effect |
| --- | --- |
| `-Open` | Opens the committed report with the default handler. A report that cannot be opened produces a warning and is still returned |
| `-SkipAttachments` | Downloads nothing; attachments of runs inside the window are still listed with name, size and a link |
| `-AllRunAttachments` | Also downloads the larger JSON and text files from every run inside the window, not only from the most recent run. `-SkipAttachments` takes precedence if both switches are supplied |
| `-AttachmentWindowDays` | Days, 1–365, in which a run must have started for its attachments to appear. Default 7 |
| `-IncludeFlaky` | Includes flaky tests; by default they are left out and only counted in the header, although a test left out can still be the one that joined two wordings of an error in By error |
| `-Path` | A directory, or an `.html` file path for a single build. With `-Format Csv`, an existing directory, or a `.csv` file path in one for a single build |
| `-Format Csv` | Writes one flat CSV file per build instead of the report, and downloads nothing. See [A CSV file for a spreadsheet](#a-csv-file-for-a-spreadsheet) |
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
| A `TestFailureReportPathInvalid` error (`InvalidArgument`) | `-Path` has an extension other than `.html`, or names an existing file that is not an `.html` file |
| A `TestFailureReportSingleFile` error for each build after the first | `-Path` names one `.html` or `.csv` file and several sets arrived. Name a directory instead |
| A `TestFailureCsvPathInvalid` error (`InvalidArgument`) with `-Format Csv` | `-Path` names neither an existing directory nor a file ending in `.csv`. A CSV export does not create a directory |
| Excel shows each row of the CSV file in one column | The Windows list separator is a semicolon, as it is in French. Import the file with **Data** > **From Text/CSV** instead of opening it |
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
[Save-AdoBuildLog](../commands/en-US/Save-AdoBuildLog.md),
[Get-AdoBuildTimeline](../commands/en-US/Get-AdoBuildTimeline.md),
[Get-AdoTestRun](../commands/en-US/Get-AdoTestRun.md),
[Connect-Ado](../commands/en-US/Connect-Ado.md),
[Get-AdoConnection](../commands/en-US/Get-AdoConnection.md).
