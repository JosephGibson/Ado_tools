---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-21-2026
PlatyPS schema version: 2024-05-01
title: Remove-AdoProfile
---

# Remove-AdoProfile

## SYNOPSIS

Supprime un profil de connexion local.

## SYNTAX

### ByName (Default)

```
Remove-AdoProfile [-Name] <string> [-WhatIf] [-Confirm]
```

### ByProfile

```
Remove-AdoProfile -InputObject <AdoProfile> [-WhatIf] [-Confirm]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Supprime le profil nommé ou reçu du pipeline par une écriture atomique de la configuration. La suppression du profil par défaut efface cette sélection. Prend en charge WhatIf et Confirm.

## EXAMPLES

### Example 1

```powershell
Get-AdoProfile -Name work | Remove-AdoProfile -WhatIf
```

Supprime un profil de connexion local.

## PARAMETERS

### -Confirm

Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- cf
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

### -InputObject

Profil à supprimer, accepté depuis Get-AdoProfile par le pipeline.

```yaml
Type: AdoToolkit.Core.Configuration.AdoProfile
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByProfile
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Name

Nom du profil; il ne peut pas être vide. Get-AdoProfile accepte les caractères génériques sans distinction de casse ; les écritures utilisent le nom littéral.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByName
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -WhatIf

Runs the command in a mode that only reports what would happen without performing the actions.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- wi
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

Cette commande accepte les paramètres communs : -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction et -WarningVariable.

## INPUTS

### AdoToolkit.Core.Configuration.AdoProfile

Objet retourné par la commande.

## OUTPUTS

### System.Void

Objet retourné par la commande.

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)


