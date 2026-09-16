# Tooling implementation

- PowerShell 7.6.5+, Windows. Scripts use `[CmdletBinding()]`, explicit parameters,
  `Set-StrictMode -Version 2.0`, and terminating errors. Dot-sourced libraries inherit
  these from `dev.ps1`; Pester files use `BeforeAll` and `Describe`.
- Return objects from libraries. The public CLI emits exactly one compact JSON
  document; suppress runner chatter at its source. Use approved verbs and no aliases.
- `lib/discovery.ps1` owns enumeration, exclusions and context. `lib/dependencies.ps1`
  owns inventories/diagnostics; `lib/validation.ps1` owns plans/outcomes;
  `lib/setup.ps1` owns explicit bootstrap and atomic initialization.
- Keep one exclusion policy for ripgrep and filesystem discovery. Never follow links
  or parse sensitive files. XML prohibits DTDs and uses a null resolver.
- Pass state into stage script blocks as arguments; `GetNewClosure()` loses the
  library scope. Stage outcomes use `Failures`, `Summary`, `Warnings`, `Unavailable`.
- Test observable failures and command contracts in `tools/tests/`. Use Pester 5.x,
  temporary fixtures and mocked external boundaries. Never install or use the network
  in tests. Keep product tests separate under `tests/`.
