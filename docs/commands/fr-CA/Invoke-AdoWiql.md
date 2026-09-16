---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Invoke-AdoWiql
---

# Invoke-AdoWiql

## SYNOPSIS

Exécute une requête WIQL plate sur les éléments de travail.

## SYNTAX

### Default (Default)

```
Invoke-AdoWiql [-Query] <string> [-Project <string>] [-Top <int>] [-Hydrate] [-Connection <AdoConnection>]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Envoie la requête au point de terminaison WIQL du projet, avec l’API version 6.0, et retourne un résultat contenant les identifiants correspondants dans l’ordre de la requête, les noms de référence des colonnes retournées et AsOf. Seules les requêtes plates sont prises en charge ; les requêtes de liens et d’arborescence produisent une erreur NotSupported. Sans Top, un résultat de 20 000 identifiants ou plus est considéré comme potentiellement incomplet et produit une erreur sans sortie ; les résultats ne sont jamais tronqués en silence. Les erreurs de limite du serveur sont signalées avec le même conseil de restreindre la requête. Avec Top, au plus ce nombre d’éléments est demandé au serveur et LimitApplied consigne la limite ; le résultat n’est donc pas présenté comme complet. Il n’y a pas de pagination par décalage. Le texte de la requête n’est jamais écrit dans les flux Verbose ou Debug.

## EXAMPLES

### Example 1

```powershell
Invoke-AdoWiql -Query "SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] = 'Test Case' ORDER BY [System.Id]"
```

Retourne les identifiants des éléments de travail correspondants dans le projet par défaut.

### Example 2

```powershell
Invoke-AdoWiql -Project 'Équipe Web' -Query "SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.State] = 'Active'" -Top 50 -Hydrate
```

Retourne au plus 50 éléments de travail dans l’ordre de la requête, avec les colonnes demandées.

## PARAMETERS

### -Query

Texte WIQL d’une requête plate. Les macros propres au portail exigent des valeurs explicites ; la requête n’est pas réécrite.

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

### -Project

Nom du projet. Par défaut, le projet par défaut de la connexion ; une erreur est produite si aucun n’est disponible.

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

### -Top

Limite volontaire du résultat, de 1 à 20 000, envoyée comme paramètre de requête $top et consignée dans LimitApplied.

```yaml
Type: System.Nullable`1[System.Int32]
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

### -Hydrate

Retourne les éléments de travail dans l’ordre de la requête au lieu de l’objet résultat, récupérés par lots de 200 avec les colonnes retournées et les champs pratiques. Les éléments qui ne sont plus retournés produisent des erreurs ObjectNotFound. Les révisions actuelles sont utilisées ; AsOf n’indique que la provenance.

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

### CommonParameters

Cette commande accepte les paramètres communs, dont ErrorAction, ErrorVariable, Verbose et Debug.

## INPUTS

### None

## OUTPUTS

### AdoToolkit.Core.WorkItems.AdoWiqlResult

Identifiants, colonnes, AsOf, projet, collection et, s’il y a lieu, la limite appliquée.

### AdoToolkit.Core.WorkItems.AdoWorkItem

Éléments de travail dans l’ordre de la requête lorsque Hydrate est utilisé.

## NOTES

Le comportement du serveur à la limite de 20 000 éléments reste à confirmer (V-06). Nécessite PowerShell 7.6 sous Windows et Azure DevOps Server 2020.

## RELATED LINKS

[Get-AdoWorkItem](Get-AdoWorkItem.md)
