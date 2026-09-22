---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-22-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoBuild
---

# Get-AdoBuild

## SYNOPSIS

Recherche les builds d’un pipeline par définition, branche, état et résultat.

## SYNTAX

### ByDefinition (Default)

```
Get-AdoBuild [[-Definition] <Object>] [-Branch <string>] [-Latest] [-Status <BuildStatus>] [-Result <BuildResult>] [-Top <int>] [-Project <string>] [-Connection <AdoConnection>]
```

### ByDefinitionObject

```
Get-AdoBuild -InputObject <AdoBuildDefinition> [-Branch <string>] [-Latest] [-Status <BuildStatus>] [-Result <BuildResult>] [-Top <int>] [-Connection <AdoConnection>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Sans Definition, la valeur defaultBuildDefinition du profil connecté s’applique ; sans elle non plus, la commande échoue avec une erreur de configuration avant toute requête. Sans Branch, la valeur defaultBranch du profil connecté s’applique, y compris aux définitions reçues du pipeline ; sans elle, les builds de toutes les branches sont retournés. Une Definition entière désigne un identifiant. Une chaîne recherche un nom exact dans toutes les définitions, sans tenir compte de la casse et avec normalisation NFC des accents. Zéro ou plusieurs correspondances produisent une erreur indiquant le nombre et recommandant un identifiant. Les branches sans préfixe refs/ reçoivent refs/heads/. Latest demande un seul build, trié par date de fin décroissante, avec Status à Completed par défaut ; sinon Status vaut All. Top limite les résultats indépendamment de la taille des pages. Les jetons de continuation sont suivis même après une page vide. Une définition reçue du pipeline fournit son projet et doit appartenir à la collection sélectionnée. Les builds comprennent les métadonnées de source et de dépôt, l’identité, les dates UTC et un WebUrl reconstruit.

## EXAMPLES

### Exemple 1

```powershell
Get-AdoBuild -Definition 42 -Branch main -Latest -Result Failed | Get-AdoBuildFailure
```

Recherche le dernier build terminé en échec sur main et détermine les échecs de sa chronologie.

### Exemple 2

```powershell
Get-AdoBuild -Latest -Result Failed
```

Recherche le dernier build en échec de la définition de build et de la branche par défaut enregistrées dans le profil de connexion.

## PARAMETERS

### -Definition

Identifiant entier positif ou nom exact de définition. Une chaîne numérique est un nom ; utilisez un entier pour un identifiant. Par défaut, la valeur defaultBuildDefinition du profil connecté ; obligatoire si le profil n’en a pas.

```yaml
Type: System.Object
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByDefinition
  Position: 0
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -InputObject

Définition reçue par valeur depuis Get-AdoBuildDefinition. La collection d’origine est vérifiée avant toute requête.

```yaml
Type: AdoToolkit.Core.Builds.AdoBuildDefinition
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByDefinitionObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Branch

Nom de branche ou chemin complet refs/. Par exemple, main devient refs/heads/main. Par défaut, la valeur defaultBranch du profil connecté ; sans elle, aucun filtre de branche n’est appliqué.

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

### -Latest

Demande au plus un build avec queryOrder=finishTimeDescending et $top=1.

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

### -Status

Completed, InProgress ou All. Par défaut, Completed avec Latest et All sinon.

```yaml
Type: System.Nullable`1[AdoToolkit.Core.Builds.BuildStatus]
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

### -Result

Filtre facultatif : None, Succeeded, PartiallySucceeded, Failed ou Canceled.

```yaml
Type: System.Nullable`1[AdoToolkit.Core.Builds.BuildResult]
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

### -Top

Limite positive de résultats. Arrête la pagination une fois atteinte. Latest limite toujours la sortie à un build.

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
- Name: ByDefinition
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

### AdoToolkit.Core.Builds.AdoBuildDefinition

## OUTPUTS

### AdoToolkit.Core.Builds.AdoBuild

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020. Les formes de chronologie et la pagination restent à confirmer sur le serveur (V-11, V-14). Les liens web restent à confirmer au travail.

## RELATED LINKS

[Get-AdoBuildFailure](Get-AdoBuildFailure.md)
