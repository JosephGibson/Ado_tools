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

Produit, pour chaque `AdoBuildTestFailureSet` reçu de `Get-AdoBuildTestFailure`, un
rapport HTML sombre dans la culture du rapport : graphique et tableau de
l’historique, index, fiche par test en échec ou instable avec chaque tentative,
messages et arborescences des appels de procédure mis en évidence, liens vers les
cas de test et diagnostics. Le rapport est complet lorsque les scripts sont bloqués.
Un petit script statique, autorisé par une stratégie de sécurité du contenu fondée
sur des hachages, ajoute le filtrage, la navigation au clavier et la copie.

Sauf avec `-SkipAttachments`, les pièces jointes de chaque résultat signalé et de
ses sous-résultats sont téléchargées dans l’ordre du rapport, dans un dossier nommé
`<nom de base du rapport>.files-<horodatage UTC>` à côté du rapport. Les fichiers
locaux utilisent les noms `r<exécution>-<résultat>[-s<sous-résultat>]-a<pièce jointe>.<ext>`.
Les noms distants sont affichés et ne deviennent jamais des chemins.

Après vérification du contenu, les PNG deviennent des vignettes et les JSON sous
la limite d’affichage sont mis en évidence. Un contenu non conforme est enregistré
en `.bin` et lié sans aperçu. Les pièces jointes HTML sont liées comme sortie du
test et s’ouvrent hors de la stratégie du rapport. Les limites de taille
(`maximumAttachmentBytes`, `maximumTotalAttachmentBytes`) et les téléchargements
en échec produisent des avertissements; les pièces jointes concernées conservent
leur nom Azure DevOps, leur taille et le lien vers le résultat. Les erreurs
d’authentification ou d’autorisation et l’annulation interrompent l’exportation.
Une build d’historique illisible ne l’interrompt pas.

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
