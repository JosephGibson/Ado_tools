---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoTestPlan
---

# Get-AdoTestPlan

## SYNOPSIS

Lists the test plans of a project.

## SYNTAX

### Default (Default)

```
Get-AdoTestPlan [[-Name] <string>] [-Project <string>] [-Id <int>] [-Connection <AdoConnection>]
```

## ALIASES

No aliases.

## DESCRIPTION

Reads every page of the project's test plan listing with the testplan area at API version 6.0-preview.1, following continuation tokens until none remain, and returns plans in server order. Id and Name filter the complete listing locally. Name matching ignores case, treats accents as significant, and compares composed and decomposed accents as equal. Each plan's WebUrl is built from the connection's collection URL, the project, and the plan ID.

## EXAMPLES

### Example 1

```powershell
Get-AdoTestPlan -Project 'Équipe Web' -Name 'Tâches*'
```

Lists plans whose names start with Tâches, but not Taches.

### Example 2

```powershell
Get-AdoTestPlan -Id 812 | Get-AdoTestSuite -Recurse
```

Retrieves one plan from the default project and lists its whole suite tree.

## PARAMETERS

### -Name

Wildcard pattern for plan names. Case-insensitive and accent-sensitive; both sides are normalized to composed form before comparison.

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

### -Project

Project name. Defaults to the connection's default project; an error is raised when neither is available.

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

### -Id

Positive plan ID, matched against the complete listing. When no plan has this ID, a non-terminating ObjectNotFound error is written.

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

### CommonParameters

This cmdlet supports the common parameters, including ErrorAction, ErrorVariable, Verbose, and Debug.

## INPUTS

### None

## OUTPUTS

### AdoToolkit.Core.TestManagement.AdoTestPlan

Plan with its root suite ID, project, collection, and web link.

## NOTES

The testplan-area route and preview version are pending server confirmation (V-04). Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020.

## RELATED LINKS

[Get-AdoTestSuite](Get-AdoTestSuite.md)
