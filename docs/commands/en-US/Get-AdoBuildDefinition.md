---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoBuildDefinition
---

# Get-AdoBuildDefinition

## SYNOPSIS

Lists build definitions in a project.

## SYNTAX

### Default (Default)

```
Get-AdoBuildDefinition [[-Name] <string>] [-Project <string>] [-Id <int>] [-Connection <AdoConnection>]
```

## ALIASES

No aliases.

## DESCRIPTION

Reads all definition pages at API version 6.0. Id and Name filter locally. Wildcards ignore case, preserve accent distinctions, and compare names in NFC Unicode form. Output preserves the original names. WebUrl is reconstructed from the connection, project, and ID; server-provided links are ignored. A missing Id reports an ObjectNotFound error.

## EXAMPLES

### Example 1

```powershell
Get-AdoBuildDefinition -Project 'Équipe Web' -Name 'Tâches*'
```

Lists definitions whose names begin with Tâches, with accents significant.

## PARAMETERS

### -Name

Wildcard name pattern. Case-insensitive, accent-sensitive, normalized to NFC.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: true
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Id

Positive definition ID, matched against the complete listing.

```yaml
Type: System.Nullable`1[System.Int32]
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

### CommonParameters

Supports common parameters including ErrorAction, ErrorVariable, Verbose, and Debug.

## INPUTS

### None

## OUTPUTS

### AdoToolkit.Core.Builds.AdoBuildDefinition

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020. Timeline shapes and paging await server confirmation (V-11, V-14). Web links await confirmation at work.

## RELATED LINKS

[Get-AdoBuild](Get-AdoBuild.md)
