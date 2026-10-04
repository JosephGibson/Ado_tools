---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 10-04-2026
PlatyPS schema version: 2024-05-01
title: Export-AdoBuildTestFailure
---

# Export-AdoBuildTestFailure

## SYNOPSIS

Writes one compact HTML failed-test report per failure set and downloads its JSON and text attachments: the small ones of every recent test run and the larger ones of the most recent run.

## SYNTAX

### Input (Default)

```
Export-AdoBuildTestFailure [-InputObject] <AdoBuildTestFailureSet> [-Culture <string>] [-Path <string>] [-SkipAttachments] [-AllRunAttachments] [-AttachmentWindowDays <int>] [-IncludeFlaky] [-NoClobber] [-Open] [-Connection <AdoConnection>] [-WhatIf] [-Confirm]
```

## ALIASES

No aliases.

## DESCRIPTION

Renders each `AdoBuildTestFailureSet` from `Get-AdoBuildTestFailure` as one dark HTML report in
the report culture. The report has five views, in this order, plus a sixth for diagnostics when
there are any. Overview is a table with one row per failed test, each part in a column of its
own: its status and number, its Test Case number linked to the work item, its name and class,
its trend, its lowest-numbered open bug as a red chip with the number of the others, in a paler
red on screen with ✦ when that bug was opened after the build was queued, the status of each
group of attempts that ran it, and the first line of its latest error. In the trend, New says
that the build before this one ran the test and it did not fail, and Since and a date that the
test also failed or was flaky in the build before; the date is the day the first build of that
run of failures finished, the chip links to that build's test results, and its title says how
many builds in a row. Nothing is said when the build before this one could not be read or did
not run the test. Under the table, cards sum up the build: new and recurring tests, the most
common errors, the tests without an open bug and, when attempts are grouped, each group. Runs
and history shows the run history as a chart and as a table of builds, then lists the build's
test runs with their ID, the attempt numbers that they have, of the stage, the job or the job
instance, their duration, test counts, reported tests and listed and downloaded attachments,
and marks the latest run; a table of the reported tests ends it, with their outcome in each
build and their trend. Details has one card per test with its run history, one chip per build
with the day it finished, told apart by glyph and shape as well as colour, and the same trend;
its open bugs, each as a chip linked to the work item with its title and state, then, for a bug
that was read whole, the day it was filed, New when it was filed after the build was queued,
and who it is assigned to or Unassigned; and its attachments, each file name once, from the
last attempt that has it. An attempt's Failing since names that build by its number when it is
in the history. By error groups the same rows under their latest error, ignoring URLs, GUIDs,
paths, hexadecimal IDs and numbers; each group names its exception type and marks the parts of
its line that differ between its tests, and a group of two tests or more says what its tests
share and shows a sample of the first message. Open bugs opens on a line counting the bugs it
lists and how many of them were opened after the build was queued, the second count left out
when the build has no queue time, then lists each bug once, on one line with the day it was
filed, who has it and what it covers, then the tests linked to it, and says whether each link
comes from a test result, the Test Case or both, and how many failed results a link through
test results reaches when it is not all of them; tests of the same Test Case without the bug
follow, and the tests without an open bug come last. The bug with the most tests comes first,
and a bug that could not be read comes last, as a grey chip marked Not read. A bug is open
unless its state is in the Completed or Removed state category; a closed bug is not shown, and
a bug that could not be read does not count as open. The day a bug was filed, the New marker
and the assignee are shown only for a bug that was read whole; a bug that could not be read
says only Not read. New means filed at or after the build's queue time, which is the only
threshold the data supports: there is no upper bound, so a report regenerated for an older
build can mark a bug that was filed for a later one, and the day beside the marker settles it.
When and where the report was made is shown under every view. Every group, attempt and
attachment preview starts collapsed, and every attempt holds its own full error message and
stack trace, even when an earlier attempt of the same test had the same text. Every date and
time is shown in the time zone of the computer that runs the export, like the report's
generation time.

When the build's test runs carry different stage, job or run names, for example one stage
per language, each card groups its attempts by them and the overview shows one status column
per group. The label uses the shortest name that tells the groups apart: the stage name,
then the job name, then the job instance, then the run name. A run named like another run of
the same job plus ` (attempt N)`, where N is its job attempt, is a retry of that run and
stays in its group. Runs without distinct names keep one list.

