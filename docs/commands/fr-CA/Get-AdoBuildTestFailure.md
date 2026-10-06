---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 10-06-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoBuildTestFailure
---

# Get-AdoBuildTestFailure

## SYNOPSIS

Rassemble les tests en échec et instables d’une exécution de pipeline.

## SYNTAX

### ByDefinition (Default)

```
Get-AdoBuildTestFailure [-Definition <Object>] [-Branch <string>] [-Result <BuildResult>] [-HistoryCount <int>]
    [-HistoryScope <AdoTestHistoryScope>] [-SkipAttachments] [-Project <string>] [-Connection <AdoConnection>]
```

### ByBuildId

```
Get-AdoBuildTestFailure [-BuildId] <int> [-HistoryCount <int>] [-HistoryScope <AdoTestHistoryScope>]
    [-SkipAttachments] [-Project <string>] [-Connection <AdoConnection>]
```

### ByBuild

```
Get-AdoBuildTestFailure -InputObject <AdoBuild> [-HistoryCount <int>] [-HistoryScope <AdoTestHistoryScope>]
    [-SkipAttachments] [-Connection <AdoConnection>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Émet un objet AdoBuildTestFailureSet par build reçu. La récupération se fait en deux passes. La première liste tous les résultats de tests de toutes les séries du build, regroupe les résultats en identités de tests d’après le stockage et le nom du test automatisé, puis classe chaque identité. La seconde lit les détails complets uniquement pour les identités ayant une tentative en échec, dans l’ordre du rapport, afin d’obtenir chaque tentative, message, arborescence des appels et liste de pièces jointes. Un test est signalé dès qu’une tentative a échoué. Les tentatives sont regroupées selon les noms de phase, de travail et d’instance du travail et le nom de leurs séries de tests; une série nommée comme une autre série du même travail suivie de « (attempt N) », où N est sa tentative de travail, est une nouvelle tentative de cette série et reste dans son groupe. Une identité est instable (Flaky) lorsque la dernière tentative de chaque groupe a réussi, sinon en échec (Failed), de sorte qu’un échec dans une phase ou une série nommée n’est jamais masqué par une réussite ultérieure dans une autre. Les séries sans noms distincts forment un seul groupe, où la dernière tentative décide. Toutes les tentatives sont conservées, y compris celles qui ont réussi. Les groupes de réexécution dans la tâche, les nouvelles tentatives de travail ou de phase et les résultats simples sont tous des sources de tentatives; les groupes pilotés par les données et les autres groupes qui ne sont pas des réexécutions sont imbriqués dans leur tentative. Les références aux cas de test sont résolues en un seul lot, qui liste aussi les liens de chaque cas de test. Chaque test signalé liste ensuite ses bogues ouverts dans Bugs : tout élément de travail ouvert associé à l’un de ses résultats de test, et tout élément de travail ouvert lié à son cas de test, quel que soit le type de lien, dont le type fait partie de la catégorie Bogue du projet (Microsoft.BugCategory). Leur titre, leur état, leur type, leur projet, leur date de création et la personne assignée sont lus par lots d’au plus 200, jamais par une requête par test. Un bogue est ouvert (IsOpen) sauf si son état appartient à la catégorie d’états Completed ou Removed, lue une fois par projet et type d’élément de travail; un bogue Resolved reste donc ouvert. Un bogue fermé est omis de Bugs, alors que la propriété AssociatedBugIds d’une tentative reste la liste du serveur. HasOpenBug est vrai lorsqu’au moins un bogue est ouvert. Si la catégorie Bogue ou les catégories d’états d’un projet ne peuvent pas être lues, seul le type nommé Bug compte et les états Closed, Done et Removed sont considérés comme fermés, avec un avertissement BugMetadataUnavailable. Un bogue illisible conserve le lien vers son ID avec un avertissement UnresolvedBug et, si la recherche des bogues échoue, les ID de bogues associés restent des liens avec un avertissement BugLookupFailed; les problèmes de recherche récupérables produisent des avertissements, tandis que l’authentification, l’autorisation et l’annulation arrêtent la récupération. L’historique des exécutions ajoute le build courant et des builds antérieurs de la même définition, avec des décomptes et une cellule par identité signalée; les problèmes d’historique récupérables produisent des diagnostics, tandis que l’authentification, l’autorisation et l’annulation arrêtent la récupération. Les tests qui ont seulement réussi ou qui n’ont pas été exécutés sont comptés dans le sommaire et l’historique, sans détail. Les métadonnées des pièces jointes sont lues ici; leur contenu n’est téléchargé que par Export-AdoBuildTestFailure. Chaque résultat détaillé coûte une requête de liste de pièces jointes, plus une par sous-résultat, comme une tentative de réexécution ou une ligne de données; avec -SkipAttachments, aucune n’est envoyée. Les requêtes d’une même étape (listes de résultats, détails, listes de pièces jointes, métadonnées des bogues) sont envoyées ensemble, au plus testResults.maximumConcurrentRequests à la fois, 6 par défaut et de 1 à 16, et l’historique des exécutions est lu pendant les requêtes principales. Les résultats et diagnostics sont réunis dans l’ordre d’entrée; avec 1, chaque requête attend la précédente. Les builds antérieurs sont lus du plus récent au plus ancien, un à la fois, pendant la lecture des pages de résultats des builds plus récents. La liste des séries d’un build compte dans testResults.maximumHistoryRequests, 500 par défaut, dès son envoi; ses pages de résultats sont ensuite calculées d’après le totalTests de ses séries et réservées d’un coup : un build dont les pages ne tiennent pas est indisponible, avec tous les builds plus anciens, avant qu’aucune de ses pages ne soit demandée, et les builds conservés ne dépendent pas de l’ordre des réponses. Un build dont une page de résultats échoue lit quand même ses autres pages et garde ce qu’il a réservé. Les pages d’une série sans totalTests ou non terminée, les nouvelles tentatives et les pages au-delà de totalTests prennent sur ce qui reste au moment de leur envoi; près de la limite, les nouvelles tentatives et ces pages en trop peuvent encore dépendre de l’ordre des réponses. Le statut est Partial lorsqu’un diagnostic d’erreur existe, par exemple lorsque le nombre d’identités en échec dépasse le maximum configuré. Avec -Definition, le dernier build terminé de la définition est choisi, comme avec Get-AdoBuild -Latest, éventuellement filtré par branche et par résultat. ByDefinition est le jeu de paramètres par défaut : sans BuildId, build reçu du pipeline ni Definition, la valeur defaultBuildDefinition du profil connecté est utilisée, et sans elle la commande échoue avec une erreur de configuration avant toute requête. Sans Branch, la valeur defaultBranch du profil connecté limite le choix.

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

### Exemple 4

```powershell
(Get-AdoBuildTestFailure -BuildId 401).Failures | Where-Object HasOpenBug -eq $false
```

Sélectionne les tests signalés qu’aucun bogue ouvert ne suit encore.

### Exemple 5

```powershell
Get-AdoBuildTestFailure -Result Failed | Export-AdoBuildTestFailure -Open
```

Produit le rapport du dernier build en échec de la définition de build et de la branche par défaut enregistrées dans le profil de connexion.

### Exemple 6

```powershell
Get-AdoBuildTestFailure -BuildId 401 -SkipAttachments | Export-AdoBuildTestFailure -Open
```

Rassemble les tests en échec du build 401 sans leurs listes de pièces jointes, puis écrit et ouvre un rapport qui ne télécharge rien et indique que les pièces jointes n’ont pas été répertoriées.

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

Identifiant entier positif ou nom exact de définition de build. Le dernier build terminé de cette définition est utilisé. Une chaîne numérique est un nom; utilisez un entier pour un identifiant. Par défaut, la valeur defaultBuildDefinition du profil connecté.

```yaml
Type: System.Object
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

