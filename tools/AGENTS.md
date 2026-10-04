# Tooling

| File | Owns |
| --- | --- |
| `tools/dev.ps1` | The CLI: parameters, the exclusion and documentation lists, command dispatch, exit codes |
| `tools/lib/discovery.ps1` | File enumeration, exclusions, `context`, `inspect`, `find` |
| `tools/lib/dependencies.ps1` | `deps`, `diagnose`, required tools, build-module selection |
| `tools/lib/validation.ps1` | The `verify` plan, the built-in stages, stage execution |
| `tools/lib/setup.ps1` | `bootstrap` |
| `tools/lib/processes.ps1` | PowerShell child processes run side by side: the Pester runs of `powershell-test` and of the product gate |
| `tools/lib/test-results.ps1` | Helpers of the product gate: TRX counters, the processes of its steps |
| `tools/check.ps1` | The product gate, stage `project-check` |
| `tools/guard-git.ps1`, `tools/validate-edit.ps1` | Claude hooks of every session |
| `tools/guard-readonly.ps1` | Claude hook of the read-only subagents |
| `tools/tests/` | Tooling tests, `*.Tests.ps1`, stage `powershell-test` |
| `tools/package/` | Packaging and release; see `tools/package/AGENTS.md` |

`docs/tooling.md` describes each command, stage and limit. Read it before changing one,
and correct it in the same change.

## Rules

- PowerShell 7.6.5+, Windows. Scripts use `[CmdletBinding()]`, explicit parameters,
  `Set-StrictMode -Version 2.0` and terminating errors. Libraries inherit these from
  `tools/dev.ps1`.
- Approved verbs, no aliases. Libraries return objects.
- `tools/dev.ps1` prints exactly one compact JSON document. Suppress runner output at its
  source.
- One exclusion policy serves ripgrep and the filesystem walk; its lists are in
  `tools/dev.ps1`. ripgrep runs from the root it searches, so that an exclusion by path,
  such as the Claude Code worktrees, applies at that root only.
- Never follow links. Never parse a sensitive file. XML is read with DTDs prohibited and a
  null resolver.
- `verify` never installs or restores anything.
- Stage definitions and outcome fields follow the [stage contracts](../docs/tooling.md#stage-contracts).
- `project-check` runs beside the in-process stages. An in-process stage reads no build
  output and writes nothing to `bin/`, `obj/` or `artifacts/` of the repository.
- A product check goes into `tools/check.ps1`: call executables with argument arrays,
  test the exit code at once, print one `check: ` line per result in a fixed order whichever
  step ends first, exit `0`, `1` or `2`, and never call `verify`.

## Tests

- Pester 5.x in `tools/tests/`, with fixtures under `$TestDrive` and mocked external
  boundaries. No installation.
- Each test file runs in a pwsh process of its own, beside the others: it may change `$env:`
  for itself, and depends on no other file.
- Test observable failures and command contracts, not implementation details.
- Product tests stay under `tests/`.