Flaky tests are left out unless `-IncludeFlaky` is supplied; the header still counts
them. The report is complete with scripts blocked. A small static script, allowed by a
hash-based Content Security Policy, adds view switching, search, keyboard navigation and
copy, and makes the counts at the top of the report shortcuts to their filters. It opens the
attempt of a test's latest error when the test is reached from its row or with j or k,
opens a stack trace that has a frame of the test's own code with the framework frames
hidden, and opens error messages wrapped; copying and printing keep every frame. Search
matches every word you type, ignoring case and accents, against the whole card, including collapsed
attempts: error messages, stack traces, inline JSON and text attachments, stage and job
names, run and attempt fields, the days of the run history, and bug titles, states, filing days
and assignees. A
Test Case ID matches with or
without `#`. When at least one test has an open bug, the Without an open bug filter shows
only the tests that no open bug tracks yet.

Attachments appear only for test runs that started within `-AttachmentWindowDays` days
of the export, 7 by default; a run without a start date counts as outside. Older runs keep
every attempt, but their attachments are left out. Every attachment name links to the
attachment in Azure DevOps, which the browser downloads with your Windows sign-in. Only JSON
(`.json`) and text (`.txt`, `.log`) attachments are ever downloaded by the export; PNG,
HTML and other attachments stay links, whatever the switches. By default, JSON and text
files of at most `maximumInlineJsonBytes` (256 KiB) are downloaded from every run inside the
window, and larger ones only from the most recent test run. The latest run is the last in
attempt order: stage, phase and job attempt, then start date and run ID. A larger file of an
older run stays a link: the export does not fall back to an older run for those. A file of an
older run that declares no size is downloaded and kept only if it is at most
`maximumInlineJsonBytes`. When no run has a file to download, no attachment folder is
created and no connection is needed. A set gathered with
`Get-AdoBuildTestFailure -SkipAttachments` lists no attachment, so its export downloads
nothing and needs no connection, whatever the switches; its report says that the attachments
were not listed, in the header and in Runs and history, instead of counting them.

Use `-AllRunAttachments` to download the larger JSON and text files from every run inside
the window too, or `-SkipAttachments` to download none. `-SkipAttachments` takes precedence
if both are supplied. Selected attachments are downloaded into a folder named
`<report base name>.files-<UTC stamp>` beside the report, the latest run first and then the
older runs, each in report order, so the total size limit is never used up by older runs
first. Up to `testResults.maximumConcurrentRequests` files, 6 by default, are read at the
same time; each file's outcome is still decided in that order, so the limits, the warnings
and the files are the same as when one file is read at a time. Local files use toolkit names:
`r<run>-<result>[-s<sub-result>]-a<attachment>.<ext>`, where `.log` files are saved as
`.txt`. Remote names are display text and never become paths.

Downloaded content is checked before it is previewed: JSON must parse, and text must be
UTF-8 or UTF-16 with a byte order mark. A failed check saves the file as `.bin` and links it
without a preview. A preview is shown, and searchable, only for files up to
`maximumInlineJsonBytes` and while the report's inline total stays within
`maximumInlineTotalBytes`; larger files are linked only. The previews of the latest run are
chosen first, then those of older runs. Size limits (`maximumAttachmentBytes`,
`maximumTotalAttachmentBytes`) and failed downloads produce warnings; affected attachments
retain their Azure DevOps name, size and download link.
Authentication, authorization and cancellation stop the export.

The report and its folder are committed in this order: download into a temporary
folder, render and validate a temporary report, rename the folder, then replace the
report. A failure before replacement leaves the previous report and folder unchanged.
Old generation folders of the same report are removed after replacement; cleanup
failures produce warnings and the new report stays committed. Reparse points are
skipped and never followed.

## EXAMPLES

### Example 1

```powershell
Get-AdoBuild -Definition 'Main Build' -Branch main -Latest -Result Failed |
    Get-AdoBuildTestFailure -HistoryCount 15 |
    Export-AdoBuildTestFailure -Culture fr-CA -Path .\triage -Open
```

Writes `.\triage\Build-<id>-TestFailures.html` in French with its attachment folder,
then opens the report. The `triage` directory is created when it does not exist.

### Example 2

```powershell
Get-AdoBuildTestFailure -BuildId 401 | Export-AdoBuildTestFailure -Path .\build-401.html -SkipAttachments -WhatIf
```

