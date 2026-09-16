---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoBuildFailure
---

# Get-AdoBuildFailure

## SYNOPSIS

Derives the deepest build timeline failures.

## SYNTAX

### ByBuildId (Default)

```
Get-AdoBuildFailure [-BuildId] <int> [-IncludeWarnings] [-Project <string>] [-Connection <AdoConnection>]
```

### ByBuild

```
Get-AdoBuildFailure -InputObject <AdoBuild> [-IncludeWarnings] [-Connection <AdoConnection>]
```

## ALIASES

No aliases.

## DESCRIPTION

Builds the parentId tree, keeps the highest attempt per identifier, and removes superseded records with their subtrees. Selects deepest failed records, or canceled records when the build was canceled. IncludeWarnings also selects deepest succeededWithIssues records. Record type names do not determine failures. Paths join ancestor names with ›. Reads the log list once per build for line counts; no log contents are downloaded. Missing log IDs or line counts remain null. With BuildId, retrieves build metadata using BuildsList and buildIds first; piped builds already supply that metadata. A foreign collection produces a ConnectionMismatch error for that input.

## EXAMPLES

### Example 1

```powershell
Get-AdoBuildFailure -BuildId 401 -IncludeWarnings
```

Returns timeline failures and warning records with their paths and log references.

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

### -IncludeWarnings

Includes the deepest succeededWithIssues records.

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

### AdoToolkit.Core.Builds.AdoBuildFailure

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020. Timeline shapes and paging await server confirmation (V-11, V-14). Web links await confirmation at work.

## RELATED LINKS

[Get-AdoBuild](Get-AdoBuild.md)
