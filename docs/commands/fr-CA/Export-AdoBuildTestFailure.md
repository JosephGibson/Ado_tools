---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 10-04-2026
PlatyPS schema version: 2024-05-01
title: Export-AdoBuildTestFailure
---

# Export-AdoBuildTestFailure

## SYNOPSIS

Écrit un rapport HTML compact des tests en échec par ensemble et télécharge ses pièces jointes JSON et texte : les petites de chaque série de tests récente et les plus volumineuses de la série la plus récente.

## SYNTAX

### Input (Default)

```
Export-AdoBuildTestFailure [-InputObject] <AdoBuildTestFailureSet> [-Culture <string>] [-Path <string>] [-SkipAttachments] [-AllRunAttachments] [-AttachmentWindowDays <int>] [-IncludeFlaky] [-NoClobber] [-Open] [-Connection <AdoConnection>] [-WhatIf] [-Confirm]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Produit, pour chaque `AdoBuildTestFailureSet` reçu de `Get-AdoBuildTestFailure`, un rapport
HTML sombre dans la culture du rapport. Le rapport compte cinq vues, dans cet ordre, et une
sixième pour les diagnostics lorsqu’il y en a. Vue d’ensemble est un tableau d’une ligne par
test en échec, chaque partie dans sa propre colonne, soit son état et son numéro, son numéro de
cas de test lié à l’élément de travail, son nom et sa classe, sa tendance, son bogue ouvert de
plus petit numéro sous forme de pastille rouge avec le nombre des autres, dans un rouge plus
pâle à l’écran avec ✦ lorsque ce bogue a été ouvert après la mise en file du build, l’état de
chaque groupe de tentatives qui l’a exécuté et la première ligne de sa dernière erreur. Dans la
tendance, Nouveau indique que le build précédent a exécuté le test sans échec, et Depuis le
suivi d’une date que le test a aussi échoué ou été instable dans le build précédent; la date
est le jour où le premier build de cette suite d’échecs s’est terminé, la pastille mène aux
résultats de tests de ce build et son titre indique le nombre de builds consécutifs. Rien n’est
indiqué lorsque le build précédent n’a pas pu être lu ou n’a pas exécuté le test. Sous le
tableau, des encadrés résument le build, soit les tests nouveaux et récurrents, les erreurs les
plus fréquentes, les tests sans bogue ouvert et, lorsque les tentatives sont regroupées, chaque
groupe. Séries et historique présente l’historique des exécutions sous forme de graphique et de
tableau des builds, puis liste les séries de tests du build avec leur ID, les numéros de
tentative qu’elles ont, de la phase, du travail ou de l’instance du travail, leur durée, leurs
décomptes de tests, les tests signalés et les pièces jointes répertoriées et téléchargées, et
signale la dernière série de tests; un tableau des tests signalés la termine, avec leur
résultat dans chaque build et leur tendance. Détails présente une fiche par test avec son
historique des exécutions, une pastille par build avec le jour où il s’est terminé, distinguée
par son glyphe et sa forme autant que par sa couleur, et la même tendance, ses bogues ouverts,
chacun sous forme de pastille liée à l’élément de travail avec son titre et son état, puis,
pour un bogue lu en entier, le jour où il a été ouvert, Nouveau lorsqu’il l’a été après la mise
en file du build, et la personne à qui il est assigné ou Non assigné, et ses pièces jointes,
chaque nom de fichier une seule fois, tiré de la dernière tentative qui l’a. Le champ En échec
depuis d’une tentative nomme ce build par son numéro lorsqu’il figure dans l’historique. Par
erreur regroupe les mêmes lignes sous leur dernière erreur, sans tenir compte des URL, des
GUID, des chemins, des ID hexadécimaux ni des nombres; chaque groupe nomme son type d’exception
et marque les parties de sa ligne qui diffèrent d’un test à l’autre, et un groupe de deux tests
ou plus indique ce que ses tests ont en commun et présente un exemple du premier message.
Bogues ouverts s’ouvre sur une ligne qui compte les bogues qu’elle liste et combien d’entre eux
ont été ouverts après la mise en file du build, le second décompte étant omis lorsque le build
n’a pas d’heure de mise en file, puis liste chaque bogue une fois, sur une ligne avec le jour
où il a été ouvert, la personne à qui il est assigné et ce qu’il couvre, puis les tests qui lui
sont liés, et indique si chaque lien vient d’un résultat de test, du cas de test ou des deux,
et combien de résultats en échec atteint un lien par les résultats de test lorsqu’il ne les
atteint pas tous; les tests du même cas de test sans le bogue suivent, et les tests sans bogue
ouvert viennent en dernier. Le bogue qui a le plus de tests vient en premier, et un bogue qui
n’a pas pu être lu vient en dernier, sous forme de pastille grise avec la mention Non lu. Un
bogue est ouvert sauf si son état appartient à la catégorie d’états Completed ou Removed; un
bogue fermé n’est pas affiché, et un bogue qui n’a pas pu être lu n’est pas compté comme
ouvert. Le jour où un bogue a été ouvert, la mention Nouveau et la personne assignée ne sont
affichés que pour un bogue lu en entier; un bogue qui n’a pas pu être lu indique seulement Non
lu. Nouveau signifie ouvert à la mise en file du build ou après elle, le seul seuil que les
données permettent : il n’y a pas de borne supérieure, si bien qu’un rapport régénéré pour un
build plus ancien peut marquer un bogue ouvert pour un build ultérieur, et le jour affiché à
côté de la mention le montre. La date et l’origine du rapport figurent sous chaque vue. Chaque
groupe, tentative et aperçu de pièce jointe est d’abord réduit, et chaque tentative contient
son propre message d’erreur complet et sa propre arborescence des appels, même lorsqu’une
tentative antérieure du même test avait le même texte. Toutes les dates et heures sont
affichées dans le fuseau horaire de l’ordinateur qui exporte le rapport, comme l’heure de
génération du rapport.

Lorsque les séries de tests du build portent des noms de phase, de travail ou de série
différents, par exemple une phase par langue, chaque fiche regroupe ses tentatives selon ces
noms et la vue d’ensemble affiche une colonne d’état par groupe. L’étiquette utilise le nom
le plus court qui distingue les groupes : le nom de la phase, puis celui du travail, puis
celui de l’instance du travail, puis celui de la série. Une série nommée comme une autre
série du même travail suivie de ` (attempt N)`, où N est sa tentative de travail, est une
nouvelle tentative de cette série et reste dans son groupe. Les séries sans noms distincts
gardent une seule liste.

Les tests instables sont omis sauf avec `-IncludeFlaky`; l’en-tête les compte quand même.
Le rapport est complet lorsque les scripts sont bloqués. Un petit script statique, autorisé
par une stratégie de sécurité du contenu fondée sur des hachages, ajoute le changement de
vue, la recherche, la navigation au clavier et la copie, et fait des décomptes du haut du
rapport des raccourcis vers leurs filtres. Il ouvre la tentative de la dernière erreur d’un
test lorsque le test est atteint depuis sa ligne ou avec j ou k, ouvre une arborescence des
appels qui contient un appel du code du test en masquant les appels du framework, et ouvre les
messages d’erreur avec retour automatique à la ligne; la copie et l’impression conservent
tous les appels. La recherche compare chaque mot saisi, sans tenir compte de la casse ni des
accents, à toute la fiche, tentatives réduites comprises : messages d’erreur, arborescences
des appels, pièces jointes JSON et texte affichées, noms de phase et de travail, champs
des séries de tests et des tentatives, jours de l’historique des exécutions, et titres, états,
jours d’ouverture et personnes assignées des bogues. Un ID de cas de test
correspond avec ou sans `#`. Lorsqu’au moins un test a un bogue ouvert, le filtre Sans
bogue ouvert n’affiche que les tests qu’aucun bogue ouvert ne suit encore.

