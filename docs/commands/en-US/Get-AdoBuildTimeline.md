---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoBuildTimeline
---

# Get-AdoBuildTimeline

## SYNOPSIS

Lists build timeline records in tree order.

## SYNTAX

### ByBuildId (Default)

```
Get-AdoBuildTimeline [-BuildId] <int> [-Project <string>] [-Connection <AdoConnection>]
```

### ByBuild

```
Get-AdoBuildTimeline -InputObject <AdoBuild> [-Connection <AdoConnection>]
```

## ALIASES

No aliases.

## DESCRIPTION

Retrieves the Build timeline at API version 6.0 and emits parents before children, sorting siblings by order. All records, including earlier attempts, retain their IDs, parent IDs, identifiers, attempt history, issues, and log IDs. Piped builds provide their own project. A foreign collection produces a ConnectionMismatch error before any request.

## EXAMPLES

### Example 1

```powershell
Get-AdoBuild -Definition 42 -Latest | Get-AdoBuildTimeline
```

Lists the latest build’s timeline, including retry metadata.

## PARAMETERS

### -BuildId

Positive build ID in the selected project.

```yaml
Type: System.Int32
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByBuildId
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -InputObject

Build received by value from Get-AdoBuild. Uses its owning project and checks CollectionUri.

```yaml
Type: AdoToolkit.Core.Builds.AdoBuild
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByBuild
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Project

Project name. Defaults to the active connection’s project. For piped objects, the owning project is used.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByBuildId
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

### CommonParameters

Supports common parameters including ErrorAction, ErrorVariable, Verbose, and Debug.

## INPUTS

### AdoToolkit.Core.Builds.AdoBuild

## OUTPUTS

### AdoToolkit.Core.Builds.AdoTimelineRecord

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020. Timeline shapes and paging await server confirmation (V-11, V-14). Web links await confirmation at work.

## RELATED LINKS

[Get-AdoBuild](Get-AdoBuild.md)
