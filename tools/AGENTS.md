# Tooling

| File | Owns |
| --- | --- |
| `tools/dev.ps1` | The CLI: parameters, the exclusion and documentation lists, command dispatch, exit codes |
| `tools/lib/discovery.ps1` | File enumeration, exclusions, `context`, `inspect`, `find` |
| `tools/lib/dependencies.ps1` | `deps`, `diagnose`, required tools, build-module selection |
| `tools/lib/validation.ps1` | The `verify` plan, the built-in stages, stage execution |
| `tools/lib/setup.ps1` | `bootstrap` |
| `tools/lib/test-results.ps1` | Helpers of the product gate: TRX counters, the reporting child process |
| `tools/check.ps1` | The product gate, stage `project-check` |
| `tools/guard-git.ps1`, `tools/validate-edit.ps1` | Claude hooks |
| `tools/tests/` | Tooling tests, `*.Tests.ps1`, stage `powershell-test` |
| `tools/package/` | Packaging and release; see `tools/package/AGENTS.md` |

`docs/tooling.md` describes each command and stage. Read it before changing one.

## Rules

- PowerShell 7.6.5+, Windows. Scripts use `[CmdletBinding()]`, explicit parameters,
  `Set-StrictMode -Version 2.0` and terminating errors. Libraries inherit these from
  `tools/dev.ps1`.
- Approved verbs, no aliases. Libraries return objects.
- `tools/dev.ps1` prints exactly one compact JSON document. Suppress runner output at its
  source.
- One exclusion policy serves ripgrep and the filesystem walk; both lists are in
  `tools/dev.ps1`.
- Never follow links. Never parse a sensitive file. XML is read with DTDs prohibited and a
  null resolver.
- A stage script block receives its state as arguments. `GetNewClosure()` loses the
  library scope.
- An in-process stage returns `Failures`, `Summary`, `Warnings` and optionally `Unavailable`.
- `tools/check.ps1` exits `0`, `1` or `2`, marks its result lines with the prefix
  `check: `, and never calls `verify`.
- `verify` never installs or restores anything.

## Tests

- Pester 5.x in `tools/tests/`, with fixtures under `$TestDrive` and mocked external
  boundaries. No network, no installation.
- Test observable failures and command contracts, not implementation details.
- Product tests stay under `tests/`.