### -Branch

Nom de branche ou chemin complet refs/ qui limite le choix du dernier build. Par exemple, main devient refs/heads/main. Par défaut, la valeur defaultBranch du profil connecté; sans elle, les builds de toutes les branches sont pris en compte.

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

Filtre facultatif sur le résultat du dernier build : None, Succeeded, PartiallySucceeded, Failed ou Canceled.

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

### -HistoryScope

Portée de branche de la fenêtre d’historique. SameBranch conserve la branche source courante; AllBranches l’élargit. Par défaut, la valeur de configuration testResults.historyScope, soit SameBranch.

```yaml
Type: System.Nullable`1[AdoToolkit.Core.TestRuns.AdoTestHistoryScope]
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

### -SkipAttachments

Ne répertorie aucune pièce jointe : aucune requête de liste de pièces jointes n’est envoyée, la liste Attachments de chaque tentative est vide et AttachmentsListed vaut False. Export-AdoBuildTestFailure ne télécharge alors rien, n’a besoin d’aucune connexion, et son rapport indique que les pièces jointes n’ont pas été répertoriées. Toutes les autres requêtes restent les mêmes.

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

### CommonParameters

Prend en charge les paramètres communs, dont ErrorAction, ErrorVariable, Verbose et Debug.

## INPUTS

### AdoToolkit.Core.Builds.AdoBuild

## OUTPUTS

### AdoToolkit.Core.TestRuns.AdoBuildTestFailureSet

