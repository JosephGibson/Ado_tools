---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoBuildFailure
---

# Get-AdoBuildFailure

## SYNOPSIS

Détermine les échecs les plus profonds de la chronologie d’un build.

## SYNTAX

### ByBuildId (Default)

```
Get-AdoBuildFailure [-BuildId] <int> [-IncludeWarnings] [-Project <string>] [-Connection <AdoConnection>]
```

### ByBuild

```
Get-AdoBuildFailure -InputObject <AdoBuild> [-IncludeWarnings] [-Connection <AdoConnection>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Construit l’arbre parentId, conserve la tentative la plus récente par identifier et supprime les enregistrements remplacés avec leurs sous-arbres. Sélectionne les enregistrements failed les plus profonds, ainsi que canceled lorsque le build a été annulé. IncludeWarnings ajoute les enregistrements succeededWithIssues les plus profonds. Les noms de types ne déterminent pas les échecs. Les chemins joignent les noms des ancêtres avec ›. Lit la liste des journaux une fois par build pour obtenir le nombre de lignes, sans télécharger leur contenu. Les identifiants de journaux et nombres de lignes absents restent null. Avec BuildId, lit d’abord les métadonnées du build via BuildsList et buildIds ; un build reçu du pipeline fournit déjà ces métadonnées. Une autre collection produit une erreur ConnectionMismatch pour cette entrée.

## EXAMPLES

### Exemple 1

```powershell
Get-AdoBuildFailure -BuildId 401 -IncludeWarnings
```

Renvoie les échecs et avertissements de la chronologie, leurs chemins et leurs références de journaux.

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

### -IncludeWarnings

Inclut les enregistrements succeededWithIssues les plus profonds.

```yaml
Type: System.Management.Automation.SwitchParameter
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

### AdoToolkit.Core.Builds.AdoBuildFailure

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020. Les formes de chronologie et la pagination restent à confirmer sur le serveur (V-11, V-14). Les liens web restent à confirmer au travail.

## RELATED LINKS

[Get-AdoBuild](Get-AdoBuild.md)
