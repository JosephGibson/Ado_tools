# Live checks

`tests/Live/*.Live.ps1` run only when the developer starts them at work, against the newest
AdoToolkit release installed for the account. `verify` never runs them, and an agent never
runs them.

- Print only `PASS`, `FAIL`, `INCONCLUSIVE`, `NOTE` or `SHAPE` lines: a check ID (`V-nn`,
  `S0-n`, `SMOKE-n`, `SHAPE-n`) and a structural note in `UPPER_SNAKE_CASE`, counts or
  allowlisted JSON paths.
- Never print or store a work value: no names, titles, URLs, identities, tokens, response
  bodies or exception messages.
- Keep responses in memory. Write nothing except a report rendered to a temporary folder
  that the script deletes.
- Exit `0` when every check passed, `1` on any `FAIL`, `2` on any `INCONCLUSIVE`.
- A check passes only on evidence that settles its `V-nn` assumption. An unobserved case is
  `INCONCLUSIVE`; an observation that contradicts the assumption is `FAIL`.
- Inputs come from `ADOTOOLKIT_LIVE_*` environment variables; `docs/tooling.md` lists them.
- Pure helpers live in `tests/Live/Live.Common.ps1`. `tools/tests/LiveAudit.Tests.ps1`
  tests them, and single validation blocks of the scripts, with synthetic data and no
  network.
