---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-16-2026
PlatyPS schema version: 2024-05-01
title: Export-AdoBuildTestFailure
---

# Export-AdoBuildTestFailure

## SYNOPSIS

Writes one interactive HTML failed-test report per failure set and downloads attachments from its most recent test run.

## SYNTAX

### Input (Default)

```
Export-AdoBuildTestFailure [-InputObject] <AdoBuildTestFailureSet> [-Culture <string>] [-Path <string>] [-SkipAttachments] [-AllRunAttachments] [-NoClobber] [-Open] [-Connection <AdoConnection>] [-WhatIf] [-Confirm]
```

## ALIASES

No aliases.

## DESCRIPTION

Renders each `AdoBuildTestFailureSet` from `Get-AdoBuildTestFailure` as one dark HTML
report in the report culture: the run history chart and table, an index, one card per
failed or flaky test with every attempt, highlighted messages and stack traces, Test
Case links, and diagnostics. The report is complete with scripts blocked; a small
static script, allowed by a hash-based Content Security Policy, adds filtering,
keyboard navigation and copy.

By default, attachments of reported results and sub-results are downloaded only from
the build's most recent test run. The latest run is the last in attempt order: stage,
phase and job attempt, then start date and run ID. Earlier runs' attachments remain
listed with name, size and an Azure DevOps result link; every run and attempt stays
in the report. If the latest run has no attachments, the export does not fall back
to an older run and creates no attachment folder.

Use `-AllRunAttachments` to download attachments from every reported run, or
`-SkipAttachments` to download none. `-SkipAttachments` takes precedence if both are
supplied. Selected attachments are downloaded in report order into a folder named
`<report base name>.files-<UTC stamp>` beside the report. Local files use toolkit
names: `r<run>-<result>[-s<sub-result>]-a<attachment>.<ext>`. Remote names are display
text and never become paths.

PNG files become thumbnails and JSON files up to the inline limit are highlighted,
after their content is checked. A failed check saves the file as `.bin` and links it
without a preview. HTML attachments are linked as test output and open outside the
report's policy. Size limits (`maximumAttachmentBytes`, `maximumTotalAttachmentBytes`)
and failed downloads produce warnings; affected attachments retain their Azure DevOps
name, size and result link. Authentication, authorization and cancellation stop the
export. An unreadable history build does not stop it.

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

Downloads nothing and creates no folder; attachments are listed with their Azure DevOps names, sizes and result links. Takes precedence over -AllRunAttachments.

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

Downloads attachments from every reported run instead of only the most recent run. Existing per-file and total size limits still apply. Has no effect with -SkipAttachments.

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

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020. Downloaded attachments are work data and stay on this machine. Attachment routes, version and fields await server confirmation (V-23), and browser behavior from local files awaits confirmation under the work browser policy (V-27).

## RELATED LINKS

[Get-AdoBuildTestFailure](Get-AdoBuildTestFailure.md)

[Get-AdoBuild](Get-AdoBuild.md)
