---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoProfile
---

# Get-AdoProfile

## SYNOPSIS

Lists locally saved connection profiles.

## SYNTAX

### Default (Default)

```
Get-AdoProfile [[-Name] <string>]
```

## ALIASES

No aliases.

## DESCRIPTION

Reads the local configuration without contacting the server. Returns profiles sorted by name; Name supports PowerShell wildcards. ADOTOOLKIT_CONFIG_PATH overrides the default configuration path.

## EXAMPLES

### Example 1

```powershell
Get-AdoProfile -Name 'work*'
```

Lists locally saved connection profiles.

## PARAMETERS

### -Name

Profile name. Get-AdoProfile accepts case-insensitive wildcard patterns; writes use the literal name.

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### AdoToolkit.Core.Configuration.AdoProfile

Object returned by the command.

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)


