---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoConnection
---

# Get-AdoConnection

## SYNOPSIS

Retourne la connexion active de l’espace d’exécution.

## SYNTAX

### Default (Default)

```
Get-AdoConnection
```

## ALIASES

Aucun alias.

## DESCRIPTION

Retourne la connexion sélectionnée par Connect-Ado sans contacter le serveur. Ne produit aucune sortie en l’absence de connexion.

## EXAMPLES

### Example 1

```powershell
Get-AdoConnection
```

Retourne la connexion active de l’espace d’exécution.

## PARAMETERS

### CommonParameters

Cette commande accepte les paramètres communs : -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction et -WarningVariable.

## INPUTS

## OUTPUTS

### AdoToolkit.Core.Connections.AdoConnection

Objet retourné par la commande.

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)


