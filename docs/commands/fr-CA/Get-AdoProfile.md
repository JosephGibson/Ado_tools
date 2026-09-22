---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-22-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoProfile
---

# Get-AdoProfile

## SYNOPSIS

Liste les profils de connexion enregistrés localement.

## SYNTAX

### Default (Default)

```
Get-AdoProfile [[-Name] <string>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Lit la configuration locale sans contacter le serveur. Retourne les profils triés par nom ; Name accepte les caractères génériques PowerShell. Chaque profil est affiché sous forme de liste avec sa collection, son projet par défaut, son délai d’expiration des requêtes, ainsi que les valeurs DefaultBranch, DefaultBuildDefinition, DefaultTestPlanId et DefaultTestSuiteId que les commandes de builds et de plans de test utilisent quand ces paramètres sont omis. Une valeur vide n’est pas définie. ADOTOOLKIT_CONFIG_PATH remplace le chemin de configuration par défaut.

## EXAMPLES

### Example 1

```powershell
Get-AdoProfile -Name 'work*'
```

Liste les profils de connexion enregistrés localement.

## PARAMETERS

### -Name

Nom du profil. Get-AdoProfile accepte les caractères génériques sans distinction de casse ; les écritures utilisent le nom littéral.

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

Cette commande accepte les paramètres communs : -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction et -WarningVariable.

## INPUTS

## OUTPUTS

### AdoToolkit.Core.Configuration.AdoProfile

Objet retourné par la commande.

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)


