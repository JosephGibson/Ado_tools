---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoProject
---

# Get-AdoProject

## SYNOPSIS

Liste les projets de la collection connectée.

## SYNTAX

### Default (Default)

```
Get-AdoProject [[-Name] <string>] [-Connection <AdoConnection>] [-Top <int>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Lit toutes les pages de projets avec l’API version 6.0. Filtre Name avec les caractères génériques PowerShell sans distinction de casse avant d’appliquer Top. Utilise un cache borné par espace d’exécution, valable quinze minutes. Connection remplace la connexion active. La complétion des projets utilise les noms en cache sans accès réseau.

## EXAMPLES

### Example 1

```powershell
Get-AdoProject -Name 'Équipe*' -Top 5
```

Liste les projets de la collection connectée.

## PARAMETERS

### -Connection

Objet de connexion explicite ; remplace la connexion active de l’espace d’exécution.

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

### -Name

Filtre de nom de projet acceptant les caractères génériques, sans distinction de casse.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Top

Nombre maximal de projets correspondants à retourner. Omettez ce paramètre pour tous les projets ; la valeur fournie doit être positive.

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

### CommonParameters

Cette commande accepte les paramètres communs : -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction et -WarningVariable.

## INPUTS

## OUTPUTS

### AdoToolkit.Core.Connections.AdoProject

Objet retourné par la commande.

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)


