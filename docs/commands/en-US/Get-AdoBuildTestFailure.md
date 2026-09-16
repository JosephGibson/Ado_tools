---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-16-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoBuildTestFailure
---

# Get-AdoBuildTestFailure

## SYNOPSIS

Gathers the failed and flaky tests of one pipeline run.

## SYNTAX

### ByBuildId (Default)

```
Get-AdoBuildTestFailure [-BuildId] <int> [-HistoryCount <int>] [-HistoryScope <AdoTestHistoryScope>]
    [-Project <string>] [-Connection <AdoConnection>]
```

### ByBuild

```
Get-AdoBuildTestFailure -InputObject <AdoBuild> [-HistoryCount <int>] [-HistoryScope <AdoTestHistoryScope>]
    [-Connection <AdoConnection>]
```

## ALIASES

No aliases.

## DESCRIPTION

Emits one AdoBuildTestFailureSet per input build. Retrieval runs in two passes. The first lists every test result of every run of the build, groups results into test identities by automated test storage and name, and classifies each identity. The second reads full details only for identities with a failing attempt, in report order, so every attempt, message, stack trace and attachment listing is available. A test is reported when any attempt failed: an identity whose last attempt passed is Flaky, otherwise Failed, and every attempt is kept, including passing ones. In-task rerun groups, job or stage re-attempts and single results are all treated as attempt sources; data-driven and other non-rerun groups are nested inside their attempt instead. Test Case references are resolved in one batch. Run history adds the current build plus earlier builds of the same definition, with summary counts and one cell per reported identity; history problems never fail the report. Tests that only passed or did not run are counted in the summary and history but never detailed. Attachment metadata is listed here; bytes are downloaded only by Export-AdoBuildTestFailure. Status is Partial when an error diagnostic exists, for example when more identities failed than the configured maximum.

## EXAMPLES

### Example 1

```powershell
Get-AdoBuild -Definition 'Main Build' -Branch main -Latest -Result Failed | Get-AdoBuildTestFailure -HistoryCount 15
```

Gathers the failed and flaky tests of the latest failed build with fifteen runs of history.

### Example 2

```powershell
(Get-AdoBuildTestFailure -BuildId 401).Failures | Where-Object Classification -eq Flaky
```

Selects the flaky tests of one build for scripting.

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

### -HistoryCount

Number of runs in the history window, including the current one, from 1 to 50. Defaults to the testResults.historyCount configuration value, 10.

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

### -HistoryScope

Branch scope of the history window. SameBranch keeps the current source branch; AllBranches widens it. Defaults to the testResults.historyScope configuration value, SameBranch.

```yaml
Type: AdoToolkit.Core.TestRuns.AdoTestHistoryScope
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
AcceptedValues:
- SameBranch
- AllBranches
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

### AdoToolkit.Core.TestRuns.AdoBuildTestFailureSet

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020. History listings are cached for the invocation, so several piped builds of one definition share them. Every test-area route, version and field awaits server confirmation (V-19 to V-25), including how retries are recorded. Web links await confirmation at work (V-26).

## RELATED LINKS

[Export-AdoBuildTestFailure](Export-AdoBuildTestFailure.md)

[Get-AdoTestRun](Get-AdoTestRun.md)

[Get-AdoBuild](Get-AdoBuild.md)
