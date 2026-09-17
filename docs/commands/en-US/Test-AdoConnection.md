---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Test-AdoConnection
---

# Test-AdoConnection

## SYNOPSIS

Checks project access using the selected connection.

## SYNTAX

### Default (Default)

```
Test-AdoConnection [[-Connection] <AdoConnection>]
```

## ALIASES

No aliases.

## DESCRIPTION

Calls the projects API with version 6.0 and returns success, elapsed time, the requested API version, and guidance. Connection overrides the active runspace connection and accepts pipeline input. Authentication, authorization, and configuration failures terminate. Other failures emit an error and a failed result; ErrorAction Stop terminates instead. A failed collection request may suggest a project URL without claiming that it is the cause. When the request is not found and the URL ends with the name of a project in the parent collection, the result names the exact Connect-Ado command to use instead; that check sends one more projects request to the parent URL.

## EXAMPLES

### Example 1

```powershell
Get-AdoConnection | Test-AdoConnection
```

Checks project access using the selected connection.

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
  Position: 0
  IsRequired: false
  ValueFromPipeline: true
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

### AdoToolkit.Core.Connections.AdoConnection

Object returned by the command.

## OUTPUTS

### AdoToolkit.Core.Connections.AdoConnectionTestResult

Object returned by the command.

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)


