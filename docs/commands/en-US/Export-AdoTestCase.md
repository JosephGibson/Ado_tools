---
document type: cmdlet
external help file: AdoToolkit.PowerShell.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: AdoToolkit
ms.date: 10-02-2026
PlatyPS schema version: 2024-05-01
title: Export-AdoTestCase
---

# Export-AdoTestCase

## SYNOPSIS

Exports one or more test cases to one HTML, Markdown, or JSON report.

## SYNTAX

### Input (Default)

```
Export-AdoTestCase [-InputObject] <AdoTestCase[]> [-Format <ReportFormat>] [-Culture <string>] [-Path <string>] [-NoClobber] [-IncludeSource] [-IncludeDetail] [-Open] [-Connection <AdoConnection>] [-WhatIf] [-Confirm]
```

## ALIASES

No aliases.

## DESCRIPTION

Collects pipeline objects and writes exactly one document, however many test cases arrive. One case uses the single-case layout. Several cases add a cover header (server, collection, project, plan and suite or WIQL source, generation time, and totals by status) and a linked table of contents grouped by suite path; each case keeps its own header, anchor tc-<id> (tc-<id>-<k> for a repeated case), and step numbering. Case bodies are rendered one at a time into the temporary file. Labels and diagnostics follow the report culture; ADO content is preserved. The HTML report is one standalone file with the theme of the failed-test report, dark on screen and light in print: a top bar with counts, section links, a search (steps of one case, cases of a document), parameter values of a chosen iteration in place of the @names, collapsible Shared Steps groups and cases, copy buttons and keyboard navigation. Formatted steps keep their lists, tables and emphasis in HTML; Markdown and JSON keep the plain text. One static embedded script runs under a hash-only Content Security Policy, and every part of the report is readable without it. With IncludeDetail the HTML report also shows, for each case, its description, tags, creation and automation fields, linked work items, hyperlinks, attachment names, and its test points with the latest outcome in each plan, suite and configuration. Output uses a neighboring temporary file, validation of every case and step count, and atomic replacement. When no case arrives, a warning is written and no file is created.

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

### Example 3

```powershell
Get-AdoTestCase -Id 101 | Export-AdoTestCase -IncludeDetail -Open
```

Exports one case with its description, links and test points, and opens the report.

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

### -IncludeDetail

Reads more about each received case and shows it in the HTML report: description, tags, created by and date, automated test name, storage and type, the work items it links to with their type, title and state, its hyperlinks and attachment names, and its test points (plan, suite, configuration, tester, latest outcome and test run). These are the only requests of an export: the cases with their relations and the linked work items in batches of 200, and a paged test points query per project and batch of up to 50 cases. A query may make several requests. They are sent only for a report that will be written, so not with WhatIf. A lookup that fails is a warning and a diagnostic in the report, which is written without that part; an authentication or authorization failure stops the export before any file is written. HTML only: another format produces a terminating InvalidArgument error before export.

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

Opens the one committed report with the default application after successful validation and replacement. Only .html, .htm, .md, .markdown, .json and .txt files are opened, because Windows runs some other file types instead of showing them; with any other extension the report is still written and a warning says it was not opened. A report that cannot be opened produces a warning and is still returned.

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

Explicit connection, otherwise the active runspace connection. Input CollectionUri must match. Export makes no server requests unless IncludeDetail is used.

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

### -Confirm

Requests confirmation before writing the report.

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

### CommonParameters

Supports common parameters including ErrorAction, WarningVariable, Verbose, and Debug.

## INPUTS

### AdoToolkit.Core.TestManagement.AdoTestCase

## OUTPUTS

### System.IO.FileInfo

The validated, committed file. No output object with WhatIf.

## NOTES

IncludeSource is JSON-only and IncludeDetail is HTML-only. JSON documents with several cases add totals and a source of TestSuite, Query, or Mixed; schema version 1 is unchanged. The field names and relation attributes read by IncludeDetail are provisional pending V-31, and the test points query pending V-32.

## RELATED LINKS

[Get-AdoTestCase](Get-AdoTestCase.md)

