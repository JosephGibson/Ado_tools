---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-16-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoTestRun
---

# Get-AdoTestRun

## SYNOPSIS

Liste les séries de tests d’une exécution de pipeline.

## SYNTAX

### ByBuildId (Default)

```
Get-AdoTestRun [-BuildId] <int> [-Project <string>] [-Connection <AdoConnection>]
```

### ByBuild

```
Get-AdoTestRun -InputObject <AdoBuild> [-Connection <AdoConnection>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Filtre les séries de tests par l’URI du build et lit leurs détails, en parcourant toutes les pages. Utilise l’URI du build lorsqu’il en possède un, sinon compose vstfs:///Build/Build/<id>. Avec BuildId, lit d’abord le build ; un build reçu du pipeline fournit déjà ces métadonnées. Les séries sont émises dans l’ordre des tentatives : la tentative de pipeline lorsque la série l’expose, puis la date de début, puis l’identifiant de la série. Les décomptes par résultat proviennent des statistiques de la série et conservent le nom exact rapporté par le serveur. Les liens Web sont reconstruits à partir de l’URL de la collection et des identifiants numériques ; les URL des réponses sont ignorées. Une autre collection produit une erreur ConnectionMismatch pour cette entrée.

## EXAMPLES

### Exemple 1

```powershell
Get-AdoBuild -Definition 'Main Build' -Latest | Get-AdoTestRun
```

Liste les séries de tests du dernier build d’une définition.

## PARAMETERS

### -BuildId

Identifiant de build positif dans le projet choisi.

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

Build reçu par valeur depuis Get-AdoBuild. Utilise son projet propriétaire et vérifie CollectionUri.

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

Nom du projet. Par défaut, le projet de la connexion active. Pour les objets reçus du pipeline, le projet propriétaire est utilisé.

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

Connexion explicite ; remplace la connexion active de l’espace d’exécution.

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

### AdoToolkit.Core.TestRuns.AdoTestRun

## NOTES

Nécessite PowerShell 7.6 sur Windows et Azure DevOps Server 2020. La route, la version, les champs et la pagination des séries de tests restent à confirmer sur le serveur (V-19, V-25), tout comme la référence à la tentative de pipeline. Les liens Web restent à confirmer au travail (V-26).

## RELATED LINKS

[Get-AdoBuildTestFailure](Get-AdoBuildTestFailure.md)
