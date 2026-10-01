# Product tests

| Suite | Files | Run by |
| --- | --- | --- |
| Core, xUnit v3 | `tests/AdoToolkit.Core.Tests/`, `*.cs` | `project-check`, twice: `ADOTOOLKIT_TEST_CULTURE` is `en-US`, then `fr-CA` |
| Cmdlets, Pester 5 | `tests/AdoToolkit.PowerShell.Tests/`, `*.Pester.ps1` | `project-check`, in a child process, against the staged module (`ADOTOOLKIT_MODULE_MANIFEST`) |
| Live | `tests/Live/`, `*.Live.ps1` | Never by `verify`; see `tests/Live/AGENTS.md` |

## Data

- Fixtures are hand-written and synthetic. Never copy work data (DD-010, DD-022).
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

- No network and no live service. Core HTTP tests use `FakeHttpMessageHandler`; Pester
  tests use `tests/AdoToolkit.PowerShell.Tests/Support/FakeAdoServer.ps1`, bound to
  `127.0.0.1`.
- No test reads the developer's configuration: the Core test assembly and the gate both
  point `ADOTOOLKIT_CONFIG_PATH` at a file that does not exist. A test that writes
  configuration uses `TestDirectory` (Core) or a path under `$TestDrive` (Pester).
- A test that needs a culture sets it explicitly.

## Style

- xUnit `Assert` only. Pass `TestContext.Current.CancellationToken` to async calls.
- An acceptance test carries its real ID: `[Trait("Acceptance", "S5-3")]` or `-Tag 'S5-3'`.
  A test without an acceptance ID carries no tag.
- A bug fix adds a test that fails before the fix (skill `fix-bug`).
- Report goldens in `tests/Fixtures/Reports/` change only through the `update-goldens`
  skill.
