---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoWorkItem
---

# Get-AdoWorkItem

## SYNOPSIS

Retrieves work items by ID.

## SYNTAX

### Fields (Default)

```
Get-AdoWorkItem [-Id] <int[]> [-Field <string[]>] [-Connection <AdoConnection>] [-CollectionUri <uri>]
```

### Relations

```
Get-AdoWorkItem [-Id] <int[]> -IncludeRelations [-Connection <AdoConnection>] [-CollectionUri <uri>]
```

## ALIASES

No aliases.

## DESCRIPTION

Buffers IDs across the invocation into batches of 200 using API version 6.0. Deduplicates on first occurrence and returns results in input order. Each absent ID produces a non-terminating ObjectNotFound error; other items are returned. Fields contains read-only .NET values keyed by case-insensitive reference names. Links use each item's own project. Objects from another collection produce ConnectionMismatch before any request for those inputs.

## EXAMPLES

### Example 1

```powershell
101, 102, 101 | Get-AdoWorkItem -Field System.AssignedTo
```

Retrieves each item once, with the requested additional field.

### Example 2

```powershell
Get-AdoWorkItem -Id 101 -IncludeRelations
```

Retrieves all fields and the item's relations.

## PARAMETERS

### -Id

Positive IDs up to Int32.MaxValue. Accepts integers by pipeline value and objects with an Id property.

```yaml
Type: System.Int32[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Field

Additional reference names, unioned with the seven System.* fields required for convenience properties. Id and Rev come from the response top level.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Fields
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -IncludeRelations

Requests relations and all fields by omitting fields from the request. Cannot be combined with Field.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Relations
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Connection

Explicit connection object; overrides the active runspace connection.

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

### -CollectionUri

Provenance bound automatically from pipeline objects to check their collection before using Id. Hidden parameter reserved for pipeline binding.

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

### CommonParameters

This cmdlet supports the common parameters, including ErrorAction, ErrorVariable, Verbose, and Debug.

## INPUTS

### System.Int32

### AdoToolkit.Core.WorkItems.AdoWorkItem

## OUTPUTS

### AdoToolkit.Core.WorkItems.AdoWorkItem

Work item with read-only fields.

## NOTES

Field and IncludeRelations are mutually exclusive in the local parameter sets. This is a toolkit restriction pending V-10, not a confirmed server rule. Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
