---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 10-05-2026
PlatyPS schema version: 2024-05-01
title: Update-AdoToolkit
---

# Update-AdoToolkit

## SYNOPSIS

Installs the newest AdoToolkit release from GitHub beside the running copy.

## SYNTAX

### Default (Default)

```
Update-AdoToolkit [-WhatIf] [-Confirm]
```

## ALIASES

No aliases.

## DESCRIPTION

Reads the latest AdoToolkit release on GitHub and compares its version with the version that runs the command. When the running version is the latest or newer, it says so and stops: nothing is downloaded or written.

When a newer release exists, the command downloads it, checks it and installs it beside the running copy, which it never changes. It recognizes the two ways AdoToolkit is installed:

- A module installed by Install-AdoToolkit.ps1, in `AdoToolkit\<version>` under a folder of PSModulePath. The new version goes into its own version folder beside the running one, where Import-Module AdoToolkit finds it. If that folder already exists and is complete, the command reports it as installed and downloads nothing; an incomplete one is replaced. A release that requires a newer PowerShell than the one running is refused.
- A portable folder started by Start-AdoToolkit.cmd. The new version goes into a new folder, `AdoToolkit-<version>-win-x64`, next to the running one. If that folder already exists, the command refuses and names it; it never deletes or overwrites that folder.

Any other copy, such as a source build or a copy outside PSModulePath, is refused before any network request, with the address of the manual installation steps.

Each download must match both the SHA-256 digest that GitHub reports and the release's .sha256 file. The archive must hold exactly the files of an AdoToolkit release of that version, and each module assembly must carry that version. It checks no code signature: the releases on GitHub are unsigned, so where signed code is required, install a signed build with its own steps instead.

The command connects only to api.github.com, github.com and release-assets.githubusercontent.com, over HTTPS, through the system proxy. It sends no Windows credentials to GitHub; an authenticating proxy receives them, as it does for other programs. No request is retried: after a failure, run the command again.

A PowerShell window cannot load a newer copy of AdoToolkit once it has loaded one. After an update, close the window and open a new one; for a portable copy, start Start-AdoToolkit.cmd in the new folder. The command never opens a window and never imports the new version. Its messages are shown by default through the information stream, and the output object carries the same facts for scripts.

## EXAMPLES

### Example 1

```powershell
Update-AdoToolkit
```

Checks GitHub for a newer release and installs it beside the running copy, then says which version was installed, where, which version was running, and to open a new PowerShell window.

### Example 2

```powershell
Update-AdoToolkit -WhatIf
```

Reads the latest release and names the folder that the new version would go into. Nothing is downloaded or written.

### Example 3

```powershell
$result = Update-AdoToolkit 6>$null
$result.Status
```

Updates without showing the messages and keeps the result. Its Status is UpToDate, Installed or AlreadyInstalled.

## PARAMETERS

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

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### AdoToolkit.Core.Update.AdoToolkitUpdate

Status (UpToDate, Installed or AlreadyInstalled), InstallMode (Module or Portable), CurrentVersion, LatestVersion, Path (the folder of the version to use next), PreviousPath (the running copy's folder once a newer version is installed) and ReleaseUri. No object with WhatIf when there is something to install.

## NOTES

Requires PowerShell 7.6 on Windows. GitHub allows 60 requests an hour without sign-in for each network address; the command uses one to read the release. Whether the proxy at work lets the three GitHub hosts through has not been confirmed (V-38). Errors are named by error ID. AdoConfiguration: this copy is not an installed release, its folder passes through a link, or the new version requires a newer PowerShell. AdoAuthentication: the proxy did not accept the Windows sign-in. AdoAuthorization: GitHub refused the request. AdoThrottled: the hourly limit is used up, and the message gives the time it resets when GitHub gives one. AdoNotFound: no release, or a file missing from it. AdoRequest, AdoServer, AdoTimeout and AdoRedirect: the network or the proxy failed, GitHub failed or answered an unexpected status, a time limit passed, or a redirect was not allowed. AdoResponseFormat: the release description, a checksum or the archive is not what the release says. AdoFileOutput: the target folder exists or is in use, another update is running, the disk is full, or a folder cannot be read or written. In the window that ran the update, Import-Module AdoToolkit fails with "Assembly with same name is already loaded" until a new window is opened.

## RELATED LINKS

[AdoToolkit releases](https://github.com/JosephGibson/Ado_tools/releases)
