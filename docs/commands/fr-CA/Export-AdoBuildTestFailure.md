---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-16-2026
PlatyPS schema version: 2024-05-01
title: Export-AdoBuildTestFailure
---

# Export-AdoBuildTestFailure

## SYNOPSIS

Écrit un rapport HTML interactif des tests en échec par ensemble et télécharge ses pièces jointes.

## SYNTAX

### Input (Default)

```
Export-AdoBuildTestFailure [-InputObject] <AdoBuildTestFailureSet> [-Culture <string>] [-Path <string>] [-SkipAttachments] [-NoClobber] [-Open] [-Connection <AdoConnection>] [-WhatIf] [-Confirm]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Produit, pour chaque AdoBuildTestFailureSet reçu de Get-AdoBuildTestFailure, un rapport HTML sombre dans la culture du rapport : le graphique et le tableau de l’historique des exécutions, un index, une fiche par test en échec ou instable avec chaque tentative, les messages et les arborescences des appels de procédure mis en évidence, les liens vers les cas de test et les diagnostics. Le rapport est complet lorsque les scripts sont bloqués; un petit script statique, autorisé uniquement par une stratégie de sécurité du contenu fondée sur des hachages, ajoute le filtrage, la navigation au clavier et la copie. Sauf avec SkipAttachments, les pièces jointes des résultats de chaque tentative signalée sont téléchargées dans l’ordre du rapport, dans un dossier nommé <nom de base du rapport>.files-<horodatage UTC> à côté du rapport, avec uniquement des noms de fichiers générés par la boîte à outils, r<exécution>-<résultat>[-s<sous-résultat>]-a<pièce jointe>.<ext>; les noms distants sont affichés, jamais utilisés dans un chemin. Après vérification du contenu, les fichiers PNG deviennent des vignettes et les fichiers JSON sous la limite d’affichage sont montrés mis en évidence; un contenu non conforme est enregistré en .bin, lié, et jamais prévisualisé. Les pièces jointes HTML sont liées comme sortie du test et s’ouvrent hors de la stratégie du rapport. Les fichiers au-delà de maximumAttachmentBytes, le budget total maximumTotalAttachmentBytes et les téléchargements en échec produisent des avertissements; ces pièces jointes sont répertoriées avec leur nom Azure DevOps, leur taille et un lien vers le résultat. Le rapport et son dossier sont validés dans un ordre fixe : téléchargement dans un dossier temporaire, rendu et validation d’un rapport temporaire, renommage du dossier, puis remplacement du rapport; les anciens dossiers de génération du même rapport sont supprimés en dernier, les liens ne sont jamais suivis, et un échec à n’importe quelle étape laisse le rapport précédent et son dossier inchangés. Une build d’historique illisible n’interrompt pas l’exportation.

## EXAMPLES

### Exemple 1

```powershell
Get-AdoBuild -Definition 'Main Build' -Branch main -Latest -Result Failed |
    Get-AdoBuildTestFailure -HistoryCount 15 |
    Export-AdoBuildTestFailure -Culture fr-CA -Path .\triage -Open
```

Écrit .\triage\Build-<id>-TestFailures.html en français avec son dossier de pièces jointes, puis ouvre le rapport.

### Exemple 2

```powershell
Get-AdoBuildTestFailure -BuildId 401 | Export-AdoBuildTestFailure -Path .\build-401.html -SkipAttachments -WhatIf
```

Nomme le rapport qui serait écrit, sans requête ni fichier.

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

Répertoire FileSystem existant, ou fichier .html lorsqu’un seul ensemble est reçu; chaque ensemble suivant produit alors une erreur propre à cette entrée. Le nom par défaut est Build-<id>-TestFailures.html et le répertoire par défaut est le dossier connu Téléchargements. Les caractères génériques ne sont pas développés.

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

Ne télécharge rien et ne crée aucun dossier; les pièces jointes sont répertoriées avec leur nom et leur taille dans Azure DevOps.

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

Nécessite PowerShell 7.6 sur Windows et Azure DevOps Server 2020. Les pièces jointes téléchargées sont des données de travail et restent sur cet ordinateur. Les routes, la version et les champs des pièces jointes restent à confirmer sur le serveur (V-23), et le comportement des navigateurs avec des fichiers locaux reste à confirmer selon la stratégie du navigateur au travail (V-27).

## RELATED LINKS

[Get-AdoBuildTestFailure](Get-AdoBuildTestFailure.md)

[Get-AdoBuild](Get-AdoBuild.md)
