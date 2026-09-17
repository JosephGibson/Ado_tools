# AdoToolkit implementation roadmap

| | |
| --- | --- |
| Spec | `docs/ado-toolkit-spec.md` v1.0, accepted 2026-09-15 |
| Scope | v1 = Slices 0–5 (§1.3, §22) |
| Plans | One self-contained plan per slice, listed below |
| Final milestone | **M8 — v1 installed at work** |

## How to use these plans

- Each slice plan splits into numbered sessions (for example 0.1–0.4 or 1A–1C). Run one session
  per agent session (Codex or Claude Code) by pasting that session's kickoff prompt from the
  plan's last section.
- Plans are self-contained: working rules, spec sections, prerequisites, deliverables, steps,
  test mapping, gates, live checks, defaults, and risks. A fresh session needs only `AGENTS.md`,
  the plan, and the spec sections it lists.
- Run slices strictly in order. A slice starts only after the previous slice's exit gate is met
  and recorded in [Status](#status).
- Agent differences: template initialization is `/template-init` in Claude Code and
  `$template-init` in Codex. Everything else is the same.

## Baseline (2026-09-15)

- The spec is accepted at v1.0. The template is **not** initialized. `src/` and `tests/` are
  empty, and the folder is not a Git repository.
- `pwsh -NoProfile -File .\tools\dev.ps1 verify` passes: `powershell-lint`, `powershell-test`
  (77 Pester tests), `configuration`, `tooling-layout`.
- Home PC: PowerShell 7.6.6, .NET SDK 10.0.400, Pester 5.9.1, PSScriptAnalyzer 1.25.0, ripgrep. No
  ADO access. Live criteria and V-items that need the server run only on the work machine (§19.5).

## Milestones

| ID | Milestone | Done when |
| --- | --- | --- |
| M0 | Spec accepted, plans written | Spec v1.0 with defaults recorded; this folder exists; `verify` result unchanged |
| M1 | Project initialized | Slice 0 session 0.1 steps 1–2: `AdoToolkit` identity set by template initialization; `verify` exit 0 |
| M2 | Foundation, offline | Slice 0 exit gate met |
| M3 | Work items and test step model, offline | Slice 1 exit gate met |
| M4 | Test Case reports, offline | Slice 2 exit gate met |
| M5 | Bulk test export, offline | Slice 3 exit gate met |
| M6 | Pipeline failure triage, offline | Slice 4 exit gate met |
| M7 | Failed-test report, offline (v1 feature-complete) | Slice 5 exit gate met, including the S5-9 home browser spike |
| **M8** | **v1 installed at work** | The module is built and signed on the work machine and installed per §21 and §21.1. S0-9, S1-7, S2-8, S3-6, S4-4, and S5-10 pass there, S5-9 is confirmed under the work browser policy, and the French strings are reviewed by a fluent speaker |

## Slice order and dependencies

```text
M0 ──► Slice 0 ──► Slice 1 ──► Slice 2 ──► Slice 3 ──► Slice 4 ──► Slice 5 ──► M8 (work track)
      (M1, M2)     (M3)        (M4)        (M5)        (M6)        (M7)
```

| Slice | Plan | Sessions | Technical dependencies | Starts after |
| --- | --- | --- | --- | --- |
| 0 Foundation | [slice-0-foundation.md](slice-0-foundation.md) | 4 | — | M0 |
| 1 Work items and test step model | [slice-1-work-items.md](slice-1-work-items.md) | 3 (1A–1C) | Slice 0 | M2 |
| 2 Test Case reports | [slice-2-reports.md](slice-2-reports.md) | 2 | Slices 0–1 | M3 |
| 3 Bulk test export | [slice-3-bulk-export.md](slice-3-bulk-export.md) | 2 | Slices 1–2 | M4 |
| 4 Pipeline failure triage | [slice-4-pipeline-triage.md](slice-4-pipeline-triage.md) | 2 | Slice 0; Slice 2 atomic writer and Downloads default | M5 (sequence rule) |
| 5 Failed-test report | [slice-5-failed-test-report.md](slice-5-failed-test-report.md) | 4 | Slices 2 and 4 (DD-016) | M6 |

17 sessions in total. Slice 4 could technically run earlier, but the sequence rule keeps one
active slice at a time.

## Gates

**Entry gate, every slice**

1. The previous slice's exit gate is recorded in [Status](#status).
2. `verify` exits 0 at the start of the first session.
3. Restore assets are current. Any authorization the plan names (restore, package, module
   install) is requested when its step is reached, not assumed.

**Exit gate, every slice**

1. `pwsh -NoProfile -File .\tools\dev.ps1 verify` exits 0 with `"Status":"pass"` and no warnings.
2. Every offline acceptance ID of the slice has at least one passing test tagged with it (xUnit
   trait or Pester tag); the plan's `rg` check lists them all.
3. The slice's live check script exists and passes lint, or its manual acceptance checklist is in
   the plan.
4. Spec §25 rows the slice verifies offline are updated.
5. New French strings are listed for fluent review.
6. The status table below is updated with the date and `project-check` duration.

**Slice-specific additions**

| Slice | Entry, in addition | Exit, in addition | Deferred to the work machine |
| --- | --- | --- | --- |
| 0 | Baseline `verify` pass; authorization for `dotnet restore` and PlatyPS when reached | V-09 and V-17 recorded; DD-023 status updated; package, sign, and install scripts tested with mocks | S0-9; V-07, V-08, V-12, V-14 (projects), V-18; confirm Q-16 and Q-20 defaults |
| 1 | — | Fixture catalog marks V-01, V-03 assumptions | S1-7; V-01, V-02, V-03, V-05, V-10, V-13 |
| 2 | Authorization for the test-only `JsonSchema.Net` package | 30 golden files; JSON schema published | S2-8; V-15 (work item route), V-16 |
| 3 | — | Multi-case golden files; pipeline binding tests per input type | S3-6; V-04, V-06, V-15 (plan/suite route) |
| 4 | — | — | S4-4; V-11, V-14, V-15 (build route) |
| 5 | Edge, Chrome, and the user available for S5-9 | S5-9 home results recorded; `project-check` well under 300 s | S5-10; S5-9 under work policy; V-16, V-19–V-27, V-29 |

## Cross-slice rules

Every plan repeats these in its section 1.

- **Spec first.** The spec is normative; plans give order and scope. On a conflict, stop and ask.
  Acceptance IDs never change. A normative spec change needs the user's agreement and a revision
  entry.
- **One gate.** `tools/dev.ps1 verify` (DD-012). Product checks live in `tools/check.ps1`
  (DD-023); the template's `project-check` stage has a 300-second timeout.
- **Authorization.** Ask before `dotnet restore`, package changes, module or tool installs, or any
  network access. Verification never installs.
- **Offline tests.** Fake `HttpMessageHandler` for Core and a loopback fake server for Pester; no
  ADO, no network.
- **Data boundary.** Synthetic fixtures only (DD-010). At work, live checks persist nothing, and
  acceptance-run outputs stay on the work machine. Only pass/fail, codes, and structural
  descriptions come home (DD-022).
- **Names.** DD-024: no excluded or sensitive directory or file names; malformed or DTD fixtures
  use `.txt`; large inputs are generated.
- **Git.** The developer owns the Git lifecycle; agents use inspection commands only. Creating a
  repository, hosting, and CI are out of plan scope.
- **Sandboxes.** If an agent sandbox blocks a required step (authorized network, the NuGet or
  module cache outside the workspace, loopback sockets or temp files during `verify`), the agent
  requests elevated permission for that command and never adapts tests to the sandbox.
- **Handoff.** Every session reports verify status and exit code, acceptance IDs covered, spec
  rows updated, authorizations used, deviations, open items, and French strings for review.
- **Open-question defaults.** Recorded in spec §24.1; each plan's section 9 lists the ones that
  affect it. To override one, tell the agent before that slice starts. It updates §24.1 with a
  revision entry, then the affected plan.

## Work-machine track (M8)

Precondition: M7. Source moves from home to work only; binaries are never carried (§21), and
nothing from work comes back except the results allowed by DD-022.

| Step | Action |
| --- | --- |
| W-1 | Transfer the source tree (`src/`, `tests/`, `tools/`, `docs/`, root files including lock files) to the work machine by a method the developer chooses. Exclude `artifacts/`, `bin/`, `obj/` |
| W-2 | V-12: `dotnet --list-sdks` shows a 10.0 SDK; `pwsh` is 7.6 at or above the pinned `System.Management.Automation` patch; `dotnet restore src/AdoToolkit.PowerShell/AdoToolkit.PowerShell.csproj --locked-mode` works from the work feed; PlatyPS 1.x is installable |
| W-3 | Q-16: `Get-ExecutionPolicy -List`; note AppLocker/WDAC. If signing is enforced, sign `tools/package/*.ps1` and `tests/Live/*.Live.ps1` in the work copy from an interactive console; those signatures never return home |
| W-4 | Publish: `tools\package\Publish-AdoToolkitPackage.ps1` → `artifacts\AdoToolkit\<version>\` |
| W-5 | Sign: `tools\package\Set-AdoToolkitPackageSignature.ps1 -PackagePath … -CertificateThumbprint … -TimestampServer …` |
| W-6 | Install: `tools\package\Install-AdoToolkitPackage.ps1 -PackagePath … -ExpectedThumbprint …`; in a new `pwsh` session, `Get-Module AdoToolkit -ListAvailable` shows the installed version under the Documents module path |
| W-7 | `Set-AdoProfile` for the work collection |
| W-8 | Run each slice plan's section 8 in slice order: S0-9, S1-7, S2-8, S3-6, S4-4, S5-9 confirmation, S5-10 |
| W-9 | Record results below. For a mismatch, write a structural description, run a fixture-correction session at home (prompt below), pass `verify`, then repeat W-1, W-4 to W-6, and the affected checks |

**Optional early checkpoints** (recommended, not gating). Each runs W-1 to W-8 for the slices done
so far.

| Checkpoint | When | Why |
| --- | --- | --- |
| W0 | After M2 | Windows authentication through the work proxy, signing policy, and installation are the highest risks to M8 |
| W1 | After M3 | Parameter and steps XML formats (V-01, V-03) are based on secondary sources |
| W5 | After Slice 5 session 5.1 (**reached 2026-09-16**) | Test-area shapes (V-19–V-25) are unconfirmed; confirm them before rendering work |

**Fixture-correction kickoff prompt**

> Correct AdoToolkit fixtures after a work-machine finding. Read `AGENTS.md`, the cross-slice rules
> in `docs/plans/README.md`, and the plan for slice <n>. Finding (structural only, no work data):
> <description>. Update the affected fixtures and catalog entries, change code only where the
> spec's assumption changed, record the V-item result in spec §25, and add a revision entry if a
> normative statement changed. Finish with a green `verify` and the handoff.

## Status

### Slices

| Slice | Offline done | Date | `project-check` duration | French review | Notes |
| --- | --- | --- | --- | --- | --- |
| 0 | **Offline done (M2)** | 2026-09-15 | 13.367 s; verify exit 0, pass, no warnings | [Pending fluent review](slice-0-french-review.md) | Sessions 0.1–0.4 complete. S0-1–S0-8 covered; 89 tooling tests, 102 Core tests under each culture, 16 product Pester tests. V-09, V-17 and DD-023 recorded; exact package layout and mocked signing/install checks pass. NuGet lookup/restore and PlatyPS 1.0.3 CurrentUser installation authorized and completed. **Slice 1A may start.** S0-9 and work-only V-items remain deferred. [Handoff and evidence](slice-0-completion-evidence.md). |
| 1 | **Offline done (M3)** | 2026-09-15 | 17.384 s recorded 1C completion verify; exit 0, pass, no warnings | Pending fluent review: [1A](slice-1-session-1A-evidence.md#french-text-for-review), [1B](slice-1-session-1B-evidence.md#french-text-for-review), [1C](slice-1-session-1C-evidence.md#french-text-for-review) | Sessions 1A–1C complete. S1-1–S1-6 covered; capability detection, batched Shared Steps resolution, literal §10.5 expansion, shared parameter joins, and Get-AdoTestCase ById with partial warnings/Strict. V-01/V-03/V-13 offline evidence recorded; V-03 shapes remain assumptions. S1-7 and live V-items remain deferred. No restore, dependency changes, installs or external network. [1C handoff and exact request counts](slice-1-session-1C-evidence.md). **Slice 2 may start.** |
| 2 | **Offline done (M4)** | 2026-09-15 | 18.970 s recorded completion verify; exit 0, pass, no warnings | [Pending fluent review](slice-2-session-2.2-evidence.md#french-text-for-review) | Sessions 2.1–2.2 complete. S2-1–S2-7 covered; 30 golden files, schema, HTML/Markdown/JSON renderers, atomic export and bilingual help complete. User accepted the neutral report design and authorized final styling/logo polish; local previews refreshed and checked. Q-21 revised at user request. S2-8 and live V-15/V-16 remain at work. No new restore, packages, installs or external network. **Slice 3 may start.** [Handoff](slice-2-session-2.2-evidence.md). |
| 3 | **Offline done (M5)** | 2026-09-15 | 3.1: 19.844 s; 3.2: 22.798 s recorded completion verify; exit 0, pass, no warnings | Pending fluent review: 3.1 message resources and help; 3.2 report labels, progress text, NoTestCasesToExport, InputNotTestCase and updated Get-AdoTestCase/Export-AdoTestCase help | Sessions 3.1–3.2 complete. S3-1–S3-5 covered (Core and Pester). 3.2: SuiteMembershipService (depth-first, `order` field, plan and tree once per plan), bulk TestCaseService path with one shared Shared Steps cache and per-membership copies with `Suite`, Get-AdoTestCase sets BySuite (default), ById, BySuiteObject, ByWiql, ByWorkItem with per-type binding proofs, throttled localized progress, multi-case HTML/Markdown/JSON document (cover, suite-grouped TOC, `tc-<id>[-k]` anchors, streamed case bodies), suite/timestamp file names, optional JSON `totals` without a schema version change, six multi-case goldens. §25 V-04/V-15 offline notes updated. No restore, packages, installs or external network. S3-6 and live V-04/V-06/V-15 remain at work. **Slice 4 may start.** |
| 4 | **Offline done (M6)** | 2026-09-15 | 4.1: 25.542 s; 4.2: **26.071 s** recorded completion verify; exit 0, pass, no warnings | Pending fluent review: [4.1 strings and help](slice-4-session-4.1-evidence.md#french-text-for-review), [4.2 strings and help](slice-4-session-4.2-evidence.md#french-text-for-review) | Sessions 4.1–4.2 complete; all five cmdlets and bilingual help. S4-1–S4-3 covered. Save-AdoBuildLog copies bytes atomically, restarts a fresh temp file on retry, enforces Download budgets, uses one tested tail-range function, and handles unknown/zero counts, WhatIf and missing-log continuation. Synthetic timeline/log fixtures cataloged; Triage.Live.ps1 exists and passes lint. §25 V-14 updated with 4.2 evidence; S4-4 and live V-11/V-14/V-15 remain at work. No restore, dependency changes, installs or external network. **Slice 5 entry gate met; current status is below.** [4.1 handoff](slice-4-session-4.1-evidence.md), [4.2 handoff](slice-4-session-4.2-evidence.md). |
| 5 | Sessions 5.1–5.4 implemented and verified; S5-9 pending | 2026-09-16 | 5.4: **31.971 s** recorded release verify before final evidence/status documentation; exit 0, pass, no warnings (entry **40.636 s**) | Pending fluent review: [5.1](slice-5-session-5.1-evidence.md#french-text-for-review), [5.2](slice-5-session-5.2-evidence.md#french-text-for-review), [5.3](slice-5-session-5.3-evidence.md#french-text-for-review), [5.4 help and labels](slice-5-session-5.4-evidence.md#french-text-for-review) | S5-1–S5-8 covered, including attachment download, content/size limits, atomic generation commit, WhatIf/NoClobber and history-error export. 805 Core tests per culture, 83 product Pester tests, 98 tooling tests; no skips. Review fixed nested attachments, timestamp-colliding history windows and invalid-duration overflow; verification now rejects skipped/missing Core tests. Automated Edge checks passed in both cultures. Manual Edge/Chrome S5-9 remains open; **M7 is not yet signed off**. No restore, dependency changes, installs or live ADO calls. [5.4 release evidence](slice-5-session-5.4-evidence.md). |

### Maintenance reviews

| Date | Scope | Result |
| --- | --- | --- |
| 2026-09-16 | Full code review between sessions 5.3 and 5.4 | Fixed: failed-test attachments and `InvalidTestCaseReference` run IDs now match attempts by run and result ID, because result IDs repeat across pipeline run attempts; `TestResultGet` rejects a detail whose ID differs from the request; `requestTimeoutSeconds` is limited to 1–86400 in configuration, `Set-AdoProfile` and every service (larger values failed every request); a numeric configured `historyScope` now falls back to `SameBranch`. Regression tests added; root README rewritten for the product. At this review, the timeout ceiling was not yet recorded in §4/§6.6; the 5.4 documentation now records it. Verify exit 0, pass, no warnings; `project-check` 32.885 s; 798 Core tests per culture and 83 product Pester tests |

### Offline spikes

| Item | Where | Result | Date |
| --- | --- | --- | --- |
| V-09 Pester ordering | Slice 0, session 0.1 | Confirmed: all seven points pass after the DD-023 fixture-isolation correction. [Evidence](slice-0-session-0.1-evidence.md). | 2026-09-15 |
| V-17 Help culture folders | Slice 0, session 0.4 | Confirmed: fr-CA, fr-FR, and fr resolve the single fr/ compiled help file; en-US resolves English. All eight topics pass fresh-process checks. [Evidence](slice-0-completion-evidence.md#v-17-help-culture-lookup). | 2026-09-15 |
| V-01 Oracle (optional) | Slice 1, increment 1B | Not run | |
| V-27 Browsers from `file://` (S5-9, home) | Slice 5, session 5.4 | Automated Edge 153.0.4234.32: 18 checks pass across en-US/fr-CA, no runtime exceptions. Manual Edge/Chrome acceptance pending; Chrome not installed in standard locations. [Evidence](slice-5-session-5.4-evidence.md#browser-check-s5-9--v-27) | 2026-09-16 |

### Live acceptance record (M8)

Pass/fail, dates, and diagnostic codes only (DD-022).

| ID | Kind | Covers | Result | Date | Notes |
| --- | --- | --- | --- | --- | --- |
| S0-9 | Live check | V-07, V-08, V-12, V-14 (projects), V-18; signed installed module | Not run | | |
| S1-7 | Live check + acceptance run | V-01, V-02, V-03, V-05, V-10, V-13 | Not run | | |
| S2-8 | Acceptance run | V-15 (work item route), V-16 | Not run | | |
| S3-6 | Live check + acceptance run | V-04, V-06, V-15 (plan/suite route) | Not run | | |
| S4-4 | Live check + acceptance run | V-11, V-14, V-15 (build route) | Not run | | |
| S5-9 | Browser checklist under work policy | V-27, Q-28 | Not run | | |
| S5-10 | Live check + acceptance run | V-16, V-19–V-26, V-29 | Not run | | |

## Traceability

| Slice | Plan | §22 deliverables | Acceptance IDs |
| --- | --- | --- | --- |
| 0 | [slice-0-foundation.md](slice-0-foundation.md) | 9 | S0-1 … S0-9 |
| 1 | [slice-1-work-items.md](slice-1-work-items.md) | 8 | S1-1 … S1-7 |
| 2 | [slice-2-reports.md](slice-2-reports.md) | 8 | S2-1 … S2-8 |
| 3 | [slice-3-bulk-export.md](slice-3-bulk-export.md) | 7 | S3-1 … S3-6 |
| 4 | [slice-4-pipeline-triage.md](slice-4-pipeline-triage.md) | 5 | S4-1 … S4-4 |
| 5 | [slice-5-failed-test-report.md](slice-5-failed-test-report.md) | 8 | S5-1 … S5-10 |

Each §22 deliverable and acceptance ID appears in exactly one slice plan. The §22 backlog is not
planned.
