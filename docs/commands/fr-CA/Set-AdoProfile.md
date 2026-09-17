---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Set-AdoProfile
---

# Set-AdoProfile

## SYNOPSIS

Crée ou modifie un profil de connexion local.

## SYNTAX

### Default (Default)

```
Set-AdoProfile [-Name] <string> [-CollectionUrl <string>] [-DefaultProject <string>]
 [-Authentication <string>] [-RequestTimeoutSeconds <int>] [-DefaultProfile] [-WhatIf] [-Confirm]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Écrit le profil de façon atomique et conserve les valeurs non précisées, les autres options et les champs inconnus. CollectionUrl est obligatoire pour un nouveau profil. DefaultProfile choisit ce profil par défaut, que Connect-Ado utilise sans argument et que les autres commandes utilisent en l’absence de connexion. WhatIf n’effectue aucune écriture. Une version de schéma plus récente est en lecture seule. Le profil ne contient ni mot de passe ni jeton.

## EXAMPLES

### Example 1

```powershell
Set-AdoProfile -Name work -CollectionUrl 'https://ado.example.test/Collection' -DefaultProject 'Équipe Web' -DefaultProfile
```

Crée ou modifie un profil de connexion local.

## PARAMETERS

### -Authentication

Mode d’authentification. La seule valeur prise en charge est WindowsIntegrated.

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

### -CollectionUrl

URL absolue de collection Azure DevOps Server. Les guillemets appariés et les barres obliques finales sont retirés. HTTPS est recommandé ; HTTP produit un avertissement.

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

### -Confirm

Prompts you for confirmation before running the cmdlet.

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

### -DefaultProfile

Choisit ce profil par défaut. Omettez ce paramètre pour conserver la sélection actuelle.

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

### -DefaultProject

Nom du projet par défaut à enregistrer dans le profil.

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

### -Name

Nom du profil. Get-AdoProfile accepte les caractères génériques sans distinction de casse ; les écritures utilisent le nom littéral.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -RequestTimeoutSeconds

Délai d’expiration des requêtes, en secondes, de 1 à 86 400 (un jour). Vaut 100 pour un nouveau profil.

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

### -WhatIf

Runs the command in a mode that only reports what would happen without performing the actions.

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

### CommonParameters

Cette commande accepte les paramètres communs : -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction et -WarningVariable.

## INPUTS

## OUTPUTS

### AdoToolkit.Core.Configuration.AdoProfile

Objet retourné par la commande.

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)

