---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoBuildTimeline
---

# Get-AdoBuildTimeline

## SYNOPSIS

Liste les enregistrements de chronologie d’un build dans l’ordre de l’arbre.

## SYNTAX

### ByBuildId (Default)

```
Get-AdoBuildTimeline [-BuildId] <int> [-Project <string>] [-Connection <AdoConnection>]
```

### ByBuild

```
Get-AdoBuildTimeline -InputObject <AdoBuild> [-Connection <AdoConnection>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Lit la chronologie Build avec la version 6.0 de l’API et émet les parents avant leurs enfants, en triant les enfants selon order. Tous les enregistrements, y compris les tentatives antérieures, conservent leurs identifiants, parents, historique des tentatives, problèmes et identifiants de journaux. Un build reçu du pipeline fournit son projet. Une autre collection produit une erreur ConnectionMismatch avant toute requête.

## EXAMPLES

### Exemple 1

```powershell
Get-AdoBuild -Definition 42 -Latest | Get-AdoBuildTimeline
```

Liste la chronologie du dernier build, y compris les métadonnées des tentatives.

## PARAMETERS

### -BuildId

Identifiant positif du build dans le projet sélectionné.

```yaml
Type: System.Int32
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByBuildId
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -InputObject

Build reçu par valeur depuis Get-AdoBuild. Utilise son projet d’origine et vérifie CollectionUri.

```yaml
Type: AdoToolkit.Core.Builds.AdoBuild
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByBuild
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Project

Nom du projet. Par défaut, utilise le projet de la connexion active. Pour les objets reçus du pipeline, utilise leur projet d’origine.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByBuildId
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Connection

Connexion explicite, prioritaire sur la connexion active de l’espace d’exécution.

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

### CommonParameters

Prend en charge les paramètres communs, dont ErrorAction, ErrorVariable, Verbose et Debug.

## INPUTS

### AdoToolkit.Core.Builds.AdoBuild

## OUTPUTS

### AdoToolkit.Core.Builds.AdoTimelineRecord

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020. Les formes de chronologie et la pagination restent à confirmer sur le serveur (V-11, V-14). Les liens web restent à confirmer au travail.

## RELATED LINKS

[Get-AdoBuild](Get-AdoBuild.md)
