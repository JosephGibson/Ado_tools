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

## ALIASES

Aucun alias.

## DESCRIPTION

Émet un objet AdoBuildTestFailureSet par build reçu. La récupération se fait en deux passes. La première liste tous les résultats de tests de toutes les séries du build, regroupe les résultats en identités de tests d’après le stockage et le nom du test automatisé, puis classe chaque identité. La seconde lit les détails complets uniquement pour les identités ayant une tentative en échec, dans l’ordre du rapport, afin d’obtenir chaque tentative, message, trace de pile et liste de pièces jointes. Un test est signalé dès qu’une tentative a échoué : une identité dont la dernière tentative a réussi est instable (Flaky), sinon en échec (Failed), et toutes les tentatives sont conservées, y compris celles qui ont réussi. Les groupes de réexécution dans la tâche, les nouvelles tentatives de travail ou d’étape et les résultats simples sont tous des sources de tentatives ; les groupes pilotés par les données et les autres groupes qui ne sont pas des réexécutions sont imbriqués dans leur tentative. Les références aux cas de test sont résolues en un seul lot. L’historique des exécutions ajoute le build courant et des builds antérieurs de la même définition, avec des décomptes et une cellule par identité signalée ; un problème d’historique ne fait jamais échouer le rapport. Les tests qui ont seulement réussi ou qui n’ont pas été exécutés sont comptés dans le sommaire et l’historique, sans détail. Les métadonnées des pièces jointes sont lues ici ; leur contenu n’est téléchargé que par Export-AdoBuildTestFailure. Le statut est Partial lorsqu’un diagnostic d’erreur existe, par exemple lorsque le nombre d’identités en échec dépasse le maximum configuré.

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

### -HistoryCount

Nombre d’exécutions dans la fenêtre d’historique, y compris l’exécution courante, de 1 à 50. Par défaut, la valeur de configuration testResults.historyCount, soit 10.

```yaml
Type: System.Int32
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
Type: AdoToolkit.Core.TestRuns.AdoTestHistoryScope
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
