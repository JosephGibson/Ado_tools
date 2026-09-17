---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-16-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoTestRun
---

# Get-AdoTestRun

## SYNOPSIS

Lists the test runs of one pipeline run.

## SYNTAX

### ByBuildId (Default)

```
Get-AdoTestRun [-BuildId] <int> [-Project <string>] [-Connection <AdoConnection>]
```

### ByBuild

```
Get-AdoTestRun -InputObject <AdoBuild> [-Connection <AdoConnection>]
```

## ALIASES

No aliases.

## DESCRIPTION

Filters test runs by the build URI and reads run details, paging through every offset page. Uses the build's own URI when it has one and composes vstfs:///Build/Build/<id> otherwise. With BuildId, retrieves the build first; piped builds already supply that metadata. Runs are emitted in attempt order: pipeline attempt when the run exposes one, then start date, then run ID. Outcome counts come from the run statistics, keyed by the exact outcome name the server reports, and stay empty when the server sends none. The run totals PassedTests, NotApplicableTests, UnanalyzedTests and IncompleteTests keep the server’s own categories; UnanalyzedTests counts results that did not pass and are not yet analyzed. Web links are rebuilt from the connection URL and numeric IDs; response URLs are ignored. A foreign collection produces a ConnectionMismatch error for that input.

## EXAMPLES

### Example 1

```powershell
Get-AdoBuild -Definition 'Main Build' -Latest | Get-AdoTestRun
```

Lists the test runs of the latest build of a definition.

## PARAMETERS

### -BuildId

Positive build ID in the selected project.

```yaml
Type: System.Int32
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByBuildId
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -InputObject

Build received by value from Get-AdoBuild. Uses its owning project and checks CollectionUri.

```yaml
Type: AdoToolkit.Core.Builds.AdoBuild
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByBuild
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
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
- Name: ByBuildId
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

### AdoToolkit.Core.Builds.AdoBuild

## OUTPUTS

### AdoToolkit.Core.TestRuns.AdoTestRun

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020. The test run route, version, fields and paging await server confirmation (V-19, V-25), as does the pipeline attempt reference. Web links await confirmation at work (V-26).

## RELATED LINKS

[Get-AdoBuildTestFailure](Get-AdoBuildTestFailure.md)
