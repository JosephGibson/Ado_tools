---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 09-15-2026
PlatyPS schema version: 2024-05-01
title: Export-AdoTestCase
---

# Export-AdoTestCase

## SYNOPSIS

Exports one or more test cases to one HTML, Markdown, or JSON report.

## SYNTAX

### Input (Default)

```
Export-AdoTestCase [-InputObject] <AdoTestCase[]> [-Format <ReportFormat>] [-Culture <string>] [-Path <string>] [-NoClobber] [-IncludeSource] [-Open] [-Connection <AdoConnection>] [-WhatIf] [-Confirm]
```

## ALIASES

No aliases.

## DESCRIPTION

Collects pipeline objects and writes exactly one document, however many test cases arrive. One case uses the single-case layout. Several cases add a cover header (server, collection, project, plan and suite or WIQL source, generation time, and totals by status) and a linked table of contents grouped by suite path; each case keeps its own header, anchor tc-<id> (tc-<id>-<k> for a repeated case), and step numbering. Case bodies are rendered one at a time into the temporary file. Labels and diagnostics follow the report culture; ADO content is preserved. Standalone HTML contains embedded styles and no scripts. Output uses a neighboring temporary file, validation of every case and step count, and atomic replacement. When no case arrives, a warning is written and no file is created.

## EXAMPLES

### Example 1

```powershell
Get-AdoTestCase -Id 101 | Export-AdoTestCase -Culture en-US -Path .\report.html
```

Exports an English HTML report in the current directory.

### Example 2

```powershell
Get-AdoTestSuite -PlanId 812 -SuiteId 813 -Recurse | Get-AdoTestCase | Export-AdoTestCase -Culture fr-CA -Path .\Release-12.html -Open
```

Exports every case of a suite tree as one French document and opens it.

## PARAMETERS

### -InputObject

Test cases to export. Accepts pipeline input; every case from one invocation goes into the same document.

```yaml
Type: AdoToolkit.Core.TestManagement.AdoTestCase[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Input
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Format

Html (default), Markdown, or Json.

```yaml
Type: AdoToolkit.Core.Reporting.ReportFormat
DefaultValue: 'Html'
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

### -Culture

Report culture: explicit value, configuration reporting.culture, then session UI culture. Unsupported languages fall back to English with a warning.

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

### -Path

Literal FileSystem file path or existing directory. The parent must exist. Defaults to the Windows Downloads known folder. Default names: TestCase-<id>-Steps.<extension> for one case, TestSuite-<suiteId>-Steps.<extension> when every case comes from one suite, otherwise TestCases-<yyyyMMdd-HHmmss>.<extension> in local time.

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

### -NoClobber

Refuses to replace an existing report, including a file created before the final atomic move.

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

### -IncludeSource

Includes original step source fields in JSON only. Other formats produce a terminating InvalidArgument error before export.

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

### -Open

Opens the one committed report with the default application after successful validation and replacement.

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

Explicit connection, otherwise the active runspace connection. Input CollectionUri must match. Export makes no server requests.

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

### -WhatIf

Lists the target path without writing or opening a file.

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

### -Confirm

Requests confirmation before writing the report.

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

### CommonParameters

Supports common parameters including ErrorAction, WarningVariable, Verbose, and Debug.

## INPUTS

### AdoToolkit.Core.TestManagement.AdoTestCase

## OUTPUTS

### System.IO.FileInfo

The validated, committed file. No output object with WhatIf.

## NOTES

IncludeSource is JSON-only. JSON documents with several cases add totals and a source of TestSuite, Query, or Mixed; schema version 1 is unchanged.

## RELATED LINKS

[Get-AdoTestCase](Get-AdoTestCase.md)

