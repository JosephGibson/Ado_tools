---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Save-AdoBuildLog
---

# Save-AdoBuildLog

## SYNOPSIS

Saves a build log locally, byte for byte.

## SYNTAX

### ByLogId (Default)

```
Save-AdoBuildLog [-BuildId] <int> [-LogId] <int> [-Tail <int>] [-Path <string>] [-Connection <AdoConnection>] [-Project <string>] [-CollectionUri <uri>] [-WhatIf] [-Confirm]
```

## ALIASES

No aliases.

## DESCRIPTION

Reads the log list, then copies the received bytes without re-encoding into a temporary file in the destination directory. After validation and flushing to disk, atomically replaces Build-<id>-Log-<logId>.txt. Overwrites existing files by default and returns FileInfo. Downloads have a ten-minute total budget and sixty-second inactivity timeout; each retry restarts a fresh temporary file. An empty log is valid only when the list reports zero lines. Tail uses a zero-based inclusive range (V-14 assumption awaiting confirmation at work). BuildId and LogId bind by property name from AdoBuildFailure. Logs remain local work data.

## EXAMPLES

### Example 1

```powershell
Get-AdoBuild -Definition 'Main Build' -Branch main -Latest -Result Failed |
    Get-AdoBuildFailure |
    Save-AdoBuildLog -Tail 200 -Path .\triage
```

Saves the final two hundred lines of each available log in the existing triage directory.

## PARAMETERS

### -BuildId

Positive build ID in the selected project. Binds by property name.

```yaml
Type: System.Int32
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -LogId

Positive log ID. A null value produces the per-input non-terminating BuildLogNotAvailable error; the pipeline continues.

```yaml
Type: System.Nullable[System.Int32]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Tail

Positive number of final lines to request. Omit for the full log. If the line count is unknown, warns and downloads the full log.

```yaml
Type: System.Nullable[System.Int32]
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

Existing FileSystem directory. Defaults to the Downloads known folder, including folder redirection. Wildcards are not expanded.

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

### -Connection

Explicit connection; overrides the active runspace connection.

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

### -Project

Project name; accepts the `TeamProject` property from piped failures. Defaults to the connection project when no value is supplied.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- TeamProject
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -CollectionUri

Provenance property bound automatically from the failure. A different collection produces a per-input error.

```yaml
Type: System.Uri
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: true
AcceptedValues: []
HelpMessage: ''
```

### -WhatIf

Lists the intended file without HTTP requests or writes.

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

Asks for confirmation before downloading and writing.

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

Supports ErrorAction, ErrorVariable, Verbose, Debug, and WarningAction.

## INPUTS

### AdoToolkit.Core.Builds.AdoBuildFailure

## OUTPUTS

### System.IO.FileInfo

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020. Log line ranges await server confirmation (V-14).

## RELATED LINKS

[Get-AdoBuildFailure](Get-AdoBuildFailure.md)
