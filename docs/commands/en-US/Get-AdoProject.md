---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoProject
---

# Get-AdoProject

## SYNOPSIS

Lists projects in the connected collection.

## SYNTAX

### Default (Default)

```
Get-AdoProject [[-Name] <string>] [-Connection <AdoConnection>] [-Top <int>]
```

## ALIASES

No aliases.

## DESCRIPTION

Reads all project pages using API version 6.0. Filters Name with case-insensitive PowerShell wildcards before applying Top. Uses a bounded runspace cache with a fifteen-minute lifetime. Connection overrides the active connection. Project completion uses cached names without network access.

## EXAMPLES

### Example 1

```powershell
Get-AdoProject -Name 'Équipe*' -Top 5
```

Lists projects in the connected collection.

## PARAMETERS

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

### -Name

Case-insensitive wildcard filter for project names.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
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

### -Top

Maximum number of matching projects to return. Omit for all projects; must be positive when supplied.

```yaml
Type: System.Int32
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

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### AdoToolkit.Core.Connections.AdoProject

Object returned by the command.

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)