Retrieves the failure set, then names the report that would be written. The export
makes no attachment-content requests and writes no files; the upstream
`Get-AdoBuildTestFailure` still retrieves data from Azure DevOps.

### Example 3

```powershell
Get-AdoBuildTestFailure -BuildId 401 | Export-AdoBuildTestFailure -IncludeFlaky -AllRunAttachments -AttachmentWindowDays 14
```

Includes flaky tests and downloads JSON and text attachments from every test run that
started in the last 14 days.

## PARAMETERS

### -InputObject

Failure set from Get-AdoBuildTestFailure. Accepts pipeline input; each set produces one report.

```yaml
Type: AdoToolkit.Core.TestRuns.AdoBuildTestFailureSet
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Input
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Culture

Report culture, for example en-US or fr-CA. Defaults to the configured report culture, then the session culture. An unsupported culture falls back to English with a warning.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Path

FileSystem directory, or an .html file when exactly one set arrives; each later set then gets a per-input error. A directory that does not exist yet is created when the report is written, never with -WhatIf; a path with another file extension is rejected, and a trailing separator always means a directory. The default name is Build-<id>-TestFailures.html and the default directory is the Downloads known folder. Wildcards are not expanded.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SkipAttachments

Downloads nothing and creates no folder; attachments of runs inside the window are listed with their Azure DevOps names, sizes and download links. Takes precedence over -AllRunAttachments.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -AllRunAttachments

Downloads JSON and text attachments of any size from every run inside the attachment window. Without it, runs other than the most recent give only their files of at most maximumInlineJsonBytes. PNG and HTML attachments are never downloaded. Existing per-file and total size limits still apply. Has no effect with -SkipAttachments.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -AttachmentWindowDays

Days before the export, from 1 to 365, in which a test run must have started for its attachments to appear and be downloaded. Older runs keep every attempt but lose their attachments; a run without a start date counts as outside. Defaults to 7.

```yaml
Type: System.Int32
DefaultValue: '7'
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -IncludeFlaky

Includes flaky tests, which failed and then passed in every stage, job or named test run. Without it they are left out of the report and only counted in its header.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -NoClobber

Refuses an existing report before any download and never replaces a report created meanwhile.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Open

Opens each committed report with the default handler. A report that cannot be opened produces a warning and is still returned.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Connection

Explicit connection used for attachment downloads; overrides the active runspace connection. When attachments are downloaded, a set from another collection produces a per-input error.

```yaml
Type: AdoToolkit.Core.Connections.AdoConnection
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -WhatIf

Names the report and the attachment folder without requests, downloads or writes.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- wi
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Confirm

Asks once per report before downloading attachments and writing.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- cf
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### CommonParameters

Supports ErrorAction, ErrorVariable, WarningVariable, Verbose, and Debug.

## INPUTS

### AdoToolkit.Core.TestRuns.AdoBuildTestFailureSet

## OUTPUTS

### System.IO.FileInfo

The committed report. When attachments were downloaded, its AttachmentDirectory note property holds the folder path. No object with WhatIf. The report's file:/// address is also written to the information stream with the PSHOST tag, so it shows in the console like Write-Host output; -InformationAction Ignore hides it. Nothing is written with WhatIf or when the export fails.

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020. Downloaded attachments are work data and stay on this machine. How Server 2020 answers several downloads at once has not been confirmed at work (V-33). With -Verbose, each step of the export writes one line with its milliseconds: the attachment downloads, with the files written and the requests sent; the rendered report, with its size in bytes; the check of the report; and its move into place, which also removes earlier attachment folders. When nothing is downloaded, the downloads line keeps its place with 0 files and 0 requests. A last line gives the build, the downloaded attachments, the requests and the elapsed time. Attachment routes, version and fields await server confirmation (V-23), as do the stage, job and run names used for grouping and the run name retry suffix (V-19), and browser behavior from local files awaits confirmation under the work browser policy (V-27). Whether Server 2020 returns the bug's creation date and assignee in a work item batch field projection has not been observed at work (V-37); without them a bug's line shows no day, no ✦ marker and no assignee, with no warning.

## RELATED LINKS

[Get-AdoBuildTestFailure](Get-AdoBuildTestFailure.md)

[Get-AdoBuild](Get-AdoBuild.md)
