---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Disconnect-Ado
---

# Disconnect-Ado

## SYNOPSIS

Clears the connection and caches for this runspace.

## SYNTAX

### Default (Default)

```
Disconnect-Ado
```

## ALIASES

No aliases.

## DESCRIPTION

Releases this runspace’s HTTP clients after active requests finish and clears its project cache. Other runspaces keep their connections. Produces no output.

## EXAMPLES

### Example 1

```powershell
Disconnect-Ado
```

Clears the connection and caches for this runspace.

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Void

Object returned by the command.

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)


