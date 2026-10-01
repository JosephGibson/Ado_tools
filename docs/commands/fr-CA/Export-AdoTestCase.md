---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 10-01-2026
PlatyPS schema version: 2024-05-01
title: Export-AdoTestCase
---

# Export-AdoTestCase

## SYNOPSIS

Exporte un ou plusieurs cas de test dans un seul rapport HTML, Markdown ou JSON.

## SYNTAX

### Input (Default)

```
Export-AdoTestCase [-InputObject] <AdoTestCase[]> [-Format <ReportFormat>] [-Culture <string>] [-Path <string>] [-NoClobber] [-IncludeSource] [-IncludeDetail] [-Open] [-Connection <AdoConnection>] [-WhatIf] [-Confirm]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Collecte les objets du pipeline et écrit exactement un document, quel que soit le nombre de cas de test reçus. Un seul cas utilise la mise en page d’un cas. Plusieurs cas ajoutent un en-tête de couverture (serveur, collection, projet, plan et suite ou source WIQL, date de génération et totaux par état) et une table des matières avec liens, regroupée par chemin de suite; chaque cas conserve son propre en-tête, son ancre tc-<id> (tc-<id>-<k> pour un cas répété) et sa numérotation des étapes. Le contenu des cas est rendu un cas à la fois dans le fichier temporaire. Les libellés et diagnostics suivent la culture du rapport ; le contenu ADO reste inchangé. Le rapport HTML est un seul fichier autonome au thème du rapport des tests en échec, sombre à l’écran et clair à l’impression : une barre supérieure avec des compteurs, des liens de sections, une recherche (étapes d’un cas, cas d’un document), les valeurs des paramètres de l’itération choisie à la place des @noms, des groupes d’étapes partagées et des cas réductibles, des boutons de copie et une navigation au clavier. Les étapes mises en forme conservent leurs listes, leurs tableaux et leur emphase en HTML; Markdown et JSON conservent le texte brut. Un seul script statique intégré s’exécute sous une stratégie de sécurité du contenu limitée à son empreinte, et tout le rapport reste lisible sans lui. Avec IncludeDetail, le rapport HTML montre aussi, pour chaque cas, sa description, ses balises, ses champs de création et d’automatisation, les éléments de travail liés, ses hyperliens, le nom de ses pièces jointes et ses points de test avec le dernier résultat dans chaque plan, suite et configuration. L’écriture utilise un fichier temporaire voisin, une validation du nombre de cas et d’étapes, et un remplacement atomique. Si aucun cas n’est reçu, un avertissement est écrit et aucun fichier n’est créé.

## EXAMPLES

### Exemple 1

```powershell
Get-AdoTestCase -Id 101 | Export-AdoTestCase -Culture fr-CA -Path .\report.html
```

Exporte un rapport HTML en français dans le dossier courant.

### Exemple 2

```powershell
Get-AdoTestSuite -PlanId 812 -SuiteId 813 -Recurse | Get-AdoTestCase | Export-AdoTestCase -Culture fr-CA -Path .\Release-12.html -Open
```

Exporte tous les cas d’une arborescence de suites dans un seul document en français et l’ouvre.

### Exemple 3

```powershell
Get-AdoTestCase -Id 101 | Export-AdoTestCase -IncludeDetail -Open
```

Exporte un cas avec sa description, ses liens et ses points de test, puis ouvre le rapport.

## PARAMETERS

### -InputObject

Cas de test à exporter. Accepte les objets du pipeline; tous les cas d’une même invocation vont dans le même document.

```yaml
Type: AdoToolkit.Core.TestManagement.AdoTestCase[]
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

### -Format

Html (par défaut), Markdown ou Json.

```yaml
Type: AdoToolkit.Core.Reporting.ReportFormat
DefaultValue: 'Html'
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

### -Culture

Culture du rapport : valeur explicite, configuration reporting.culture, puis culture d’interface de la session. Les langues non prises en charge utilisent l’anglais avec un avertissement.

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

Chemin littéral FileSystem d’un fichier ou dossier existant. Le dossier parent doit exister. Par défaut, utilise le dossier Téléchargements de Windows. Noms par défaut : TestCase-<id>-Steps.<extension> pour un cas, TestSuite-<suiteId>-Steps.<extension> lorsque tous les cas proviennent d’une même suite, sinon TestCases-<yyyyMMdd-HHmmss>.<extension> en heure locale.

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

### -NoClobber

Refuse de remplacer un rapport existant, y compris un fichier créé avant le déplacement atomique final.

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

### -IncludeSource

Inclut les champs source des étapes dans le JSON uniquement. Les autres formats provoquent une erreur bloquante InvalidArgument avant l’exportation.

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

### -IncludeDetail

Lit davantage d’information sur chaque cas reçu et la montre dans le rapport HTML : description, balises, auteur et date de création, nom, assembly et type du test automatisé, éléments de travail liés avec leur type, leur titre et leur état, hyperliens et noms des pièces jointes, et points de test (plan, suite, configuration, testeur, dernier résultat et série de tests). Ce sont les seules requêtes d’une exportation : les cas avec leurs relations et les éléments de travail liés par lots de 200, et une requête de points de test par projet et par tranche de 50 cas. Elles sont envoyées seulement pour un rapport qui sera écrit, donc pas avec WhatIf. Une recherche qui échoue devient un avertissement et un diagnostic dans le rapport, écrit sans cette partie; un échec d’authentification ou d’autorisation arrête l’exportation avant l’écriture de tout fichier. HTML seulement : un autre format provoque une erreur bloquante InvalidArgument avant l’exportation.

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

Ouvre le rapport validé et enregistré dans l’application par défaut. Seuls les fichiers .html, .htm, .md, .markdown, .json et .txt sont ouverts, car Windows exécute certains autres types de fichiers au lieu de les afficher; avec une autre extension, le rapport est tout de même écrit et un avertissement indique qu’il n’a pas été ouvert. Un rapport qui ne peut pas être ouvert produit un avertissement et est quand même retourné.

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

Connexion explicite, sinon connexion active de l’espace d’exécution. CollectionUri de l’entrée doit correspondre. L’exportation n’envoie aucune requête au serveur, sauf avec IncludeDetail.

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

Affiche le chemin cible sans écrire ni ouvrir de fichier.

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

Demande confirmation avant l’écriture du rapport.

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

Prend en charge les paramètres communs, notamment ErrorAction, WarningVariable, Verbose et Debug.

## INPUTS

### AdoToolkit.Core.TestManagement.AdoTestCase

## OUTPUTS

### System.IO.FileInfo

Le fichier validé et enregistré. Aucun objet avec WhatIf.

## NOTES

IncludeSource est réservé au JSON et IncludeDetail au HTML. Les documents JSON de plusieurs cas ajoutent les totaux et une source TestSuite, Query ou Mixed; la version 1 du schéma reste inchangée. Les noms de champs et les attributs de relations lus par IncludeDetail sont provisoires en attendant V-31, et la requête de points de test en attendant V-32.

## RELATED LINKS

[Get-AdoTestCase](Get-AdoTestCase.md)

