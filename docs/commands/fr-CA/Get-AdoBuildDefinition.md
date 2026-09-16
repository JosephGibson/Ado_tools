---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoBuildDefinition
---

# Get-AdoBuildDefinition

## SYNOPSIS

Liste les définitions de build d’un projet.

## SYNTAX

### Default (Default)

```
Get-AdoBuildDefinition [[-Name] <string>] [-Project <string>] [-Id <int>] [-Connection <AdoConnection>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Lit toutes les pages de définitions avec la version 6.0 de l’API. Id et Name filtrent localement. Les caractères génériques ignorent la casse, distinguent les accents et comparent les noms normalisés en NFC. Les noms d’origine sont conservés. WebUrl est reconstruit à partir de la connexion, du projet et de l’identifiant. Un Id absent produit une erreur ObjectNotFound.

## EXAMPLES

### Exemple 1

```powershell
Get-AdoBuildDefinition -Project 'Équipe Web' -Name 'Tâches*'
```

Liste les définitions dont le nom commence par Tâches, en distinguant les accents.

## PARAMETERS

### -Name

Motif de nom avec caractères génériques. Ignore la casse, distingue les accents et utilise la normalisation NFC.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: true
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

### -Id

Identifiant positif de définition, recherché dans la liste complète.

```yaml
Type: System.Nullable`1[System.Int32]
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

### None

## OUTPUTS

### AdoToolkit.Core.Builds.AdoBuildDefinition

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020. Les formes de chronologie et la pagination restent à confirmer sur le serveur (V-11, V-14). Les liens web restent à confirmer au travail.

## RELATED LINKS

[Get-AdoBuild](Get-AdoBuild.md)
