---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-22-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoTestCase
---

# Get-AdoTestCase

## SYNOPSIS

Retrieves test cases by ID, suite, WIQL result or work item and expands Shared Steps.

## SYNTAX

### BySuite (Default)

```
Get-AdoTestCase [-PlanId <int>] [-SuiteId <int>] [-Recurse] [-Project <string>] [-MaximumSharedStepDepth <int>] [-MaximumExpandedSteps <int>] [-Strict] [-Connection <AdoConnection>]
```

### ById

```
Get-AdoTestCase [-Id] <int[]> [-MaximumSharedStepDepth <int>] [-MaximumExpandedSteps <int>] [-Strict] [-Connection <AdoConnection>]
```

### BySuiteObject

```
Get-AdoTestCase -Suite <AdoTestSuite> [-Recurse] [-MaximumSharedStepDepth <int>] [-MaximumExpandedSteps <int>] [-Strict] [-Connection <AdoConnection>]
```

### ByWiql

```
Get-AdoTestCase -WiqlResult <AdoWiqlResult> [-MaximumSharedStepDepth <int>] [-MaximumExpandedSteps <int>] [-Strict] [-Connection <AdoConnection>]
```

### ByWorkItem

```
Get-AdoTestCase -WorkItem <AdoWorkItem> [-MaximumSharedStepDepth <int>] [-MaximumExpandedSteps <int>] [-Strict] [-Connection <AdoConnection>]
```

## ALIASES

No aliases.

## DESCRIPTION

Collects every input of the invocation, then retrieves each distinct test case once: root work items in batches of 200, one Shared Steps resolution over all roots with a shared cache, and one expansion per case. Suites are read with SuiteTestCaseList; with Recurse, child suites are walked depth-first and cases follow the suite order field. A case that belongs to several suites is emitted once per suite, each copy with Suite set and sharing the expanded steps. IDs keep input order and are deduplicated; a case is also emitted at most once per suite. Missing IDs produce ObjectNotFound errors; other types produce NotAStepContainer errors; a missing plan or suite produces an ObjectNotFound error for that input. Error diagnostics produce a Partial object and one warning with diagnostic counts. Strict replaces each partial object with a non-terminating error carrying its diagnostics. Progress is reported for suite enumeration, work item batches and expansion. Typed suites, WIQL results and work items bind to their own parameter sets by pipeline value; a suite ID is never used as a test case ID. BySuite is the default parameter set: without PlanId, the defaultTestPlanId of the connected profile is used, and without SuiteId, its defaultTestSuiteId. The profile's suite is used only together with the profile's plan, so an explicit PlanId needs an explicit SuiteId. A missing plan or suite is a configuration error before any request.

## EXAMPLES

### Example 1

```powershell
101, 102 | Get-AdoTestCase | Select-Object -ExpandProperty Steps
```

Retrieves two cases and displays their numbered steps.

### Example 2

```powershell
Get-AdoTestSuite -PlanId 812 -SuiteId 813 -Recurse | Get-AdoTestCase | Export-AdoTestCase -Culture fr-CA -Path .\Release-12.html
```

Retrieves every case of a suite tree and exports them as one French document.

### Example 3

```powershell
Invoke-AdoWiql -Query "SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] = 'Test Case'" | Get-AdoTestCase
```

Retrieves the cases returned by a flat WIQL query in query order.

### Example 4

```powershell
Get-AdoTestCase -Recurse | Export-AdoTestCase -Open
```

Exports every case of the default test suite tree saved in the connection profile.

## PARAMETERS

### -Id

Positive test case IDs. Accepts integers by pipeline value and untyped objects with an Id property. Deduplicates on first occurrence. Objects shaped like a test plan or suite (SuitePath or RootSuiteId properties) are rejected with InputNotTestCase.

