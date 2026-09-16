# Slice 0 offline completion evidence

Date: 2026-09-15. This record covers sessions 0.1–0.4 and the M2 gate.

## Final verification

`pwsh -NoProfile -File .\tools\dev.ps1 verify` returned **exit 0**,
`"Status":"pass"`, with empty top-level and per-stage `Warnings` arrays.

| Stage | Result | Duration |
| --- | --- | --- |
| powershell-lint | Pass; 27 PowerShell files | 5.921 s |
| powershell-test | Pass; 89 tests, no failures or skips | 26.663 s |
| configuration | Pass; 22 configuration files | 0.024 s |
| tooling-layout | Pass | 0.010 s |
| project-check | Pass; build, both Core cultures, package/help, product Pester | **13.367 s** |

The acceptance-tag scan lists every ID from S0-2 through S0-8. S0-1 is the full
gate itself. Slice 0 is **offline done** and Slice 1A may start.

## Offline acceptance

| ID | Evidence |
| --- | --- |
| S0-1 | Final full `tools/dev.ps1 verify` result recorded in the [roadmap](README.md#status); exit 0, pass, every warnings array empty |
| S0-2 | URL normalization rules and edge cases: `CollectionUrlNormalizerTests` |
| S0-3 | Synthetic HTTP fixtures, status mapping, retries with fake clock, pagination, guards, serialization, and loopback cmdlet tests |
| S0-4 | Core cancellation/timeouts and Pester in-flight `PowerShell.Stop()`: under five seconds, Stopped, no stack-trace error |
| S0-5 | Atomic config failure injection and profile WhatIf/pipeline/schema tests; isolated config paths |
| S0-6 | Import leaves eight session preferences unchanged; explicit exports and metadata |
| S0-7 | Resource and placeholder parity, human-message source scan, French 401 HTML response with stable error ID |
| S0-8 | 102 Core tests pass separately under en-US and fr-CA, including invariant machine output |
| S0-9 | Work-machine only; not run. Opt-in `tests/Live/Connection.Live.ps1` is linted, never invoked by verify |

Additional tests cover runspace ownership, bounded LRU/TTL caches, all eight help topics,
package allowlisting, mocked signature refusal and signing, WhatIf, and sibling staging.
The product Pester suite has 16 tests. Tooling has 89 tests, including 10 packaging tests.

## V-17: help culture lookup

Fresh `pwsh -NoProfile` children set only their thread UI culture before module import.
The staged package contains English MAML in `en-US/` and French MAML in `fr/`, with
no regional French help directories. Each of the eight commands has a synopsis,
description, parameters, and example. The checked Connect-Ado synopsis was:

| Thread UI culture | Resolved synopsis | Result |
| --- | --- | --- |
| en-US | Selects the connection for this runspace. | Pass |
| fr-CA | Sélectionne la connexion de cet espace d’exécution. | Pass, parent fr fallback |
| fr-FR | Sélectionne la connexion de cet espace d’exécution. | Pass, parent fr fallback |
| fr | Sélectionne la connexion de cet espace d’exécution. | Pass |

The four fresh-process cases remain in `Help.Pester.ps1` as regression coverage.
No user or system culture setting was changed. The package follows Q-14: one `fr/` file.

## Authorization and package pins

- Owner authorized nuget.org lookup, four package pins, and solution restore:
  System.Management.Automation **7.6.6**, Microsoft.NET.Test.Sdk **18.10.1**,
  xunit.v3 **4.0.1**, xunit.runner.visualstudio **4.0.0**.
- Owner separately authorized PSGallery lookup and CurrentUser installation of
  Microsoft.PowerShell.PlatyPS 1.x: **1.0.3** installed.
- Verification uses existing dependencies with `--no-restore`; no implicit installs.
- No live ADO access, real signing, product installation, Git mutation, or publishing
  to an external destination occurred. Package generation stays under `artifacts/`.

## Corrections and deviations

- V-09 initially stopped at point 4, with the fallback proposed. After the owner's
  instruction to fix the blocker, the unrelated tooling regression test was isolated
  in fixture repositories. All seven probes then passed. See the retained
  [session 0.1 record](slice-0-session-0.1-evidence.md) and spec DD-023.
  The gate implementation and its exit codes were not changed.
- `AdoCmdletBase` is public abstract because C# exported cmdlets require an equally
  accessible base. Supporting adapter infrastructure is internal. The spec now records
  this language constraint and fixes the nonexistent S0-10 reference without adding
  or changing an acceptance ID.
- The xUnit 4 package defaults are explicitly configured to retain the plan's VSTest
  runner. Product symbols are embedded and publish excludes dependency manifests by
  project settings, yielding the exact eight-file package without post-publish deletion.

## Open work

- [French strings and help](slice-0-french-review.md) await fluent review.
- Work-only S0-9 and V-07, V-08, V-12, V-14 (projects), V-18 remain unverified.
  Confirm Q-16 signing/execution policy and Q-20 system proxy assumptions there.
- The live script reports inconclusive for error-shape/language observations and
  multi-page confirmation it cannot establish from a successful connection run;
  it never substitutes success for missing work-machine evidence.
- Signed deployment and live checks belong to M8 or the optional W0 checkpoint;
  they do not block the offline Slice 1 entry gate.
