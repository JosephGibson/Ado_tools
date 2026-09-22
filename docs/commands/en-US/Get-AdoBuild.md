---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-22-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoBuild
---

# Get-AdoBuild

## SYNOPSIS

Finds pipeline builds by definition, branch, status, and result.

## SYNTAX

### ByDefinition (Default)

```
Get-AdoBuild [[-Definition] <Object>] [-Branch <string>] [-Latest] [-Status <BuildStatus>] [-Result <BuildResult>] [-Top <int>] [-Project <string>] [-Connection <AdoConnection>]
```

### ByDefinitionObject

```
Get-AdoBuild -InputObject <AdoBuildDefinition> [-Branch <string>] [-Latest] [-Status <BuildStatus>] [-Result <BuildResult>] [-Top <int>] [-Connection <AdoConnection>]
```

## ALIASES

No aliases.

## DESCRIPTION

Without Definition, the defaultBuildDefinition of the connected profile applies; without that either, the command fails with a configuration error before any request. Without Branch, the defaultBranch of the connected profile applies, also to piped definitions; without that, builds of every branch are returned. An integer Definition selects an ID. A string resolves an exact name across all definitions, ignoring case and normalizing accents to NFC. Zero or several matches produce an error with the count and guidance to use an ID. Branch names without refs/ are prefixed with refs/heads/. Latest requests one build ordered by descending finish time and defaults Status to Completed; otherwise Status defaults to All. Top limits output independently of page size. Continuation tokens are followed even after empty pages. Piped definitions supply their project and must belong to the selected collection. Builds include source and repository metadata, identity, UTC timestamps, and a reconstructed WebUrl.

## EXAMPLES

### Example 1

```powershell
Get-AdoBuild -Definition 42 -Branch main -Latest -Result Failed | Get-AdoBuildFailure
```

Finds the most recently finished failed build on main and derives its timeline failures.

### Example 2

```powershell
Get-AdoBuild -Latest -Result Failed
```

Finds the latest failed build of the default build definition and branch saved in the connection profile.

## PARAMETERS

### -Definition

Positive integer ID or exact definition name. A numeric string is a name; use an integer for an ID. Defaults to the defaultBuildDefinition of the connected profile, and is required when the profile has none.

```yaml
Type: System.Object
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByDefinition
  Position: 0
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -InputObject

Definition received by value from Get-AdoBuildDefinition. Collection provenance is checked before requests.

```yaml
Type: AdoToolkit.Core.Builds.AdoBuildDefinition
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByDefinitionObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Branch

Branch name or fully qualified refs/ path. For example, main becomes refs/heads/main. Defaults to the defaultBranch of the connected profile; without one, no branch filter is applied.

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

### -Latest

Requests at most one build with queryOrder=finishTimeDescending and $top=1.

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

### -Status

Completed, InProgress, or All. Defaults to Completed with Latest and All otherwise.

```yaml
Type: System.Nullable`1[AdoToolkit.Core.Builds.BuildStatus]
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

### -Result

Optional result filter: None, Succeeded, PartiallySucceeded, Failed, or Canceled.

```yaml
Type: System.Nullable`1[AdoToolkit.Core.Builds.BuildResult]
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

Positive result limit. Stops fetching pages when satisfied. Latest always limits output to one.

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

### -Project

Project name. Defaults to the active connection’s project. For piped objects, the owning project is used.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByDefinition
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

Explicit connection; overrides the active runspace connection.

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

Supports common parameters including ErrorAction, ErrorVariable, Verbose, and Debug.

## INPUTS

### AdoToolkit.Core.Builds.AdoBuildDefinition

## OUTPUTS

### AdoToolkit.Core.Builds.AdoBuild

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020. Timeline shapes and paging await server confirmation (V-11, V-14). Web links await confirmation at work.

## RELATED LINKS

[Get-AdoBuildFailure](Get-AdoBuildFailure.md)