```yaml
Type: System.Int32[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ById
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -PlanId

Test plan ID that contains the suite. Defaults to the defaultTestPlanId of the connected profile, and is required when the profile has none.

```yaml
Type: System.Nullable`1[System.Int32]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BySuite
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SuiteId

Test suite ID. The suite must belong to the plan; otherwise an ObjectNotFound error is written for that input. When PlanId is also omitted, defaults to the defaultTestSuiteId of the connected profile; otherwise it is required.

```yaml
Type: System.Nullable`1[System.Int32]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BySuite
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Suite

A test suite from Get-AdoTestSuite, bound by pipeline value only. Its Id is the suite ID, and its PlanId and TeamProject select the plan and project; its Id is never used as a test case ID.

```yaml
Type: AdoToolkit.Core.TestManagement.AdoTestSuite
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BySuiteObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -WiqlResult

A flat result from Invoke-AdoWiql, bound by pipeline value only. Its Ids are retrieved in query order.

```yaml
Type: AdoToolkit.Core.WorkItems.AdoWiqlResult
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByWiql
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -WorkItem

A work item, for example from Invoke-AdoWiql -Hydrate, bound by pipeline value only. Its Id is retrieved as a test case.

```yaml
Type: AdoToolkit.Core.WorkItems.AdoWorkItem
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByWorkItem
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Recurse

Includes child suites depth-first in server order. Without it, only the given suite is read.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BySuite
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: BySuiteObject
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

Project of the plan. Defaults to the connection's default project. Piped suites use their own TeamProject.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BySuite
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -MaximumSharedStepDepth

Maximum Shared Steps nesting depth. Overrides testCases.maximumSharedStepDepth.

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

### -MaximumExpandedSteps

Maximum expanded row count per case. Overrides testCases.maximumExpandedSteps.

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

### -Strict

Replaces each partial object with a non-terminating error. Diagnostics are available on the error target and in Exception.Data['Diagnostics'].

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

### -CollectionUri

Hidden provenance parameter bound from objects with an Id property. A different collection produces ConnectionMismatch before that input is queried. Typed suites, WIQL results and work items are checked with their own CollectionUri.

```yaml
Type: System.Uri
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ById
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: true
AcceptedValues: []
HelpMessage: ''
```

### -SuitePath

Hidden guard bound by property name. When an object with an Id also has SuitePath, it has the shape of a suite and is rejected instead of being read as a test case ID.

```yaml
Type: System.Object
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ById
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: true
AcceptedValues: []
HelpMessage: ''
```

### -RootSuiteId

Hidden guard bound by property name. When an object with an Id also has RootSuiteId, it has the shape of a test plan and is rejected instead of being read as a test case ID.

```yaml
Type: System.Object
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ById
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: true
AcceptedValues: []
HelpMessage: ''
```

### CommonParameters

Supports common parameters including ErrorAction, ErrorVariable, WarningVariable, Verbose, and Debug.

## INPUTS

### System.Int32

### AdoToolkit.Core.TestManagement.AdoTestSuite

### AdoToolkit.Core.WorkItems.AdoWiqlResult

### AdoToolkit.Core.WorkItems.AdoWorkItem

### AdoToolkit.Core.TestManagement.AdoTestCase

## OUTPUTS

### AdoToolkit.Core.TestManagement.AdoTestCase

Test case metadata, expanded rows, parameters, Shared Steps references, diagnostics, and Suite when retrieved through a suite.

## NOTES

IDs are collection-wide. Category lookups use each item's project. Limits default to configuration values: depth 10 and 5,000 rows. The resolution budget defaults to 10,000 additional work items per invocation. The size check follows each node and its recursive expansion; the row that crosses the limit is retained, followed by one Truncated marker. No later rows are emitted. Plan and suite listings are read once per project and plan per invocation. Testplan routes and suite order are provisional pending V-04; shared parameter formats remain provisional pending V-03.

## RELATED LINKS

[Get-AdoTestSuite](Get-AdoTestSuite.md)

[Invoke-AdoWiql](Invoke-AdoWiql.md)

[Export-AdoTestCase](Export-AdoTestCase.md)
