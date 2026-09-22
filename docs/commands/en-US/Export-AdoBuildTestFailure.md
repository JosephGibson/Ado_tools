---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-21-2026
PlatyPS schema version: 2024-05-01
title: Export-AdoBuildTestFailure
---

# Export-AdoBuildTestFailure

## SYNOPSIS

Writes one compact HTML failed-test report per failure set and downloads the JSON and text attachments of its most recent test run.

## SYNTAX

### Input (Default)

```
Export-AdoBuildTestFailure [-InputObject] <AdoBuildTestFailureSet> [-Culture <string>] [-Path <string>] [-SkipAttachments] [-AllRunAttachments] [-AttachmentWindowDays <int>] [-IncludeFlaky] [-NoClobber] [-Open] [-Connection <AdoConnection>] [-WhatIf] [-Confirm]
```

## ALIASES

No aliases.

## DESCRIPTION

Renders each `AdoBuildTestFailureSet` from `Get-AdoBuildTestFailure` as one dark HTML
report in the report culture. The report has four views, plus a fifth for diagnostics
when there are any. Overview is a table with one row per failed test: its Test Case
number linked to the work item, the status of each stage or job that ran it, and the
first line of its latest error. A test with at least one open bug shows an Open bug mark
after its name. By error groups the same rows under their latest error. Details has one
card per test, which lists its bugs, each with its ID linked to the work item, its title
and state, and an Open mark when it is open. A bug is open unless its state is in the
Completed or Removed state category. Runs and history lists the build's test runs, the run
history chart and the report details. Every group, attempt and attachment preview starts
collapsed, and an error message or stack trace repeated from an earlier attempt of the
same test is referenced instead of repeated. Every date and time is shown in the time zone
of the computer that runs the export, like the report's generation time.

When the build's test runs carry different stage or job names, for example one stage per
language, each card groups its attempts by them and the overview shows one status column
per group. The label uses the shortest name that tells the groups apart: the stage name,
then the job name, then the job instance. Runs without distinct names keep one list.

Flaky tests are left out unless `-IncludeFlaky` is supplied; the header still counts
them. The report is complete with scripts blocked. A small static script, allowed by a
hash-based Content Security Policy, adds view switching, search, keyboard navigation and
copy. Search matches every word you type against the whole card, including collapsed
attempts: error messages, stack traces, inline JSON and text attachments, stage and job
names, run and attempt fields, and bug titles and states. A Test Case ID matches with or
without `#`. When at least one test has an open bug, the Without an open bug filter shows
only the tests that no open bug tracks yet.

Attachments appear only for test runs that started within `-AttachmentWindowDays` days
of the export, 7 by default; a run without a start date counts as outside. Older runs keep
every attempt, but their attachments are left out. Only JSON (`.json`) and text (`.txt`,
`.log`) attachments are ever downloaded. PNG, HTML and other attachments stay links to
their Azure DevOps result, whatever the switches. By default, only the most recent test run
is downloaded, and only when it is inside the window. The latest run is the last in attempt
order: stage, phase and job attempt, then start date and run ID. If it has no JSON or text
attachments, the export does not fall back to an older run and creates no attachment folder.

Use `-AllRunAttachments` to download JSON and text from every run inside the window, or
`-SkipAttachments` to download none. `-SkipAttachments` takes precedence if both are
supplied. Selected attachments are downloaded in report order into a folder named
`<report base name>.files-<UTC stamp>` beside the report. Local files use toolkit names:
`r<run>-<result>[-s<sub-result>]-a<attachment>.<ext>`, where `.log` files are saved as
`.txt`. Remote names are display text and never become paths.

Downloaded content is checked before it is previewed: JSON must parse, and text must be
UTF-8 or UTF-16 with a byte order mark. A failed check saves the file as `.bin` and links it
without a preview. A preview is shown, and searchable, only for files up to
`maximumInlineJsonBytes` and while the report's inline total stays within
`maximumInlineTotalBytes`; larger files are linked only. Size limits
(`maximumAttachmentBytes`, `maximumTotalAttachmentBytes`) and failed downloads produce
warnings; affected attachments retain their Azure DevOps name, size and result link.
Authentication, authorization and cancellation stop the export. An unreadable history
build does not stop it.

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
then opens the report. The `triage` directory must already exist.

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

Downloads nothing and creates no folder; attachments of runs inside the window are listed with their Azure DevOps names, sizes and result links. Takes precedence over -AllRunAttachments.

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

Downloads JSON and text attachments from every run inside the attachment window instead of only the most recent run. PNG and HTML attachments are never downloaded. Existing per-file and total size limits still apply. Has no effect with -SkipAttachments.

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

Includes flaky tests, which failed and then passed in every stage or job. Without it they are left out of the report and only counted in its header.

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

Opens each committed report with the default handler.

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

Explicit connection used for attachment downloads; overrides the active runspace connection. A set from another collection produces a per-input error.

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

### -Confirm

Asks once per report before downloading attachments and writing.

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

### CommonParameters

Supports ErrorAction, ErrorVariable, WarningVariable, Verbose, and Debug.

## INPUTS

### AdoToolkit.Core.TestRuns.AdoBuildTestFailureSet

## OUTPUTS

### System.IO.FileInfo

The committed report. When attachments were downloaded, its AttachmentDirectory note property holds the folder path. No object with WhatIf.

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020. Downloaded attachments are work data and stay on this machine. Attachment routes, version and fields await server confirmation (V-23), as do the stage and job names used for grouping (V-19), and browser behavior from local files awaits confirmation under the work browser policy (V-27).

## RELATED LINKS

[Get-AdoBuildTestFailure](Get-AdoBuildTestFailure.md)

[Get-AdoBuild](Get-AdoBuild.md)
