---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 10-04-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoBuildTestFailure
---

# Get-AdoBuildTestFailure

## SYNOPSIS

Gathers the failed and flaky tests of one pipeline run.

## SYNTAX

### ByDefinition (Default)

```
Get-AdoBuildTestFailure [-Definition <Object>] [-Branch <string>] [-Result <BuildResult>] [-HistoryCount <int>]
    [-HistoryScope <AdoTestHistoryScope>] [-SkipAttachments] [-Project <string>] [-Connection <AdoConnection>]
```

### ByBuildId

```
Get-AdoBuildTestFailure [-BuildId] <int> [-HistoryCount <int>] [-HistoryScope <AdoTestHistoryScope>]
    [-SkipAttachments] [-Project <string>] [-Connection <AdoConnection>]
```

### ByBuild

```
Get-AdoBuildTestFailure -InputObject <AdoBuild> [-HistoryCount <int>] [-HistoryScope <AdoTestHistoryScope>]
    [-SkipAttachments] [-Connection <AdoConnection>]
```

## ALIASES

No aliases.

## DESCRIPTION

Emits one AdoBuildTestFailureSet per input build. Retrieval runs in two passes. The first lists every test result of every run of the build, groups results into test identities by automated test storage and name, and classifies each identity. The second reads full details only for identities with a failing attempt, in report order, so every attempt, message, stack trace and attachment listing is available. A test is reported when any attempt failed. Attempts are grouped by the stage, job and job instance names and the name of their test runs; a run named like another run of the same job plus " (attempt N)", where N is its job attempt, is a retry of that run and stays in its group. An identity is Flaky when the last attempt of every group passed, otherwise Failed, so a failure in one stage or named run is never hidden by a later pass in another. Runs without distinct names form one group, where the last attempt decides. Every attempt is kept, including passing ones. In-task rerun groups, job or stage re-attempts and single results are all treated as attempt sources; data-driven and other non-rerun groups are nested inside their attempt instead. Test Case references are resolved in one batch, which also lists each Test Case's links. Each reported test then lists its open bugs in Bugs: every open work item associated with one of its test results, and every open work item linked to its Test Case by any link type whose type is in the project's Bug category (Microsoft.BugCategory). Their title, state, type, project, creation date and assignee are read in batches of up to 200, never one request per test. A bug is open (IsOpen) unless its state is in the Completed or Removed state category, which is read once per project and work item type, so a Resolved bug is still open. A closed bug is left out of Bugs, although the AssociatedBugIds of an attempt stay the server's own list; HasOpenBug is true when at least one bug is open. When a project's Bug category or state categories cannot be read, only the type named Bug counts and the states Closed, Done and Removed count as closed, with a BugMetadataUnavailable warning. A bug that cannot be read keeps its ID link with an UnresolvedBug warning, and when the bug lookup fails, the associated bug IDs stay as links with a BugLookupFailed warning; recoverable lookup problems produce warnings, while authentication, authorization and cancellation stop the retrieval. Run history adds the current build plus earlier builds of the same definition, with summary counts and one cell per reported identity; recoverable history problems produce diagnostics, while authentication, authorization and cancellation stop the retrieval. Tests that only passed or did not run are counted in the summary and history but never detailed. Attachment metadata is listed here; bytes are downloaded only by Export-AdoBuildTestFailure. Each detailed result costs one attachment-list request, plus one per sub-result, such as a rerun attempt or a data row; with -SkipAttachments none is sent. The requests of one stage (result listings, details, attachment lists, bug metadata) are sent together, at most testResults.maximumConcurrentRequests at a time, 6 by default and 1 to 16, and the run history is read while the main requests run. Results and diagnostics are combined in input order; with 1, every request waits for the previous one. Earlier builds are read newest first, one at a time, while the result pages of newer builds are read. A build's run list counts against testResults.maximumHistoryRequests, 500 by default, as it is sent; its result pages are then counted from the totalTests of its runs and set aside at once, so a build whose pages do not fit is unavailable, with every older build, before any of its pages is requested, and the builds kept do not depend on response order. A build whose result page fails still reads its other pages and keeps what it set aside. The pages of a run without totalTests or not completed, retries, and pages beyond totalTests take from what is left when they are sent; near the limit, retries and such extra pages can still depend on response order. Status is Partial when an error diagnostic exists, for example when more identities failed than the configured maximum. With -Definition, the latest completed build of the definition is selected, as Get-AdoBuild -Latest does, optionally filtered by branch and result. ByDefinition is the default parameter set: without BuildId, a piped build or Definition, the defaultBuildDefinition of the connected profile is used, and without that the command fails with a configuration error before any request. Without Branch, the defaultBranch of the connected profile limits the selection.

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

