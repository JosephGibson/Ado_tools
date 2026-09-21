---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Save-AdoBuildLog
---

# Save-AdoBuildLog

## SYNOPSIS

Enregistre un journal de build localement, octet pour octet.

## SYNTAX

### ByLogId (Default)

```
Save-AdoBuildLog [-BuildId] <int> [-LogId] <int> [-Tail <int>] [-Path <string>] [-Connection <AdoConnection>] [-Project <string>] [-CollectionUri <uri>] [-WhatIf] [-Confirm]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Lit la liste des journaux, puis copie les octets reçus sans réencodage dans un fichier temporaire du dossier de destination. Après validation et vidage sur disque, remplace atomiquement Build-<id>-Log-<logId>.txt. Remplace les fichiers existants par défaut et renvoie un FileInfo. Le délai total de téléchargement est de dix minutes, avec soixante secondes d’inactivité; une nouvelle tentative recommence avec un nouveau fichier temporaire. Un journal vide est valide uniquement si la liste indique zéro ligne. Tail utilise un intervalle inclusif à partir de zéro (hypothèse V-14 à confirmer au travail). BuildId et LogId se lient par nom de propriété depuis AdoBuildFailure. Les journaux restent des données de travail locales.

## EXAMPLES

### Exemple 1

```powershell
Get-AdoBuild -Definition 'Main Build' -Branch main -Latest -Result Failed |
    Get-AdoBuildFailure |
    Save-AdoBuildLog -Tail 200 -Path .\triage
```

Enregistre les deux cents dernières lignes de chaque journal disponible dans le dossier existant triage.

## PARAMETERS

### -BuildId

Identifiant positif du build dans le projet sélectionné. Liaison par nom de propriété.

```yaml
Type: System.Int32
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -LogId

Identifiant positif du journal. Une valeur nulle produit l’erreur non terminale BuildLogNotAvailable pour cet élément; le pipeline continue.

```yaml
Type: System.Nullable[System.Int32]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Tail

Nombre positif de lignes finales à demander. Sans cette option, télécharge le journal complet. Si le nombre de lignes est inconnu, avertit et télécharge le journal complet.

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

### -Path

Dossier FileSystem existant. Par défaut, utilise le dossier connu Téléchargements, y compris sa redirection. Aucun caractère générique n’est interprété.

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

### -Project

Nom du projet; reçoit la propriété `TeamProject` des échecs transmis dans le pipeline. Par défaut, celui de la connexion si aucune valeur n’est fournie.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- TeamProject
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -CollectionUri

Propriété de provenance liée automatiquement depuis l’échec. Une collection différente produit une erreur pour cet élément.

```yaml
Type: System.Uri
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: true
AcceptedValues: []
HelpMessage: ''
```

### -WhatIf

Affiche le fichier prévu sans requête HTTP ni écriture.

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

Demande confirmation avant le téléchargement et l’écriture.

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

Prend en charge ErrorAction, ErrorVariable, Verbose, Debug et WarningAction.

## INPUTS

### AdoToolkit.Core.Builds.AdoBuildFailure

## OUTPUTS

### System.IO.FileInfo

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020. Les intervalles de lignes attendent une confirmation sur le serveur (V-14).

## RELATED LINKS

[Get-AdoBuildFailure](Get-AdoBuildFailure.md)
