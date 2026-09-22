---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-21-2026
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

### ByDefinition

```
Get-AdoBuildTestFailure -Definition <Object> [-Branch <string>] [-Result <BuildResult>] [-HistoryCount <int>]
    [-HistoryScope <AdoTestHistoryScope>] [-Project <string>] [-Connection <AdoConnection>]
```

## ALIASES

No aliases.

## DESCRIPTION

Emits one AdoBuildTestFailureSet per input build. Retrieval runs in two passes. The first lists every test result of every run of the build, groups results into test identities by automated test storage and name, and classifies each identity. The second reads full details only for identities with a failing attempt, in report order, so every attempt, message, stack trace and attachment listing is available. A test is reported when any attempt failed. Attempts are grouped by the stage, job and job instance names of their test runs: an identity is Flaky when the last attempt of every group passed, otherwise Failed, so a failure in one stage is never hidden by a later pass in another. Runs without distinct names form one group, where the last attempt decides. Every attempt is kept, including passing ones. In-task rerun groups, job or stage re-attempts and single results are all treated as attempt sources; data-driven and other non-rerun groups are nested inside their attempt instead. Test Case references are resolved in one batch, which also lists each Test Case's links. Each reported test then lists its bugs in Bugs: every work item associated with one of its test results, and every work item linked to its Test Case by any link type whose type is in the project's Bug category (Microsoft.BugCategory). Their title, state, type and project are read in batches of up to 200, never one request per test. A bug is open (IsOpen) unless its state is in the Completed or Removed state category, which is read once per project and work item type, so a Resolved bug is still open; HasOpenBug is true when at least one bug is open. When a project's Bug category or state categories cannot be read, only the type named Bug counts and the states Closed, Done and Removed count as closed, with a BugMetadataUnavailable warning. A bug that cannot be read keeps its ID link with an UnresolvedBug warning, and when the bug lookup fails, the associated bug IDs stay as links with a BugLookupFailed warning; none of these fail the retrieval. Run history adds the current build plus earlier builds of the same definition, with summary counts and one cell per reported identity; history problems never fail the report. Tests that only passed or did not run are counted in the summary and history but never detailed. Attachment metadata is listed here; bytes are downloaded only by Export-AdoBuildTestFailure. Status is Partial when an error diagnostic exists, for example when more identities failed than the configured maximum. With -Definition, the latest completed build of the definition is selected, as Get-AdoBuild -Latest does, optionally filtered by branch and result.

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

### Example 3

```powershell
Get-AdoBuildTestFailure -Definition 'Main Build' -Branch main -Result Failed |
    Export-AdoBuildTestFailure -Path .\reports\nightly -Open
```

Gathers the failed tests of the latest failed build on main, writes the report into a new folder and opens it.

### Example 4

```powershell
(Get-AdoBuildTestFailure -BuildId 401).Failures | Where-Object HasOpenBug -eq $false
```

Selects the reported tests that no open bug tracks yet.

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

### -Definition

Positive integer ID or exact build definition name. The latest completed build of that definition is used. A numeric string is a name; use an integer for an ID.

```yaml
Type: System.Object
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByDefinition
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Branch

Branch name or fully qualified refs/ path that limits the latest-build selection. For example, main becomes refs/heads/main.

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

### -Result

Optional result filter for the latest-build selection: None, Succeeded, PartiallySucceeded, Failed, or Canceled.

```yaml
Type: System.Nullable`1[AdoToolkit.Core.Builds.BuildResult]
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

### -HistoryCount

Number of runs in the history window, including the current one, from 1 to 50. Defaults to the testResults.historyCount configuration value, 10.

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

### -HistoryScope

Branch scope of the history window. SameBranch keeps the current source branch; AllBranches widens it. Defaults to the testResults.historyScope configuration value, SameBranch.

```yaml
Type: System.Nullable[AdoToolkit.Core.TestRuns.AdoTestHistoryScope]
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

### AdoToolkit.Core.Builds.AdoBuild

## OUTPUTS

### AdoToolkit.Core.TestRuns.AdoBuildTestFailureSet

Build, Runs, Summary, History, Failures, FailedCount, FlakyCount, Status, Diagnostics, RetrievedAt and CollectionUri. Each AdoTestFailure has its Attempts, TestCase, Bugs and HasOpenBug. Each AdoTestBug has Id, Title, State, WorkItemType, TeamProject, StateCategory, IsOpen, IsResolved, IsAssociatedWithResult, IsLinkedToTestCase and WebUrl, and is shown as a table of Id, IsOpen, State, WorkItemType and Title. TeamProject is empty when the bug could not be read or reported no usable project.

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020. History listings are cached for the invocation, so several piped builds of one definition share them. Every test-area route, version and field awaits server confirmation (V-19 to V-25), including how retries are recorded. Web links await confirmation at work (V-26). State categories come from the work item type states route at version 6.0-preview.1, which the Server 2020 REST documentation lists but which has not been confirmed at work.

## RELATED LINKS

[Export-AdoBuildTestFailure](Export-AdoBuildTestFailure.md)

[Get-AdoTestRun](Get-AdoTestRun.md)

[Get-AdoBuild](Get-AdoBuild.md)
