# Work items and WIQL

Retrieve work items by ID and run flat WIQL queries. The examples assume that you
are [connected](getting-started.md#connect).

## Get work items by ID

```powershell
Get-AdoWorkItem -Id 101, 102
101, 102, 101 | Get-AdoWorkItem          # duplicates are returned once, in input order
```

Each `AdoWorkItem` has convenience properties (`Id`, `Rev`, `WorkItemType`,
`Title`, `State`, `TeamProject`, `AreaPath`, `IterationPath`, `ChangedDate`,
`WebUrl`). A missing ID writes a non-terminating `ObjectNotFound` error, and the
other items are still returned. IDs are collection-wide, so no project is needed.

### Add fields or relations

```powershell
$item = Get-AdoWorkItem -Id 101 -Field System.AssignedTo, Microsoft.VSTS.Common.Priority
$item.Fields['Microsoft.VSTS.Common.Priority']

(Get-AdoWorkItem -Id 101 -IncludeRelations).Relations
```

`Fields` is read-only, and its keys are field reference names, matched without
regard to case. `-IncludeRelations` returns every field and can't be combined with
`-Field`.

## Run a WIQL query

```powershell
$query = "SELECT [System.Id], [System.Title], [Microsoft.VSTS.Common.Priority] FROM WorkItems " +
         "WHERE [System.WorkItemType] = 'Bug' AND [System.State] = 'Active' ORDER BY [System.Id]"

Invoke-AdoWiql -Query $query                       # one result: Ids, Columns, AsOf
Invoke-AdoWiql -Query $query -Top 50 -Hydrate      # up to 50 work items, in query order
```

- The query runs in the default project unless you pass `-Project`.
- Only flat queries are supported. Link and tree queries produce a `NotSupported`
  error.
- The query text is sent as written. Macros that only the web portal can resolve
  need explicit values.
- Without `-Top`, a result of 20,000 or more IDs is an error, not a truncated list.
  Narrow the query or set `-Top`. When `-Top` is used, the result's `LimitApplied`
  property records the limit.
- `-Hydrate` returns full work items with the queried columns in `Fields`.

## Use query results elsewhere

Results are ordinary objects, so they work with `Select-Object`, `Export-Csv` and
other cmdlets. A query that returns Test Cases can also be piped into
`Get-AdoTestCase`. See
[Test Case reports and bulk export](test-case-reports.md#export-many-test-cases).

```powershell
Invoke-AdoWiql -Query $query -Hydrate |
    Select-Object Id, State, Title, @{ n = 'Priority'; e = { $_.Fields['Microsoft.VSTS.Common.Priority'] } } |
    Export-Csv .\active-bugs.csv -NoTypeInformation
```

Full help: [Get-AdoWorkItem](../commands/en-US/Get-AdoWorkItem.md),
[Invoke-AdoWiql](../commands/en-US/Invoke-AdoWiql.md).
