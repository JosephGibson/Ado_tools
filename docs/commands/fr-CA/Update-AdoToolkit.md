---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: fr-CA
Module Name: AdoToolkit
ms.date: 10-05-2026
PlatyPS schema version: 2024-05-01
title: Update-AdoToolkit
---

# Update-AdoToolkit

## SYNOPSIS

Installe depuis GitHub la version publiée la plus récente d’AdoToolkit, à côté de la copie en cours d’exécution.

## SYNTAX

### Default (Default)

```
Update-AdoToolkit [-WhatIf] [-Confirm]
```

## ALIASES

Aucun alias.

## DESCRIPTION

Lit la dernière version publiée d’AdoToolkit sur GitHub et compare son numéro de version à celui de la version qui exécute la commande. Lorsque la version en cours d’exécution est la dernière ou une version plus récente, la commande l’indique et s’arrête : rien n’est téléchargé ni écrit.

Lorsqu’une version publiée plus récente existe, la commande la télécharge, la vérifie et l’installe à côté de la copie en cours d’exécution, qu’elle ne modifie jamais. Elle reconnaît les deux façons dont AdoToolkit est installé :

- Un module installé par Install-AdoToolkit.ps1, dans `AdoToolkit\<version>` sous un dossier de PSModulePath. La nouvelle version est placée dans son propre dossier de version, à côté de celui de la version en cours d’exécution, où Import-Module AdoToolkit la trouve. Si ce dossier existe déjà et est complet, la commande indique que la version est installée et ne télécharge rien; un dossier incomplet est remplacé. Une version publiée qui nécessite une version de PowerShell plus récente que celle en cours d’exécution est refusée.
- Un dossier portable lancé par Start-AdoToolkit.cmd. La nouvelle version est placée dans un nouveau dossier, `AdoToolkit-<version>-win-x64`, à côté de celui en cours d’exécution. Si ce dossier existe déjà, la commande refuse l’installation et nomme le dossier; elle ne supprime ni n’écrase jamais ce dossier.

Toute autre copie, par exemple compilée à partir des sources ou située hors de PSModulePath, est refusée avant toute requête réseau, avec l’adresse des étapes d’installation manuelle.

Chaque téléchargement doit correspondre à la fois au condensé SHA-256 indiqué par GitHub et au fichier de somme de contrôle .sha256 de la version publiée. L’archive doit contenir exactement les fichiers d’AdoToolkit publiés sous ce numéro de version, et chaque assembly du module doit porter ce numéro. Elle ne vérifie aucune signature de code : les versions publiées sur GitHub ne sont pas signées; là où du code signé est exigé, installez plutôt une version signée en suivant ses propres étapes.

La commande se connecte uniquement à api.github.com, à github.com et à release-assets.githubusercontent.com, en HTTPS, par le proxy du système. Elle n’envoie pas les informations d’identification Windows à GitHub; un proxy qui exige une authentification les reçoit, comme pour les autres programmes. Aucune requête n’est relancée : après un échec, exécutez de nouveau la commande.

Une fenêtre PowerShell ne peut pas charger une copie plus récente d’AdoToolkit une fois qu’elle en a chargé une. Après une mise à jour, fermez la fenêtre et ouvrez-en une nouvelle; pour une copie portable, lancez Start-AdoToolkit.cmd dans le nouveau dossier. La commande n’ouvre jamais de fenêtre et n’importe jamais la nouvelle version. Ses messages s’affichent par défaut par l’intermédiaire du flux d’information, et l’objet retourné porte les mêmes renseignements pour les scripts.

## EXAMPLES

### Exemple 1

```powershell
Update-AdoToolkit
```

Cherche sur GitHub une version publiée plus récente et l’installe à côté de la copie en cours d’exécution, puis indique quelle version a été installée et où, quelle version s’exécutait, et qu’il faut ouvrir une nouvelle fenêtre PowerShell.

### Exemple 2

```powershell
Update-AdoToolkit -WhatIf
```

Lit la dernière version publiée et nomme le dossier où la nouvelle version serait placée. Rien n’est téléchargé ni écrit.

### Exemple 3

```powershell
$result = Update-AdoToolkit 6>$null
$result.Status
```

Effectue la mise à jour sans afficher les messages et conserve le résultat. Sa propriété Status vaut UpToDate, Installed ou AlreadyInstalled.

## PARAMETERS

### -Confirm

Demande confirmation avant d’exécuter la commande.

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

### -WhatIf

Exécute la commande dans un mode qui indique seulement ce qui se produirait, sans effectuer les actions.

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

Cette commande accepte les paramètres communs : -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction et -WarningVariable. Pour en savoir plus, consultez
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### AdoToolkit.Core.Update.AdoToolkitUpdate

Status (UpToDate, Installed ou AlreadyInstalled), InstallMode (Module ou Portable), CurrentVersion, LatestVersion, Path (le dossier de la version à utiliser ensuite), PreviousPath (le dossier de la copie en cours d’exécution, une fois qu’une version plus récente est installée) et ReleaseUri. Aucun objet avec WhatIf lorsqu’il y a quelque chose à installer.

## NOTES

Nécessite PowerShell 7.6 sous Windows. GitHub autorise 60 requêtes par heure sans authentification pour chaque adresse réseau; la commande en utilise une pour lire la version publiée. Le fait que le proxy au travail laisse passer les trois hôtes GitHub n’a pas été confirmé (V-38). Les erreurs sont désignées par leur ID d’erreur. AdoConfiguration : cette copie n’est pas une version publiée installée, son dossier passe par un lien, ou la nouvelle version nécessite une version de PowerShell plus récente. AdoAuthentication : le proxy n’a pas accepté l’authentification Windows. AdoAuthorization : GitHub a refusé la requête. AdoThrottled : la limite horaire est atteinte, et le message indique l’heure à laquelle elle est réinitialisée, lorsque GitHub la fournit. AdoNotFound : aucune version publiée, ou un fichier absent de celle-ci. AdoRequest, AdoServer, AdoTimeout et AdoRedirect : le réseau ou le proxy a échoué, GitHub a échoué ou a répondu par un code d’état inattendu, un délai a été dépassé, ou une redirection n’était pas autorisée. AdoResponseFormat : la description de la version publiée, une somme de contrôle ou l’archive ne correspond pas à ce qu’annonce la version publiée. AdoFileOutput : le dossier cible existe ou est en cours d’utilisation, une autre mise à jour s’exécute, le disque est plein, ou un dossier est inaccessible en lecture ou en écriture. Dans la fenêtre qui a exécuté la mise à jour, Import-Module AdoToolkit échoue avec « Assembly with same name is already loaded » jusqu’à ce qu’une nouvelle fenêtre soit ouverte.

## RELATED LINKS

[AdoToolkit releases](https://github.com/JosephGibson/Ado_tools/releases)
