---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-22-2026
PlatyPS schema version: 2024-05-01
title: Connect-Ado
---

# Connect-Ado

## SYNOPSIS

Sélectionne la connexion de cet espace d’exécution.

## SYNTAX

### Default (Default)

```
Connect-Ado [-Project <string>]
```

### ByUrl

```
Connect-Ado -CollectionUrl <string> [-Project <string>]
```

### ByProfile

```
Connect-Ado -Profile <string> [-Project <string>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Crée une connexion de session à partir d’une URL de collection explicite, d’un profil nommé ou du profil par défaut, dans cet ordre. Utilise votre identité Windows. Cette commande ne contacte pas le serveur ; utilisez Test-AdoConnection pour vérifier l’accès. Project remplace le projet par défaut du profil. Une connexion par profil porte aussi la branche, la définition de build, le plan de test et la suite de tests par défaut du profil, que les commandes utilisent quand les paramètres correspondants sont omis ; une connexion par URL de collection n’en a aucun. Les valeurs sont copiées au moment de la connexion ; reconnectez-vous après avoir modifié le profil. Les URL Azure DevOps Services sont refusées. En l’absence de connexion, les autres commandes AdoToolkit se connectent de la même façon avec le profil par défaut, sans contacter le serveur.

## EXAMPLES

### Example 1

```powershell
Connect-Ado -Profile work -Project 'Équipe Web'
```

Sélectionne la connexion de cet espace d’exécution.

## PARAMETERS

### -CollectionUrl

URL absolue de collection Azure DevOps Server. Les guillemets appariés et les barres obliques finales sont retirés. HTTPS est recommandé ; HTTP produit un avertissement.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByUrl
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Profile

Nom d’un profil local enregistré. La complétion lit uniquement la configuration locale.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByProfile
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Project

Projet par défaut de cette connexion. La complétion utilise uniquement le cache de projets de l’espace d’exécution, et un nom complété est placé entre guillemets simples de sorte qu’il puisse être exécuté tel quel. Une commande qui utilise un projet nommé . ou .. échoue avec une erreur de configuration avant toute requête.

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

### CommonParameters

Cette commande accepte les paramètres communs : -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction et -WarningVariable.

## INPUTS

## OUTPUTS

### AdoToolkit.Core.Connections.AdoConnection

Objet retourné par la commande.

## NOTES

Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)


