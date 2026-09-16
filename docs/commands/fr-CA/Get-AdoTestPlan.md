---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoTestPlan
---

# Get-AdoTestPlan

## SYNOPSIS

Liste les plans de test d’un projet.

## SYNTAX

### Default (Default)

```
Get-AdoTestPlan [[-Name] <string>] [-Project <string>] [-Id <int>] [-Connection <AdoConnection>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Lit toutes les pages de la liste des plans de test du projet dans la zone testplan, avec l’API version 6.0-preview.1, en suivant les jetons de continuation jusqu’au dernier, et retourne les plans dans l’ordre du serveur. Id et Name filtrent localement la liste complète. La correspondance de Name ignore la casse, tient compte des accents et considère comme égaux les accents composés et décomposés. Le WebUrl de chaque plan est construit à partir de l’URL de collection de la connexion, du projet et de l’identifiant du plan.

## EXAMPLES

### Example 1

```powershell
Get-AdoTestPlan -Project 'Équipe Web' -Name 'Tâches*'
```

Liste les plans dont le nom commence par Tâches, mais pas par Taches.

### Example 2

```powershell
Get-AdoTestPlan -Id 812 | Get-AdoTestSuite -Recurse
```

Récupère un plan du projet par défaut et liste toute son arborescence de suites.

## PARAMETERS

### -Name

Modèle générique pour les noms de plan. Sans distinction de casse, mais avec distinction des accents ; les deux côtés sont normalisés en forme composée avant la comparaison.

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

### -Project

Nom du projet. Par défaut, le projet par défaut de la connexion ; une erreur est produite si aucun n’est disponible.

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

### -Id

Identifiant de plan positif, comparé à la liste complète. Si aucun plan n’a cet identifiant, une erreur ObjectNotFound non bloquante est écrite.

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

### CommonParameters

Cette commande accepte les paramètres communs, dont ErrorAction, ErrorVariable, Verbose et Debug.

## INPUTS

### None

## OUTPUTS

### AdoToolkit.Core.TestManagement.AdoTestPlan

Plan avec l’identifiant de sa suite racine, son projet, sa collection et son lien web.

## NOTES

La route de la zone testplan et sa version préliminaire restent à confirmer sur le serveur (V-04). Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Get-AdoTestSuite](Get-AdoTestSuite.md)
