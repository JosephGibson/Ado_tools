---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-22-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoTestCase
---

# Get-AdoTestCase

## SYNOPSIS

Récupère les cas de test par identifiant, suite, résultat WIQL ou élément de travail et développe les étapes partagées.

## SYNTAX

### BySuite (Default)

```
Get-AdoTestCase [-PlanId <int>] [-SuiteId <int>] [-Recurse] [-Project <string>] [-MaximumSharedStepDepth <int>] [-MaximumExpandedSteps <int>] [-Strict] [-Connection <AdoConnection>]
```

### ById

```
Get-AdoTestCase [-Id] <int[]> [-MaximumSharedStepDepth <int>] [-MaximumExpandedSteps <int>] [-Strict] [-Connection <AdoConnection>]
```

### BySuiteObject

```
Get-AdoTestCase -Suite <AdoTestSuite> [-Recurse] [-MaximumSharedStepDepth <int>] [-MaximumExpandedSteps <int>] [-Strict] [-Connection <AdoConnection>]
```

### ByWiql

```
Get-AdoTestCase -WiqlResult <AdoWiqlResult> [-MaximumSharedStepDepth <int>] [-MaximumExpandedSteps <int>] [-Strict] [-Connection <AdoConnection>]
```

### ByWorkItem

```
Get-AdoTestCase -WorkItem <AdoWorkItem> [-MaximumSharedStepDepth <int>] [-MaximumExpandedSteps <int>] [-Strict] [-Connection <AdoConnection>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Regroupe toutes les entrées de l’invocation, puis récupère une seule fois chaque cas de test distinct : éléments de travail racines par lots de 200, une seule résolution des étapes partagées pour toutes les racines avec un cache commun, et un seul développement par cas. Les suites sont lues avec SuiteTestCaseList; avec Recurse, les suites enfants sont parcourues en profondeur et les cas suivent le champ order de la suite. Un cas présent dans plusieurs suites est produit une fois par suite; chaque copie a sa propriété Suite et partage les étapes développées. Les identifiants conservent l’ordre d’entrée et sont dédoublonnés; un cas est aussi produit au plus une fois par suite. Les identifiants absents produisent des erreurs ObjectNotFound; les autres types produisent des erreurs NotAStepContainer; un plan ou une suite introuvable produit une erreur ObjectNotFound pour cette entrée. Les diagnostics d’erreur produisent un objet Partial et un avertissement indiquant le nombre de diagnostics. Strict remplace chaque objet partiel par une erreur non bloquante contenant ses diagnostics. La progression est signalée pour la lecture des suites, les lots d’éléments de travail et le développement. Les suites, résultats WIQL et éléments de travail typés se lient à leur propre jeu de paramètres par valeur depuis le pipeline; un identifiant de suite n’est jamais utilisé comme identifiant de cas de test. BySuite est le jeu de paramètres par défaut : sans PlanId, la valeur defaultTestPlanId du profil connecté est utilisée, et sans SuiteId, sa valeur defaultTestSuiteId. La suite du profil n’est utilisée qu’avec le plan du profil; un PlanId explicite exige donc un SuiteId explicite. Un plan ou une suite manquant produit une erreur de configuration avant toute requête.

## EXAMPLES

### Exemple 1

```powershell
101, 102 | Get-AdoTestCase | Select-Object -ExpandProperty Steps
```

Récupère deux cas et affiche leurs étapes numérotées.

### Exemple 2

```powershell
Get-AdoTestSuite -PlanId 812 -SuiteId 813 -Recurse | Get-AdoTestCase | Export-AdoTestCase -Culture fr-CA -Path .\Release-12.html
```

Récupère tous les cas d’une arborescence de suites et les exporte dans un seul document en français.

### Exemple 3

```powershell
Invoke-AdoWiql -Query "SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] = 'Test Case'" | Get-AdoTestCase
```

Récupère, dans l’ordre de la requête, les cas renvoyés par une requête WIQL plate.

### Exemple 4

```powershell
Get-AdoTestCase -Recurse | Export-AdoTestCase -Open
```

Exporte tous les cas de l’arborescence de la suite de tests par défaut enregistrée dans le profil de connexion.

## PARAMETERS

### -Id

Identifiants positifs de cas de test. Accepte les entiers du pipeline et les objets non typés ayant une propriété Id. Conserve la première occurrence de chaque identifiant. Les objets ayant la forme d’un plan ou d’une suite de tests (propriétés SuitePath ou RootSuiteId) sont refusés avec InputNotTestCase.

```yaml
Type: System.Int32[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ById
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -PlanId

Identifiant du plan de test qui contient la suite. Par défaut, la valeur defaultTestPlanId du profil connecté; obligatoire si le profil n’en a pas.

```yaml
Type: System.Nullable`1[System.Int32]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BySuite
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SuiteId

Identifiant de la suite de tests. La suite doit appartenir au plan; sinon une erreur ObjectNotFound est écrite pour cette entrée. Si PlanId est aussi omis, la valeur par défaut est defaultTestSuiteId du profil connecté; sinon il est obligatoire.

```yaml
Type: System.Nullable`1[System.Int32]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BySuite
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Suite

Suite de tests obtenue avec Get-AdoTestSuite, liée uniquement par valeur depuis le pipeline. Son Id est l’identifiant de la suite, et ses propriétés PlanId et TeamProject choisissent le plan et le projet; son Id n’est jamais utilisé comme identifiant de cas de test.

```yaml
Type: AdoToolkit.Core.TestManagement.AdoTestSuite
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BySuiteObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -WiqlResult

Résultat plat d’Invoke-AdoWiql, lié uniquement par valeur depuis le pipeline. Ses Ids sont récupérés dans l’ordre de la requête.

```yaml
Type: AdoToolkit.Core.WorkItems.AdoWiqlResult
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByWiql
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -WorkItem

Élément de travail, par exemple obtenu avec Invoke-AdoWiql -Hydrate, lié uniquement par valeur depuis le pipeline. Son Id est récupéré comme cas de test.

```yaml
Type: AdoToolkit.Core.WorkItems.AdoWorkItem
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByWorkItem
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Recurse

Inclut les suites enfants en profondeur, dans l’ordre du serveur. Sans ce paramètre, seule la suite indiquée est lue.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BySuite
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: BySuiteObject
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

Projet du plan. Par défaut, le projet par défaut de la connexion. Les suites du pipeline utilisent leur propre TeamProject.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BySuite
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -MaximumSharedStepDepth

Profondeur maximale d’imbrication des étapes partagées. Remplace testCases.maximumSharedStepDepth.

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

### -MaximumExpandedSteps

Nombre maximal de lignes développées par cas. Remplace testCases.maximumExpandedSteps.

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

### -Strict

Remplace chaque objet partiel par une erreur non bloquante. Les diagnostics figurent dans la cible de l’erreur et dans Exception.Data['Diagnostics'].

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

### -Connection

Connexion explicite; remplace la connexion active de l’espace d’exécution.

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

Paramètre de provenance masqué, lié aux objets ayant une propriété Id. Une autre collection produit ConnectionMismatch avant toute requête pour cette entrée. Les suites, résultats WIQL et éléments de travail typés sont vérifiés avec leur propre CollectionUri.

```yaml
Type: System.Uri
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ById
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: true
AcceptedValues: []
HelpMessage: ''
```

### -SuitePath

Garde masquée liée par nom de propriété. Lorsqu’un objet ayant un Id possède aussi SuitePath, il a la forme d’une suite et il est refusé au lieu d’être lu comme identifiant de cas de test.

```yaml
Type: System.Object
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ById
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: true
AcceptedValues: []
HelpMessage: ''
```

### -RootSuiteId

Garde masquée liée par nom de propriété. Lorsqu’un objet ayant un Id possède aussi RootSuiteId, il a la forme d’un plan de test et il est refusé au lieu d’être lu comme identifiant de cas de test.

```yaml
Type: System.Object
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ById
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

Prend en charge les paramètres communs, notamment ErrorAction, ErrorVariable, WarningVariable, Verbose et Debug.

## INPUTS

### System.Int32

### AdoToolkit.Core.TestManagement.AdoTestSuite

### AdoToolkit.Core.WorkItems.AdoWiqlResult

### AdoToolkit.Core.WorkItems.AdoWorkItem

### AdoToolkit.Core.TestManagement.AdoTestCase

## OUTPUTS

### AdoToolkit.Core.TestManagement.AdoTestCase

Métadonnées du cas de test, lignes développées, paramètres, références d’étapes partagées, diagnostics et Suite lorsque le cas est récupéré par une suite.

## NOTES

Les identifiants sont propres à la collection. Les recherches de catégories utilisent le projet de chaque élément. Les limites par défaut proviennent de la configuration : profondeur de 10 et 5 000 lignes. Le budget de résolution par défaut est de 10 000 éléments de travail supplémentaires par invocation. La taille est vérifiée après chaque nœud et son développement récursif; la ligne qui dépasse la limite est conservée, suivie d’un seul marqueur Truncated. Aucune ligne ultérieure n’est produite. Les listes de plans et de suites sont lues une fois par projet et par plan pour chaque invocation. Les routes testplan et l’ordre des suites restent provisoires en attendant V-04; les formats des paramètres partagés restent provisoires en attendant V-03.

## RELATED LINKS

[Get-AdoTestSuite](Get-AdoTestSuite.md)

[Invoke-AdoWiql](Invoke-AdoWiql.md)

[Export-AdoTestCase](Export-AdoTestCase.md)
