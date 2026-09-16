---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Get-AdoTestSuite
---

# Get-AdoTestSuite

## SYNOPSIS

Retrieves test suites of a test plan.

## SYNTAX

### ByPlanId (Default)

```
Get-AdoTestSuite [-PlanId] <int> [-SuiteId <int>] [-Recurse] [-Project <string>] [-Connection <AdoConnection>]
```

### ByPlan

```
Get-AdoTestSuite -InputObject <AdoTestPlan> [-SuiteId <int>] [-Recurse] [-Connection <AdoConnection>]
```

## ALIASES

No aliases.

## DESCRIPTION

Reads every page of the plan's suite listing with the testplan area at API version 6.0-preview.1, builds the suite tree from parent references, and computes each SuitePath from the root suite to the suite. Returns the suite given by SuiteId, or the plan's root suite, alone. With Recurse, returns that suite and its whole subtree depth-first, keeping sibling order as returned by the server. A missing SuiteId produces a non-terminating ObjectNotFound error. Piped plans from another collection produce ConnectionMismatch before any request.

## EXAMPLES

### Example 1

```powershell
Get-AdoTestSuite -PlanId 812 -SuiteId 814 -Recurse
```

Lists suite 814 followed by its descendants depth-first.

### Example 2

```powershell
Get-AdoTestPlan -Name 'Release*' | Get-AdoTestSuite
```

Lists the root suite of each matching plan, using each plan's project.

## PARAMETERS

### -PlanId

Positive test plan ID.

```yaml
Type: System.Int32
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByPlanId
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

Test plan from Get-AdoTestPlan, accepted by pipeline value only. Its Id is used as the plan ID and its TeamProject as the project.

```yaml
Type: AdoToolkit.Core.TestManagement.AdoTestPlan
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByPlan
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SuiteId

Positive suite ID to start from. Without it, the plan's root suite is the start.

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

### -Recurse

Also returns every descendant of the start suite, depth-first in server order.

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

Project name. Defaults to the connection's default project; an error is raised when neither is available.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByPlanId
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

Explicit connection object; overrides the active runspace connection.

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

This cmdlet supports the common parameters, including ErrorAction, ErrorVariable, Verbose, and Debug.

## INPUTS

### AdoToolkit.Core.TestManagement.AdoTestPlan

## OUTPUTS

### AdoToolkit.Core.TestManagement.AdoTestSuite

Suite with its plan ID, parent suite ID, suite path, project, and web link. Its Id is a suite ID, never a test case ID.

## NOTES

The testplan-area route, preview version, and sibling order are pending server confirmation (V-04). Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020.

## RELATED LINKS

[Get-AdoTestPlan](Get-AdoTestPlan.md)