### Example 5

```powershell
Get-AdoBuildTestFailure -Result Failed | Export-AdoBuildTestFailure -Open
```

Reports the latest failed build of the default build definition and branch saved in the connection profile.

### Example 6

```powershell
Get-AdoBuildTestFailure -BuildId 401 -SkipAttachments | Export-AdoBuildTestFailure -Open
```

Gathers the failed tests of build 401 without their attachment lists, then writes and opens a report that downloads nothing and says that the attachments were not listed.

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

Positive integer ID or exact build definition name. The latest completed build of that definition is used. A numeric string is a name; use an integer for an ID. Defaults to the defaultBuildDefinition of the connected profile.

```yaml
Type: System.Object
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

### -Branch

Branch name or fully qualified refs/ path that limits the latest-build selection. For example, main becomes refs/heads/main. Defaults to the defaultBranch of the connected profile; without one, builds of every branch are considered.

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

### -HistoryScope

Branch scope of the history window. SameBranch keeps the current source branch; AllBranches widens it. Defaults to the testResults.historyScope configuration value, SameBranch.

```yaml
Type: System.Nullable`1[AdoToolkit.Core.TestRuns.AdoTestHistoryScope]
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

### -SkipAttachments

Lists no attachment: no attachment-list request is sent, every attempt has an empty Attachments list, and AttachmentsListed is False. Export-AdoBuildTestFailure then downloads nothing, needs no connection, and its report says that the attachments were not listed. Every other request is the same.

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

Build, Runs, Summary, History, Failures, FailedCount, FlakyCount, Status, Diagnostics, RetrievedAt, CollectionUri and AttachmentsListed, which is False when -SkipAttachments left out the attachment lists. Each AdoTestFailure has its Attempts, TestCase, Bugs and HasOpenBug, and is shown as a table of Ordinal, Classification, ShortName, Attempts, HasOpenBug and LatestError, the first line of its latest error as the report shows it. Each AdoTestBug has Id, Title, State, WorkItemType, TeamProject, StateCategory, IsOpen, IsResolved, CreatedDate, AssignedTo, IsAssociatedWithResult, IsLinkedToTestCase and WebUrl, and is shown as a table of Id, IsOpen, State, WorkItemType and Title. CreatedDate and AssignedTo are not in that table, which keeps its width for Title; select them explicitly. IsOpen is True, or empty for a bug that could not be read; closed bugs are not listed. TeamProject is empty when the bug could not be read or reported no usable project. CreatedDate is the day the bug was filed, in UTC, and is empty when the bug could not be read or the server did not send the field. AssignedTo is an AdoIdentityRef and is empty both when nobody is assigned and when the bug could not be read; IsOpen tells the two apart, because it is True only for a bug that was read whole.

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020. The test results of each earlier build, the Test Case links and the Bug category and state categories of each project are cached for the invocation, so several piped builds of one definition read them once; a Test Case that was not found still produces its warning in every set that references it. The listing of a run's results ends without a further request when the run is completed and its last page, shorter than a full page, brings the results read to the run's total; otherwise it ends on an empty page. Concurrent requests have been exercised only against a synthetic server: how Server 2020 with Windows authentication handles several at once has not been observed at work (V-33). With -Verbose, each stage of the retrieval writes one line with its requests and milliseconds: the build when the command reads it, the test runs, the result listings, the failure details, the attachment lists, the Test Case links, the bugs and the run history. With -SkipAttachments the attachment-list line stays in its place with 0 requests. The run history is read beside the other stages, so its time overlaps theirs. A last line gives the build, the failed and flaky tests found, every request sent and the elapsed time. Every test-area route, version and field awaits server confirmation (V-19 to V-25), including how retries are recorded. Web links await confirmation at work (V-26). State categories come from the work item type states route at version 6.0-preview.1, which the Server 2020 REST documentation lists but which has not been confirmed at work (V-30). Whether Server 2020 returns System.CreatedDate and System.AssignedTo in a work item batch field projection, and which shape the identity takes, has not been observed at work (V-37); both properties stay empty when the fields do not come back, with no warning.

## RELATED LINKS

[Export-AdoBuildTestFailure](Export-AdoBuildTestFailure.md)

[Get-AdoTestRun](Get-AdoTestRun.md)

[Get-AdoBuild](Get-AdoBuild.md)