Les pièces jointes n’apparaissent que pour les séries de tests commencées dans les
`-AttachmentWindowDays` jours précédant l’exportation, 7 par défaut; une série sans date
de début est considérée hors de la fenêtre. Les séries plus anciennes conservent toutes
leurs tentatives, mais leurs pièces jointes sont omises. Le nom de chaque pièce jointe est
un lien vers la pièce jointe dans Azure DevOps, que le navigateur télécharge avec votre
connexion Windows. Seules les pièces jointes JSON (`.json`) et texte (`.txt`, `.log`) sont
téléchargées par l’exportation; les pièces jointes PNG, HTML et autres restent des liens,
quels que soient les paramètres. Par défaut, les fichiers JSON et texte d’au plus
`maximumInlineJsonBytes` (256 Kio) sont téléchargés de chaque série de tests de la fenêtre,
et les plus volumineux seulement de la série la plus récente. Cette série est la dernière
dans l’ordre des tentatives de phase, de travail et d’instance du travail, puis de la date
de début et de l’ID de série. Un fichier plus volumineux d’une série plus ancienne reste un
lien : l’exportation ne se rabat pas sur une série précédente pour ceux-là. Un fichier
d’une série plus ancienne qui ne déclare aucune taille est téléchargé et conservé seulement
s’il fait au plus `maximumInlineJsonBytes`. Lorsqu’aucune série n’a de fichier à
télécharger, aucun dossier de pièces jointes n’est créé et aucune connexion n’est requise.
Un ensemble obtenu avec `Get-AdoBuildTestFailure -SkipAttachments` ne répertorie aucune
pièce jointe : son exportation ne télécharge rien et n’a besoin d’aucune connexion, quels
que soient les paramètres, et son rapport indique que les pièces jointes n’ont pas été
répertoriées, dans l’en-tête et dans Séries et historique, au lieu de les compter.

