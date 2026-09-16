---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoWorkItem
---

# Get-AdoWorkItem

## SYNOPSIS

Récupère les éléments de travail par identifiant.

## SYNTAX

### Fields (Default)

```
Get-AdoWorkItem [-Id] <int[]> [-Field <string[]>] [-Connection <AdoConnection>] [-CollectionUri <uri>]
```

### Relations

```
Get-AdoWorkItem [-Id] <int[]> -IncludeRelations [-Connection <AdoConnection>] [-CollectionUri <uri>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Regroupe les identifiants de toute l’invocation en lots de 200, avec l’API version 6.0. Supprime les doublons en conservant la première occurrence et retourne les résultats dans l’ordre d’entrée. Chaque identifiant absent produit une erreur ObjectNotFound non bloquante ; les autres éléments sont retournés. Fields contient les valeurs .NET en lecture seule, avec des noms de référence sans distinction de casse. Les liens utilisent le projet propre à chaque élément. Les objets d’une autre collection produisent ConnectionMismatch avant toute requête pour ces objets.

## EXAMPLES

### Example 1

```powershell
101, 102, 101 | Get-AdoWorkItem -Field System.AssignedTo
```

Récupère une fois chaque élément, avec le champ supplémentaire demandé.

### Example 2

```powershell
Get-AdoWorkItem -Id 101 -IncludeRelations
```

Récupère tous les champs et les relations de l’élément.

## PARAMETERS

### -Id

Identifiants positifs, jusqu’à Int32.MaxValue. Accepte les entiers du pipeline et les objets ayant une propriété Id.

```yaml
Type: System.Int32[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Field

Noms de référence supplémentaires, réunis avec les sept champs System.* nécessaires aux propriétés pratiques. Id et Rev proviennent du niveau supérieur de la réponse.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Fields
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -IncludeRelations

Demande les relations et tous les champs en omettant fields dans la requête. Ne peut pas être combiné avec Field.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Relations
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Connection

Objet de connexion explicite ; remplace la connexion active de l’espace d’exécution.

```yaml
Type: AdoToolkit.Core.Connections.AdoConnection
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

### -CollectionUri

Provenance liée automatiquement depuis les objets du pipeline pour vérifier leur collection avant d’utiliser Id. Paramètre masqué réservé à la liaison du pipeline.

```yaml
Type: System.Uri
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: true
AcceptedValues: []
HelpMessage: ''
```

### CommonParameters

Cette commande accepte les paramètres communs, dont ErrorAction, ErrorVariable, Verbose et Debug.

## INPUTS

### System.Int32

### AdoToolkit.Core.WorkItems.AdoWorkItem

## OUTPUTS

### AdoToolkit.Core.WorkItems.AdoWorkItem

Élément de travail avec ses champs en lecture seule.

## NOTES

Field et IncludeRelations sont incompatibles dans les jeux de paramètres locaux. Il s’agit d’une restriction de la boîte à outils en attente de V-10, et non d’une règle confirmée du serveur. Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
