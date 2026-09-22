---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-21-2026
PlatyPS schema version: 2024-05-01
title: Export-AdoBuildTestFailure
---

# Export-AdoBuildTestFailure

## SYNOPSIS

Écrit un rapport HTML compact des tests en échec par ensemble et télécharge les pièces jointes JSON et texte de son exécution de tests la plus récente.

## SYNTAX

### Input (Default)

```
Export-AdoBuildTestFailure [-InputObject] <AdoBuildTestFailureSet> [-Culture <string>] [-Path <string>] [-SkipAttachments] [-AllRunAttachments] [-AttachmentWindowDays <int>] [-IncludeFlaky] [-NoClobber] [-Open] [-Connection <AdoConnection>] [-WhatIf] [-Confirm]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Produit, pour chaque `AdoBuildTestFailureSet` reçu de `Get-AdoBuildTestFailure`, un
rapport HTML sombre dans la culture du rapport. Le rapport compte quatre vues, et une
cinquième pour les diagnostics lorsqu’il y en a. Vue d’ensemble est un tableau d’une ligne
par test en échec : son numéro de cas de test lié à l’élément de travail, l’état de chaque
phase ou travail qui l’a exécuté et la première ligne de sa dernière erreur. Un test qui a
au moins un bogue ouvert porte la mention Bogue ouvert après son nom. Par erreur regroupe
les mêmes lignes sous leur dernière erreur. Détails présente une fiche par test, qui liste
ses bogues avec leur ID lié à l’élément de travail, leur titre et leur état, et la mention
Ouvert pour ceux qui sont ouverts. Un bogue est ouvert sauf si son état appartient à la
catégorie d’états Completed ou Removed. Exécutions et historique liste les séries de tests
du build, le graphique de l’historique et les détails du rapport. Chaque groupe, tentative
et aperçu de pièce jointe est d’abord réduit, et un message d’erreur ou une arborescence
des appels répétés d’une tentative antérieure du même test sont référencés au lieu d’être
répétés. Toutes les dates et heures sont affichées dans le fuseau horaire de l’ordinateur
qui exporte le rapport, comme l’heure de génération du rapport.

Lorsque les séries de tests du build portent des noms de phase ou de travail différents,
par exemple une phase par langue, chaque fiche regroupe ses tentatives selon ces noms et la
vue d’ensemble affiche une colonne d’état par groupe. L’étiquette utilise le nom le plus
court qui distingue les groupes : le nom de la phase, puis celui du travail, puis celui de
l’instance du travail. Les séries sans noms distincts gardent une seule liste.

Les tests instables sont omis sauf avec `-IncludeFlaky`; l’en-tête les compte quand même.
Le rapport est complet lorsque les scripts sont bloqués. Un petit script statique, autorisé
par une stratégie de sécurité du contenu fondée sur des hachages, ajoute le changement de
vue, la recherche, la navigation au clavier et la copie. La recherche compare chaque mot
saisi à toute la fiche, tentatives réduites comprises : messages d’erreur, arborescences
des appels, pièces jointes JSON et texte affichées, noms de phase et de travail, champs
des exécutions et des tentatives, et titres et états des bogues. Un ID de cas de test
correspond avec ou sans `#`. Lorsqu’au moins un test a un bogue ouvert, le filtre Sans
bogue ouvert n’affiche que les tests qu’aucun bogue ouvert ne suit encore.

Les pièces jointes n’apparaissent que pour les séries de tests commencées dans les
`-AttachmentWindowDays` jours précédant l’exportation, 7 par défaut; une série sans date
de début est considérée hors de la fenêtre. Les séries plus anciennes conservent toutes
leurs tentatives, mais leurs pièces jointes sont omises. Seules les pièces jointes JSON
(`.json`) et texte (`.txt`, `.log`) sont téléchargées. Les pièces jointes PNG, HTML et
autres restent des liens vers leur résultat Azure DevOps, quels que soient les paramètres.
Par défaut, seule l’exécution de tests la plus récente est téléchargée, et seulement si elle
est dans la fenêtre. Cette exécution est la dernière dans l’ordre des tentatives : étape,
phase et travail, puis date de début et ID d’exécution. Si elle n’a aucune pièce jointe JSON
ou texte, l’exportation ne télécharge rien d’une exécution précédente et ne crée aucun
dossier de pièces jointes.

