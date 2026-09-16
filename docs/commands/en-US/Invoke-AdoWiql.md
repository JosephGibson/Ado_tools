---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Invoke-AdoWiql
---

# Invoke-AdoWiql

## SYNOPSIS

Runs a flat WIQL work item query.

## SYNTAX

### Default (Default)

```
Invoke-AdoWiql [-Query] <string> [-Project <string>] [-Top <int>] [-Hydrate] [-Connection <AdoConnection>]
```

## ALIASES

No aliases.

## DESCRIPTION

Posts the query to the project's WIQL endpoint at API version 6.0 and returns one result with the matching IDs in query order, the returned column reference names, and AsOf. Only flat queries are supported; link and tree queries produce a NotSupported error. Without Top, a result of 20,000 or more IDs is treated as possibly incomplete and raises an error without output; results are never truncated silently. Server limit errors are reported with the same advice to narrow the query. With Top, the server is asked for at most that many items and LimitApplied records the limit, so the result is not claimed to be complete. There is no offset paging. The query text is never written to the Verbose or Debug streams.

## EXAMPLES

### Example 1

```powershell
Invoke-AdoWiql -Query "SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] = 'Test Case' ORDER BY [System.Id]"
```

Returns the IDs of matching work items in the default project.

### Example 2

```powershell
Invoke-AdoWiql -Project 'Équipe Web' -Query "SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.State] = 'Active'" -Top 50 -Hydrate
```

Returns at most 50 work items in query order, including the queried columns.

## PARAMETERS

### -Query

WIQL text for a flat query. Portal-only macros need explicit values; the query is not rewritten.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
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

### -Top

Intentional result limit from 1 to 20,000, sent as the $top query parameter and recorded in LimitApplied.

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

### -Hydrate

Returns work items in query order instead of the result object, retrieved in batches of 200 with the returned columns and the convenience fields. Items that are no longer returned produce ObjectNotFound errors. Current revisions are used; AsOf is provenance only.

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

### AdoToolkit.Core.WorkItems.AdoWiqlResult

IDs, columns, AsOf, project, collection, and the applied limit, if any.

### AdoToolkit.Core.WorkItems.AdoWorkItem

Work items in query order when Hydrate is used.

## NOTES

The server's behavior at the 20,000-item limit is pending confirmation (V-06). Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020.

## RELATED LINKS

[Get-AdoWorkItem](Get-AdoWorkItem.md)
