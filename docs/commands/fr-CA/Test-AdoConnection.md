---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Test-AdoConnection
---

# Test-AdoConnection

## SYNOPSIS

Vérifie l’accès aux projets avec la connexion sélectionnée.

## SYNTAX

### Default (Default)

```
Test-AdoConnection [[-Connection] <AdoConnection>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Appelle l’API des projets en version 6.0 et retourne le succès, la durée, la version d’API demandée et des conseils. Connection remplace la connexion active et accepte une entrée de pipeline. Les échecs d’authentification, d’autorisation et de configuration sont bloquants. Les autres échecs produisent une erreur et un résultat d’échec ; ErrorAction Stop interrompt la commande. Un échec peut suggérer une URL de projet sans affirmer qu’elle en est la cause.

## EXAMPLES

### Example 1

```powershell
Get-AdoConnection | Test-AdoConnection
```

Vérifie l’accès aux projets avec la connexion sélectionnée.

## PARAMETERS

### -Connection

Objet de connexion explicite ; remplace la connexion active de l’espace d’exécution.

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

Cette commande accepte les paramètres communs : -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction et -WarningVariable.

## INPUTS

### AdoToolkit.Core.Connections.AdoConnection

Objet retourné par la commande.

## OUTPUTS

### AdoToolkit.Core.Connections.AdoConnectionTestResult

Objet retourné par la commande.

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)