Utilisez `-AllRunAttachments` pour télécharger aussi les fichiers JSON et texte plus
volumineux de toutes les séries de tests de la fenêtre, ou `-SkipAttachments` pour n’en
télécharger aucun. `-SkipAttachments` a priorité si les deux paramètres sont fournis. Les
pièces jointes sélectionnées sont téléchargées dans un dossier nommé
`<nom de base du rapport>.files-<horodatage UTC>` à côté du rapport, la série la plus
récente d’abord, puis les séries plus anciennes, chacune dans l’ordre du rapport, afin que
la limite de taille totale ne soit jamais épuisée d’abord par les séries plus anciennes.
Jusqu’à `testResults.maximumConcurrentRequests` fichiers, 6 par défaut, sont lus en même
temps; le sort de chaque fichier reste décidé dans cet ordre, de sorte que les limites, les
avertissements et les fichiers sont les mêmes que lorsqu’un seul fichier est lu à la fois.
Les fichiers locaux utilisent les noms
`r<série>-<résultat>[-s<sous-résultat>]-a<pièce jointe>.<ext>`; les fichiers `.log` sont
enregistrés en `.txt`. Les noms distants sont affichés et ne deviennent jamais des chemins.

Le contenu téléchargé est vérifié avant l’aperçu : un JSON doit être valide, et un texte
doit être en UTF-8 ou en UTF-16 avec marque d’ordre des octets. Un contenu non conforme est
enregistré en `.bin` et lié sans aperçu. Un aperçu n’est affiché, et donc cherchable, que
pour les fichiers d’au plus `maximumInlineJsonBytes` et tant que le total affiché du
rapport reste sous `maximumInlineTotalBytes`; les fichiers plus volumineux sont seulement
liés. Les aperçus de la série la plus récente sont choisis d’abord, puis ceux des séries
plus anciennes. Les limites de taille (`maximumAttachmentBytes`,
`maximumTotalAttachmentBytes`) et les téléchargements en échec produisent des
avertissements; les pièces jointes concernées
conservent leur nom Azure DevOps, leur taille et le lien de téléchargement. Les erreurs
d’authentification ou d’autorisation et l’annulation interrompent l’exportation.

