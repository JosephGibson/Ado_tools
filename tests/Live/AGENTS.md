# Live checks

`tests/Live/*.Live.ps1` run only when the developer starts them at work, against the newest
AdoToolkit release installed for the account. An agent never runs them.

- Print only `PASS`, `FAIL`, `INCONCLUSIVE`, `NOTE` or `SHAPE` lines: a check ID (`V-nn`,
  `S0-n`, `SMOKE-n`, `SHAPE-n`), then structural notes: `UPPER_SNAKE_CASE` words, counts,
  allowlisted JSON paths, and toolkit codes made of letters only (an error ID, an
  operation, a status, a diagnostic code).
- Never print or store a work value: no names, titles, URLs, identities, tokens, response
  bodies or exception messages.
- Keep responses in memory. Write nothing except a report rendered to a temporary folder
  that the script deletes.
- Exit `0` when every check passed, `1` on any `FAIL`, `2` on any `INCONCLUSIVE`.
- A check passes only on evidence that settles its `V-nn` assumption. An unobserved case is
  `INCONCLUSIVE`; an observation that contradicts the assumption is `FAIL`.
- One verdict per check ID: a failure after a verdict was printed does not print a second.
- Inputs come from `ADOTOOLKIT_LIVE_*` environment variables; `docs/tooling.md` lists them
  with what each script checks.
- Pure helpers live in `tests/Live/Live.Common.ps1`. `tools/tests/LiveAudit.Tests.ps1`
  tests them, and single validation blocks of the scripts, with synthetic data.
