---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Disconnect-Ado
---

# Disconnect-Ado

## SYNOPSIS

Efface la connexion et les caches de cet espace d’exécution.

## SYNTAX

### Default (Default)

```
Disconnect-Ado
```

## ALIASES

Aucun alias.

## DESCRIPTION

Libère les clients HTTP de cet espace d’exécution après la fin des requêtes actives et vide son cache de projets. Les autres espaces conservent leurs connexions. Ne produit aucune sortie.

## EXAMPLES

### Example 1

```powershell
Disconnect-Ado
```

Efface la connexion et les caches de cet espace d’exécution.

## PARAMETERS

### CommonParameters

Cette commande accepte les paramètres communs : -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction et -WarningVariable.

## INPUTS

## OUTPUTS

### System.Void

Objet retourné par la commande.

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)