Utilisez `-AllRunAttachments` pour télécharger les pièces jointes JSON et texte de toutes
les exécutions de la fenêtre, ou `-SkipAttachments` pour n’en télécharger aucune.
`-SkipAttachments` a priorité si les deux paramètres sont fournis. Les pièces jointes
sélectionnées sont téléchargées dans l’ordre du rapport, dans un dossier nommé
`<nom de base du rapport>.files-<horodatage UTC>` à côté du rapport. Les fichiers locaux
utilisent les noms `r<exécution>-<résultat>[-s<sous-résultat>]-a<pièce jointe>.<ext>`; les
fichiers `.log` sont enregistrés en `.txt`. Les noms distants sont affichés et ne
deviennent jamais des chemins.

Le contenu téléchargé est vérifié avant l’aperçu : un JSON doit être valide, et un texte
doit être en UTF-8 ou en UTF-16 avec marque d’ordre des octets. Un contenu non conforme est
enregistré en `.bin` et lié sans aperçu. Un aperçu n’est affiché, et donc cherchable, que
pour les fichiers d’au plus `maximumInlineJsonBytes` et tant que le total affiché du
rapport reste sous `maximumInlineTotalBytes`; les fichiers plus volumineux sont seulement
liés. Les limites de taille (`maximumAttachmentBytes`, `maximumTotalAttachmentBytes`) et
les téléchargements en échec produisent des avertissements; les pièces jointes concernées
conservent leur nom Azure DevOps, leur taille et le lien vers le résultat. Les erreurs
d’authentification ou d’autorisation et l’annulation interrompent l’exportation. Une build
d’historique illisible ne l’interrompt pas.

L’enregistrement suit cet ordre : téléchargement dans un dossier temporaire,
rendu et validation d’un rapport temporaire, renommage du dossier, puis remplacement
du rapport. Un échec avant le remplacement laisse le rapport précédent et son
dossier inchangés. Les anciens dossiers de génération du même rapport sont supprimés
après le remplacement; un échec du nettoyage produit un avertissement et le nouveau
rapport reste enregistré. Les points d’analyse sont ignorés et ne sont jamais suivis.

## EXAMPLES

### Exemple 1

```powershell
Get-AdoBuild -Definition 'Main Build' -Branch main -Latest -Result Failed |
    Get-AdoBuildTestFailure -HistoryCount 15 |
    Export-AdoBuildTestFailure -Culture fr-CA -Path .\triage -Open
```

Écrit `.\triage\Build-<id>-TestFailures.html` en français avec son dossier de pièces
jointes, puis ouvre le rapport. Le dossier `triage` doit déjà exister.

### Exemple 2

```powershell
Get-AdoBuildTestFailure -BuildId 401 | Export-AdoBuildTestFailure -Path .\build-401.html -SkipAttachments -WhatIf
```

Récupère l’ensemble des tests en échec, puis nomme le rapport qui serait écrit.
L’exportation ne télécharge aucune pièce jointe et n’écrit aucun fichier;
`Get-AdoBuildTestFailure`, en amont, récupère toujours les données d’Azure DevOps.

### Exemple 3

```powershell
Get-AdoBuildTestFailure -BuildId 401 | Export-AdoBuildTestFailure -IncludeFlaky -AllRunAttachments -AttachmentWindowDays 14
```

Inclut les tests instables et télécharge les pièces jointes JSON et texte de toutes les
séries de tests commencées au cours des 14 derniers jours.
## PARAMETERS

### -InputObject

Ensemble de tests en échec produit par Get-AdoBuildTestFailure. Accepte l’entrée du pipeline; chaque ensemble produit un rapport.