L’enregistrement suit cet ordre : téléchargement dans un dossier temporaire,
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
jointes, puis ouvre le rapport. Le dossier `triage` est créé s’il n’existe pas.

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

Ne télécharge rien et ne crée aucun dossier; les pièces jointes des séries de tests de la fenêtre sont répertoriées avec leur nom, leur taille et un lien de téléchargement. A priorité sur -AllRunAttachments.

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

Télécharge les pièces jointes JSON et texte de toute taille de chaque série de tests de la fenêtre. Sans ce paramètre, les séries autres que la plus récente ne fournissent que leurs fichiers d’au plus maximumInlineJsonBytes. Les pièces jointes PNG et HTML ne sont jamais téléchargées. Les limites de taille par fichier et au total continuent de s’appliquer. Sans effet avec -SkipAttachments.

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

Inclut les tests instables, qui ont échoué puis réussi dans chaque phase, travail ou série de tests nommée. Sans ce paramètre, ils sont omis du rapport et seulement comptés dans son en-tête.

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

Ouvre chaque rapport validé avec l’application par défaut. Un rapport qui ne peut pas être ouvert produit un avertissement et est quand même retourné.

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

Connexion explicite utilisée pour télécharger les pièces jointes; remplace la connexion active de l’espace d’exécution. Lorsque des pièces jointes sont téléchargées, un ensemble d’une autre collection produit une erreur propre à cette entrée.

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
Aliases:
- wi
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
Aliases:
- cf
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

Le rapport validé et enregistré. Lorsque des pièces jointes ont été téléchargées, sa propriété de note AttachmentDirectory contient le chemin du dossier. Aucun objet avec WhatIf. L’adresse file:/// du rapport est aussi écrite dans le flux d’information avec la balise PSHOST, de sorte qu’elle s’affiche dans la console comme une sortie de Write-Host; -InformationAction Ignore la masque. Rien n’est écrit avec WhatIf ni lorsque l’exportation échoue.

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020. Les pièces jointes téléchargées sont des données de travail et restent sur cet ordinateur. La façon dont Server 2020 répond à plusieurs téléchargements simultanés n’a pas été confirmée au travail (V-33). Avec -Verbose, chaque étape de l’exportation écrit une ligne indiquant sa durée en millisecondes : les téléchargements des pièces jointes, avec les fichiers écrits et les requêtes envoyées; le rapport produit, avec sa taille en octets; la vérification du rapport; et sa mise en place, qui supprime aussi les dossiers de pièces jointes antérieurs. Lorsque rien n’est téléchargé, la ligne des téléchargements reste à sa place avec 0 fichier et 0 requête. Une dernière ligne indique le build, les pièces jointes téléchargées, les requêtes et la durée écoulée. Les routes, la version et les champs des pièces jointes restent à confirmer sur le serveur (V-23), tout comme les noms de phase, de travail et de série utilisés pour le regroupement et le suffixe de nouvelle tentative des noms de série (V-19), et le comportement des navigateurs avec des fichiers locaux reste à confirmer selon la stratégie du navigateur au travail (V-27). Le fait que Server 2020 renvoie la date de création et la personne assignée d’un bogue dans une projection de champs d’un lot d’éléments de travail n’a pas été observé au travail (V-37); sans eux, la ligne d’un bogue n’affiche ni jour, ni marque ✦, ni personne assignée, sans avertissement.

## RELATED LINKS

[Get-AdoBuildTestFailure](Get-AdoBuildTestFailure.md)

[Get-AdoBuild](Get-AdoBuild.md)
