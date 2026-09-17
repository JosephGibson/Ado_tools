# Approved Server 2020 audit fixes

2026-09-17. Scope: approved findings F01–F16. The uncommitted 0.1.1 changes,
including numeric-string number handling and `ServerWireShapeTests`, were the
baseline and are preserved. No live script was run and no server connection was
attempted. The supplied SHAPE block contained no observations.

Microsoft's public REST 6.0 schemas establish the documented portions:
[test definitions](https://github.com/MicrosoftDocs/vsts-rest-api-specs/blob/master/specification/test/6.0/test.json)
(`PipelineReference`, `StageReference`, `PhaseReference`, `JobReference`, shallow
references and optional result fields), and
[build definitions](https://github.com/MicrosoftDocs/vsts-rest-api-specs/blob/master/specification/build/6.0/build.json)
(`BuildLog.lineCount: int64`). Synthetic robustness tests do not establish live
Server 2020 behavior. No API version was raised.

Each finding had a failing regression before its fix. The initial Core regression
run had 14 failures and two passing string/null controls. The pipeline and two
package regressions each failed on the baseline. Live validation regressions were
run as isolated blocks with mocked requests, never as live scripts. Further
boundary tests cover parent attempt resets, encoding failures, history window and
retry budgets, rollback after a partial commit, and incomplete retry observations.

| Finding | Regression source and observed failure before the fix | Implemented correction |
| --- | --- | --- |
| F01 | `WireAuditRegressionTests.F01…`: nested attempts ignored; reverse clocks ordered runs 4,3,2,1 | Read stage/phase/job reference attempts; order parent counters before child counters, then date/ID; correct the reattempt fixture |
| F02 | `WireAuditRegressionTests.F02…`: 2147483648 failed deserialization; `LiveAudit.Tests.ps1` triage case also failed | Use Int64 counts and range arithmetic through DTO, model, derivation, downloads and triage |
| F03 | `WireAuditRegressionTests.F03…`: BOM/UTF-16 responses failed and encoded error messages disappeared | One strict BOM/charset decoder used by every JSON HTTP consumer and bounded error translation |
| F04 | `Builds.Pester.ps1`, F04: actual log request used `/Wrong/` instead of the piped build project | Carry `TeamProject` on failures and bind it to `Save-AdoBuildLog -Project` |
| F05 | `WireAuditRegressionTests.F05…`: history exceeded a two-request budget | Enforce the scoped budget before each send, including retries and window pages; do not cache budget-exhausted build data |
| F06 | `WireAuditRegressionTests.F06…`: `url:"http://["` failed valid run/result retrieval | Ignore response URLs before deserialization and exclude them from extension data |
| F07 | `WireAuditRegressionTests.F07…`: absent custom value threw during mapping | Skip absent values while retaining explicit nulls |
| F08 | `WireAuditRegressionTests.F08…`: numeric Test Case references failed deserialization | Narrow reference converter preserves numeric tokens as text for existing positive-Int32 validation |
| F09 | `Package.Tests.ps1`, F09: locked checksum left changed ZIP beside old checksum | Stage and validate ZIP, checksum and installer; restore previous assets on a failed replacement |
| F10 | `Package.Tests.ps1`, F10: relative path used the process directory and failed the root boundary | Resolve through the PowerShell FileSystem provider, then apply the existing root/link guards |
| F11 | `LiveAudit.Tests.ps1`, F11: aggregate-only runs, missing/null Test Case IDs, minimal attachments, unfinished builds and manual history results produced FAIL | Separate optional absence from failure; tolerate empty missing-field arrays and the empty attachment query suffix |
| F12 | `LiveAudit.Tests.ps1`, F12: two empty retry build listings incorrectly produced PASS | Require observed retry structures and complete paging; handle lowercase group values and nested attempt references |
| F13 | `LiveAudit.Tests.ps1`, F13: 50 nonterminal pages had no completeness flag | Track completion and repeated tokens; incomplete bulk enumeration is INCONCLUSIVE |
| F14 | `LiveAudit.Tests.ps1`, F14: accepted combined parameters produced PASS for the opposite hypothesis | Acceptance contradicts V-10; rejection passes only with successful individual controls |
| F15 | `LiveAudit.Tests.ps1`, F15: formatted plain text and unformatted `List<T>` produced FAIL | Record flag observations; leave formatting semantics to visual confirmation |
| F16 | `LiveAudit.Tests.ps1`, F16: customer-like keys and unknown outcomes appeared in output | Schema allowlists and opaque dynamic bags; unknown names/values omitted; smoke exception paths use the same allowlist |

Core regressions are in `tests/AdoToolkit.Core.Tests/TestRuns/WireAuditRegressionTests.cs`.
All new synthetic input sources are cataloged in `tests/Fixtures/README.md`.
Existing history expectations now allocate five requests for a complete history
build: one window request, two run pages and two result pages.

`Shape.Live.ps1` additionally records build log/timeline and sub-result attachment
shapes. It remains a bounded sample, not proof that all pages or optional fields
have been observed. The [live-check commands and variables](../../tooling.md#live-checks)
identify the remaining work-machine checks. V-09 and V-17 retain their existing
confirmation; the fixes do not mark other V-items live-confirmed.

Release rollback is tested for locked existing checksum/installer files and a
missing staged file after the first replacement. Multiple files cannot be
replaced as one filesystem transaction; a process crash or failed rollback can
leave `.previous-*` recovery files.

Validation: `pwsh -NoProfile -File .\tools\dev.ps1 verify` exited 0. The gate
includes PowerShell parsing/lint, tooling Pester, configuration/layout checks,
the Release build, Core tests under en-US and fr-CA, and product Pester against
the staged module. All approved findings now have passing regression coverage.
