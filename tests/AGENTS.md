# Product tests

| Suite | Files | Run by |
| --- | --- | --- |
| Core, xUnit v3 | `tests/AdoToolkit.Core.Tests/`, `*.cs` | `project-check`, in two runs side by side: with `ADOTOOLKIT_TEST_CULTURE` `en-US` every test, with `fr-CA` every class not marked culture-invariant |
| Cmdlets, Pester 5 | `tests/AdoToolkit.PowerShell.Tests/`, `*.Pester.ps1` | `project-check`, each file in a process of its own, against the staged module (`ADOTOOLKIT_MODULE_MANIFEST`) |
| Live | `tests/Live/`, `*.Live.ps1` | Never by `verify`; see `tests/Live/AGENTS.md` |

## Data

- Fixtures are hand-written; never copy work data (DD-010, DD-022).
- Hosts end in `.test`, for example `ado.example.test`.
- Every fixture file has one entry in `tests/Fixtures/README.md`; `FixtureCatalogTests`
  enforces it. Delete a fixture that no test reads.
- Names (DD-024): no directory named `bin`, `obj`, `artifacts`, `build`, `dist`,
  `coverage`, `target`, `TestResults`, `secret` or `secrets`; no file named `*.log`,
  `*credentials*`, `*.local.*`, `*.key`, `*.pem`, `*.pfx`, `*.p12` or `.env*`.
- `.json` and `.xml` fixtures must parse. Malformed input, DTD-bearing input and log bodies
  use `.txt`.
- Generate input larger than 1 MiB inside the test.

## Isolation

- Core HTTP tests use `FakeHttpMessageHandler`; Pester tests use
  `tests/AdoToolkit.PowerShell.Tests/Support/FakeAdoServer.ps1`, bound to `127.0.0.1`.
- No test reads the developer's configuration: the Core test assembly and the gate both
  point `ADOTOOLKIT_CONFIG_PATH` at a file that does not exist. A test that writes
  configuration uses `TestDirectory` (Core) or a path under `$TestDrive` (Pester).
- Core test classes run in parallel. A class that changes process-wide state (an
  environment variable, the default thread culture, the current directory, the console)
  joins `[Collection(ProcessEnvironment.Name)]`, which runs alone; `ProcessStateTests`
  enforces it. `TestDirectory.WithConfigurationPath()` points `ADOTOOLKIT_CONFIG_PATH` at
  the directory, so only that collection calls it. `CultureInfo.CurrentCulture` belongs to
  the test that sets it.
- Each product Pester file runs in a process of its own, beside the others: it may change
  `$env:` for itself, and depends on no other file.
- A test that needs a culture sets it explicitly. A test that needs a recent date computes
  it from the clock: a fixed date ages out of the window.

## Cultures

The fr-CA run leaves out a class marked `[Trait("Culture", "Invariant")]`, so a new class
runs in both cultures. Mark a class only when the process culture cannot change what it
asserts: it lies outside `Localization/`, `Reporting/` and `RichText/`, reads no process
culture, catalog text (`Messages`, `AdoMessage`, `DiagnosticMessageRenderer`),
`FrenchTypography`, French culture or message text, and uses no fixture that reads the
process culture. `CultureSelectionTests` checks the folders, those reads in the class's own
file, and its fixtures.

## Style

- xUnit `Assert` only. Pass `TestContext.Current.CancellationToken` to async calls.
- An acceptance test carries its real ID: `[Trait("Acceptance", "S5-3")]` or `-Tag 'S5-3'`.
  A test without an acceptance ID carries no tag, apart from the culture trait above.
- Report goldens in `tests/Fixtures/Reports/` change only through the `update-goldens` skill.
