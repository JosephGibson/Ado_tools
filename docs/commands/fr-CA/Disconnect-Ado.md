---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 10-02-2026
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

Libère les clients HTTP de cet espace d’exécution après la fin des requêtes actives et vide ses caches de projets et de catégories de test. Les autres espaces conservent leurs connexions. Ne produit aucune sortie.

## EXAMPLES

### Exemple 1

```powershell
Disconnect-Ado
```

Efface la connexion et les caches de cet espace d’exécution.

## PARAMETERS

### CommonParameters

Cette commande accepte les paramètres communs : -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction et -WarningVariable. Pour en savoir plus, consultez
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Void

La commande ne produit aucune sortie.

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoConnection](Get-AdoConnection.md)