Build, Runs, Summary, History, Failures, FailedCount, FlakyCount, Status, Diagnostics, RetrievedAt, CollectionUri et AttachmentsListed, qui vaut False lorsque -SkipAttachments a omis les listes de pièces jointes. Chaque AdoTestFailure a ses tentatives (Attempts), TestCase, Bugs et HasOpenBug, et s’affiche sous forme de tableau Ordinal, Classification, ShortName, Attempts, HasOpenBug et LatestError, la première ligne de sa dernière erreur telle que le rapport la montre. Chaque AdoTestBug a Id, Title, State, WorkItemType, TeamProject, StateCategory, IsOpen, IsResolved, CreatedDate, AssignedTo, IsAssociatedWithResult, IsLinkedToTestCase et WebUrl, et s’affiche sous forme de tableau Id, IsOpen, State, WorkItemType et Title. CreatedDate et AssignedTo ne figurent pas dans ce tableau, qui garde sa largeur pour Title; sélectionnez-les explicitement. IsOpen vaut True, ou reste vide pour un bogue qui n’a pas pu être lu; les bogues fermés ne sont pas listés. TeamProject est vide lorsque le bogue n’a pas pu être lu ou n’indique aucun projet utilisable. CreatedDate est le jour où le bogue a été ouvert, en UTC, et reste vide lorsque le bogue n’a pas pu être lu ou que le serveur n’a pas envoyé le champ. AssignedTo est un AdoIdentityRef et reste vide aussi bien lorsque personne n’est assigné que lorsque le bogue n’a pas pu être lu; IsOpen distingue les deux cas, car il ne vaut True que pour un bogue lu en entier. La propriété History d’un AdoTestFailure contient un AdoTestHistoryEntry par build de la fenêtre, avec BuildId, BuildNumber, Outcome, IsCurrent, WebUrl et ErrorMessages. Pour un build antérieur où le test a échoué, ErrorMessages contient les débuts distincts des messages d’erreur répertoriés pour les résultats en échec du test dans ce build, cinq au plus, chacun limité à ses 20 premières lignes, telles que la liste les a envoyées, et à 8192 caractères; Export-AdoBuildTestFailure les compare à l’erreur du test. ErrorMessages est vide pour le build courant, dont les tentatives portent leurs messages complets, pour un build où le test n’a pas échoué, pour les tests lus une fois que les débuts d’un build atteignent 2 millions de caractères, ce qui limite la taille de l’historique d’une invocation, et lorsque la liste ne porte aucun message. Le fait que Server 2020 répertorie le message n’a pas été confirmé au travail (V-39).

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020. Les résultats de tests de chaque build antérieur, les liens des cas de test ainsi que la catégorie Bogue et les catégories d’états de chaque projet sont mis en cache pour l’invocation, de sorte que plusieurs builds d’une même définition reçus du pipeline ne les lisent qu’une fois; un cas de test introuvable produit quand même son avertissement dans chaque ensemble qui y fait référence. La liste des résultats d’une série de tests se termine sans autre requête lorsque la série est terminée et que sa dernière page, plus courte qu’une page complète, porte les résultats lus au total de la série; sinon, elle se termine sur une page vide. Les requêtes simultanées n’ont été exercées que contre un serveur synthétique : le comportement de Server 2020 avec l’authentification Windows devant plusieurs requêtes à la fois n’a pas été observé au travail (V-33). Avec -Verbose, chaque étape de la récupération écrit une ligne indiquant ses requêtes et sa durée en millisecondes : le build lorsque la commande le lit, les séries de tests, les listes de résultats, les détails des échecs, les listes de pièces jointes, les liens des cas de test, les bogues et l’historique des exécutions. Avec -SkipAttachments, la ligne des listes de pièces jointes reste à sa place avec 0 requête. L’historique est lu en même temps que les autres étapes, si bien que sa durée chevauche la leur. Une dernière ligne indique le build, les tests en échec ou instables trouvés, toutes les requêtes envoyées et la durée écoulée. Toutes les routes, versions et champs de la zone de tests restent à confirmer sur le serveur (V-19 à V-25), y compris la façon dont les nouvelles tentatives sont enregistrées. Les liens Web restent à confirmer au travail (V-26). Les catégories d’états proviennent de la route des états des types d’éléments de travail en version 6.0-preview.1, que la documentation REST de Server 2020 mentionne, mais qui n’a pas encore été confirmée au travail (V-30). Le fait que Server 2020 renvoie System.CreatedDate et System.AssignedTo dans une projection de champs d’un lot d’éléments de travail, et la forme que prend l’identité, n’ont pas été observés au travail (V-37); les deux propriétés restent vides lorsque les champs ne reviennent pas, sans avertissement.

## RELATED LINKS

[Export-AdoBuildTestFailure](Export-AdoBuildTestFailure.md)

[Get-AdoTestRun](Get-AdoTestRun.md)

[Get-AdoBuild](Get-AdoBuild.md)
