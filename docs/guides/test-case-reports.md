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

A profile with `defaultTestPlanId` and `defaultTestSuiteId` supplies `-PlanId` and
`-SuiteId` when you leave both out, so `Get-AdoTestCase -Recurse` reads the default
suite tree. The default suite is used only with the default plan. See
[Profiles](configuration.md#profiles).

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
Get-AdoTestCase -Id 1234 | Export-AdoTestCase -IncludeDetail -Open
Get-AdoTestCase -Id 1234 | Export-AdoTestCase -Format Json -IncludeSource -Path .\out
```

| Format | Output |
| --- | --- |
| `Html` (default) | One standalone page; see [The HTML report](#the-html-report) |
| `Markdown` | Plain Markdown document. Line breaks inside a step are kept |
| `Json` | Data that follows [testcase.v1.schema.json](../schemas/testcase.v1.schema.json). `-IncludeSource` adds the original step source fields and works only with JSON |

- `-Path` accepts a file path or an existing directory. By default, the report goes
  to your Downloads folder as `TestCase-<id>-Steps.<ext>`.
- An existing file is replaced atomically. Use `-NoClobber` to refuse instead, or
  `-WhatIf` to see the target path.
- `-Open` opens the written report with its default program. Only `.html`, `.htm`,
  `.md`, `.markdown`, `.json` and `.txt` files are opened, because Windows runs some
  other file types instead of showing them. With any other extension the report is
  still written, and a warning says it was not opened. A report that cannot be opened
  also produces a warning, and the file is still returned.
- Report labels use `-Culture`, then the configured report culture, then your
  session UI culture. Azure DevOps content is never translated.
- Export makes no server requests unless you add `-IncludeDetail`. The report links back
  to Azure DevOps.

## The HTML report

The page looks like the [failed-test report](build-report.md): dark on screen, light on
paper, with a top bar that stays in view.

- **Formatted steps.** A step written with lists, tables, bold, italic or underlined text
  keeps that structure. Markup is rebuilt from a fixed set of elements, so nothing of the
  step can run or load; a link shows its address after its text, and an image is shown as
  `[image]` with its description. Markdown and JSON keep the plain text of the step.
- **Top bar.** Counts of steps, Shared Steps and parameter iterations, links to each
  section, and a link to the Test Case in Azure DevOps.
- **Search.** Type in the search box, or press `/`, to keep only the steps that contain
  every word. `j` and `k` move to the next and previous step, and `Esc` clears the search.
- **Parameters.** Each `@name` of a declared parameter is marked in the steps. Choose an
  iteration above the parameter table to read the steps with its values instead.
- **Shared Steps.** A group can be collapsed and expanded, one by one or all at once, and
  the Shared Steps of the case are listed with links under its title.
- **Diagnostics.** A problem with one step links to that step.
- **Copy.** Buttons copy the title and, with `-IncludeDetail`, the automated test name.

The controls need the script that the page carries. Without it, for example in a mail
preview, the report is complete and only the controls are hidden. Printing shows every
step, including collapsed groups.

### More about each Test Case

`-IncludeDetail` reads more from the server and adds it to the HTML report:

| Section | Shows |
| --- | --- |
| Overview | Tags, who created the case and when, the automated test name, assembly and type |
| Description | The description of the case, formatted like a step |
| Links | Each linked work item with its link type, type, title and state; hyperlinks; the names and sizes of attachments |
| Test points | Each plan, suite and configuration in which the case is planned, with its tester, latest outcome and a link to the latest test run |

```powershell
Get-AdoTestCase -PlanId 812 -SuiteId 813 -Recurse | Export-AdoTestCase -IncludeDetail -Path .\release-12.html
```

- These are the only requests of an export: two batch requests per 200 cases, and one
  test points query per project and 50 cases. Nothing is requested with `-WhatIf`.
- A lookup that fails becomes a warning and a diagnostic in the report, which is written
  without that part. An authentication or authorization failure stops the export before a
  file is written.
- `-IncludeDetail` works with the HTML format only.
- The server answers that `-IncludeDetail` relies on are not yet confirmed on Azure DevOps
  Server 2020 (V-31 for the fields and links, V-32 for the test points).

## Export many Test Cases

`Export-AdoTestCase` writes one document for everything it receives. A report with
several cases adds an overview with totals by status and a table of contents grouped by
suite path. In HTML, the search and the `j` and `k` keys then work on whole cases, a
checkbox keeps only the partial cases, each case can be collapsed, and with
`-IncludeDetail` the table of contents shows the latest outcomes of each case and another
checkbox keeps the cases whose latest outcome failed somewhere.

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
