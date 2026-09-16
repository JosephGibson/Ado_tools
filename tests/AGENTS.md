# Product tests

- Use hand-written synthetic fixtures and `.test` hosts; catalog fixtures in
  `Fixtures/README.md`. Never copy work data (DD-010, DD-022).
- Tests use no network or live services. Core HTTP tests use a fake
  `HttpMessageHandler`; PowerShell integration tests may use a loopback-only fake
  server bound to `127.0.0.1`.
- Follow DD-024: no authored directory named `bin`, `obj`, `artifacts`, `build`,
  `dist`, `coverage`, `target`, `TestResults`, `secret`, or `secrets`; no file named
  `*.log`, `*credentials*`, `*.local.*`, `*.key`, `*.pem`, `*.pfx`, `*.p12`, or `.env*`.
  JSON/XML fixtures must parse; malformed or DTD-bearing inputs and log bodies use
  `.txt`. Generate inputs above 1 MiB inside tests.
- Tag acceptance tests with `[Trait("Acceptance", "S0-n")]` or `-Tag 'S0-n'` using
  the actual acceptance ID. Use xUnit `Assert` only.
- Product Pester files are `*.Pester.ps1` and run against the staged module in a
  child process. `Live/*.Live.ps1` checks are opt-in and never run by `verify`.
- Culture-sensitive tests set their culture explicitly; the Core suite also runs
  under `en-US` and `fr-CA` through `ADOTOOLKIT_TEST_CULTURE`.
