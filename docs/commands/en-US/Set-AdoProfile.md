---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-22-2026
PlatyPS schema version: 2024-05-01
title: Set-AdoProfile
---

# Set-AdoProfile

## SYNOPSIS

Creates or updates a local connection profile.

## SYNTAX

### Default (Default)

```
Set-AdoProfile [-Name] <string> [-CollectionUrl <string>] [-DefaultProject <string>]
 [-DefaultBranch <string>] [-DefaultBuildDefinition <Object>] [-DefaultTestPlanId <int>]
 [-DefaultTestSuiteId <int>] [-Authentication <string>] [-RequestTimeoutSeconds <int>] [-DefaultProfile]
 [-WhatIf] [-Confirm]
```

## ALIASES

No aliases.

## DESCRIPTION

Writes the profile atomically and preserves unspecified values, unrelated options, and unknown configuration fields. CollectionUrl is required for a new profile. DefaultProfile selects this profile as the default, which Connect-Ado uses without arguments and other commands use when no connection exists. DefaultBranch, DefaultBuildDefinition, DefaultTestPlanId and DefaultTestSuiteId save the values that build and test plan commands use when the matching parameter is omitted; pass $null, or an empty string for the first two, to remove one. Values are validated before the file is written. Commands read them from the connection, so connect again with Connect-Ado after changing them. WhatIf performs no write. A newer configuration schema is read-only. The profile stores no password or token.

## EXAMPLES

### Example 1

```powershell
Set-AdoProfile -Name work -CollectionUrl 'https://ado.example.test/Collection' -DefaultProject 'Équipe Web' -DefaultProfile
```

Creates or updates a local connection profile.

### Example 2

```powershell
Set-AdoProfile -Name work -DefaultBranch develop -DefaultBuildDefinition Test_Plan -DefaultTestPlanId 812 -DefaultTestSuiteId 813
```

Saves the defaults that Get-AdoBuild, Get-AdoBuildTestFailure, Get-AdoTestCase and Get-AdoTestSuite use when their parameters are omitted.

### Example 3

```powershell
Set-AdoProfile -Name work -DefaultBranch '' -DefaultTestSuiteId $null
```

Removes the default branch and test suite and keeps the other settings.

## PARAMETERS

### -Authentication

Authentication mode. The only supported value is WindowsIntegrated.

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

### -CollectionUrl

Absolute Azure DevOps Server collection URL. Matching quotes and trailing slashes are removed. HTTPS is recommended; HTTP emits a warning.

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

### -DefaultBranch

Branch used by Get-AdoBuild and Get-AdoBuildTestFailure when -Branch is omitted, for example develop, which becomes refs/heads/develop. An empty string or $null removes it.

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

### -DefaultBuildDefinition

Build definition used by Get-AdoBuild and Get-AdoBuildTestFailure when -Definition is omitted: a positive integer ID or an exact definition name, as for -Definition. An empty string or $null removes it.

```yaml
Type: System.Object
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

### -DefaultProfile

Selects this profile as the default. Omit to preserve the current default selection.

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

### -DefaultProject

Default project name to save in the profile.

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

### -DefaultTestPlanId

Test plan ID used by Get-AdoTestCase and Get-AdoTestSuite when -PlanId is omitted. $null removes it.

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

### -DefaultTestSuiteId

Test suite ID used by Get-AdoTestCase and Get-AdoTestSuite when both -PlanId and -SuiteId are omitted; it is used only with the profile's test plan. $null removes it.

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

### -Name

Profile name; it can't be blank. Get-AdoProfile accepts case-insensitive wildcard patterns; writes use the literal name.

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

### -RequestTimeoutSeconds

Request timeout in seconds, from 1 to 86400 (one day). Defaults to 100 for a new profile.

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

### AdoToolkit.Core.Configuration.AdoProfile

Object returned by the command.

## NOTES

Requires PowerShell 7.6 on Windows and Azure DevOps Server 2020.

## RELATED LINKS

[Connect-Ado](Connect-Ado.md)
[Get-AdoProject](Get-AdoProject.md)

