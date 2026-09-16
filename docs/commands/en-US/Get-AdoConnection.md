---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoConnection
---

# Get-AdoConnection

## SYNOPSIS

Returns the active runspace connection.

## SYNTAX

### Default (Default)

```
Get-AdoConnection
```

## ALIASES

No aliases.

## DESCRIPTION

Returns the connection selected by Connect-Ado without contacting the server. Produces no output when disconnected.

## EXAMPLES

### Example 1

```powershell
Get-AdoConnection
```

Returns the active runspace connection.

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### AdoToolkit.Core.Connections.AdoConnection

Object returned by the command.

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)


