---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-22-2026
PlatyPS schema version: 2024-05-01
title: Connect-Ado
---

# Connect-Ado

## SYNOPSIS

Selects the connection for this runspace.

## SYNTAX

### Default (Default)

```
Connect-Ado [-Project <string>]
```

### ByUrl

```
Connect-Ado -CollectionUrl <string> [-Project <string>]
```

### ByProfile

```
Connect-Ado -Profile <string> [-Project <string>]
```

## ALIASES

No aliases.

## DESCRIPTION

Creates a session connection using an explicit collection URL, a named profile, or the default profile, in that order. Uses your Windows identity. This command does not contact the server; use Test-AdoConnection to check access. Project overrides the profile default. A profile connection also carries the profile's default branch, build definition, test plan and test suite, which commands use when the matching parameters are omitted; a connection from a collection URL has none. The values are copied when the connection is made, so connect again after changing the profile. Azure DevOps Services URLs are rejected. When no connection exists, other AdoToolkit commands connect with the default profile in the same way, without contacting the server.

## EXAMPLES

### Example 1

```powershell
Connect-Ado -Profile work -Project 'Équipe Web'
```

Selects the connection for this runspace.

## PARAMETERS

### -CollectionUrl

Absolute Azure DevOps Server collection URL. Matching quotes and trailing slashes are removed. HTTPS is recommended; HTTP emits a warning.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByUrl
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Profile

Name of a saved local profile. Completion reads the local configuration only.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByProfile
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Project

Default project for this connection. Completion uses the runspace project cache only, and a completed name is quoted so that it can be run as shown. A command that uses a project named . or .. fails with a configuration error before any request.

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