```yaml
Type: AdoToolkit.Core.TestRuns.AdoBuildTestFailureSet
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Input
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Culture

Culture du rapport, par exemple en-US ou fr-CA. Par défaut, la culture de rapport configurée, puis celle de la session. Une culture non prise en charge revient à l’anglais avec un avertissement.

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

### -Path

Répertoire FileSystem, ou fichier .html lorsqu’un seul ensemble est reçu; chaque ensemble suivant produit alors une erreur propre à cette entrée. Un répertoire qui n’existe pas encore est créé au moment d’écrire le rapport, jamais avec -WhatIf; un chemin avec une autre extension de fichier est refusé, et un séparateur final désigne toujours un répertoire. Le nom par défaut est Build-<id>-TestFailures.html et le répertoire par défaut est le dossier connu Téléchargements. Les caractères génériques ne sont pas développés.

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

### -SkipAttachments

Ne télécharge rien et ne crée aucun dossier; les pièces jointes des exécutions de la fenêtre sont répertoriées avec leur nom, leur taille et un lien vers le résultat Azure DevOps. A priorité sur -AllRunAttachments.

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
### -AllRunAttachments

Télécharge les pièces jointes JSON et texte de toutes les exécutions de la fenêtre plutôt que celles de l’exécution la plus récente seulement. Les pièces jointes PNG et HTML ne sont jamais téléchargées. Les limites de taille par fichier et au total continuent de s’appliquer. Sans effet avec -SkipAttachments.

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
### -AttachmentWindowDays

Nombre de jours avant l’exportation, de 1 à 365, pendant lesquels une série de tests doit avoir commencé pour que ses pièces jointes apparaissent et soient téléchargées. Les séries plus anciennes conservent toutes leurs tentatives, mais perdent leurs pièces jointes; une série sans date de début est considérée hors de la fenêtre. Par défaut, 7.

```yaml
Type: System.Int32
DefaultValue: '7'
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
### -IncludeFlaky

Inclut les tests instables, qui ont échoué puis réussi dans chaque phase ou travail. Sans ce paramètre, ils sont omis du rapport et seulement comptés dans son en-tête.

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
### -NoClobber

Refuse un rapport existant avant tout téléchargement et ne remplace jamais un rapport créé entre-temps.

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

### -Open

Ouvre chaque rapport validé avec l’application par défaut.

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

Connexion explicite utilisée pour télécharger les pièces jointes; remplace la connexion active de l’espace d’exécution. Un ensemble d’une autre collection produit une erreur propre à cette entrée.

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

### -WhatIf

Nomme le rapport et le dossier de pièces jointes sans requête, téléchargement ni écriture.

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

### -Confirm

Demande une confirmation par rapport avant de télécharger les pièces jointes et d’écrire.

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

### CommonParameters

Prend en charge les paramètres communs, notamment ErrorAction, ErrorVariable, WarningVariable, Verbose et Debug.

## INPUTS

### AdoToolkit.Core.TestRuns.AdoBuildTestFailureSet

## OUTPUTS

### System.IO.FileInfo

Le rapport validé et enregistré. Lorsque des pièces jointes ont été téléchargées, sa propriété de note AttachmentDirectory contient le chemin du dossier. Aucun objet avec WhatIf.

## NOTES

Nécessite PowerShell 7.6 sur Windows et Azure DevOps Server 2020. Les pièces jointes téléchargées sont des données de travail et restent sur cet ordinateur. Les routes, la version et les champs des pièces jointes restent à confirmer sur le serveur (V-23), tout comme les noms de phase et de travail utilisés pour le regroupement (V-19), et le comportement des navigateurs avec des fichiers locaux reste à confirmer selon la stratégie du navigateur au travail (V-27).

## RELATED LINKS

[Get-AdoBuildTestFailure](Get-AdoBuildTestFailure.md)

[Get-AdoBuild](Get-AdoBuild.md)
