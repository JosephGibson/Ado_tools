# Test Case reports and bulk export

Retrieve Test Cases with their Shared Steps and parameters, then export them as
HTML, Markdown or JSON. The examples assume that you are
[connected](getting-started.md#connect).

## Find plans and suites

```powershell
Get-AdoTestPlan -Name 'Release*'
Get-AdoTestSuite -PlanId 812 -Recurse        # root suite and its whole tree, depth-first
```

Name filters ignore case but not accents: `'Tâches*'` doesn't match `Taches`.
A suite's `Id` is a suite ID, and AdoToolkit never treats it as a Test Case ID.

## Get Test Cases

```powershell
Get-AdoTestCase -Id 1234 | Select-Object -ExpandProperty Steps | Format-Table Number, Action, ExpectedResult
Get-AdoTestCase -PlanId 812 -SuiteId 813 -Recurse
Get-AdoTestSuite -PlanId 812 -SuiteId 813 | Get-AdoTestCase
```

- Shared Steps are expanded in place, recursively, with outline numbers such as
  `3.1` and `3.2`. `@name` tokens stay as written, and their values are in the
  `Parameters` property.
- Each distinct case is retrieved once per call. A case that belongs to several
  suites is returned once per suite, with `Suite` set.
- A case with a retrieval problem, such as a missing Shared Step or a circular
  reference, is still returned with `Status` set to `Partial` and details in
  `Diagnostics`. AdoToolkit also writes one summary warning. Use `-Strict` to get
  an error instead.
- `-MaximumSharedStepDepth` (default 10) and `-MaximumExpandedSteps` (default
  5,000 rows) guard against very large expansions. To change the defaults, see
  [Configuration](configuration.md#settings).

## Export one Test Case

```powershell
Get-AdoTestCase -Id 1234 | Export-AdoTestCase -Open
Get-AdoTestCase -Id 1234 | Export-AdoTestCase -Format Json -IncludeSource -Path .\out
```

| Format | Output |
| --- | --- |
| `Html` (default) | Standalone page with embedded styles and no scripts |
| `Markdown` | Plain Markdown document |
| `Json` | Data that follows [testcase.v1.schema.json](../schemas/testcase.v1.schema.json). `-IncludeSource` adds the original step source fields and works only with JSON |

- `-Path` accepts a file path or an existing directory. By default, the report goes
  to your Downloads folder as `TestCase-<id>-Steps.<ext>`.
- An existing file is replaced atomically. Use `-NoClobber` to refuse instead, or
  `-WhatIf` to see the target path.
- Report labels use `-Culture`, then the configured report culture, then your
  session UI culture. Azure DevOps content is never translated.
- Export makes no server requests. The report links back to Azure DevOps.

## Export many Test Cases

`Export-AdoTestCase` writes one document for everything it receives. A report with
several cases adds a cover section with totals by status and a table of contents
grouped by suite path.

```powershell
# A whole suite tree as one French HTML report
Get-AdoTestPlan -Id 812 | Get-AdoTestSuite -Recurse | Get-AdoTestCase |
    Export-AdoTestCase -Culture fr-CA -Path .\release-12.html

# Test Cases selected by a WIQL query, as Markdown
$query = "SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] = 'Test Case' " +
         "AND [System.AreaPath] UNDER 'Web\Checkout' ORDER BY [System.Id]"
Invoke-AdoWiql -Query $query | Get-AdoTestCase | Export-AdoTestCase -Format Markdown
```

When all cases come from one suite, the default file name is
`TestSuite-<suiteId>-Steps.<ext>`. Otherwise, it is `TestCases-<yyyyMMdd-HHmmss>.<ext>`.
If no case arrives, you get a warning and no file is written. See
[Work items and WIQL](work-items.md#run-a-wiql-query) for query limits.

Full help: [Get-AdoTestPlan](../commands/en-US/Get-AdoTestPlan.md),
[Get-AdoTestSuite](../commands/en-US/Get-AdoTestSuite.md),
[Get-AdoTestCase](../commands/en-US/Get-AdoTestCase.md),
[Export-AdoTestCase](../commands/en-US/Export-AdoTestCase.md).
