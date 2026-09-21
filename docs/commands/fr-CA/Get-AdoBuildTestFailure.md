---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-16-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoBuildTestFailure
---

# Get-AdoBuildTestFailure

## SYNOPSIS

Rassemble les tests en échec et instables d’une exécution de pipeline.

## SYNTAX

### ByBuildId (Default)

```
Get-AdoBuildTestFailure [-BuildId] <int> [-HistoryCount <int>] [-HistoryScope <AdoTestHistoryScope>]
    [-Project <string>] [-Connection <AdoConnection>]
```

### ByBuild

```
Get-AdoBuildTestFailure -InputObject <AdoBuild> [-HistoryCount <int>] [-HistoryScope <AdoTestHistoryScope>]
    [-Connection <AdoConnection>]
```

### ByDefinition

```
Get-AdoBuildTestFailure -Definition <Object> [-Branch <string>] [-Result <BuildResult>] [-HistoryCount <int>]
    [-HistoryScope <AdoTestHistoryScope>] [-Project <string>] [-Connection <AdoConnection>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Émet un objet AdoBuildTestFailureSet par build reçu. La récupération se fait en deux passes. La première liste tous les résultats de tests de toutes les séries du build, regroupe les résultats en identités de tests d’après le stockage et le nom du test automatisé, puis classe chaque identité. La seconde lit les détails complets uniquement pour les identités ayant une tentative en échec, dans l’ordre du rapport, afin d’obtenir chaque tentative, message, trace de pile et liste de pièces jointes. Un test est signalé dès qu’une tentative a échoué. Les tentatives sont regroupées selon les noms de phase, de travail et d’instance du travail de leurs séries de tests : une identité est instable (Flaky) lorsque la dernière tentative de chaque groupe a réussi, sinon en échec (Failed), de sorte qu’un échec dans une phase n’est jamais masqué par une réussite ultérieure dans une autre. Les séries sans noms distincts forment un seul groupe, où la dernière tentative décide. Toutes les tentatives sont conservées, y compris celles qui ont réussi. Les groupes de réexécution dans la tâche, les nouvelles tentatives de travail ou d’étape et les résultats simples sont tous des sources de tentatives ; les groupes pilotés par les données et les autres groupes qui ne sont pas des réexécutions sont imbriqués dans leur tentative. Les références aux cas de test sont résolues en un seul lot. L’historique des exécutions ajoute le build courant et des builds antérieurs de la même définition, avec des décomptes et une cellule par identité signalée ; un problème d’historique ne fait jamais échouer le rapport. Les tests qui ont seulement réussi ou qui n’ont pas été exécutés sont comptés dans le sommaire et l’historique, sans détail. Les métadonnées des pièces jointes sont lues ici ; leur contenu n’est téléchargé que par Export-AdoBuildTestFailure. Le statut est Partial lorsqu’un diagnostic d’erreur existe, par exemple lorsque le nombre d’identités en échec dépasse le maximum configuré. Avec -Definition, le dernier build terminé de la définition est choisi, comme avec Get-AdoBuild -Latest, éventuellement filtré par branche et par résultat.

## EXAMPLES

### Exemple 1

```powershell
Get-AdoBuild -Definition 'Main Build' -Branch main -Latest -Result Failed | Get-AdoBuildTestFailure -HistoryCount 15
```

Rassemble les tests en échec et instables du dernier build échoué, avec quinze exécutions d’historique.

### Exemple 2

```powershell
(Get-AdoBuildTestFailure -BuildId 401).Failures | Where-Object Classification -eq Flaky
```

Sélectionne les tests instables d’un build pour les scripts.

### Exemple 3

```powershell
Get-AdoBuildTestFailure -Definition 'Main Build' -Branch main -Result Failed |
    Export-AdoBuildTestFailure -Path .\reports\nightly -Open
```

Rassemble les tests en échec du dernier build échoué de la branche main et écrit le rapport dans un nouveau dossier, puis l’ouvre.

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

### -Definition

Identifiant entier positif ou nom exact de définition de build. Le dernier build terminé de cette définition est utilisé. Une chaîne numérique est un nom ; utilisez un entier pour un identifiant.

```yaml
Type: System.Object
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByDefinition
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Branch

Nom de branche ou chemin complet refs/ qui limite le choix du dernier build. Par exemple, main devient refs/heads/main.

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

### -Result

Filtre facultatif sur le résultat du dernier build : None, Succeeded, PartiallySucceeded, Failed ou Canceled.

```yaml
Type: System.Nullable`1[AdoToolkit.Core.Builds.BuildResult]
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

### -HistoryCount

Nombre d’exécutions dans la fenêtre d’historique, y compris l’exécution courante, de 1 à 50. Par défaut, la valeur de configuration testResults.historyCount, soit 10.

```yaml
Type: System.Nullable[System.Int32]
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

### -HistoryScope

Portée de branche de la fenêtre d’historique. SameBranch conserve la branche source courante ; AllBranches l’élargit. Par défaut, la valeur de configuration testResults.historyScope, soit SameBranch.

```yaml
Type: System.Nullable[AdoToolkit.Core.TestRuns.AdoTestHistoryScope]
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
AcceptedValues:
- SameBranch
- AllBranches
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

### AdoToolkit.Core.TestRuns.AdoBuildTestFailureSet

## NOTES

Nécessite PowerShell 7.6 sur Windows et Azure DevOps Server 2020. Les listes d’historique sont mises en cache pour l’invocation, de sorte que plusieurs builds d’une même définition reçus du pipeline les partagent. Toutes les routes, versions et champs de la zone de tests restent à confirmer sur le serveur (V-19 à V-25), y compris la façon dont les nouvelles tentatives sont enregistrées. Les liens Web restent à confirmer au travail (V-26).

## RELATED LINKS

[Export-AdoBuildTestFailure](Export-AdoBuildTestFailure.md)

[Get-AdoTestRun](Get-AdoTestRun.md)

[Get-AdoBuild](Get-AdoBuild.md)
