---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Export-AdoTestCase
---

# Export-AdoTestCase

## SYNOPSIS

Exporte un ou plusieurs cas de test dans un seul rapport HTML, Markdown ou JSON.

## SYNTAX

### Input (Default)

```
Export-AdoTestCase [-InputObject] <AdoTestCase[]> [-Format <ReportFormat>] [-Culture <string>] [-Path <string>] [-NoClobber] [-IncludeSource] [-Open] [-Connection <AdoConnection>] [-WhatIf] [-Confirm]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Collecte les objets du pipeline et écrit exactement un document, quel que soit le nombre de cas de test reçus. Un seul cas utilise la mise en page d’un cas. Plusieurs cas ajoutent un en-tête de couverture (serveur, collection, projet, plan et suite ou source WIQL, date de génération et totaux par état) et une table des matières avec liens, regroupée par chemin de suite; chaque cas conserve son propre en-tête, son ancre tc-<id> (tc-<id>-<k> pour un cas répété) et sa numérotation des étapes. Le contenu des cas est rendu un cas à la fois dans le fichier temporaire. Les libellés et diagnostics suivent la culture du rapport ; le contenu ADO reste inchangé. Le rapport HTML autonome contient des styles intégrés et aucun script. L’écriture utilise un fichier temporaire voisin, une validation du nombre de cas et d’étapes, et un remplacement atomique. Si aucun cas n’est reçu, un avertissement est écrit et aucun fichier n’est créé.

## EXAMPLES

### Example 1

```powershell
Get-AdoTestCase -Id 101 | Export-AdoTestCase -Culture fr-CA -Path .\report.html
```

Exporte un rapport HTML en français dans le dossier courant.

### Example 2

```powershell
Get-AdoTestSuite -PlanId 812 -SuiteId 813 -Recurse | Get-AdoTestCase | Export-AdoTestCase -Culture fr-CA -Path .\Release-12.html -Open
```

Exporte tous les cas d’une arborescence de suites dans un seul document en français et l’ouvre.

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

### -Open

Ouvre le rapport validé et enregistré dans l’application par défaut.

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

Connexion explicite, sinon connexion active de l’espace d’exécution. CollectionUri de l’entrée doit correspondre. L’exportation n’envoie aucune requête au serveur.

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

Demande confirmation avant l’écriture du rapport.

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

Prend en charge les paramètres communs, notamment ErrorAction, WarningVariable, Verbose et Debug.

## INPUTS

### AdoToolkit.Core.TestManagement.AdoTestCase

## OUTPUTS

### System.IO.FileInfo

Le fichier validé et enregistré. Aucun objet avec WhatIf.

## NOTES

IncludeSource est réservé au JSON. Les documents JSON de plusieurs cas ajoutent les totaux et une source TestSuite, Query ou Mixed; la version 1 du schéma reste inchangée.

## RELATED LINKS

[Get-AdoTestCase](Get-AdoTestCase.md)

