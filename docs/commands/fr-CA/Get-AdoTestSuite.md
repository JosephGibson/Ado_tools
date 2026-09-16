---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoTestSuite
---

# Get-AdoTestSuite

## SYNOPSIS

Récupère les suites de tests d’un plan de test.

## SYNTAX

### ByPlanId (Default)

```
Get-AdoTestSuite [-PlanId] <int> [-SuiteId <int>] [-Recurse] [-Project <string>] [-Connection <AdoConnection>]
```

### ByPlan

```
Get-AdoTestSuite -InputObject <AdoTestPlan> [-SuiteId <int>] [-Recurse] [-Connection <AdoConnection>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Lit toutes les pages de la liste des suites du plan dans la zone testplan, avec l’API version 6.0-preview.1, construit l’arborescence à partir des références aux suites parentes et calcule chaque SuitePath de la suite racine jusqu’à la suite. Retourne seule la suite indiquée par SuiteId, ou la suite racine du plan. Avec Recurse, retourne cette suite et toute sa sous-arborescence en profondeur d’abord, en conservant l’ordre des suites sœurs retourné par le serveur. Un SuiteId absent produit une erreur ObjectNotFound non bloquante. Les plans d’une autre collection reçus du pipeline produisent ConnectionMismatch avant toute requête.

## EXAMPLES

### Example 1

```powershell
Get-AdoTestSuite -PlanId 812 -SuiteId 814 -Recurse
```

Liste la suite 814 suivie de ses descendantes, en profondeur d’abord.

### Example 2

```powershell
Get-AdoTestPlan -Name 'Release*' | Get-AdoTestSuite
```

Liste la suite racine de chaque plan correspondant, dans le projet de chaque plan.

## PARAMETERS

### -PlanId

Identifiant positif du plan de test.

```yaml
Type: System.Int32
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByPlanId
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

Plan de test provenant de Get-AdoTestPlan, accepté uniquement par valeur dans le pipeline. Son Id sert d’identifiant de plan et son TeamProject, de projet.

```yaml
Type: AdoToolkit.Core.TestManagement.AdoTestPlan
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByPlan
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SuiteId

Identifiant positif de la suite de départ. Sans lui, la suite racine du plan sert de départ.

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

### -Recurse

Retourne aussi toutes les descendantes de la suite de départ, en profondeur d’abord et dans l’ordre du serveur.

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

Nom du projet. Par défaut, le projet par défaut de la connexion ; une erreur est produite si aucun n’est disponible.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByPlanId
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

### AdoToolkit.Core.TestManagement.AdoTestPlan

## OUTPUTS

### AdoToolkit.Core.TestManagement.AdoTestSuite

Suite avec l’identifiant de son plan, celui de sa suite parente, son chemin, son projet et son lien web. Son Id est un identifiant de suite, jamais un identifiant de cas de test.

## NOTES

La route de la zone testplan, sa version préliminaire et l’ordre des suites sœurs restent à confirmer sur le serveur (V-04). Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Get-AdoTestPlan](Get-AdoTestPlan.md)
