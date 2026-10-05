---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 10-05-2026
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

Appelle l’API des projets en version 6.0 et retourne le succès, la durée, la version d’API demandée et des conseils. Connection remplace la connexion active et accepte une entrée de pipeline. Les échecs d’authentification, d’autorisation et de configuration sont bloquants. Les autres échecs produisent une erreur et un résultat d’échec; ErrorAction Stop interrompt la commande. Un échec peut suggérer une URL de projet sans affirmer qu’elle en est la cause. Lorsque la ressource est introuvable et que l’URL se termine par le nom d’un projet de la collection parente, le résultat indique la commande Connect-Ado exacte à utiliser; cette vérification énumère les projets de l’URL parente, soit une requête par tranche de 100 projets et une dernière qui n’en trouve plus.

## EXAMPLES

### Exemple 1

```powershell
Get-AdoConnection | Test-AdoConnection
```

Vérifie l’accès aux projets avec la connexion sélectionnée.

## PARAMETERS

### -Connection

Objet de connexion explicite; remplace la connexion active de l’espace d’exécution.

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

Cette commande accepte les paramètres communs : -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction et -WarningVariable. Pour en savoir plus, consultez
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### AdoToolkit.Core.Connections.AdoConnection

Connexion provenant de Connect-Ado ou de Get-AdoConnection, reçue du pipeline.

## OUTPUTS

### AdoToolkit.Core.Connections.AdoConnectionTestResult

Objet retourné par la commande.

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoConnection](Get-AdoConnection.md)


