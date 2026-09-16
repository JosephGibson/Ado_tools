# ADO Toolkit — Technical Specification

| | |
| --- | --- |
| Working name | `AdoToolkit` |
| Version | 1.0 — accepted |
| Date | 2026-09-15 |
| Status | Accepted 2026-09-15. Implementation follows `docs/plans/` (roadmap plus one plan per slice). Later normative changes need a revision entry. |
| Supersedes | `ADO-Toolkit-Greenfield-Technical-Spec.md` (v0.1) |

---

## 0. About this document

### 0.1 Conventions

- **MUST / SHOULD / MAY** carry their RFC 2119 meanings. Everything else is explanation.
- `DD-nnn` design decisions (§23), `Q-nn` open questions (§24), `V-nn` claims that still need
  confirmation (§25), `S<n>-<m>` slice acceptance criteria (§22).
- **[Doc-checked]** marks the documented portion of a claim checked against primary documentation
  on 2026-09-13; sources are in §26. It does not certify the installed server's behavior.
  **[Verify V-nn]** marks the remaining uncertainty; §25 records partial checks and corrections.
  Verification uses an offline spike or an explicitly invoked work-machine check before the
  dependent slice is accepted. No server is contacted during this review.
- Examples use the reserved `.test` domain and fictional names. This document contains no real
  server, project, or identity data and MUST stay that way (DD-010).

### 0.2 Revision history

#### 2026-09-15 — Owner-requested Test Case report UI refinement

- The owner requested a dark-mode UI pass during session 2.2. Q-21 now selects
  charcoal/graphite screen surfaces, high-contrast text, a restrained cool accent and
  system UI/monospace fonts for Test Case reports. Print output remains light.
- Add section navigation, compact metadata and keyboard-scrollable parameter tables
  within the script-free single-file contract. Data, links and acceptance IDs are unchanged.

#### 2026-09-15 — Slice 0 adapter clarification and help verification

- Record the C# accessibility constraint in §3.3: exported cmdlets derive from a public
  abstract base; supporting infrastructure remains internal.
- Correct §3.4's nonexistent S0-10 cross-reference to the Slice 0 runspace tests.
  Acceptance IDs and behavior are unchanged.
- V-17 passed in fresh PowerShell 7.6.6 processes under en-US, fr-CA, fr-FR, and fr.
  Per Q-14, French help ships once in `fr/`; §21 and the verification ledger record it.
  See [offline evidence](plans/slice-0-completion-evidence.md).

#### 2026-09-15 — V-09 fixture isolation (DD-023 correction)

The owner requested completion of the Slice 0 blockers before Slice 1. The first
V-09 probe found a tooling regression test that invoked the real repository and
assumed every nested stage passed, even when restore assets were deliberately
missing. Isolate that invocation-scope test in fixture repositories and assert both
ready and unavailable product checks. DD-023 allows this narrow tooling-test
correction; gate implementation, acceptance IDs, and missing-assets exit 2 stay
unchanged. Retain §20 option 2: option 1 would not repair the unrelated assertion.
Repeat all V-09 probes before confirming the decision.

#### v1.0 — acceptance

The owner accepted v0.5 as the v1 scope (Slices 0–5). Implementation plans are in `docs/plans/`.
No primary documentation was checked in this pass. Acceptance IDs are unchanged.

| Area | v0.5 | v1.0 |
| --- | --- | --- |
| Status | Draft; no implementation | Accepted; one plan per slice |
| Q-18–Q-20 | Referenced, no rows | Restored and resolved in §24.1: Q-18 by DD-022, Q-19 by DD-023, Q-20 by default |
| Live acceptance (§19.5, §22) | Automated live checks persist nothing, but several live criteria need real exports (Q-18) | DD-022 separates read-only live checks from manual acceptance runs whose outputs stay on the work machine |
| Product gate (§3.5, §20) | V-09 left two options | DD-023: `tools/check.ps1` owns build, test, staged publish, and Pester; product Pester files are `*.Pester.ps1`. The Slice 0 spike confirms V-09 |
| Layout (§3.5, §19.2) | `TestResults/` folders; fixture file types unconstrained | DD-024: names avoid the template's excluded and sensitive paths; `TestResults/` becomes `TestRuns/`; malformed or DTD fixtures use `.txt`; package scripts in `tools/package/` |
| Slice 1 (§22) | Increments mentioned only in v0.4 history | Increments 1A–1C named in §22 |
| Install path (§21) | Literal `$HOME\Documents` | Documents resolved as a known folder so redirection is honored |
| Bulk anchors (§12.5) | `#tc-<id>` repeated when a case is emitted twice | Later occurrences get `#tc-<id>-<k>` |
| Open questions (§24) | 24 open rows | Only Q-12 stays open (not blocking). Other questions get the defaults below. The owner can override any of them |

Defaults recorded in §24.1:

| ID | Default |
| --- | --- |
| Q-02 | Ignore step attachments in v1; inline images stay `[image]` text (§11.2) |
| Q-05 | No historical export in v1; `AsOf` is provenance only |
| Q-06 | Keep multiple named profiles as specified |
| Q-07 | YAML multi-stage and classic through the same Build timeline; full log unless `-Tail` |
| Q-10 | xUnit v3 |
| Q-11 | PlatyPS 1.x Markdown sources, MAML generated at publish |
| Q-13 | `AdoToolkit` and the `Ado` noun prefix are final |
| Q-14 | One neutral `fr` wording tested under `fr-CA`; help folders follow V-17 |
| Q-15 | One language per report |
| Q-16 | Assume Group Policy enforces signing (worst case); sign DLLs; timestamp server is a signing-script parameter |
| Q-17 | Header metadata as proposed; no custom fields |
| Q-18 | DD-022 |
| Q-19 | DD-023 |
| Q-20 | System proxy only; no proxy configuration keys |
| Q-21 | Test Case report keeps §12.2 styling in v1 |
| Q-22 | `SameBranch`, 10 runs; pull-request builds follow their own `sourceBranch` |
| Q-23 | `Failed`, `Error`, `Timeout`, `Aborted`; flaky counts as passed in bars |
| Q-24 | Absolute `http(s)` links only; no path mapping |
| Q-25 | All three attempt sources, equal priority |
| Q-26 | HTML only |
| Q-27 | Limits as specified; no run-level attachments |
| Q-28 | Current Microsoft Edge first, Chrome second; the report must be complete with scripts blocked |
| Q-29 | No timeline links in v1 |
| Q-30 | Dark only on screen; light print styles |

#### v0.5 — pass 4 (failed-test report)

No primary documentation was checked in this pass. Every new Test-area endpoint, field, web route,
and browser behavior is marked [Verify V-19]–[Verify V-29].

| Area | v0.4 | v0.5 |
| --- | --- | --- |
| Scope (§1.3–§1.4, §22) | Slices 0–4; downloading attachments out of scope | New Slice 5: failed-test report for one pipeline run (DD-016). Test *result* attachments are downloaded (DD-020); Test Case step attachments stay Q-02 |
| Test results (§6.2, §15.6–§15.11) | No Test-area endpoints | Build, test run, test result, and attachment endpoints; two-pass retrieval; every attempt kept; flaky derived by the toolkit (DD-019) |
| Report content (§15.12–§15.14) | — | Run history bars with the current run highlighted, per-test history strips, highlighted error messages and stack traces with clickable links, Test Case links, attachment gallery |
| Scripts (§12.2, §15.15, §18) | No script in any report | Static inline scripts allowed by CSP hashes in the failed-test report only (DD-017). The Test Case report stays script-free |
| Visual style (§12.7) | Per-report CSS | Shared dark visual baseline; highlighting and charts rendered in C# (DD-018, DD-021). Test Case report adoption is Q-21 |
| Output (§13.2, §13.4) | Single-file atomic writes | Report plus attachment generation folder, with a crash-consistent commit order |
| Questions/ledger (§24–§25) | Q-02…Q-17 rows; V-01…V-18 | Q-21–Q-30 and V-19–V-29 added. Q-18–Q-20 are referenced by v0.4 text but have no rows; the IDs stay reserved |

#### v0.4 — pass 3 (Codex review)

| Area | Before | After |
| --- | --- | --- |
| Evidence (§25–§26) | Unqualified claims and unresolved placeholders | Primary-source checks distinguish documented contracts, corrections, and work-machine verification; existing IDs retained |
| Binary module (§3, §21) | `PrivateAssets` treated as compile-only; implicit session and async behavior | Exclude runtime assets, inspect package contents, define pipeline-thread pump/cancellation and runspace-owned state; resource and MAML layouts separated |
| HTTP (§5–§6) | Implicit version/proxy settings; optimistic paging and error contracts | Exact HTTP/1.1 request policy, explicit system proxy, bounded paging, optional error fields, and exact Test Plan preview versions |
| Work items/WIQL (§7, §9) | Null-slot batch assumption, presumed fields/expand rejection, broken WIQL pipeline | Reconcile returned IDs, locally exclusive field options pending evidence, explicit WIQL input binding and cap policy |
| Expansion (§10) | Empty root cache, inaccurate request bound, post-emission limit | Seed roots, traverse cached roots at each frontier, count per-level chunks, cap resolution and rows, and define empty/malformed groups |
| Models/bulk (§7, §12, §14) | Missing supporting shapes, repeated HTML IDs, suite-order ambiguity | Explicit types/provenance/source retention, unique occurrence anchors, invocation-wide batching, and documented case order versus provisional suite order |
| Output/security (§11–§13, §18) | Literal Unicode/encoder conflict; unconditional file rollback claim | Preserve rendered Unicode, clarify pre-commit guarantees and race-safe NoClobber, and keep raw content off toolkit telemetry |
| Template fit (§19–§20) | Pester ordering left to two alternatives | Source-checked recommendation: existing `tools/check.ps1` extension owns build/publish/test/Pester; no template file edited |
| Delivery/coverage (§19, §22) | Large Slice 1; acceptance gaps and contradictory live reporting | Three Slice 1 increments retain S1 IDs; section traceability and boundary tests added; DD-010 conflict raised as Q-18 |
| French/scope (§3.6, §24) | Glossary asserted as Server 2020 UI terminology | Per-term evidence with unresolved UI wording; shared-data format and build triage gaps isolated as owner questions |

#### v0.3 — pass 2

| Area | v0.2 | v0.3 |
| --- | --- | --- |
| Step numbering | Flat global number, with outline numbers secondary | Outline numbers (`3`, `3.1`, `3.2`) are the displayed numbers. Each Shared Steps reference is a numbered group row (§10.5.4) |
| Bulk output | Per-case files + index, or one combined document | Every export produces one document. Per-case files moved to the backlog (§12.5) |
| Report content | Collection name only; links as plain text | Server URL, project, work item metadata, and hyperlinks back to ADO. `http(s)` URLs in step text become links (§12.2) |
| Languages | English only; message localization out of scope | Full English and French: messages, diagnostics, help, report labels, French typography. The scripting API stays English (§3.6, §11.5) |
| Signing | Open question | Packages are Authenticode-signed on the work machine before installation (§21.1) |

#### v0.2 — pass 1 (changes from v0.1)

| Area | v0.1 | v0.2 |
| --- | --- | --- |
| Platform | Server and Services, Entra, PAT, `7.1` APIs | Azure DevOps Server 2020 only, Windows auth, `6.0` APIs (DD-003, DD-004) |
| Implementation | PowerShell script module first, C# "later if justified" | C# core library + compiled cmdlets from day one (DD-001) |
| ADO writes | Designed into v1 | v1 is read-only against ADO (DD-009) |
| Rich text | Sanitized HTML discussed | Plain text only, encoded at every output (DD-008) |
| Shared Steps | Recursive fetch per reference | Two-phase: batched level-by-level fetch, then pure in-memory expansion (DD-007) |
| `compref` resolution | Fell back to the `id` attribute | `ref` only. On a `compref`, `id` is the step's own position id, so the fallback would expand the wrong work item |
| Expansion limits | Depth only | Depth plus a total expanded-step limit (repeated references can grow exponentially without any cycle) |
| Test Case detection | "Steps field present" | Steps field present, or the type belongs to the stable `Microsoft.TestCaseCategory` / `Microsoft.SharedStepCategory` categories |
| Parameters | Not covered | `@tokens` kept literal; parameter table model, including shared parameter sets |
| Roadmap | Work items → tests → reports → pipelines → repos | Tests → reports → bulk test export → pipeline failure triage. Repos/PRs and writes go to the backlog |
| Verification | New `scripts/Invoke-Verify.ps1` | The template's `tools/dev.ps1 verify` stays the single gate (DD-012) |
| Test data | Fixtures, source unstated | Synthetic fixtures only; work data never leaves work (DD-010) |
| Report opening | Auto-open by default | Opt-in `-Open` (bulk export would otherwise open hundreds of files) |
| Removed | — | CSV reports, `Get-AdoTestStep`, `Get-AdoSharedStep`, generic `Export-AdoReport`, concurrency, persistent cache, `scripts/` folder |

### 0.3 Relationship to the project template

This spec is designed to be implemented inside the agent-ready project template:

- Run `/template-init` (Claude Code) or `$template-init` (Codex) with project name `AdoToolkit`
  at the start of Slice 0 (`docs/plans/slice-0-foundation.md`).
- The template's rules still apply: product code in `src/`, product tests in `tests/`, and
  `tools/dev.ps1 verify` as the handoff gate. Tests never touch live services, generated files use
  atomic replacement, and no credentials or customer URLs appear in the repository or agent output.
- This file lives in `docs/` and is not always-loaded agent context. Implementers read the
  sections for the slice they are working on.

---

## 1. Purpose and scope

### 1.1 Purpose

`AdoToolkit` is a PowerShell toolkit for Azure DevOps Server. It makes routine ADO work faster
and scriptable. It returns structured objects that compose in pipelines, and produces readable
reports where people need a document.

The first deep capability is Test Case retrieval with recursive Shared Steps and parameter
tables (`@tokens` stay literal), rendered as standalone reports. It crosses every hard boundary once: connection,
authentication, HTTP, batching, XML parsing, graph traversal, rich-text conversion, diagnostics,
rendering, and atomic file output. Later slices reuse that infrastructure.

### 1.2 Users and usage modes

| Mode | Where | Needs |
| --- | --- | --- |
| Interactive | pwsh on the work machine | Discoverable cmdlets, sensible defaults, readable table views, one-command reports, messages and help in English or French |
| Scripted | pwsh scripts / scheduled tasks at work | Non-interactive parameters, predictable objects, error records, exit behavior |
| Development | Home PC (no ADO access) and work machine | Fully offline build and test, fast feedback, synthetic fixtures |

### 1.3 In scope for v1 (Slices 0–5, §22)

1. Connection profiles and Windows integrated authentication to one Server 2020 collection.
2. A shared HTTP pipeline: endpoint registry, batching, pagination, retry, timeouts, errors, cancellation.
3. Work item retrieval by ID.
4. Test Case and Shared Steps retrieval with recursive expansion, parameters, and diagnostics.
5. Test Case reports in HTML, Markdown, and JSON, with links back to ADO.
6. Bulk Test Case retrieval by Test Plan/Suite or WIQL query, exported as one combined document.
7. Pipeline failure triage: latest build, failed stages/jobs/tasks, issues, failed-task logs.
8. Full English and French support for all user-facing text, and correct handling of French content.
9. A failed-test report for one pipeline run (§15.6): failed and flaky tests with every attempt,
   highlighted error messages and stack traces, Test Case links, downloaded result attachments,
   and run history, in an interactive dark HTML report.

### 1.4 Out of scope for v1

- Azure DevOps Services, Microsoft Entra ID, PATs, and Azure DevOps Server 2022 or later
  (extension points are kept; §5.3, §6.2).
- **Any** create/update/delete/queue operation against ADO.
- Preserving rich formatting (sanitized HTML), and downloading Test Case step images or
  attachments (Q-02). Test *result* attachments for the failed-test report are in scope
  (§15.13, DD-020).
- Passed-test reporting, run-level test attachments, and comparisons between arbitrary runs; the
  failed-test report covers failures of one run (Q-26, Q-27).
- Repositories, pull requests, artifacts, work item mutation, and general-purpose WIQL tooling
  beyond hydration.
- Parallel requests, persistent disk caches, a standalone `ado` executable or TUI.
- Translating ADO content. The toolkit localizes its own text, not test case data (§3.6).
- Languages other than English and French.

### 1.5 Prior art and build-versus-buy

| Option | Assessment |
| --- | --- |
| **VSTeam** PowerShell module | Not adopted. This review does not rely on an exhaustive claim about its current feature coverage; the selected approach is DD-002. |
| **Microsoft .NET client libraries** (`Microsoft.TeamFoundationServer.Client`) | Typed clients with server version negotiation. Heavy dependency tree (e.g. Newtonsoft.Json), which collides with PowerShell's own assemblies and would need load-context isolation (§3.2). Not a runtime dependency. The package's Test Management helper, which parses step XML, MAY serve as an **offline test oracle** in a spike (V-01). |
| **Own thin REST layer** over `HttpClient` | v1 needs about 20 endpoints. Owned DTOs, BCL-only, fully fakeable in tests. **Adopted** (DD-002). Revisit if endpoint count passes ~40 or DTO upkeep dominates. |

---

## 2. Target environment

### 2.1 Server and API versions

- Azure DevOps Server 2020, on-prem, addressed by **collection URL**.
- Server 2020 supports REST API versions up to **6.0**; 7.0 is not available. **[Doc-checked]**
- Every request carries an explicit `api-version`, supplied only by the HTTP pipeline (§6.2).
- Some capabilities exist on 6.0 only as preview versions, e.g. listing a suite's test cases is
  `6.0-preview.2`. **[Doc-checked]** On Azure DevOps Services, preview versions are deactivated
  some weeks after release. An on-prem server exposes a fixed version set per server build, so
  pinning an exact preview version (`6.0-preview.2`, never bare `-preview`) is acceptable here.
  A server upgrade requires re-running the live checks (§19.5).

### 2.2 Runtime

| Component | Requirement |
| --- | --- |
| PowerShell | 7.6, `pwsh.exe`, Windows; development tooling additionally requires 7.6.5+ per the template. Pin the tested patch at implementation time |
| .NET | PowerShell 7.6 runs on .NET 10 **[Doc-checked]**; all projects target `net10.0` |
| C# | C# 14, nullable reference types enabled, warnings as errors |
| SDK | .NET 10 SDK pinned by `global.json` (`rollForward: latestFeature`) |
| Module manifest | `PowerShellVersion = '7.6'`, `CompatiblePSEditions = @('Core')` |
| OS | Windows only (Windows authentication, Windows known folders) |
| Languages | English and French user-facing text; English is the neutral fallback (§3.6) |

### 2.3 Machines and the data boundary

| | Home development PC | Work machine |
| --- | --- | --- |
| ADO access | None | Yes |
| .NET 10 SDK | 10.0.400 present | C# toolchain present; exact SDK band [Verify V-12] |
| Package feeds | nuget.org | NuGet and npm reachable [Verify V-12 for NuGet] |
| Permitted data | Synthetic only | Real |

**Rule (DD-010):** Nothing from the work environment leaves it or enters the repository. That
covers payloads, names, URLs, identities, logs, and generated reports. Fixtures are written by
hand. Live checks at work report pass/fail and diagnostic codes only (§19.5).

Reports, logs, and downloaded test result attachments generated at work are local work artifacts.
They may freely contain server URLs, metadata, and links (§12.2, §15.13); the rule only stops them
leaving the work environment or entering the repository.

---

## 3. Architecture

### 3.1 Overview

```text
PowerShell session
  └─ AdoToolkit  (binary module)                       src/AdoToolkit.PowerShell
       cmdlets · parameter binding · ShouldProcess · error records
       format views · argument completers · session connection
            │  calls async APIs
            ▼
     AdoToolkit.Core  (class library)                   src/AdoToolkit.Core
       Configuration · Connections · Http · Diagnostics
       WorkItems · TestManagement · RichText · Builds
       TestResults · Reporting · IO
            │  HttpClient (Negotiate)
            ▼
     Azure DevOps Server 2020 REST (6.0)
```

Rendering is separate from retrieval:

```text
Domain objects ──► Report model ──► Renderer (Html | Markdown | Json) ──► Atomic file writer
```

### 3.2 Dependency rules

- `AdoToolkit.Core` MUST NOT reference `System.Management.Automation`. That keeps it testable
  without PowerShell and reusable by a future executable.
- `AdoToolkit.PowerShell` references Core, plus a centrally pinned `System.Management.Automation`
  7.6.x package for compilation: `PrivateAssets="all"` AND `ExcludeAssets="runtime"`.
  `PrivateAssets` alone controls transitive exposure; it does not suppress runtime copy/publish
  assets. **[Doc-checked]** The package MUST NOT ship `System.Management.Automation.dll`,
  PowerShell's dependency assemblies, or a private .NET runtime. Inspect the complete publish
  output, including transitive assets (§19.4); the host supplies PowerShell at runtime.
- In Core, the HTTP layer knows no domain types. Domain services never render. Renderers do no
  I/O of their own; they write to a `TextWriter` supplied by the IO layer, so large documents
  stream to the temporary file.
- **v1 runtime dependencies are BCL-only** (`System.Net.Http`, `System.Text.Json`, `System.Xml`,
  `System.Text.Encodings.Web`) (DD-011). Modules commonly share the default assembly load context
  with PowerShell; custom load contexts are possible. A third-party dependency can conflict with
  PowerShell or other modules and needs an isolation design. **[Doc-checked]** Adding one requires a DD entry and a
  load-isolation plan.

### 3.3 Async work and the pipeline thread

- Core APIs are `async` and accept a `CancellationToken`.
- Cmdlets derive from a public abstract `AdoCmdletBase` (C# requires an exported class's base
  to be at least as accessible); supporting infrastructure stays internal. `BeginProcessing`, `ProcessRecord`, and
  `EndProcessing` remain synchronous; no `async void`. `WriteObject` and the other pipeline
  `Write*` methods MUST run on the thread executing those callbacks. **[Doc-checked]**
- Core work sends typed events through a bounded queue. The active callback drains the queue
  and calls `Write*` while waiting for the task to complete. Blocking on the task without draining
  the queue is forbidden. Do not assume PowerShell installs a synchronization context that
  marshals `await` continuations. Workers MUST NOT access `SessionState`, provider APIs,
  `ShouldProcess`, or command runtime methods.
- `StopProcessing` cancels the invocation token and wakes the pump; it MUST NOT write to streams
  or wait for the worker. Queue writes/waits, HTTP, parsing/expansion loops, and rendering MUST
  observe cancellation. The callback observes task failures and disposes resources in `finally`,
  including when downstream stops early. `EndProcessing` is not a guaranteed cleanup hook.
  Preserve `PipelineStoppedException`; user cancellation is not a retryable timeout.
- Core reports operational messages through a small `IAdoLog` interface (no
  `Microsoft.Extensions.Logging`, per DD-011). The cmdlet adapter maps it to Verbose/Debug/Warning.

### 3.4 Session state

- Connection and metadata state MUST belong to one runspace and survive individual cmdlet
  instances. Do not use a static current connection or assume a binary module has a script
  module's private variable scope. A runspace-keyed weak registry of state holders is acceptable;
  resolve its holder on the pipeline thread. New runspaces start disconnected. Runspace session
  isolation is documented **[Doc-checked]**; prove the binary adapter with the Slice 0 runspace tests.
- Each holder owns long-lived `HttpClient` instances per connection key (collection URI and
  authentication mode; system proxy policy is fixed in v1). Reuse connections across commands;
  do not create a client per request. `Connect-Ado`, `Disconnect-Ado`, and runspace disposal release
  only that holder's clients/caches after in-flight work ends. Different runspaces MUST NOT
  dispose each other's resources. No impersonation support in v1.
- Request-specific headers, including `Accept-Language`, belong to `HttpRequestMessage`;
  do not mutate a reused client's defaults during an invocation.

### 3.5 Repository layout (mapped onto the template)

```text
src/
  AdoToolkit.Core/
    Configuration/ Connections/ Http/ Diagnostics/
    WorkItems/ TestManagement/ RichText/ Builds/
    TestRuns/                     Slice 5 retrieval, attempts, history (§15.6); not TestResults/ (DD-024)
    Reporting/ IO/
    Reporting/Assets/             report CSS and JS as embedded resources (§12.7, §15.15)
    Resources/                    Strings.resx (English, neutral), Strings.fr.resx
  AdoToolkit.PowerShell/
    Commands/                     one file per cmdlet
    Completion/
    Resources/                    Strings.resx, Strings.fr.resx
    AdoToolkit.Format.ps1xml
    AdoToolkit.psd1
tests/
  AdoToolkit.Core.Tests/          IsTestProject=true; unit, HTTP-contract, golden tests
  AdoToolkit.PowerShell.Tests/    Pester tests (*.Pester.ps1) against the staged module (DD-023)
  Fixtures/                       Rest/ Steps/ Parameters/ Timelines/ TestRuns/ Attachments/
                                  Reports/ + README catalog
  Live/                           *.Live.ps1: opt-in, work machine only, never run by verify
docs/
  ado-toolkit-spec.md             this document
  commands/                       command help sources, one folder per language
  schemas/testcase.v1.schema.json
tools/
  check.ps1                       product gate (§20, DD-023)
  package/                        publish, sign, and install scripts (§21, §21.1); tests in tools/tests/
global.json
Directory.Build.props             Nullable, TreatWarningsAsErrors, AnalysisLevel, version
Directory.Packages.props          central package versions (test/build-time packages only in v1)
AdoToolkit.slnx
```

v0.1's `scripts/` folder is dropped. Build and verification go through the template (§20).

### 3.6 Languages and localization

English and French are both first-class (DD-013). The toolkit localizes **its own** text; ADO
content is never translated.

| Surface | Localized | Rule |
| --- | --- | --- |
| Cmdlet, parameter, property, and enum names; `FullyQualifiedErrorId`; diagnostic codes; JSON property names; table column headers | No, English | These are the scripting API. Scripts must behave identically in either language, as with PowerShell itself |
| Error, warning, verbose, and progress messages | Yes | `.resx` resources |
| Diagnostic messages | Yes | Stored as code + arguments and rendered in the target culture when displayed or exported |
| Report labels, headings, dates, and numbers | Yes | Report culture (below) |
| Command help | Yes | Help files per culture folder |
| ADO content (titles, steps, names, server error text) | Passed through | Never translated. French typography preserved (§11.5) |

**Culture selection**

- **Shell messages:** capture the session UI culture (`$PSUICulture`) at invocation entry.
  ResourceManager falls back from a regional culture to `fr` to neutral English. **[Doc-checked]**
  **Help:** PowerShell's separate locale lookup (§21), not ResourceManager.
- **Reports:**
  1. `-Culture` on `Export-AdoTestCase` or `Export-AdoBuildTestFailure`, else
  2. config `reporting.culture`, else
  3. the session UI culture.

  Only `en*` and `fr*` are accepted; anything else falls back to English with a warning. Report
  language is chosen separately because a French-speaking tester and an English-speaking
  developer share the same reports.
- **HTTP:** requests send `Accept-Language` for the session UI culture as a preference, not a
  guarantee. Server error localization remains [Verify V-18]. Toolkit hints use the toolkit language.

**Implementation rules**

- Core never takes behavior from ambient culture. APIs that produce human text take an explicit
  `CultureInfo`, which the cmdlet layer supplies.
- Machine formats always use the invariant culture: URLs, WIQL, JSON, file names, API dates, and
  numbers. Analyzer rules CA1304, CA1305, CA1307, CA1309, CA1310 and CA1311 are build errors.
  Identifiers compare ordinally (ignoring case where ADO does).
- Neutral English resources live in `Strings.resx`, French in `Strings.fr.resx` (satellite
  assemblies under `fr/` beside each main assembly, generated by the SDK). Set the neutral
  resource language to `en`; keep neutral resources in the main assembly. **[Doc-checked]**
  Resources MUST contain strings only. Regional overrides (`fr-CA`, `fr-FR`) are added only if
  Q-14 requires different wording; regional date/number formatting does not require satellites.
- Before a slice is accepted, a fluent speaker reviews its French strings. Terminology follows
  the glossary.

**Glossary** — primary French Learn usage checked below (sources in §26). These pages now
cover newer Server releases even with an older view requested; they do not certify the exact
Server 2020 language-pack labels. That final UI comparison remains [Verify V-16].

| English | Français | Evidence / remaining check |
| --- | --- | --- |
| Test Case | Cas de test | **[Doc-checked]** French test-case guide |
| Shared Steps | Étapes partagées | **[Doc-checked]** French shared-steps guide |
| Shared Parameters | Paramètres partagés | **[Doc-checked]** French parameter guide |
| Step | Étape | **[Doc-checked]** French test-case guide |
| Action | Action | **[Doc-checked]** French test-case guide |
| Expected Result | Résultat attendu | **[Doc-checked]** French test-case guide |
| Parameters | Paramètres | **[Doc-checked]** French parameter guide |
| Test Plan | Plan de test | **[Doc-checked]** French test-case guide |
| Test Suite | Suite de tests | **[Doc-checked]** French test-case guide |
| Work item | Élément de travail | **[Doc-checked]** French shared-steps guide |
| State | État | **[Doc-checked]** French numeric-query guide |
| Assigned To | Assigné à | **[Doc-checked]** French numeric-query guide; other Learn pages also use « Affecté à »; verify UI |
| Priority | Priorité | Not confirmed in the inspected text; [Verify V-16] |
| Area Path | Chemin de zone | **[Doc-checked]** French area/iteration guide; also uses « Chemin d’accès à la zone »; verify UI |
| Iteration Path | Chemin d’itération | **[Doc-checked]** French area/iteration guide |
| Build | Build | Provisional; Learn also uses « génération » in test-plan prose; [Verify V-16] |
| Generated on | Généré le | Toolkit wording, no matching ADO UI label confirmed; fluent-speaker review |
| Test run | Série de tests | Provisional (Slice 5); [Verify V-16] |
| Test result | Résultat de test | Provisional (Slice 5); [Verify V-16] |
| Passed / Failed / Other | Réussi / Échec / Autre | Provisional (Slice 5); [Verify V-16] |
| Flaky | Instable | Provisional toolkit wording (Slice 5); [Verify V-16] |
| Attempt | Tentative | Provisional toolkit wording (Slice 5); fluent-speaker review |
| Attachment | Pièce jointe | Provisional (Slice 5); [Verify V-16] |
| Error message | Message d’erreur | Provisional (Slice 5); [Verify V-16] |
| Stack trace | Arborescence des appels de procédure | Provisional (Slice 5); [Verify V-16] |

---

## 4. Configuration and connections

### 4.1 Configuration file

- Location: `%APPDATA%\AdoToolkit\config.json`. Tests use an `ADOTOOLKIT_CONFIG_PATH`
  environment-variable override so they never read or write the real file.
- Format: JSON with `schemaVersion`. Written atomically (§13.1).
- It MUST NOT contain secrets. v1 has none to store.
- Unknown properties produce a warning and are preserved on write.
- Older `schemaVersion` values migrate forward in memory. A newer `schemaVersion` is read-only,
  and writes are refused with a clear error.

```json
{
  "schemaVersion": 1,
  "defaultProfile": "work",
  "profiles": {
    "work": {
      "collectionUrl": "https://ado.example.test/DefaultCollection",
      "defaultProject": "Contoso Web",
      "authentication": "WindowsIntegrated",
      "requestTimeoutSeconds": 100
    }
  },
  "testCases": {
    "maximumSharedStepDepth": 10,
    "maximumExpandedSteps": 5000,
    "maximumResolvedWorkItems": 10000
  },
  "testResults": {
    "historyCount": 10,
    "historyScope": "SameBranch",
    "maximumReportedFailures": 1000,
    "maximumHistoryRequests": 400,
    "maximumAttachmentBytes": 52428800,
    "maximumTotalAttachmentBytes": 524288000,
    "maximumInlineJsonBytes": 262144
  },
  "reporting": {
    "culture": "fr-CA"
  }
}
```

### 4.2 Collection URL normalization

Applied once, when a profile is saved or `Connect-Ado` runs. The result is an absolute `Uri`
with no trailing slash.

1. Trim whitespace and one pair of matching surrounding quotes.
2. Require an absolute `https` or `http` URI. `http` is allowed with a warning, because Windows
   auth over plain HTTP exposes the NTLM exchange.
3. Reject user info (`user:pass@`), query strings, and fragments.
4. Reject paths containing `/_apis`, `/_git`, `/_workitems` and similar UI or API segments. The
   error suggests the collection URL.
5. Reject `dev.azure.com` and `*.visualstudio.com` hosts: Services is not supported in v1.
6. Preserve path segments and their case, e.g. `/tfs/DefaultCollection`. Remove trailing slashes.

A project URL passed as the collection URL can't be detected reliably offline.
`Test-AdoConnection` (§16) MAY suggest this cause after a projects-call failure; a 404 alone
does not prove that the URL contains a project.

### 4.3 Connection resolution

`Connect-Ado` resolves its target in this order: `-CollectionUrl`, then `-Profile`, then
`defaultProfile`, otherwise an error. Every network cmdlet accepts `-Connection`, which overrides
the session connection. `-Project` defaults to the connection's default project.

### 4.4 Cross-connection safety

Every independently piped ADO entity/result carries `CollectionUri` (nested value objects may
inherit provenance from their owner). If an object from one connection is piped into a
command using a different connection, the command writes a `ConnectionMismatch` error for that
input instead of querying the wrong server.

---

## 5. Authentication

### 5.1 v1: Windows integrated authentication

- `SocketsHttpHandler.Credentials = CredentialCache.DefaultCredentials`; Negotiate can select
  Kerberos or NTLM. Set `UseProxy = true`, `Proxy = null`, and
  `DefaultProxyCredentials = CredentialCache.DefaultCredentials` for the system proxy.
  The latter property applies only to that default proxy. **[Doc-checked]** No custom proxy
  config keys in v1; a work-network exception is Q-20.
- Every request MUST set `Version = HttpVersion.Version11` and
  `VersionPolicy = HttpVersionPolicy.RequestVersionExact`. HTTP/1.1 is a conservative v1 policy
  for Windows authentication; authentication over HTTP/2 requires a downgrade. **[Doc-checked]**
- Set `AllowAutoRedirect = false`, `UseCookies = false`, and a finite
  `PooledConnectionLifetime` (initial policy: 15 min; not a config key). Keep normal TLS
  certificate validation. Redirects are errors; no automatic reissue to the target.
  Diagnostic targets MUST discard user info/query/fragment and encode control characters.
- The toolkit stores no passwords/tickets. `Authorization`, `Proxy-Authorization`, cookies,
  and authentication challenge headers MUST never be logged. Remote text can still contain
  sensitive material (§18); default credentials do not make arbitrary response text safe.
- Work-network behavior (Kerberos vs NTLM, proxy) [Verify V-08].

### 5.2 Failure guidance

A 401 becomes `AdoAuthenticationException`. Its remediation hint covers the user identity in use,
the collection URL, and proxy or Negotiate interference. Response bodies from IIS 401 pages are
HTML, so they are never parsed as JSON and never echoed.

### 5.3 Extension seam

`IAdoCredentialProvider` configures the handler or request. v1 ships only `WindowsIntegrated`.
A future provider (for example a PAT for Server) requires an ADR that picks a secret store
(Q-12).

---

## 6. HTTP pipeline

### 6.1 Responsibilities

The pipeline owns URI composition, `api-version` injection, JSON (de)serialization, pagination,
ID chunking, retry, timeouts, cancellation, error translation, correlation IDs, and request
logging. Domain services MUST NOT re-implement any of these.

### 6.2 Endpoint registry

Every endpoint is declared once, as data:

```csharp
internal sealed record EndpointDefinition(
    string Name,              // "WorkItemsBatch"
    HttpMethod Method,
    string RouteTemplate,     // "{project}/_apis/wit/wiql"
    string ApiVersion,        // exact, e.g. "6.0" or "6.0-preview.2"
    PagingStrategy Paging,    // None | ContinuationHeader | TopSkip | IdChunks(200)
    bool IsSafeToRetry,       // GET, and read-only POSTs such as workitemsbatch and wiql
    TimeoutClass Timeout);    // Metadata | Query | Download
```

v1 endpoint set (`{c}` = collection URL, `{p}` = project):

| Name | Method | Route | Version | Paging | Status |
| --- | --- | --- | --- | --- | --- |
| ProjectsList | GET | `{c}/_apis/projects` | 6.0 | TopSkip; §6.4 termination | **[Doc-checked]** parameters; [Verify V-14] server paging |
| WorkItemTypeCategory | GET | `{c}/{p}/_apis/wit/workitemtypecategories/{category}` | 6.0 | None | **[Doc-checked]** route; [Verify V-13] category names |
| WorkItemsBatch | POST | `{c}/_apis/wit/workitemsbatch` | 6.0 | IdChunks(200) | **Doc-checked** |
| Wiql | POST | `{c}/{p}/_apis/wit/wiql` | 6.0 | None (cap, §9.2) | **[Doc-checked]** route and `$top`; [Verify V-06] cap behavior |
| TestPlansList | GET | `{c}/{p}/_apis/testplan/plans` | 6.0-preview.1 | ContinuationHeader | **[Doc-checked]** V-04 |
| TestSuitesForPlan | GET | `{c}/{p}/_apis/testplan/Plans/{planId}/suites` | 6.0-preview.1 | ContinuationHeader | **[Doc-checked]** V-04 |
| SuiteTestCaseList | GET | `{c}/{p}/_apis/testplan/Plans/{planId}/Suites/{suiteId}/TestCase` | 6.0-preview.2 | ContinuationHeader (`x-ms-continuationtoken`) | **Doc-checked** |
| BuildDefinitionsList | GET | `{c}/{p}/_apis/build/definitions` | 6.0 | ContinuationHeader | **[Doc-checked]** version/token parameter; [Verify V-14] response header |
| BuildsList | GET | `{c}/{p}/_apis/build/builds` | 6.0 | ContinuationHeader | **[Doc-checked]** version/token parameter; [Verify V-14] response header |
| BuildTimeline | GET | `{c}/{p}/_apis/build/builds/{buildId}/timeline` | 6.0 | None | **Doc-checked** |
| BuildLogsList | GET | `{c}/{p}/_apis/build/builds/{buildId}/logs` | 6.0 | None; `Accept: application/json` | **[Doc-checked]** V-14 |
| BuildLog | GET | `{c}/{p}/_apis/build/builds/{buildId}/logs/{logId}` | 6.0 | None; `Accept: text/plain` | **[Doc-checked]** range parameters; [Verify V-14] indexing |
| BuildGet | GET | `{c}/{p}/_apis/build/builds/{buildId}` | 6.0 | None | [Verify V-25] route and fields |
| TestRunsList | GET | `{c}/{p}/_apis/test/runs` (`buildUri`, `includeRunDetails`) | 6.0 | TopSkip; §6.4 termination | [Verify V-19] |
| TestResultsList | GET | `{c}/{p}/_apis/test/Runs/{runId}/results` | 6.0 | TopSkip; §6.4 termination | [Verify V-20] version, `$top` maximum, fields |
| TestResultGet | GET | `{c}/{p}/_apis/test/Runs/{runId}/results/{resultId}` (`detailsToInclude`) | 6.0 | None | [Verify V-21] |
| TestResultAttachmentsList | GET | `{c}/{p}/_apis/test/Runs/{runId}/Results/{resultId}/attachments` | 6.0-preview.1 | None | [Verify V-23] version and route |
| TestSubResultAttachmentsList | GET | same route, `testSubResultId={subResultId}` | 6.0-preview.1 | None | [Verify V-23] |
| TestResultAttachmentContent | GET | `…/Results/{resultId}/attachments/{attachmentId}` (plus `testSubResultId` for sub-results) | 6.0-preview.1 | None; `Accept: application/octet-stream`; Download timeout class | [Verify V-23] |

The single-item `GET _apis/wit/workitems/{id}` is not needed: batch covers one ID too.

### 6.3 Request construction

- Route values are escaped per segment (`Uri.EscapeDataString`), because project names contain
  spaces and non-ASCII characters. Query strings are built with an encoder, never concatenated.
- JSON uses `System.Text.Json` source-generated contexts. DTOs are internal. Domain mapping
  converts field values to .NET primitives: `string`, `long`, `double`, `bool`, `DateTimeOffset`,
  and `AdoIdentityRef`. `JsonElement` never reaches PowerShell.
- Every request sends `Accept-Language` (§3.6). Dates and numbers in requests are formatted with
  the invariant culture.

### 6.4 Pagination and chunking

- **ContinuationHeader:** read the `x-ms-continuationtoken` response header and pass it back as
  the `continuationToken` query parameter until it is absent/empty. Treat tokens as opaque and
  encode once. A short or empty page with a token MUST NOT end enumeration.
- **TopSkip (ProjectsList, TestRunsList, TestResultsList):** request a fixed `$top` (for test
  results, at most the documented maximum for the requested `detailsToInclude` [Verify V-20]), advance `$skip` by the number actually
  returned, and stop on an empty page. A short page alone does not prove completion; the server
  may cap page size. Do not mix offsets and continuation tokens in the same enumeration.
- **IdChunks(n):** split ID lists into chunks of at most *n*, request sequentially, and reassemble
  results in input order.
- Page size and the caller's result limit (`-Top`) are separate. The pipeline stops fetching once
  the limit is satisfied.
- Repeated continuation tokens, repeated offset pages, or more than 10,000 pages per enumeration
  MUST fail with `AdoResponseFormatException`; the page ceiling is an internal guard. No silent
  success after a paging failure. Test both guards and short nonterminal pages (§19.4).

### 6.5 Retry

- Retry only when `IsSafeToRetry` is true. Retry on 429, 502, 503, 504, and connection-level
  transient `HttpRequestException`/`IOException`. Do not retry certificate/authentication,
  invalid-response, file, or cancellation failures. Do not retry 500 as a v1 policy; no claim
  that all server 500s are deterministic.
- At most 3 attempts. Honor `Retry-After` (capped at 60 s). Otherwise wait 1 s, then 2 s, ±20%
  jitter.
- Every retry is logged at Verbose with attempt number and reason.
- Recreate request/content per attempt and dispose responses. `Retry-After` accepts delta seconds
  or an HTTP date; test time/jitter with injected abstractions. A log or attachment download retry
  MUST restart a fresh temporary file; never append a repeated prefix to a partially written file.

### 6.6 Timeouts

- `HttpClient.Timeout` is infinite. Each request gets a linked `CancellationTokenSource` for its
  timeout class.
- Metadata and Query: 100 s by default (profile `requestTimeoutSeconds`).
- Download: 10 min total and 60 s of inactivity.
- Timeouts raise `AdoTimeoutException`, which is distinct from user cancellation.
- The per-operation budget includes attempts, response-body consumption, and retry delays.
  Use `ResponseHeadersRead` for downloads; the body read loop owns the inactivity timeout.
  Timeouts are not retried in v1; cancellation of the caller token always takes precedence.

### 6.7 Error translation

| Response | Exception | Retryable |
| --- | --- | --- |
| 3xx | `AdoRedirectException` (includes target) | No |
| 400 | `AdoRequestException`. An api-version rejection gets an "endpoint registry mismatch" hint | No |
| 401 | `AdoAuthenticationException` | No |
| 403 | `AdoAuthorizationException` | No |
| 404 | `AdoNotFoundException` | No |
| 409 / 412 | `AdoRequestException` | No |
| 429 | `AdoThrottledException` (after retries) | Yes |
| 5xx | `AdoServerException` | 502/503/504 |
| Timeout | `AdoTimeoutException` | No |
| Unparseable body | `AdoResponseFormatException` | No |

- `message`, `typeKey`, and the `ActivityId` response header are optional candidates, not a
  universal Server 2020 contract [Verify V-07]. Status-based translation MUST work when all are
  absent, of unexpected types, or the error body is invalid JSON. An invalid error body MUST NOT
  hide a 401/403 or change retry policy; `AdoResponseFormatException` covers malformed success
  payloads. Bound/sanitize remote messages and correlation text before display. Non-JSON error
  bodies are never echoed; successful text log bodies follow §15.4.
- Exceptions carry the operation (endpoint name), status, resource identifiers, project,
  correlation ID, and retryability. They never carry headers or credentials.

### 6.8 Request logging

- **Verbose:** `POST WorkItemsBatch 200 143 ms ids=200 attempt=1` — endpoint name and key
  resource identifiers.
- **Debug:** route/operation and page counts; omit query values, continuation tokens, and WIQL.
  Real resource identifiers in work-session output stay local (§2.3).
- Headers and bodies are never logged.

---

## 7. Domain model

### 7.1 Conventions

- Public sealed C# classes with init-only properties and `IReadOnlyList<T>` collections.
  Nullability is annotated.
- Timestamps are `DateTimeOffset` in UTC; format views show local time.
- Every cmdlet declares `[OutputType]`. `AdoToolkit.Format.ps1xml` provides default table views.
- Independently piped objects carry `CollectionUri` (§4.4). They do not keep raw JSON. `AdoWorkItem.Fields` gives
  forward-compatible access to any field.

### 7.2 Types in v1

| Type | Slice | Purpose |
| --- | --- | --- |
| `AdoConnection`, `AdoProfile`, `AdoConnectionTestResult` | S0 | Connection state and diagnostics |
| `AdoProject`, `AdoIdentityRef` | S0 | Discovery, identity fields |
| `AdoWorkItem`, `AdoWorkItemRelation` | S1 | Generic work items |
| `AdoTestCase`, `AdoTestStep`, `AdoSharedStepInfo`, `AdoTestParameters` | S1 | Test step model |
| `AdoDiagnostic` | S1 | Partial-result diagnostics |
| `AdoTestPlan`, `AdoTestSuite`, `AdoWiqlResult` | S3 | Bulk export inputs |
| `AdoTestSuiteRef`, `AdoSharedParameterInfo` | S1 / S3 | Membership and parameter provenance (§7.4) |
| `AdoBuildDefinition`, `AdoBuild`, `AdoTimelineRecord`, `AdoBuildFailure` | S4 | Failure triage |
| `AdoTestRun`, `AdoBuildTestFailureSet`, `AdoTestFailure`, `AdoTestAttempt`, `AdoTestAttachment`, `AdoTestHistoryEntry`, `AdoBuildTestSummary` | S5 | Failed-test report (§15.11) |

### 7.3 Key shapes

**AdoWorkItem**

| Property | Type | Notes |
| --- | --- | --- |
| `Id`, `Rev` | int | |
| `WorkItemType`, `Title`, `State`, `TeamProject`, `AreaPath`, `IterationPath` | string | Convenience projections of `System.*` fields |
| `ChangedDate` | DateTimeOffset | |
| `Fields` | IReadOnlyDictionary<string, object?> | Keyed by reference name, case-insensitive |
| `Relations` | IReadOnlyList<AdoWorkItemRelation>? | Null unless requested |
| `WebUrl` | Uri | Browser link, built from the collection URL, project, and ID (§12.2) |
| `CollectionUri` | Uri | |

**AdoTestCase**

| Property | Type | Notes |
| --- | --- | --- |
| `Id`, `Rev` | int | From the root work item |
| `Title`, `WorkItemType`, `TeamProject`, `State` | string | From the root work item |
| `Priority` | int? | Null when absent |
| `AutomationStatus`, `AreaPath`, `IterationPath` | string? | Null when absent |
| `AssignedTo`, `ChangedBy` | AdoIdentityRef? | |
| `ChangedDate` | DateTimeOffset | |
| `WebUrl` | Uri | Browser link to the test case |
| `Steps` | IReadOnlyList<AdoTestStep> | Expanded rows in execution order, including Shared Steps group rows |
| `StepCount` | int | `Action` and `Validate` rows only |
| `Parameters` | AdoTestParameters | Empty when none |
| `SharedSteps` | IReadOnlyList<AdoSharedStepInfo> | Distinct referenced Shared Steps with title, rev, reference count |
| `Status` | `Complete` \| `Partial` | `Partial` when any Error diagnostic exists |
| `Diagnostics` | IReadOnlyList<AdoDiagnostic> | |
| `Suite` | AdoTestSuiteRef? | Set when retrieved through a suite (§14) |
| `RetrievedAt` | DateTimeOffset | |
| `CollectionUri` | Uri | |

**AdoTestStep**

| Property | Type | Notes |
| --- | --- | --- |
| `Sequence` | int | 1-based position among all rows, for sorting and counting |
| `Number` | string | Displayed number in ADO outline style: `1`, `3`, `3.1`, `3.2.1` (§10.5.4) |
| `Kind` | `Action` \| `Validate` \| `SharedStep` \| `Truncated` | `SharedStep` is the group row for a reference; `Truncated` marks where expansion stopped |
| `Action`, `ExpectedResult` | string | Plain text (§11); empty on `SharedStep` and `Truncated` rows |
| `ActionSource`, `ExpectedResultSource` | string? | Original XML-decoded inner values before rich-text conversion; retained at retrieval for JSON `-IncludeSource`; null on group/truncated rows |
| `SharedStep` | AdoSharedStepInfo? | On `SharedStep` rows: ID, title, rev, `WebUrl`. Null when the reference is invalid |
| `IsExpanded` | bool | On `SharedStep` rows: whether child rows follow |
| `SourceWorkItemId`, `SourceRev` | int | Work item document that contains the row |
| `SourceStepId` | int? | The XML `id` within the owning document (links step attachments later) |
| `SharedStepPath` | IReadOnlyList<AdoSharedStepInfo> | Outermost to innermost; empty for direct steps |
| `Depth` | int | `SharedStepPath.Count` |
| `DiagnosticCode` | string? | Set on unexpanded `SharedStep` rows and on `Truncated` |

**AdoDiagnostic**

| Property | Type | Notes |
| --- | --- | --- |
| `Code` | string | Stable identifier (§8.5) |
| `Severity` | `Info` \| `Warning` \| `Error` | |
| `Arguments` | IReadOnlyList<string> | Culture-invariant values used to build the message |
| `Message` | string | Localized in the session UI culture (§3.6); never contains credentials |
| `WorkItemId` | int? | Document where the issue was found |
| `StepNumber` | string? | Displayed step number, when applicable |
| `ReferenceChain` | IReadOnlyList<int> | Root to offending ID, for cycles and depth |

### 7.4 Supporting contracts

Names below are the public contract; wire DTOs remain internal. Use named enums
`AdoTestStepKind`, `AdoTestCaseStatus`, `AdoDiagnosticSeverity`, and `AdoParameterSource` for the
value sets in §7.3/§10.6. No renderer-specific step kinds are permitted.

| Type | Required properties |
| --- | --- |
| `AdoSharedStepInfo` | `Id: int`, `Title: string?`, `Rev: int?`, `TeamProject: string?`, `WebUrl: Uri?`, `ReferenceCount: int`. Unknown metadata stays null; a valid unresolved ID is retained. ReferenceCount counts group occurrences in this root's bounded output |
| `AdoSharedParameterInfo` | `Id: int`, `Title: string?`, `Rev: int?`, `TeamProject: string?`, `WebUrl: Uri?` |
| `AdoTestSuiteRef` | `PlanId: int`, `SuiteId: int`, `PlanName: string`, `SuiteName: string`, `SuitePath: IReadOnlyList<string>`, `PlanWebUrl: Uri?`, `WebUrl: Uri?`, `TeamProject: string`, `CollectionUri: Uri` |
| `AdoTestPlan` | `Id: int`, `Name: string`, `RootSuiteId: int`, `TeamProject: string`, `CollectionUri: Uri`, `WebUrl: Uri?` |
| `AdoTestSuite` | `Id: int` (suite ID), `PlanId: int`, `ParentSuiteId: int?`, `Name: string`, `SuitePath: IReadOnlyList<string>`, `TeamProject: string`, `CollectionUri: Uri`, `WebUrl: Uri?`. Pipeline BySuite maps `Id` to `SuiteId`, never a test-case ID |
| `AdoWiqlResult` | `Ids: IReadOnlyList<int>`, `Columns: IReadOnlyList<string>` (reference names), `AsOf: DateTimeOffset`, `TeamProject: string`, `CollectionUri: Uri`, `LimitApplied: int?` |
| `AdoIdentityRef` | `Id: string?`, `DisplayName: string`, `UniqueName: string?`; identifiers are opaque, not assumed GUIDs |
| `AdoConnectionTestResult` | `Success: bool`, `Elapsed: TimeSpan`, `ApiVersion: string`, `Hint: string?`, `CollectionUri: Uri`; API version is the requested version, not an inferred server build |

`AdoDiagnostic.Message` is a convenience projection produced with the explicitly captured UI
culture. Export MUST re-render `Code + Arguments` with report culture, not translate the existing
Message. Unknown field arrays/objects map recursively to read-only lists/dictionaries of the
§6.3 primitives; do not guess date or identity types merely from arbitrary string contents.

---

## 8. Errors, result status, and diagnostics

### 8.1 Exception hierarchy (Core)

```text
AdoException
├─ AdoConfigurationException
├─ AdoConnectionMismatchException
├─ AdoRedirectException
├─ AdoAuthenticationException
├─ AdoAuthorizationException
├─ AdoNotFoundException
├─ AdoRequestException
├─ AdoThrottledException
├─ AdoServerException
├─ AdoTimeoutException
├─ AdoResponseFormatException
└─ AdoFileOutputException
```

Content problems inside payloads (steps XML, parameters, rich text) are **diagnostics, not
exceptions**. They preserve partial results.

### 8.2 PowerShell mapping

| Exception | `ErrorCategory` |
| --- | --- |
| Configuration, ConnectionMismatch | `InvalidArgument` |
| Redirect, Request | `InvalidOperation` |
| Authentication | `AuthenticationError` |
| Authorization | `PermissionDenied` |
| NotFound | `ObjectNotFound` |
| Throttled, Server, ResponseFormat | `ConnectionError` / `InvalidResult` |
| Timeout | `OperationTimeout` |
| FileOutput | `WriteError` |

`FullyQualifiedErrorId` = `<Code>,<CmdletClass>`, e.g. `AdoNotFound,AdoToolkit.GetAdoWorkItemCommand`.
The original exception is preserved on the `ErrorRecord`.

### 8.3 Terminating versus non-terminating

- **Terminating:** no connection, authentication/authorization failure, invalid configuration,
  cancellation. Nothing else could succeed.
- **Non-terminating (`WriteError`, continue):** per-input failures, such as one missing ID among
  many, `NotAStepContainer`, or one file that cannot be written. Malformed root XML produces a
  `Partial` object with diagnostics (§8.4); it is a per-input error only with `-Strict`.

### 8.4 Partial results and `-Strict`

- A test case with Error diagnostics is emitted with `Status = Partial`. A one-line warning names
  the ID and the diagnostic counts.
- With `-Strict`, a `Partial` case is not emitted. A non-terminating error carrying its
  diagnostics is written instead.

### 8.5 Diagnostic codes

Codes are stable English identifiers; their messages are localized (§3.6).

| Code | Severity | Meaning |
| --- | --- | --- |
| `NotAStepContainer` | Error (per-input error record) | Root lacks steps data and is not in a test category (§10.4) |
| `EmptySteps` | Info | Test-capable item with no steps |
| `MalformedStepsXml` | Error | Steps XML failed to parse (line/position in message) |
| `UnknownStepElement` | Warning | Unrecognized element skipped |
| `UnknownStepType` | Warning | `step/@type` not `ActionStep`/`ValidateStep` |
| `UnexpectedComprefChildren` | Warning | `compref` has child elements (V-01) |
| `InvalidSharedStepReference` | Error | `compref` lacks a positive integer `ref` |
| `UnresolvedSharedStep` | Error | Referenced item missing, deleted, or inaccessible |
| `SharedStepHasNoSteps` | Warning | Referenced item exists but has no steps data |
| `CircularSharedStepReference` | Error | ID already on the active path |
| `MaximumDepthExceeded` | Error | Nesting deeper than the configured maximum |
| `ExpansionLimitExceeded` | Error | Total expanded steps passed the configured maximum; expansion stopped |
| `ResolutionLimitExceeded` | Error | Phase 1 document budget exhausted; affected references remain unexpanded (§10.5.1) |
| `NestedEncodingDecoded` | Info | Rich text needed more than one conversion pass (§11.3) |
| `MalformedParameterData` | Warning | Parameter or data source XML/JSON could not be parsed |
| `UnresolvedSharedParameter` | Warning | Referenced shared parameter set missing or inaccessible |
| `NoTestRuns` | Info | The build has no test runs (§15.9) |
| `TestRunInProgress` | Warning | The build or one of its test runs is not complete; results may change |
| `FailureLimitExceeded` | Error | More failing test identities than `maximumReportedFailures`; the rest are counted but not detailed |
| `UngroupedTestResult` | Warning | A result has no automated test name; its attempts cannot be grouped (§15.10) |
| `InvalidTestCaseReference` | Warning | A result's Test Case reference is not a positive integer |
| `UnresolvedTestCase` | Warning | The referenced Test Case work item is missing or inaccessible; the ID link is kept |
| `HistoryUnavailable` | Warning | A history build could not be read; its bar and cells show as unavailable |
| `HistoryLimitExceeded` | Warning | `maximumHistoryRequests` reached; older history entries are unavailable |
| `TestTextTruncated` | Info | An error message or stack trace passed the display limit (§15.14) |
| `AttachmentTooLarge` | Warning | Attachment larger than `maximumAttachmentBytes`; listed and linked to ADO, not downloaded |
| `AttachmentBudgetExceeded` | Warning | Total attachment budget reached; remaining attachments not downloaded |
| `AttachmentDownloadFailed` | Warning | Attachment download failed after retries; listed without a local file |
| `AttachmentContentMismatch` | Warning | PNG signature or JSON parse check failed; the file is linked, not previewed |

---

## 9. Work items and WIQL

### 9.1 `Get-AdoWorkItem` (Slice 1)

- `-Id <int[]>`: `ValidateRange(1, int.MaxValue)`; accepts pipeline input by value and by
  property name `Id`.
- `-Field <string[]>`: reference names, unioned with the fields required for the non-null
  convenience properties in §7.3. `Id` and `Rev` come from the top-level response.
  `-IncludeRelations` requests JSON body `"$expand": "relations"` and omits `fields`.
  The batch schema documents BOTH properties **[Doc-checked]**, without a mutual-exclusion rule.
  Until their combination is checked [Verify V-10], v1 parameter sets MUST reject
  `-Field` with `-IncludeRelations` locally; do not describe that restriction as a server fact.
- Uses WorkItemsBatch in chunks of 200 **[Doc-checked]**, with `errorPolicy = omit`
  **[Doc-checked]**. The docs do not promise positional null placeholders [Verify V-05].
  Reconcile requested IDs against IDs in non-null returned work items; never zip request and
  response positions. Each absent ID produces one non-terminating `ObjectNotFound` error, without
  distinguishing missing from inaccessible; the rest are emitted. Reject unexpected/duplicate
  response IDs as a response-format failure. Test compact omissions and nulls.
- Output follows input order. Duplicate IDs are requested once and emitted once each, in the
  position of their first occurrence.
- Buffer IDs across pipeline records and retrieve in `EndProcessing` so batching/deduplication
  are invocation-wide. Preserve and validate provenance from typed pipeline inputs before reducing
  them to IDs (§4.4); no network call for a mismatched input.

### 9.2 `Invoke-AdoWiql` (Slice 3)

- `-Query <string>`, `-Project`, `-Top <int>`.
- Flat queries only in v1. Link and tree queries produce a `NotSupported` error.
- Default output: `AdoWiqlResult` (`Ids`, `Columns`, `AsOf`). With `-Hydrate`, `AdoWorkItem`
  objects are emitted in query order via WorkItemsBatch.
- A 20,000-result limit is documented **[Doc-checked]**; the precise Server 2020 REST failure
  shape/truncation behavior remains [Verify V-06]. `$top` and `queryType = flat` are documented.
  Toolkit policy: absent `-Top`, reject a response of 20,000 or more as potentially incomplete,
  even if exactly 20,000 matched. Also surface server limit errors with a narrower-query hint.
  With explicit `-Top` (1–20,000), an intentionally limited result is allowed and
  `AdoWiqlResult.LimitApplied` records the limit; do not claim it is the complete matching set.
  No offset paging or invented continuation for WIQL.
- Primary v1 use: `Invoke-AdoWiql ... | Get-AdoTestCase | Export-AdoTestCase`.
- `Get-AdoTestCase` has a ByWiql parameter set taking `AdoWiqlResult` by value and consuming
  `Ids` in order, plus ByWorkItem for `-Hydrate` output. Validate `CollectionUri` first. Query
  `AsOf` is provenance only: v1 hydration/Shared Steps use current revisions, not a historical
  snapshot (Q-05). Portal-only macros need explicit values; do not add a WIQL rewrite engine.

---

## 10. Test step data (Slice 1)

### 10.1 Fields

| Reference name | Used for |
| --- | --- |
| `Microsoft.VSTS.TCM.Steps` | Steps XML (Test Cases and Shared Steps) |
| `Microsoft.VSTS.TCM.Parameters` | Parameter names declared by the test case [Verify V-03] |
| `Microsoft.VSTS.TCM.LocalDataSource` | Local data table (XML) or shared parameter mapping (JSON) [Verify V-03] |
| `System.Title`, `System.Rev`, `System.WorkItemType`, `System.TeamProject`, `System.State` | Header and provenance |
| `System.AreaPath`, `System.IterationPath`, `System.AssignedTo`, `System.ChangedDate`, `System.ChangedBy`, `Microsoft.VSTS.Common.Priority`, `Microsoft.VSTS.TCM.AutomationStatus` | Report metadata (root test cases only; absent fields are omitted) |

### 10.2 Expected steps XML (illustrative)

Based on secondary sources; confirm per V-01 and V-02.

```xml
<steps id="0" last="5">
  <step id="2" type="ActionStep">
    <parameterizedString isformatted="true">&lt;DIV&gt;&lt;P&gt;Open the app&lt;/P&gt;&lt;/DIV&gt;</parameterizedString>
    <parameterizedString isformatted="true">&lt;DIV&gt;&lt;P&gt;&lt;BR/&gt;&lt;/P&gt;&lt;/DIV&gt;</parameterizedString>
    <description/>
  </step>
  <compref id="3" ref="4521" />
  <step id="5" type="ValidateStep">
    <parameterizedString isformatted="true">&lt;P&gt;Sign in as @user&lt;/P&gt;</parameterizedString>
    <parameterizedString isformatted="true">&lt;P&gt;The dashboard is shown&lt;/P&gt;</parameterizedString>
    <description/>
  </step>
</steps>
```

- `id` attributes are positions inside the owning document. They are not work item IDs.
- `compref/@ref` is the Shared Steps work item ID.
- The escaping is part of the XML. After XML parsing, a formatted string is ordinary HTML.

### 10.3 Parser rules

1. `XmlReaderSettings`: `DtdProcessing.Prohibit`, `XmlResolver = null`, comments and processing
   instructions ignored, `ValidationType.None`, and `MaxCharactersInDocument = 10,485,760`
   characters (not bytes). **[Doc-checked]** Use the reader over the field string, never a URI.
   No schema/type loading, XInclude processing, or document APIs that bypass these settings.
   Require one `steps` root; a different root produces `MalformedStepsXml`.
2. Read the **element children of the root, in document order**. Do not use a descendant search.
   v0.1's `//*[local-name()="step" or local-name()="compref"]` would count any steps nested under
   a `compref` twice.
3. Match elements by local name, ignoring namespace prefixes.
4. `step`:
   - `parameterizedString[0]` → Action, `[1]` → Expected Result. A missing one is an empty string.
   - More than two → `UnknownStepElement` warning; extras ignored.
   - `type="ActionStep"` → `Action`; `ValidateStep` → `Validate`. Anything else →
     `UnknownStepType`, and the kind is inferred from whether Expected Result is non-empty.
   - `isformatted="true"` → HTML, converted per §11. Otherwise the text is already plain.
5. `compref`:
   - The work item ID comes **only** from `ref`.
   - A missing, non-integer, or non-positive `ref` → unexpanded group row +
     `InvalidSharedStepReference`.
   - Child elements [Verify V-01]: MUST NOT be counted a second time on top of fetched Shared
     Steps. Until the format is confirmed, skip them with `UnexpectedComprefChildren` and retain
     the reference group. Do not guess whether they are duplicated steps, overrides, or metadata.
6. Any other element → `UnknownStepElement` warning, skipped.
7. Output is a pure `StepDocument` (work item ID, rev, ordered nodes, diagnostics). The parser does
   no I/O and produces no HTML.
8. Malformed XML → `MalformedStepsXml` (Error) and no nodes for that document.

### 10.4 Test-capability detection

An absent/null steps field alone does not prove a non-test type. Detection therefore uses
capability data, not English type names:

1. `Microsoft.VSTS.TCM.Steps` present → step container.
2. Otherwise, if the item's type belongs to `Microsoft.TestCaseCategory` or
   `Microsoft.SharedStepCategory` (category reference names are stable across localized and
   customized processes) → step container with zero steps, plus `EmptySteps` (Info). Category
   membership is cached per project for the session. The category route and membership response
   are **[Doc-checked]**; exact category reference names remain [Verify V-13]. An unsuccessful
   category lookup MUST NOT be cached as an empty category or misreported as `NotAStepContainer`.
3. Otherwise → per-input `NotAStepContainer` error.

`Get-AdoTestCase` therefore also accepts Shared Steps items directly. No separate
`Get-AdoSharedStep` is needed.

### 10.5 Shared Steps resolution and expansion

#### 10.5.1 Phase 1 — resolve (batched I/O)

```text
cache ← {}                                    // id → StepDocument | Unresolved
frontier ← compref IDs from all root documents
level ← 1
while frontier ≠ ∅ and level ≤ MaximumSharedStepDepth:
    fetch ← frontier − cache.keys
    WorkItemsBatch(fetch, fields = [System.Id, System.Rev, System.Title,
                                    System.WorkItemType, Microsoft.VSTS.TCM.Steps],
                   errorPolicy = omit)        // chunks of 200
    parse each result into cache; omitted IDs → Unresolved
    frontier ← compref IDs found in the newly parsed documents
    level ← level + 1
```

Shared parameter sets referenced by any root (§10.6) are fetched in the same way.

Request count is **O(levels × ⌈distinct IDs / 200⌉)**, not O(references). In bulk export, all
roots share one cache. That is an acceptance property (S1-6, S3-4).

#### 10.5.2 Phase 2 — expand (pure, no I/O)

```text
Expand(document, activePath, numberPrefix):
    position ← 0
    for node in document.Nodes:
        position ← position + 1
        number ← numberPrefix + position
        if node is Step:
            emit step row (Number ← number, Sequence ← ++context.Sequence,
                           SharedStepPath ← activePath without root)
        else if node is SharedStepReference r:
            if r.IsInvalid                        → emit unexpanded group row (InvalidSharedStepReference)
            else if r.Id ∈ activePath              → emit unexpanded group row (CircularSharedStepReference,
                                                                               chain = activePath + r.Id)
            else if depth(activePath) = MaxDepth   → emit unexpanded group row (MaximumDepthExceeded)
            else if cache[r.Id] is Unresolved      → emit unexpanded group row (UnresolvedSharedStep)
            else
                emit group row (Kind ← SharedStep, Number ← number, IsExpanded ← true)
                Expand(cache[r.Id], activePath + r.Id, number + ".")
        if context.RowCount > MaximumExpandedSteps:
            emit Truncated row (ExpansionLimitExceeded); stop all expansion
```

Unexpanded group rows keep their number and carry the diagnostic code.

- `activePath` starts with the root ID, so a Shared Steps item that references itself is detected.
- **Cycles** are detected only on the active path. A Shared Step referenced twice, or reached
  through two branches (a diamond), expands every time and is fetched once.
- **Size limit:** acyclic repetition can still explode. For example, if each of 10 levels
  references the next level twice, one root yields 2¹⁰ expansions. `MaximumExpandedSteps`
  (default 5,000) bounds this.
- Traversal state lives in an explicit context object. No static or module-scoped state.

#### 10.5.3 Limits and configuration

| Setting | Default | Override |
| --- | --- | --- |
| `MaximumSharedStepDepth` | 10 | Config `testCases.maximumSharedStepDepth`; `-MaximumSharedStepDepth` |
| `MaximumExpandedSteps` | 5,000 rows | Config `testCases.maximumExpandedSteps`; `-MaximumExpandedSteps` |

#### 10.5.4 Numbering and provenance

Numbers follow the outline style testers already use in ADO:

```text
1       Open the app
2       Go to Settings
3       ▸ Sign in (#4521)                ← Shared Steps group row
3.1       Enter the user name
3.2       Enter the password
3.3       ▸ Accept terms (#4530)         ← nested group row
3.3.1       Tick the checkbox
4       Save
```

- Direct rows at the top level are numbered `1…n`. A Shared Steps reference takes one number as
  its group row, and its steps are numbered beneath it. The next direct row continues at the
  parent level (`4`).
- A reference that cannot be expanded keeps its number, so later numbers never shift when a
  Shared Step fails to resolve.
- `Number` is a string. `Sequence` is the integer to sort by (`"10"` sorts before `"9"`).
- Every step records `SourceWorkItemId`, `SourceRev`, and `SharedStepPath`. Test Cases reference
  the *latest* revision of a Shared Step, so the rev in the output records what was actually
  expanded.

### 10.6 Parameters

Keep `@tokens` literal in steps and expose the data as a table.

**AdoTestParameters**

| Property | Type | Notes |
| --- | --- | --- |
| `Source` | `None` \| `Local` \| `Shared` | |
| `Names` | IReadOnlyList<string> | Declared order |
| `Rows` | IReadOnlyList<IReadOnlyDictionary<string, string>> | One per iteration |
| `SharedParameterSets` | IReadOnlyList<AdoSharedParameterInfo> | ID, title, rev when `Source = Shared` |

Parsing rules [Verify V-03 for all formats]:

- **Local:** `LocalDataSource` is expected to hold a DataSet-style XML document: an inline
  `xs:schema` followed by repeated row elements whose children are the column values. Parse it
  with `XmlReader` (same settings as §10.3), skip the `xs:schema` element, and read row children
  as name/value pairs.
  **Never use `DataSet.ReadXml`**, which carries type-loading security risks.
- **Shared:** `LocalDataSource` is expected to hold JSON mapping test parameter names to shared
  parameter set work items. The sets' own parameter XML is fetched in Phase 1 and joined into
  rows via the mapping.
- Parse failures → `MalformedParameterData` (Warning); unreachable sets →
  `UnresolvedSharedParameter` (Warning). Steps are still emitted.

---

## 11. Rich text to plain text

### 11.1 Where it applies

Formatted `parameterizedString` values in v1. The same converter will serve other HTML fields,
such as `System.Description`, later. It lives in `Core/RichText` and nowhere else.

Test result error messages and stack traces are plain text and are **not** converted: `<` in
`List<int>` must survive. They are lexed and encoded per §15.14.

### 11.2 Conversion

A small HTML tokenizer (not regular expressions) walks the markup:

| Markup | Plain-text effect |
| --- | --- |
| `script`, `style`, `head`, comments | Dropped with content |
| `br` | Line break |
| `p`, `div`, `h1`–`h6`, `blockquote`, `pre`, `tr`, `table` | Block boundary |
| `li` in `ul` / `ol` | New line prefixed `- ` / `1. ` (numbered per list, nested lists indented) |
| `td`, `th` | Cells joined with ` \| ` |
| `img` | `[image]`, or `[image: <alt>]` when alt text exists |
| `a` | Link text; if `href` is absolute http(s) and differs from the text: `text (href)` |
| Any other tag | Tag removed, content kept |

Then:

1. Decode entities in text nodes.
2. Turn no-break spaces into ordinary spaces, except where French typography needs them (§11.5).
3. Collapse runs of ordinary spaces and tabs. Preserved no-break spaces are not collapsed.
4. Trim each line's end.
5. Collapse three or more consecutive newlines to two.
6. Trim the result.

No Unicode normalization is applied. French and other non-ASCII text passes through unchanged.

### 11.3 Nested encoding

ADO content sometimes contains markup escaped more than once. The converter repeats the **whole
conversion** until the output stops changing, at most 8 passes, and adds `NestedEncodingDecoded`
(Info) when more than one pass changed the text.

It does **not** decode entities before tokenizing, as v0.1 described. That would turn escaped
text into live markup ahead of the parser. Each pass takes and produces plain text, so the output
is always text that gets encoded at the sink.

Known trade-off, pinned by a fixture: literal text that looks like markup or entities (a step
saying "type `&lt;b&gt;`") is consumed. JSON export with `-IncludeSource` preserves the original
strings for such cases.

### 11.4 Encoding at output sinks

| Sink | Rule |
| --- | --- |
| HTML | `HtmlEncoder.Create(UnicodeRanges.All)`, so non-ASCII stays readable, markup-significant characters are escaped, and newlines become `<br>` after encoding |
| Markdown | Backslash-escape Markdown-significant characters (backslash, backtick, `*`, `_`, `[`, `]`, `#`, `\|`, and line-leading `-`, `+`, or `1.`); always encode `<` and `>` |
| JSON | `System.Text.Json`. Relaxed escaping is acceptable because JSON output is never embedded in HTML or script |
| HTML highlighted code (§15.14) | Each lexer token is HTML-encoded separately, then wrapped in a toolkit `<span class>`; class names come from a fixed set, never from content |
| HTML attributes and SVG text | Attribute-encoded. `href`/`src` values are toolkit-built URLs (§12.2) or relative paths made of toolkit-generated names (§13.4); numbers in `style`/SVG attributes are invariant-culture numerics |
| Inline `<script>` (§15.15) | Static toolkit bytes only; no remote or per-report text is ever written inside a script element |

### 11.5 French text

- Apart from the rules in §11.2, content passes through unchanged. No accent stripping, case
  folding, transliteration, or Unicode normalization in output.
- **No-break spaces.** French typography puts a no-break space (U+00A0) or narrow no-break space
  (U+202F) before `:`, `;`, `!`, `?`, `»`, after `«`, and between digit groups (`1 234`). These
  are kept wherever they sit next to those marks or between digits. Other no-break spaces become
  ordinary spaces. Both characters are written literally, not as entities.
- **Quotes and apostrophes.** Typographic `’`, `«`, and `»` are never altered, and Markdown
  escaping leaves them alone.
- **Matching.** `-Name` wildcard filters are case-insensitive and treat accents as significant
  (`Tâche*` does not match `Tache`). Both sides are normalized to NFC before comparison, so
  composed and decomposed accents match. Output keeps the original form.
- **Reports.** The HTML `lang` attribute is the report culture. Dates and numbers use that
  culture's formats (`13 septembre 2026` vs `September 13, 2026`).
- **Paths.** User profile and output paths may contain accents (e.g. `C:\Users\Hélène\Downloads`);
  all file APIs are Unicode-safe.

---

## 12. Reporting (Slice 2)

### 12.1 Report model

`TestCaseReportModel` contains:

- culture
- server and collection URL, project
- title, ID, rev, work item type, state, priority, automation status, area and iteration paths,
  assigned to, last changed (date and by)
- web links
- generated-at (local time with offset), toolkit version
- status, step count, diagnostics summary with localized messages
- parameters table
- ordered rows (step rows and Shared Steps group rows, with numbers and links)

A `ReportDocumentModel` wraps one or more case models (§12.5). Renderers receive only these models.

### 12.2 HTML

- Standalone single file, UTF-8, `<meta charset="utf-8">` first in `<head>`.
- **No scripts at all** in the Test Case report. The failed-test report's scripted exception is
  §15.15 (DD-017) and does not apply here. Content Security Policy as defense in depth:
  `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; img-src data:">`.
- `<html lang="<report culture>">`. All labels come from resources in the report culture.
- Screen styling uses deep charcoal/graphite backgrounds, elevated surfaces, subtle borders,
  high-contrast text and muted secondary text with a restrained cool accent. Use local
  system UI fonts and monospace for technical values. Section links support keyboard
  navigation; parameter tables scroll within the viewport on small screens (Q-21).
- Embedded CSS: step cards with Action and Expected Result side by side on desktop, stacked below
  about 700 px. Expected Result is visually distinct. Each card shows its outline number
  (`3.2`).
- Shared Steps group rows render as a banner that opens an indented group. It shows the number,
  title, ID, and a link, e.g. `3 ▸ Sign in (#4521)`. Nested groups indent further. Unexpanded
  groups are styled as warnings and show their localized diagnostic message.
- Print styles: white background, high contrast, no shadows, `break-inside: avoid` on cards.
  Link URLs are not appended in print.
- **Header:**
  - "Azure DevOps Test Case" / « Cas de test Azure DevOps », title linked to the test case
  - ID, rev, type, state, priority, automation status, area path, iteration path, assigned to,
    last changed
  - server and collection URL, project, and plan/suite when known (linked)
  - generated-at, toolkit version, step count, status
  - diagnostics summary when `Partial`
  - Absent metadata fields are omitted, not shown blank.
- A parameters table, when present, sits after the header, with a link to each shared parameter
  set. A diagnostics section, when any exist, sits at the end.
- **Hyperlinks** open ADO in the browser: the test case, every Shared Steps group, shared parameter
  sets, the test plan and suite, and builds. URLs are built from the connection's collection URL,
  the project, and numeric IDs, never copied from response text [Verify V-15 for Server 2020 web
  routes].
- **Links in content:** absolute `http`, `https`, and `mailto` URLs inside step text render as
  links (`rel="noreferrer"`). Any other scheme (`javascript:`, `data:`, `file:`) stays plain text.
  Link text and `href` are encoded separately. The CSP does not restrict navigation, so links
  work while scripts stay blocked.
- Markers used by validation: `<meta name="generator" content="AdoToolkit <version>">`,
  `data-case-count="<n>"` on the document, and `data-step-count="<n>"` on each case's step list.

### 12.3 Markdown

One heading per step, prefixed with its number (tables break on multiline content). Shared Steps
groups are shown as blockquote banners with links. Links use `[text](url)`, with parentheses
percent-encoded in the URL. Labels come from the report culture. Escaping per §11.4.

### 12.4 JSON

- `schemaVersion: 1`, published as `docs/schemas/testcase.v1.schema.json`.
- Includes metadata, web URLs, rows (`number`, `sequence`, `kind`), parameters, Shared Steps,
  diagnostics (`code`, `arguments`, and `message` in the report culture), and status.
- Property names and enum values are English regardless of culture (§3.6).
- `-IncludeSource` adds the original `actionSource` / `expectedResultSource` strings.
- The schema version changes only on breaking changes.

### 12.5 One document per export

- Every `Export-AdoTestCase` invocation writes **exactly one document**, however many test cases
  arrive through the pipeline.
- **One case:** the single-case layout from §12.2.
- **Several cases** (Slice 3), in this order:
  1. A cover header: server, collection, project, the source (plan and suite, or "WIQL query"),
     generated-at, and totals by status.
  2. A linked table of contents, grouped by suite path.
  3. Each case in order, with its own header and an anchor `#tc-<id>`. A case emitted more than
     once (several suites) gets `#tc-<id>` on its first occurrence and `#tc-<id>-<k>` on its
     k-th occurrence (k ≥ 2), so every HTML `id` is unique.
- Step numbering restarts for each case.
- Rendering streams to the temporary file (§3.2), so a large suite never needs the whole document
  in memory.
- One file per test case is backlog.

### 12.6 Validation before commit

| Format | Checks |
| --- | --- |
| All | Temporary file exists and is non-empty |
| HTML | Generator marker present; `data-case-count` equals the number of cases; each `data-step-count` equals its model; rendered step cards equal the model's row count |
| Markdown | Case and step heading counts equal the model's |
| JSON | Re-parses and `schemaVersion` matches |

Real rendering correctness is covered by golden-file tests (§19). Commit-time validation only
catches truncated or empty output.

### 12.7 Shared visual baseline

One visual baseline serves the toolkit's HTML reports (DD-018). The failed-test report (§15.14)
uses the detailed baseline below. The owner-requested Q-21 revision brings dark screen styling
to Test Case reports with the layout and script-free contract in §12.2; their palette remains
independently scoped and both report types retain light print output.

**Delivery**

- `Reporting/Assets/report-base.css` is an embedded resource inlined into each report's `<style>`
  element, followed by report-specific CSS. Nothing is loaded from outside the document: no web
  fonts, CDNs, icon fonts, or remote images. Icons are inline SVG or Unicode glyphs.
- The baseline is static text. No remote or per-report values are interpolated into CSS.
- Highlighting and charts are markup produced in C# at export time (DD-021). The baseline styles
  them and needs no script.

**Design tokens** (CSS custom properties on `:root`; dark is the default and only screen theme
in v1, Q-30):

| Token group | Tokens (initial values) |
| --- | --- |
| Surfaces | `--bg #0f1115`, `--surface #171a21`, `--surface-2 #1f232c`, `--border #2a2f3a` |
| Text | `--text #e6e8ee`, `--text-muted #9aa3b2`, `--link #7cb7ff`, `--link-visited #b9a3ff`, `--focus #ffd166` |
| Status | `--pass #3fb950`, `--fail #f85149`, `--flaky #d29922`, `--other #8b949e`, `--unavailable #6e7681`, `--info #58a6ff`, `--current` (outline for the highlighted run) |
| Code | `--code-bg`, `--tok-keyword`, `--tok-type`, `--tok-method`, `--tok-namespace` (dimmed), `--tok-string`, `--tok-number`, `--tok-path`, `--tok-line`, `--tok-property`, `--tok-literal`, `--tok-punct`, `--tok-framework` (dimmed frames) |
| Shape | spacing scale `4 8 12 16 24 32` px, `--radius 6px`, 1 px borders, no heavy shadows |
| Type | UI `system-ui, "Segoe UI", sans-serif` 15 px / 1.5; code `ui-monospace, "Cascadia Mono", Consolas, monospace` 13 px, `tab-size: 4` |

Initial values are a starting point; golden files pin whatever ships.

**Rules**

- Contrast: body text and code tokens at least 4.5:1 against their background; chart marks,
  borders that carry meaning, and focus rings at least 3:1. Checked by a unit test over the token
  table.
- Status is never conveyed by color alone: every status has a glyph and a label (`✕ Failed`,
  `✓ Passed`, `≈ Flaky`, `– Other`, `? Unavailable`); chart segments also carry labels or titles.
- Layout: content up to 1280 px wide, a sticky top bar, cards with a 1 px border. Below about
  700 px, side-by-side regions stack (the §12.2 breakpoint).
- Links: underlined on hover and focus; links that open ADO carry a `↗` glyph with a localized
  accessible label. Visited links use `--link-visited`.
- Components: top bar, status badge, count chip, card, metadata grid (label/value), data table,
  `<details>` section, code block (language label, horizontal scroll by default, wrap toggle when
  scripts run), diagnostic banner, inline SVG bar chart, history strip, thumbnail grid.
- Accessibility: landmarks (`header`, `nav`, `main`), ordered headings, visible focus, keyboard
  reachable controls, `prefers-reduced-motion` disables transitions, and every SVG chart has a
  `<title>` plus an equivalent data table.
- Print: `@media print` switches to a light palette with high contrast, hides interactive controls,
  wraps code blocks, and applies `break-inside: avoid` to cards.
- Labels come from resources in the report culture; CSS `content` holds glyphs only, never words.

---

## 13. File output

### 13.1 Atomic writer

1. Create `.<name>.<random>.tmp` in the **destination directory** (same volume), exclusive access.
2. Write, then `Flush(flushToDisk: true)`.
3. Run format validation (§12.6).
4. `File.Move(temp, destination, overwrite: true)`.
5. On any failure, delete the temp file and leave any existing destination untouched.
6. A locked destination raises `AdoFileOutputException` naming the path.

### 13.2 Paths and defaults

- `-Path` is resolved through the PowerShell provider. The FileSystem provider is required, and
  the parent directory must exist.
- With no `-Path`, the document goes to the user's **Downloads known folder**, resolved with
  `SHGetKnownFolderPath(FOLDERID_Downloads)` rather than `%USERPROFILE%\Downloads`, which breaks
  under folder redirection.
- `-Path` may name a file or an existing directory. For a directory, the default file name is used.
- Default file names come from IDs and invariant-culture timestamps only:

  | Content | File name |
  | --- | --- |
  | One case | `TestCase-<id>-Steps.<ext>` |
  | All cases from one suite | `TestSuite-<suiteId>-Steps.<ext>` |
  | Anything else | `TestCases-<yyyyMMdd-HHmmss>.<ext>` |
  | Build log | `Build-<id>-Log-<logId>.txt` |
  | Failed-test report | `Build-<id>-TestFailures.html` |
  | Its attachment folder | `<report base name>.files-<yyyyMMddTHHmmssfffZ>` beside the report (§13.4) |
  | One attachment | `r<runId>-<resultId>[-s<subResultId>]-a<attachmentId>.<ext>`, `<ext>` from §15.13 |

  Remote strings (titles in either language) are never used in file names in v1. That removes
  path traversal, reserved-name, and encoding risks.

### 13.3 Overwrite, confirmation, and output

- PowerShell `Export-*` convention: overwrite by default, `-NoClobber` refuses.
- `SupportsShouldProcess` on every file write, so `-WhatIf` lists the files.
- Export cmdlets emit `FileInfo` objects.
- `-Open` (opt-in) opens the committed document with the default handler.

### 13.4 Report with an attachment folder (Slice 5)

A failed-test report and its attachments cannot be replaced in one atomic operation. The commit
order below keeps the committed report consistent with the folder it links to after a failure
or crash at any step; the worst case is an orphaned folder.

1. Choose the generation stamp (UTC, invariant culture) and the final folder name
   `<base>.files-<stamp>` before rendering, so links use the final relative path.
2. With `-NoClobber`, refuse before any download when the report exists.
3. Create `.<base>.files.<random>.tmp` in the destination directory. Download attachments into it
   (§15.13), each streamed with the Download timeout class; a retry restarts that file (§6.5).
4. Render the report to `.<name>.<random>.tmp` beside the destination (§13.1 steps 1–2).
5. Validate the report (§15.16) and that every local attachment link names an existing, non-empty
   file of the recorded size in the temporary folder.
6. Rename the temporary folder to the final folder name; fail if that name exists.
7. Move the temporary report over the destination (`overwrite: true`; with `-NoClobber`, a
   non-overwriting move). If this fails, delete the new folder; the previous report and its folder
   stay untouched.
8. Delete previous generation folders for the same base name: direct children of the destination
   directory whose names match `<base>.files-` followed exactly by the stamp format, excluding the
   new one. Never follow or delete through reparse points; a reparse point is skipped with a
   warning. A deletion failure is a warning, not an error.

On failure before step 6, delete the temporary report and folder. With no attachments to
download, no folder is created and steps 3, 5 (file checks), and 6 are skipped; step 8 still
removes older generation folders. `-WhatIf` names the report and folder and downloads nothing;
one `ShouldProcess` call covers steps 3–8.

---

## 14. Bulk test export (Slice 3)

### 14.1 Commands

```powershell
Get-AdoTestPlan  [-Project <string>] [-Id <int>] [-Name <wildcard>]
Get-AdoTestSuite -PlanId <int> [-SuiteId <int>] [-Recurse]
Get-AdoTestCase  -Id <int[]>                                  # ById (pipeline: Id)
Get-AdoTestCase  -PlanId <int> -SuiteId <int> [-Recurse]      # BySuite (pipeline: AdoTestSuite)
```

### 14.2 Behavior

- Suite membership is read with SuiteTestCaseList (`6.0-preview.2`, continuation header)
  **[Doc-checked]**. Plans and suites use the testplan area [Verify V-04]. If a GA route in the
  older `test` area covers the need on the actual server, V-04 decides which to use.
- Order follows suite order (the `order` field), with suites walked depth-first when `-Recurse`
  is used.
- Retrieval: collect all test case IDs, WorkItemsBatch in chunks of 200, Phase 1 over **all**
  roots with one shared cache, then Phase 2 per case (§10.5).
- A test case present in several suites is fetched and expanded once, and emitted once per
  membership with `Suite` set.
- `Write-Progress` reports enumeration, fetch, and expansion phases with counts.
- `Export-AdoTestCase` collects pipeline input and renders one document in `EndProcessing`
  (§12.5).

### 14.3 Example

```powershell
Connect-Ado -Profile work
Get-AdoTestSuite -PlanId 812 -SuiteId 813 -Recurse |
    Get-AdoTestCase |
    Export-AdoTestCase -Culture fr-CA -Path .\Release-12.html -Open
```

---

## 15. Pipeline failure triage (Slices 4 and 5)

§15.1–§15.5 are Slice 4: build timeline failures and logs. §15.6–§15.17 are Slice 5: the
failed-test report for one pipeline run. They are separate slices (DD-016).

### 15.1 API choice

Use the **Build** REST area. YAML pipeline runs are builds, and the timeline endpoint is GA at 6.0
**[Doc-checked]**. The Pipelines area adds nothing needed for triage. Record types for YAML
stages and jobs on the actual server: [Verify V-11].

### 15.2 Commands

```powershell
Get-AdoBuildDefinition [-Project] [-Name <wildcard>] [-Id <int>]
Get-AdoBuild           -Definition <string|int> [-Branch <string>] [-Latest]
                       [-Status <Completed|InProgress|All>] [-Result <Failed|...>] [-Top <int>]
Get-AdoBuildTimeline   -BuildId <int>                           # pipeline: AdoBuild
Get-AdoBuildFailure    -BuildId <int> [-IncludeWarnings]        # pipeline: AdoBuild
Save-AdoBuildLog       -BuildId <int> -LogId <int> [-Tail <int>] [-Path <dir>]   # pipeline: AdoBuildFailure
```

- `-Branch main` is normalized to `refs/heads/main` unless it already starts with `refs/`.
- `-Latest` = `$top=1` ordered by finish time, descending.

### 15.3 Failure derivation

1. Build the record tree from `parentId`, ordered by `order`.
2. Where a record has `previousAttempts`, keep only the latest attempt for each `identifier`.
3. A failure is the **deepest** record with `result = failed`, or `canceled` when the build was
   canceled. Ancestors are summarized in its path.
4. Emit `AdoBuildFailure`: build ID/number, definition, branch, `Path`
   (`Stage › Job › Task`), record type and name, result, error issues (message, category), log ID,
   log line count, error/warning counts, start/finish time, attempt.
5. `-IncludeWarnings` adds `succeededWithIssues` records.

### 15.4 Logs

- `Save-AdoBuildLog` streams to the atomic writer with the Download timeout class.
- `-Tail N` uses the log list's line count to request only the final lines [Verify V-14].
- Logs stay local. ADO masks registered secrets, but logs can still hold sensitive values, so
  they follow the same rule as reports (DD-010).

### 15.5 Example

```powershell
Get-AdoBuild -Definition 'Main Build' -Branch main -Latest -Result Failed |
    Get-AdoBuildFailure |
    Save-AdoBuildLog -Tail 200 -Path .\triage
```

### 15.6 Failed-test report: purpose and input (Slice 5)

ADO's web UI shows test failures across several slow views. The failed-test report gathers
everything about one pipeline run's failed tests into one local document built for fixing them
quickly.

- **Input:** exactly one pipeline run, i.e. one build ID (YAML runs are builds, §15.1), given as
  `-BuildId` or an `AdoBuild` from the pipeline. Each input produces one report.
- **Scope:** failures only. A test is reported when any attempt in the run has a failure-class
  outcome (§15.10). Tests that only passed or were not executed are counted in the summary and
  history, never detailed (§1.4).
- **Read-only** against ADO (DD-009). The only local writes are the report and its attachment
  folder (§13.4).
- **Prerequisites:** Slice 2 (report culture, renderer infrastructure, atomic writer, Downloads
  default) and Slice 4 (`AdoBuild`, BuildsList).

### 15.7 API choice

Use the **Test** REST area (`_apis/test`) for runs, results, and attachments, and the Build area
for the build and its history. The newer `testresults` area and result-summary and test-history
query endpoints are not assumed to exist on Server 2020 [Verify V-28]; v1 works from run and
result listings only, so the verification surface stays at the endpoints in §6.2.

Every Test-area route, version, parameter, and field in §15.7–§15.13 is unconfirmed for Server
2020 until V-19–V-25 pass. Fixtures follow the shapes described here and are corrected by those
checks (§19.2).

### 15.8 Commands

```powershell
Get-AdoTestRun             -BuildId <int>                              # pipeline: AdoBuild
Get-AdoBuildTestFailure    -BuildId <int> [-HistoryCount <int>] [-HistoryScope <SameBranch|AllBranches>]
                                                                       # pipeline: AdoBuild
Export-AdoBuildTestFailure [-Culture <string>] [-Path <string>] [-SkipAttachments]
                           [-NoClobber] [-Open]                        # pipeline: AdoBuildTestFailureSet
```

- `Get-AdoBuildTestFailure` retrieves; `Export-AdoBuildTestFailure` renders and downloads
  (DD-005). Attachment metadata is retrieved by `Get-`; attachment bytes are downloaded only by
  `Export-`, because they are files.
- `Get-AdoBuildTestFailure` emits one `AdoBuildTestFailureSet` per input build. Its `Failures`
  property holds the per-test objects for scripting.
- `-HistoryCount` (1–50, default config `testResults.historyCount`, 10) counts runs including the
  current one. `-HistoryScope` defaults to config `testResults.historyScope` (`SameBranch`, Q-22).
- `Export-AdoBuildTestFailure` writes one report per input set. `-Path` may name an `.html` file
  only when exactly one set arrives; with several sets it must be a directory, and each later set
  gets a per-input error. The default directory is Downloads (§13.2).
- Verbs and nouns follow §16.1; the noun choice is covered by Q-13.

### 15.9 Retrieval

All requests are sequential (§1.4) and every stage observes cancellation (§3.3).

1. **Build.** BuildGet, unless an `AdoBuild` was piped. Validate `CollectionUri` first (§4.4).
   The build's `uri` (`vstfs:///Build/Build/<id>`) filters test runs; if absent, compose it from
   the numeric ID [Verify V-25].
2. **Runs.** TestRunsList with `buildUri` and `includeRunDetails=true`, all pages. No runs →
   `NoTestRuns` (Info); the report is still produced with summary and history. A run or build not
   completed → `TestRunInProgress` (Warning).
3. **Pass 1 — listing.** TestResultsList per run with `detailsToInclude=None`, all pages, keeping
   only the lightweight fields needed for identity, outcome, grouping, and ordering
   (`id`, `outcome`, `automatedTestName`, `automatedTestStorage`, `testCaseTitle`,
   `resultGroupType`, `startedDate`, `testCase`) [Verify V-20]. Build the identity/attempt map
   (§15.10) and classify.
   The server `outcomes` filter MUST NOT be used until V-20 confirms its semantics for rerun
   groups; filtering early would hide a flaky test whose final result passed.
4. **Pass 2 — detail.** For every result record holding an attempt of a reported identity
   (failed-class or not), TestResultGet with `detailsToInclude=Iterations,WorkItems,SubResults`
   [Verify V-21]. Rerun sub-results arrive with their parent, so a rerun group costs one request.
   Stop
   detailing after `maximumReportedFailures` identities, in report order (§15.14):
   `FailureLimitExceeded` (Error), `Status = Partial`; remaining identities are counted only.
5. **Attachment metadata.** For each detailed result, TestResultAttachmentsList; for each sub-result
   that is an attempt or iteration, TestSubResultAttachmentsList [Verify V-23].
6. **Test Cases.** Distinct valid Test Case IDs from step 4 → one WorkItemsBatch pass (§9.1
   reconciliation rules; fields `System.Id`, `System.Rev`, `System.Title`, `System.State`,
   `System.WorkItemType`).
7. **History.** §15.12.

Request count: runs pages + result pages (current and history builds) + one detail request per
reported result record + attachment lists + ⌈distinct Test Case IDs / 200⌉. S5-1 asserts the bound for a
fixture. `Write-Progress` reports each stage with counts.

### 15.10 Attempts, identity, and flaky tests

Tests may be retried up to N times. The toolkit does not need N: it shows every attempt it finds
and derives the classification itself (DD-019). Server flaky flags, if any exist on Server 2020,
are shown as metadata only [Verify V-22].

**Test identity.** Attempts group by identity within the run and across history builds:

1. Automated results: `automatedTestStorage` (ordinal, ignoring case) plus `automatedTestName`
   (ordinal, case-sensitive, because C# names are). Stability across builds is [Verify V-24].
2. No automated name: `UngroupedTestResult` (Warning); the result is its own identity keyed by run
   and result ID, has one attempt, and has no history cells.

**Attempt sources** (how Server 2020 represents each is [Verify V-22]; which one your pipelines
use is Q-25):

| Source | Shape | Attempt ordering |
| --- | --- | --- |
| In-task rerun (e.g. a test runner's rerun-failed-tests option) | One parent result with `resultGroupType = Rerun` whose sub-results are the attempts | Sub-result order (`sequenceId`, then `startedDate`, then sub-result ID) |
| Job or stage re-attempt | Separate test runs in the same build containing the same identity | Run's pipeline attempt when the run exposes it, else run `startedDate`, then run ID |
| Single result | One result, no rerun group | Attempt 1 |

Both sources can combine; ordering is by run first, then sub-result. In pass 1 a parent with
`resultGroupType = Rerun` is always a detail candidate whatever its own outcome, because its
failed attempts are only visible in sub-results. Data-driven iterations and other non-rerun group
types (`DataDriven`, `OrderedTest`, `Generic`) are **not** attempts: they render inside their
attempt as sub-results.

**Outcome classes.** Failure-class: `Failed`, `Error`, `Timeout`, `Aborted` (default pending
Q-23). Pass-class: `Passed`. Everything else (`NotExecuted`, `Inconclusive`, `Blocked`,
`Warning`, `NotApplicable`, `NotImpacted`, `None`, `InProgress`, `Paused`, unknown values) is
other-class. Unknown outcome strings are kept verbatim and never fail parsing.

**Classification** of an identity in the current run:

| Attempts | Classification | Reported |
| --- | --- | --- |
| No failure-class attempt | — | No (counted only) |
| At least one failure-class attempt, last attempt pass-class | `Flaky` | Yes |
| At least one failure-class attempt, last attempt not pass-class | `Failed` | Yes |

Every attempt of a reported identity is shown, including passing ones. The report states
"attempt k of m" and shows an attempt strip (`✕ ✕ ✓`).

### 15.11 Model

Types follow §7.1: public sealed, init-only, nullable-annotated, `IReadOnlyList<T>`, no raw JSON.
Unknown scalar top-level fields of a result map to `AdditionalFields` so "all available
information" survives server additions, following the §7.4 value rules.

| Type | Required properties |
| --- | --- |
| `AdoTestRun` | `Id: int`, `Name: string`, `BuildId: int`, `State: string`, `IsAutomated: bool`, `StartedDate: DateTimeOffset?`, `CompletedDate: DateTimeOffset?`, `PipelineAttempt: int?`, `TotalTests: int?`, `OutcomeCounts: IReadOnlyDictionary<string, int>`, `WebUrl: Uri?`, `TeamProject: string`, `CollectionUri: Uri` |
| `AdoBuildTestFailureSet` | `Build: AdoBuild`, `Runs: IReadOnlyList<AdoTestRun>`, `Summary: AdoBuildTestSummary`, `History: IReadOnlyList<AdoBuildTestSummary>` (oldest first, current last), `Failures: IReadOnlyList<AdoTestFailure>`, `FailedCount: int`, `FlakyCount: int`, `Status: Complete \| Partial`, `Diagnostics: IReadOnlyList<AdoDiagnostic>`, `RetrievedAt: DateTimeOffset`, `CollectionUri: Uri` |
| `AdoTestFailure` | `Ordinal: int`, `Classification: Failed \| Flaky`, `TestName: string?` (automated name), `ShortName: string` (last name segment, display only), `Storage: string?`, `Title: string?` (`testCaseTitle`), `Attempts: IReadOnlyList<AdoTestAttempt>`, `TestCase: AdoTestCaseLink?`, `History: IReadOnlyList<AdoTestHistoryEntry>`, `Owner: AdoIdentityRef?`, `Priority: int?`, `CollectionUri: Uri` |
| `AdoTestCaseLink` | `Id: int`, `Title: string?`, `State: string?`, `Rev: int?`, `WebUrl: Uri`, `IsResolved: bool` |
| `AdoTestAttempt` | `Number: int`, `Source: Single \| Rerun \| RunAttempt`, `RunId: int`, `ResultId: int`, `SubResultId: int?`, `Outcome: string`, `OutcomeClass: Pass \| Failure \| Other`, `ErrorMessage: string?`, `StackTrace: string?`, `StartedDate`/`CompletedDate: DateTimeOffset?`, `Duration: TimeSpan?`, `ComputerName: string?`, `RunBy: AdoIdentityRef?`, `FailureType: string?`, `ResolutionState: string?`, `Comment: string?`, `FailingSinceBuildId: int?`, `AssociatedBugIds: IReadOnlyList<int>`, `SubResults: IReadOnlyList<AdoTestSubResult>` (non-attempt groups, same text fields, nested at most 3 levels), `Iterations` (iteration ID, outcome, error message, parameters, action results), `CustomFields: IReadOnlyDictionary<string, object?>`, `AdditionalFields: IReadOnlyDictionary<string, object?>`, `Attachments: IReadOnlyList<AdoTestAttachment>`, `WebUrl: Uri?` |
| `AdoTestAttachment` | `Id: int`, `RunId: int`, `ResultId: int`, `SubResultId: int?`, `FileName: string` (remote; display only), `Comment: string?`, `Size: long?`, `AttachmentType: string?`, `Kind: Png \| Json \| Html \| Other`, `LocalRelativePath: string?` and `DownloadStatus: NotRequested \| Downloaded \| TooLarge \| BudgetExceeded \| Failed \| ContentMismatch` (both set only by export) |
| `AdoTestHistoryEntry` | `BuildId: int`, `BuildNumber: string`, `Outcome: Passed \| Failed \| Flaky \| Other \| NotRun \| Unavailable`, `IsCurrent: bool`, `WebUrl: Uri` |
| `AdoBuildTestSummary` | `BuildId: int`, `BuildNumber: string`, `SourceBranch: string?`, `FinishTime: DateTimeOffset?`, `Result: string?`, `Passed: int`, `Failed: int`, `Flaky: int`, `Other: int`, `IsAvailable: bool`, `IsCurrent: bool`, `WebUrl: Uri` |

Field names in parentheses are the expected wire names [Verify V-20, V-21, V-23]. Every field
in `AdoTestAttempt` other than `RunId`, `ResultId`, `Outcome`, and `Number` is optional:
absent fields are null or empty and are omitted from the report, not shown blank.

### 15.12 Run history

**Window.** The current build plus up to `HistoryCount − 1` earlier builds of the same definition:
BuildsList with `definitions=<id>`, `statusFilter=completed`, `queryOrder=finishTimeDescending`,
`maxTime=<current finish or queue time>`, and, for `SameBranch`, `branchName=<current
sourceBranch>` [Verify V-25]. The current build is always included and last, even when it is
still running or is older than the latest builds, so the highlight is always present. Fewer
builds than requested is normal and not a diagnostic.

**Data.** For each earlier build, TestRunsList and pass-1 TestResultsList (§15.9 step 3), then the
same identity grouping and classification (§15.10). The current build reuses its own pass 1.
Using one derivation for bars and cells keeps them consistent; server run statistics are not
mixed in.

**Summary counts** (`AdoBuildTestSummary`) count test identities by classification of their last
attempt: `Passed` (pass-class, including flaky), `Failed` (failure-class), `Other`; `Flaky` is
reported separately and is a subset of `Passed`. Ungrouped results count once each.

**Per-test cells** (`AdoTestHistoryEntry`) for each reported identity:

| Cell | Meaning |
| --- | --- |
| `Passed` / `Failed` / `Flaky` / `Other` | The identity's classification in that build |
| `NotRun` | The build's listing was read and the identity is absent |
| `Unavailable` | The build's listing could not be read, was skipped by the request budget, or the build has no readable runs |

**Bounds and failures.** History requests stop at `maximumHistoryRequests`: `HistoryLimitExceeded`
(Warning) and the remaining older builds are `Unavailable`. Any per-build failure other than
authentication, authorization, or cancellation gives `HistoryUnavailable` (Warning) for that build
and does not fail the report. History listings are cached for the invocation (§17).

History does not change `Status`: a report with unavailable history is `Complete` if the current
run is fully detailed.

### 15.13 Attachments

**Scope.** Attachments of each detailed result and of its attempt and iteration sub-results.
Run-level attachments are out of scope (Q-27).

**Kinds.** Decided by the extension of the remote `fileName`, compared ordinally ignoring case,
then confirmed by content where noted. The server's `attachmentType` is shown, not trusted.

| Extension | Kind | Local extension | Content check | Report shows |
| --- | --- | --- | --- | --- |
| `.png` | `Png` | `.png` | First 8 bytes are the PNG signature | Thumbnail (CSS-scaled `<img>`, lazy loading) linking to the full file |
| `.json` | `Json` | `.json` | Parses with `Utf8JsonReader`/`JsonDocument` (max depth 64, no comments, no trailing commas) | Pretty-printed, highlighted inline block up to `maximumInlineJsonBytes`; larger files are linked only |
| `.html`, `.htm` | `Html` | `.html` | None | Link that opens the local file, labeled as test output HTML |
| anything else | `Other` | `.bin` | None | Link to the local file with the original name and size shown |

A failed content check gives `AttachmentContentMismatch` (Warning): the file is still downloaded
and linked with the `.bin` extension, and never previewed.

**Download.**

- Bytes are streamed into the temporary folder with the Download timeout class and toolkit file
  names only (§13.2); the remote file name is never used in a path (§18, item 7).
- Before download, a known `Size` larger than `maximumAttachmentBytes` gives `AttachmentTooLarge`.
  While streaming, the byte count is enforced for unknown or understated sizes and the partial
  file is deleted. The running total stops at `maximumTotalAttachmentBytes`:
  `AttachmentBudgetExceeded` once, remaining attachments `BudgetExceeded`. Order is report order,
  so the most important failures get their files first.
- A download failure after retries is `AttachmentDownloadFailed` (Warning); the report still
  commits. Authentication, authorization, and cancellation remain terminating (§8.3).
- `-SkipAttachments` downloads nothing and creates no folder; attachments are listed with their
  ADO names and sizes.
- Every attachment also shows its ADO name and size, and links to the result in ADO so the
  original stays reachable.

**Local artifacts.** Downloaded attachments are work data, like logs (§15.4, DD-010, DD-020): they
stay on the machine, never enter the repository, and are never written by live checks (§19.5).
They are kept byte-for-byte; the toolkit does not sanitize, resize, or rewrite them. HTML
attachments can contain script and run under the browser's local-file rules when the user opens
them, outside the report's CSP (§18, item 12).

### 15.14 HTML report

Standalone UTF-8 document following §12.2's head rules (`<meta charset>` first, `lang` = report
culture, resource labels, generator marker) and the shared baseline (§12.7). Scripts follow
§15.15. HTML is the only format in v1 (Q-26). The report is complete and usable with scripts
disabled.

**Order of the page**

1. **Top bar** (sticky): pipeline definition name (links to the definition), build number (links to
   the build), source branch, commit (short SHA; links to the commit for Git repositories in the
   collection), build result, finish time, count chips (`✕ Failed n`, `≈ Flaky n`,
   `Attachments n`), and, when `Partial`, a diagnostic banner link. Generated-at, toolkit version,
   server, collection, and project sit in a secondary line.
2. **Run history chart:** one inline SVG stacked bar per build in the window, oldest left, current
   right. Segments: passed, failed, other, with a count label when space allows. The current build
   has the `--current` outline, a "This run" label, and `aria-current="true"`. Unavailable builds
   show an empty hatched bar labeled unavailable. Each bar links to that build's test results view
   and has a `<title>` with build number, branch, finish time, and all four counts. A
   `<details>` data table below repeats the numbers.
3. **Failure index:** one row per reported identity, in report order: status badge, short name
   (links to the card), class or namespace (muted), attempt strip, history strip (one cell per
   build, current outlined), last-attempt duration, Test Case link, attachment count.
4. **Failure cards**, one per identity (below).
5. **Diagnostics** section, when any exist, with localized messages (§7.4 re-render rule).

**Report order.** `Failed` before `Flaky`; within each, by storage then automated name (ordinal),
then result ID. Deterministic, so golden files are stable.

**Failure card**

- Header: status badge, short name as heading, full automated name and storage in monospace,
  "attempt k of m" and attempt strip, Test Case link, links to the result and run in ADO,
  associated bug links, owner, priority.
- Test Case link: `#<id> <title>` with state, linking to the work item route (§12.2). Unresolved
  → `#<id>` link without title plus `UnresolvedTestCase`; invalid reference → no link plus
  `InvalidTestCaseReference`.
- Per-test history strip with a legend, same cells as the index.
- One `<details>` per attempt, newest last. Failure-class attempts are open by default; pass-class
  and other attempts are closed. Each attempt shows: outcome, duration, started/completed, machine,
  run (linked), run by, failure type, resolution state, failing since (build link), comment,
  error message block, stack trace block, sub-results and iterations (each with their own
  outcome, message, and stack trace blocks), attachments, custom fields, and additional fields.
- Anchors use ordinals only: `f-<ordinal>` for cards, `f-<ordinal>-a<number>` for attempts,
  `f-<ordinal>-a<number>-att<id>` for attachments. No `id` derives from remote text.

**Code blocks and highlighting** (DD-021). Error messages, stack traces, and JSON attachments
render as `<pre><code class="lang-…">` with a language label. Lexers are owned, run in C# at export
time, and are pure functions. Invariant, tested on every fixture: concatenating the token texts
reproduces the input exactly; lexers never drop, reorder, or rewrite characters. Each token is
encoded separately (§11.4).

| Lexer | Recognizes |
| --- | --- |
| Stack trace (.NET) | Frame lines `at <method>( <params> )` with optional ` in <path>:line <n>`, and the French runtime form `à … dans …:ligne <n>` [Verify V-29]; namespace, type, method, parameters, path, and line number tokens; `--- End of inner exception stack trace ---`, `--- End of stack trace from previous location ---` and their French forms; exception header lines `<Type>: <message>`; URLs |
| Error message | Leading exception type, quoted strings, numbers, assertion markers (`Expected`, `Actual`, `But was` and their French forms), `<…>` value delimiters used by MSTest, URLs |
| JSON | `JsonDocument` pretty-printed with two-space indentation, then keys, strings, numbers, literals, punctuation. The original bytes remain available through the file link |

- Frames whose method starts with `System.`, `Microsoft.`, `NUnit.`, `Xunit.`, or
  `Microsoft.VisualStudio.TestPlatform.` get a framework class (dimmed). The first non-framework
  frame gets a "first user frame" marker.
- Unrecognized lines render as plain tokens; lexing never fails the report.
- Display limit: 1 MiB of characters per message or stack trace; beyond it, the head is shown with
  `TestTextTruncated` (Info). Truncation happens at a line boundary where one exists.

**Links in messages and stack traces.** Stack traces from C# projects contain links to the
repository; they stay clickable inside the highlighted block:

- Recognize absolute `http` and `https` URLs in the plain text, ending at whitespace, `<`, `>`,
  `"`, `'`, or a backtick. Trailing `.`, `,`, `;`, `:`, `!`, `?` are excluded, and a trailing `)`
  or `]` only when unbalanced within the URL.
- `Uri.TryCreate` must succeed with scheme `http` or `https` and no user info; otherwise the text
  stays plain. `href` is the attribute-encoded absolute URI; the link text is the encoded original
  substring. `rel="noreferrer"`. `javascript:`, `data:`, `file:`, and every other scheme stay
  plain text (DD-008, §12.2).
- Links to any host are allowed, as in §12.2. Whether traces also carry relative links or agent
  file paths that should map to repository links is Q-24; v1 does not rewrite paths.

**Toolkit-built links** come from the collection URL, project, and numeric IDs, never from
response URLs (§12.2): build and its test results view, definition, test run, test result,
commit, Test Case and bug work items. The commit link is the one exception to numeric IDs: it is
built only when the repository type is Git, the repository ID parses as a GUID, and the source
version is 40 hexadecimal characters; each is escaped as a route segment. Routes beyond V-15 are
[Verify V-26].

### 15.15 Scripts and Content Security Policy

This report is the only toolkit output that may contain JavaScript (DD-017). §12.2's no-script
rule is unchanged for the Test Case report.

**CSP** (meta element, before any style or script; one line in the output, wrapped here):

```text
default-src 'none'; script-src 'sha256-<base64>'…; style-src 'unsafe-inline';
img-src 'self' data:; base-uri 'none'; form-action 'none'
```

- `script-src` lists only SHA-256 hashes, one per inline script element. Never `'unsafe-inline'`,
  `'unsafe-eval'`, `'unsafe-hashes'`, `'strict-dynamic'`, nonces, hosts, or schemes.
- `default-src 'none'` leaves `connect-src`, `frame-src`, `object-src`, `font-src`, and
  `media-src` closed. Link navigation is not restricted, so ADO and local attachment links work.
- `img-src 'self'` must allow sibling PNG files when the report is opened from `file://`. Browser
  behavior is [Verify V-27]; if it fails, V-27 records the replacement source expression before
  S5 acceptance. `data:` remains for toolkit glyph images only.
- The hash is computed by the renderer from the exact UTF-8 bytes it writes into the element,
  never hard-coded, so line-ending conversion cannot break it.

**Script assets**

- Scripts are static embedded resources (`Reporting/Assets/test-failures.js`), identical for every
  report and culture. They contain no user-visible words; labels come from the DOM
  (`data-label-*` attributes and `<template>` elements in the report culture).
- No remote or per-report data is written inside a `<script>` element, and no `type="application/json"`
  data blocks. Scripts read state from existing markup (`data-*` attributes, classes, anchors).
- Forbidden in script assets, enforced by a token scan test: `eval`, `Function(`, string
  arguments to `setTimeout`/`setInterval`, `innerHTML`, `outerHTML`, `insertAdjacentHTML`,
  `document.write`, `fetch`, `XMLHttpRequest`, `WebSocket`, `EventSource`, `sendBeacon`,
  `import(`, `localStorage`, `sessionStorage`, `indexedDB`, `window.open`, `postMessage`. Text is
  set with `textContent`; elements are shown and hidden, not created from strings.
- No inline event-handler attributes (`onclick=`) and no `javascript:` URLs; handlers attach from
  the script.
- No external or third-party script is bundled (DD-011 applies to report assets too).

**Behavior** (progressive enhancement only; everything below has a script-free equivalent):

| Feature | With scripts | Without scripts |
| --- | --- | --- |
| Filter | Text filter over short and full names; toggles for Failed, Flaky, has attachments | Full index and all cards visible |
| Navigation | `/` focuses filter; `j`/`k` next/previous card; `o` toggles current attempt; chart bars and history cells scroll to the build or card | Anchors and links |
| Expand | Expand all / collapse all; open attempt from `#f-…-a…` fragment | `<details>` defaults (§15.14) |
| Stack traces | Hide or show framework frames; wrap toggle | All frames shown, horizontal scroll |
| Copy | Copy full name, error message, stack trace (Clipboard API; select-text fallback when unavailable under `file://`) | Manual selection |
| Images | Thumbnail opens a `<dialog>` with the already loaded local image; `Esc` closes | Thumbnail links to the file |
| Print | `beforeprint` opens all failure-class attempts | Defaults |

Controls that need script carry `hidden` in the markup and are revealed by the script, so a
script-blocked browser shows no dead controls. A browser policy that blocks scripts on local files
leaves a complete report (Q-28).

### 15.16 Validation, status, and output

Before commit (§13.4 step 5), in addition to §12.6 "All":

- Generator marker present; `data-failure-count` equals `Failures.Count`; each card's
  `data-attempt-count` equals its model; `data-history-count` equals `History.Count`.
- The CSP meta element is present, and its `script-src` hashes equal the SHA-256 of every
  `<script>` element's contents, one to one; no `<script` element has a `src` attribute.
- Every local attachment link refers to a downloaded file (§13.4).

`Status = Partial` when any Error diagnostic exists (§8.4); `-Strict` is not offered in v1 because
a partial failed-test report is still useful. `Export-AdoBuildTestFailure` emits the report
`FileInfo`; the folder path is on its `AttachmentDirectory` note property when a folder was
created. `-Open` opens the report.

### 15.17 Example

```powershell
Get-AdoBuild -Definition 'Main Build' -Branch main -Latest -Result Failed |
    Get-AdoBuildTestFailure -HistoryCount 15 |
    Export-AdoBuildTestFailure -Culture fr-CA -Path .\triage -Open
```

Writes `.\triage\Build-<id>-TestFailures.html` and, when attachments exist,
`.\triage\Build-<id>-TestFailures.files-<stamp>\`.

---

## 16. Command surface (v1)

| Command | Slice | Pipeline input | Output | Notes |
| --- | --- | --- | --- | --- |
| `Connect-Ado` | S0 | — | `AdoConnection` | `-Profile` / `-CollectionUrl` / `-Project` |
| `Disconnect-Ado` | S0 | — | — | Clears session connection and caches |
| `Get-AdoConnection` | S0 | — | `AdoConnection` | |
| `Test-AdoConnection` | S0 | `AdoConnection` | `AdoConnectionTestResult` | Projects call; success, elapsed, API version, hint |
| `Get-AdoProfile` | S0 | — | `AdoProfile` | Local only |
| `Set-AdoProfile` | S0 | — | `AdoProfile` | Local write, ShouldProcess |
| `Remove-AdoProfile` | S0 | `AdoProfile` | — | Local write, ShouldProcess |
| `Get-AdoProject` | S0 | — | `AdoProject` | |
| `Get-AdoWorkItem` | S1 | `int`, `Id` | `AdoWorkItem` | |
| `Get-AdoTestCase` | S1 / S3 | `int`, `Id`, `AdoTestSuite` | `AdoTestCase` | ById, BySuite |
| `Export-AdoTestCase` | S2 / S3 | `AdoTestCase` | `FileInfo` | One document per call; `-Format Html\|Markdown\|Json`, `-Culture`, `-Path`, `-Open` |
| `Get-AdoTestPlan` | S3 | — | `AdoTestPlan` | |
| `Get-AdoTestSuite` | S3 | `AdoTestPlan` | `AdoTestSuite` | |
| `Invoke-AdoWiql` | S3 | — | `AdoWiqlResult` / `AdoWorkItem` | `-Hydrate` |
| `Get-AdoBuildDefinition` | S4 | — | `AdoBuildDefinition` | |
| `Get-AdoBuild` | S4 | `AdoBuildDefinition` | `AdoBuild` | |
| `Get-AdoBuildTimeline` | S4 | `AdoBuild` | `AdoTimelineRecord` | |
| `Get-AdoBuildFailure` | S4 | `AdoBuild` | `AdoBuildFailure` | |
| `Save-AdoBuildLog` | S4 | `AdoBuildFailure` | `FileInfo` | |
| `Get-AdoTestRun` | S5 | `AdoBuild` | `AdoTestRun` | Runs of one build |
| `Get-AdoBuildTestFailure` | S5 | `AdoBuild` | `AdoBuildTestFailureSet` | One set per build; `-HistoryCount`, `-HistoryScope` |
| `Export-AdoBuildTestFailure` | S5 | `AdoBuildTestFailureSet` | `FileInfo` | One report per set plus attachment folder; `-Culture`, `-Path`, `-SkipAttachments`, `-NoClobber`, `-Open` |

### 16.1 Cmdlet standards

- C# `PSCmdlet` classes, approved verbs, `Ado` noun prefix, explicit parameter sets with a default.
- IDs use `ValidateRange(1, int.MaxValue)`. Enumerations are used only for genuinely closed sets.
  Free-form ADO values (project, branch, names) are not constrained.
- Every network cmdlet has `-Connection`. Project-scoped cmdlets have `-Project`.
- Local writes use `SupportsShouldProcess`. There are no ADO writes in v1.
- Argument completers: `-Profile` (local config) and `-Project` (session cache only, no network
  call while typing).
- Data goes to the success stream only. No `Write-Host`. Verbose, Debug, and Warning streams
  follow §6.8 and §8.4.
- Importing the module MUST NOT change session preferences (`$ErrorActionPreference`, etc.).
- Help: every cmdlet has synopsis, description, parameter help, and at least one example, **in
  English and French**. Binary cmdlets need MAML help files per culture folder; the generation
  tool is Q-11 and folder fallback is V-17.
- Messages written to any stream come from resources (§3.6). Tests fail on a hard-coded
  user-facing string.

---

## 17. Caching

| Scope | Contents | Lifetime |
| --- | --- | --- |
| Invocation | Shared Steps documents, shared parameter sets | One cmdlet invocation (a whole bulk pipeline stage) |
| Invocation | History build listings and Test Case link metadata (§15.12, §15.9) | One `Get-AdoBuildTestFailure` invocation, so several piped builds of one definition share history |
| Session | Projects, work item type category membership | Per connection key, 15 min TTL; cleared by `Connect-Ado` / `Disconnect-Ado` |
| Disk | None in v1 | — |

Session caches are bounded (LRU, a few hundred entries). They never hold credentials. v1 has no
`-NoCache` switch: invocation caches are always correct within a run, and session caches hold
slow-changing metadata only.

---

## 18. Security requirements (consolidated)

1. No secrets in source, configuration, logs, errors, fixtures, or reports. v1 stores none.
2. No real server URLs, project names, identities, or payloads in the repository (DD-010).
   Reports and logs generated at work may contain them and stay on work machines.
3. Credentials never follow redirects (§5.1). Plain-HTTP collections warn.
4. All XML parsing prohibits DTDs, has no resolver, and caps document size. `DataSet.ReadXml` is
   banned.
5. Remote text is untrusted: plain-text conversion (§11), then encoding at every sink.
6. HTML reports carry a restrictive CSP. The Test Case report contains no script. The failed-test
   report may contain only static toolkit scripts allowed by SHA-256 hashes, with no remote data
   inside scripts and no network access (§15.15, DD-017). Generated links come from the
   connection URL plus numeric IDs (validated GUID and SHA for commit links, §15.14). Only `http`,
   `https`, and `mailto` URLs from content become links; stack traces and error messages link
   `http` and `https` only.
7. File names never derive from remote strings. Writes are atomic and support `-WhatIf`.
8. Expansion is bounded by depth and total size. WIQL and paging honor limits; nothing loops
   without bound.
9. v1 performs no ADO mutations (DD-009). Any later write command requires ShouldProcess, explicit
   targets, no automatic retry of non-idempotent requests, and an ADR.
10. Runtime dependencies are BCL-only (DD-011). Build and test packages are pinned centrally.
11. Installed packages are Authenticode-signed; the installer refuses files whose signature is not
    `Valid` (§21.1). The signing certificate never enters the repository.
12. Downloaded test result attachments are untrusted files kept byte-for-byte. They get
    toolkit-generated names and allowlisted extensions, per-file and total size limits, and
    content checks before any preview (§15.13). PNG previews load only as `<img>`; JSON renders as
    encoded tokens. HTML attachments are only linked: opening one runs it under the browser's
    local-file rules, outside the report CSP, so its link is labeled as test output.
13. Deleting previous attachment folders matches an exact name pattern in the destination
    directory only and never follows reparse points (§13.4).

---

## 19. Testing strategy

### 19.1 Layers

| Layer | Tooling | Scope |
| --- | --- | --- |
| Core unit | xUnit (Q-10) in `AdoToolkit.Core.Tests` | Normalization, parser, expansion, parameters, rich text, report models, atomic writer |
| HTTP contract | Same project; fake `HttpMessageHandler` serving fixtures | Routes, escaping, api-version, bodies, paging, chunking, retry, timeouts, error translation, request counts |
| Renderer golden files | Same project | HTML/Markdown/JSON compared to approved files in English and French (timestamps and version normalized) |
| Localization | Same project | Resource parity, placeholder parity, culture-invariant machine formats under `fr-CA` and `en-US` |
| PowerShell surface | Pester 5 against the built module | Parameter sets, pipeline binding, `-WhatIf`, error records and IDs, format views, no session preference changes |
| Live (work only) | `tests/Live/*.Live.ps1` | Read-only confirmation of V-items against the real server (§19.5) |

### 19.2 Synthetic fixture policy

- Fixtures are written by hand from Microsoft documentation and the shapes in this spec. Hosts use
  `.test`; names are fictional.
- `tests/Fixtures/README.md` lists each fixture, what it represents, its source (doc link or
  assumption), and any related V-item.
- When a live check at work finds a mismatch, the developer describes the difference and the
  fixture is rewritten synthetically. Real payloads are never copied. The description is
  structural only: element and field names, value types, versions, header names, status codes,
  and diagnostic codes, never values (DD-022).
- Fixture files follow DD-024: every `.json` and `.xml` file must parse under the template's
  configuration stage, so deliberately malformed or DTD-bearing fixtures use a `.txt` extension;
  log bodies use `.txt`; content above 1 MiB (for example the 2 MiB message or the 300-case suite)
  is generated by the test instead of committed.

### 19.3 Required fixtures

**Steps and expansion**

1. One direct step
2. Multiple direct steps
3. ActionStep with empty Expected Result
4. Unknown `type`
5. `isformatted` absent, with literal `<` and `&` in text
6. HTML-rich action and result (lists, tables, links, images)
7. Doubly encoded markup
8. Literal-entity trade-off (§11.3)
9. French content: accents, `’`, `« »`, no-break spaces before `: ; ! ?`, narrow no-break space
   in numbers, decomposed (NFD) accents; plus emoji and other non-ASCII
10. One Shared Steps group
11. Nested Shared Steps
12. Same Shared Step referenced twice
13. Diamond references
14. Direct cycle
15. Indirect cycle
16. Self-referencing Shared Steps root
17. `compref` without `ref`
18. `compref` whose `ref` is non-numeric
19. `compref` with child elements
20. Referenced item missing (omitted from batch)
21. Referenced item without steps
22. Root without steps: in a test category, and outside one
23. Malformed XML: root, and one Shared Step
24. Depth overflow
25. Exponential fan-out hitting `MaximumExpandedSteps`
26. Hostile content: `<script>`, event attributes, `javascript:` links, CSS
27. DTD / external entity attempt (must be rejected)
28. Numbering: nested groups (`3.3.1`), and an unresolved group followed by direct steps whose
    numbers must not shift
29. URLs in step text: `http`, `https`, and `mailto` become links; `javascript:`, `data:`, and
    `file:` do not

**Parameters**

1. Local data table
2. Shared parameter set mapping
3. Parameters declared with no data
4. Malformed data source XML/JSON
5. Unresolved shared set
6. French parameter names and values (accents in names and data)

**HTTP**

1. Success
2. 401 with an HTML body
3. 403
4. 404 with a JSON error body
5. api-version rejection (400)
6. 302 redirect
7. 429 with `Retry-After` followed by success
8. 503 ×3 (retries exhausted)
9. 500 (no retry)
10. Timeout
11. User cancellation
12. Multi-page continuation header
13. TopSkip paging
14. Batch of 450 IDs (3 requests, order preserved)
15. Batch with omitted IDs
16. Malformed JSON
17. Empty body
18. Project name with spaces and non-ASCII characters (route escaping)

**Timelines**

1. Multi-stage failure
2. Retried job with `previousAttempts`
3. Canceled build
4. `succeededWithIssues` only
5. Failure without a log

**Test results** (Slice 5; shapes per §15.9–§15.11, corrected by V-19–V-25)

1. Build with no test runs
2. Build with two runs: failed, passed, and not-executed identities
3. In-task rerun group: fail → pass (flaky), fail → fail → pass (flaky), fail → fail → fail (failed)
4. Job re-attempt: same identity in a second run of the same build, failing then passing
5. Both attempt sources combined for one identity
6. Data-driven sub-results inside one attempt (not attempts)
7. Result without an automated test name (ungrouped)
8. Outcomes `Error`, `Timeout`, `Aborted`, `Inconclusive`, and an unknown outcome string
9. Test Case reference: valid, missing from the batch, non-integer
10. Result list paging across `$top` pages, including a short nonterminal page
11. More failing identities than `maximumReportedFailures`
12. Build still in progress
13. History: fewer builds than requested; one history build returns 500; request budget reached;
    identity absent from a history build (`NotRun`); `SameBranch` and `AllBranches` queries
14. Stack traces: English frames with paths and line numbers, French runtime frames
    (`à … dans …:ligne`), inner exception separators, async frames, generic types (`List<int>`),
    frames without paths, repository URLs with query strings and trailing punctuation, a
    `javascript:` URL
15. Error messages: MSTest `Expected:<1>. Actual:<2>.`, NUnit and xUnit assertion text, French
    assertion text, 2 MiB message (truncation)
16. Hostile text in test names, error messages, stack traces, comments, custom fields, and
    attachment file names: `<script>`, `</script>`, event attributes, `"` and `'` breaking
    attributes, `../` and reserved device names in file names

**Attachments** (Slice 5)

1. Valid PNG; `.png` without the PNG signature
2. Valid JSON; malformed JSON; JSON above `maximumInlineJsonBytes`; deeply nested JSON (depth 65)
3. HTML containing script
4. Unknown extension; no extension
5. Declared size above `maximumAttachmentBytes`; understated size exceeded while streaming
6. Total budget reached mid-report
7. Download failing after retries; download retry after a partial body
8. Sub-result attachment

**Localization**

1. Every resource key exists in the neutral and French files with a non-empty value and the same
   placeholders
2. The same report rendered in English and French (golden files)
3. A French server error body passed through with the toolkit's French hint appended
4. Unsupported culture (`de-DE`) falls back to English with a warning

### 19.4 Key properties to assert

- Request count bounds for expansion and bulk retrieval (§10.5.1).
- Output order equals input or suite order.
- Repeated references are never reported as cycles; true cycles always are.
- Atomic writer: injected validation failure leaves the destination byte-identical and no temp
  file behind.
- HTML: hostile input appears encoded, no `<script` in output, CSP present.
- Module import leaves `$ErrorActionPreference`, `$ProgressPreference`, and `$VerbosePreference`
  unchanged.
- Running the Core suite with current culture set to `fr-CA` and to `en-US` produces
  byte-identical machine output: JSON, request URIs, WIQL, and file names. This catches
  decimal-comma and date-format bugs. Tests always set culture explicitly and never depend on the
  machine's culture.
- With UI culture `fr-CA`, cmdlet errors and warnings are French while `FullyQualifiedErrorId`,
  diagnostic codes, and property names are unchanged.
- Failed-test report (Slice 5):
  - Lexers: concatenated token text equals the input for every fixture, including hostile and
    truncated input.
  - Every `<script>` element's SHA-256 appears in the CSP exactly once and no other hash does; no
    `src` on scripts; no `unsafe-` keyword in the policy; the script asset passes the forbidden-token
    scan; no fixture string appears inside a `<script>` element.
  - With scripts ignored (structural HTML assertions), every reported failure, attempt, message,
    stack trace, attachment link, and chart value is present.
  - Hostile strings appear only encoded; no `id`, file name, or local path contains remote text.
  - The §13.4 commit order: injected failures at each step leave the previous report and folder
    byte-identical and no temporary items; a simulated stop after step 6 leaves the previous
    report and its folder consistent; re-export removes only matching earlier generation folders.
  - The theme token table meets §12.7 contrast ratios.

### 19.5 Live checks at work

- Files are named `*.Live.ps1`, so the template gate (which runs `*.Tests.ps1`) never picks them up.
- They run only when the developer invokes them with a profile name, e.g.
  `ADOTOOLKIT_LIVE_PROFILE=work`.
- **Read-only.** They print pass/fail per V-item plus diagnostic codes. They never write payloads,
  reports, logs, or attachments to disk.
- **Live acceptance runs** (DD-022) are separate from these scripts. The live criteria in §22 that
  export a report, save a log, or download attachments run the installed toolkit's normal commands
  at work. Their outputs are local work artifacts (§2.3) and stay on the work machine. Only
  pass/fail per acceptance ID and V-item, diagnostic codes, status codes, exception types, and
  structural descriptions (§19.2) leave it. Results are recorded in `docs/plans/README.md`.
- Re-run after any server upgrade (§2.1).

---

## 20. Verification and build integration

- `tools/dev.ps1 verify` is the only gate (DD-012). The template already builds each .NET project
  with `--no-restore` and runs projects marked `IsTestProject=true`. `dotnet restore` is a
  separate, explicitly authorized setup step.
- `Directory.Build.props`: `Nullable=enable`, `TreatWarningsAsErrors=true`,
  `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, deterministic builds, and
  a single version source. Globalization rules CA1304/CA1305/CA1307/CA1309/CA1310/CA1311 are
  errors (§3.6). `SatelliteResourceLanguages=fr` keeps packages from shipping other languages.
- Signing never runs in `verify`: the gate stays offline and certificate-free (§21.1).
- **Pester ordering (Slice 0 spike, V-09):** Pester surface tests need the built module, but the
  template's PowerShell test stage may run before any .NET build. The options were:
  1. Pester `BeforeAll` builds the module (`dotnet build --no-restore`) into a test output
     directory.
  2. A product `tools/check.ps1` owns build → `dotnet test` → Pester, with product Pester files
     named so the built-in stage does not also run them.

  Option 2 is chosen (DD-023). The Slice 0 spike confirms it; if the spike fails, option 1 becomes
  the fallback and DD-023 is revised.
- PSScriptAnalyzer continues to cover all `.ps1` files (tests, live checks, tools).

---

## 21. Packaging and deployment

- `dotnet publish` of `AdoToolkit.PowerShell` produces:

  ```text
  artifacts/AdoToolkit/<version>/
    AdoToolkit.psd1
    AdoToolkit.Format.ps1xml
    AdoToolkit.PowerShell.dll
    AdoToolkit.Core.dll
    fr/AdoToolkit.PowerShell.resources.dll
    fr/AdoToolkit.Core.resources.dll
    en-US/AdoToolkit.PowerShell.dll-Help.xml
    fr/AdoToolkit.PowerShell.dll-Help.xml    (Q-14; V-17 parent fallback confirmed)
  ```
- The manifest version comes from the single version source. SemVer. The JSON report
  `schemaVersion` is versioned independently.
- **Deployment to work:** clone and build on the work machine, where the SDK is present, then copy
  into `$HOME\Documents\PowerShell\Modules\AdoToolkit\<version>` with a product install script.
  Resolve Documents as the user's known folder, as PowerShell does for its user module path, so
  folder redirection is honored. Binaries are never carried from the home PC.

### 21.1 Code signing

Scripts on the work machine must be signed to run.

1. **What is signed.** A product signing script, run on the work machine after `dotnet publish`
   and before installation, Authenticode-signs every `.psd1`, `.ps1xml`, and `.ps1` in the package
   and every `.dll` the toolkit builds, including the satellite resource assemblies. Execution
   policy checks script files only. DLLs are signed as well in case AppLocker or WDAC rules apply
   (Q-16).
2. **Certificate.** Comes from the team's code-signing certificate in the user's certificate store
   (`Cert:\CurrentUser\My`, code-signing EKU). It is never exported, copied, or referenced by
   thumbprint in the repository. The thumbprint is a signing-script parameter or local
   configuration.
3. **Timestamp.** Signatures are timestamped so they stay valid after the certificate expires. The
   timestamp server is Q-16.
4. **Order.** Any change to a signed file invalidates its signature, so signing is the last step
   before installation, and nothing edits the package afterwards.
5. **Install check.** The install script runs `Get-AuthenticodeSignature` on every signable file
   and refuses to install unless all are `Valid` and signed by the expected certificate.
6. **Why compiled cmdlets help.** The only scripts in the shipped module are the manifest and the
   format file, so the signed script surface stays tiny.
7. **Development at work.** The repository's own scripts (template `tools/`, Pester `.Tests.ps1`
   files, `*.Live.ps1` checks) fall under the same policy. How the inner loop runs there is Q-16.

---

## 22. Delivery slices and acceptance criteria

Each slice is useful end to end and passes `dev.ps1 verify` before the next begins. Live
acceptance happens at work via §19.5: read-only live checks plus manual acceptance runs (DD-022).
Plans, gates, and the live results record are in `docs/plans/`.

### Slice 0 — Foundation

**Deliverables:**

- Solution skeleton (§3.5) and build settings
- Configuration and profiles
- URI normalization
- Windows auth handler
- HTTP pipeline with endpoint registry, retry, timeouts, and errors
- Cmdlet base with pipeline-thread marshalling and cancellation
- `Connect-Ado`, `Disconnect-Ado`, `Get-AdoConnection`, `Test-AdoConnection`, profile cmdlets,
  `Get-AdoProject`
- Pester integration decision (V-09)
- Localization plumbing: neutral and French resources, culture selection, `Accept-Language`,
  globalization analyzers

| ID | Acceptance |
| --- | --- |
| S0-1 | `tools/dev.ps1 verify` passes with zero warnings |
| S0-2 | Normalization table tests cover every rule in §4.2 |
| S0-3 | HTTP fixtures 1–13 and 16–18 (§19.3) pass, including retry timing via an injected clock |
| S0-4 | Cancellation propagates to an in-flight fake request; the cmdlet stops without a stack trace |
| S0-5 | Profile cmdlets honor `-WhatIf`; config writes are atomic; `ADOTOOLKIT_CONFIG_PATH` isolates tests |
| S0-6 | Module import changes no session preferences |
| S0-7 | Resource parity tests pass; under UI culture `fr-CA` an authentication failure reads in French with an unchanged `FullyQualifiedErrorId` |
| S0-8 | The Core suite passes with current culture `fr-CA` and `en-US`, with identical machine output |
| S0-9 (live) | `Connect-Ado -Profile work; Test-AdoConnection; Get-AdoProject` succeeds at work from a signed installed module (§21.1) |

### Slice 1 — Work items and the test step model

Delivered in three increments that keep the S1 IDs: **1A** work items (`Get-AdoWorkItem`),
**1B** pure parsers (steps XML, parameters, rich text, diagnostics), **1C** capability detection,
two-phase expansion, and `Get-AdoTestCase -Id`.

**Deliverables:**

- `Get-AdoWorkItem`
- Steps parser
- Test-capability detection
- Two-phase Shared Steps expansion
- Parameters
- Rich-text converter
- `Get-AdoTestCase -Id`
- Diagnostics and `-Strict`

| ID | Acceptance |
| --- | --- |
| S1-1 | 450 IDs → 3 batch requests; output order equals input order; omitted IDs → one error each, others emitted |
| S1-2 | Steps and parameter fixtures (§19.3) produce the documented rows, `3.1`-style numbers, sequences, and diagnostics; an unresolved group does not shift later numbers |
| S1-3 | Repeated and diamond references expand fully with no cycle diagnostic; direct, indirect, and self cycles yield `CircularSharedStepReference` with the chain |
| S1-4 | Depth limits produce unexpanded group rows; the size limit emits one `Truncated` row and stops |
| S1-5 | Rich-text fixtures match expected plain text, including nested-encoding and literal-entity cases |
| S1-6 | A case with 3 levels of Shared Steps issues at most 4 batch requests |
| S1-7 (live) | V-01, V-02, V-03, V-05, V-10, V-13 confirmed or fixtures corrected |

### Slice 2 — Test Case reports

**Deliverables:**

- Report model
- HTML, Markdown, and JSON renderers
- JSON schema
- Report labels in English and French, `-Culture`
- Report metadata and hyperlinks
- Atomic writer
- Downloads default
- `Export-AdoTestCase` for single cases

| ID | Acceptance |
| --- | --- |
| S2-1 | Golden files match for direct, nested, partial, parameterized, and French-content cases, each in English and French report cultures |
| S2-2 | Hostile-content fixture renders encoded; output contains no `<script`; CSP present |
| S2-3 | Injected validation failure leaves an existing report byte-identical and no temp files |
| S2-4 | `-WhatIf` writes nothing; `-NoClobber` refuses existing files; output is `FileInfo` |
| S2-5 | JSON output validates against `testcase.v1.schema.json` |
| S2-6 | Header shows server URL, project, and available metadata; the test case and every Shared Steps group link to the constructed web URLs; content `javascript:` URLs are never links |
| S2-7 | French no-break spaces around `: ; ! ? « »` survive into HTML and Markdown |
| S2-8 (live) | A real multi-level test case exports in both languages, reads correctly in a browser and in Print to PDF, and its links open the right items (V-15) |

### Slice 3 — Bulk test export

**Deliverables:**

- `Get-AdoTestPlan`
- `Get-AdoTestSuite`
- `Get-AdoTestCase -PlanId -SuiteId [-Recurse]`
- `Invoke-AdoWiql [-Hydrate]`
- Shared cache across roots
- Multi-case document with cover header and linked table of contents
- Progress

| ID | Acceptance |
| --- | --- |
| S3-1 | Continuation-header paging returns all plans, suites, and suite test cases in order |
| S3-2 | Recursive suite traversal is depth-first in suite order; a case in two suites is fetched once and emitted twice with `Suite` set |
| S3-3 | WIQL `-Hydrate` preserves query order; reaching the cap raises an error, never truncates |
| S3-4 | 300 cases sharing 20 Shared Steps over 2 levels: at most 2 + 1 + 1 batch requests plus suite paging |
| S3-5 | Any number of cases exports as one document with a linked TOC grouped by suite and `data-case-count` matching; a 300-case fixture renders by streaming; `-Open` opens that one file |
| S3-6 (live) | V-04, V-06 confirmed; a real suite exports end to end at work |

### Slice 4 — Pipeline failure triage

**Deliverables:**

- `Get-AdoBuildDefinition`
- `Get-AdoBuild`
- `Get-AdoBuildTimeline`
- `Get-AdoBuildFailure`
- `Save-AdoBuildLog`

| ID | Acceptance |
| --- | --- |
| S4-1 | Timeline fixtures yield the documented failures, paths, and attempts |
| S4-2 | `-Latest -Branch main -Result Failed` builds the expected query |
| S4-3 | Logs stream to disk atomically; `-Tail` requests only the needed range; download timeout class applies |
| S4-4 (live) | V-11, V-14 confirmed; a real failed run is triaged at work |

### Slice 5 — Failed-test report

Separate from Slice 4 (DD-016). Requires Slices 2 and 4. The shared visual baseline (§12.7)
ships here.

**Deliverables:**

- Test-area endpoints in the registry (§6.2)
- `Get-AdoTestRun`, `Get-AdoBuildTestFailure`, `Export-AdoBuildTestFailure`
- Two-pass retrieval, identity and attempt grouping, flaky classification (§15.9–§15.10)
- Run history and per-test history (§15.12)
- Attachment download with limits and content checks (§15.13) and the §13.4 commit order
- Shared visual baseline, lexers, inline SVG charts, HTML renderer (§12.7, §15.14)
- Static script asset and hash-based CSP (§15.15)
- English and French labels for the report and new messages

| ID | Acceptance |
| --- | --- |
| S5-1 | Test result fixtures 1–12 (§19.3) yield the documented identities, attempts in order, classifications, diagnostics, and a request count within the §15.9 bound |
| S5-2 | Fail-then-pass attempts from an in-task rerun group and from a job re-attempt are both reported as `Flaky` with every attempt shown; a test that only passed is not reported; data-driven sub-results are never counted as attempts |
| S5-3 | History fixture 13: the window ends at the current build, which is highlighted; bars and cells agree; an unreadable history build gives `HistoryUnavailable` and the report still commits |
| S5-4 | Golden HTML in English and French for failed, flaky, partial, and hostile fixtures; stack trace and message fixtures highlight as documented and repository URLs are links; `javascript:` never is; lexer round-trip holds |
| S5-5 | CSP and script properties in §19.4 hold; the report passes structural completeness assertions with scripts ignored |
| S5-6 | Attachment fixtures: valid PNG → thumbnail; invalid signature → linked `.bin` with `AttachmentContentMismatch`; JSON inline up to the limit; HTML linked as a local file; size and budget limits enforced; `-SkipAttachments` downloads nothing and creates no folder; all local names are toolkit-generated |
| S5-7 | §13.4: injected failures leave the previous report and folder byte-identical with no temporary items; re-export removes the previous generation folder only; `-WhatIf` writes nothing; `-NoClobber` refuses before any download |
| S5-8 | Test Case links: valid ID links with title and state; missing ID keeps the link with `UnresolvedTestCase`; invalid reference gives no link and `InvalidTestCaseReference` |
| S5-9 (local spike) | V-27 confirmed in the browsers used at work (Q-28): the hash-allowed script runs from `file://`, sibling PNGs load under the CSP, HTML attachments open, and the report is complete with scripts blocked |
| S5-10 (live) | V-19–V-26 and V-29 confirmed or fixtures corrected; a real run with failed and flaky tests is reported at work in both languages and its links open the right items |

### Backlog (not scheduled)

- One report file per test case (`-Layout Files`)
- Test step attachments and images (Q-02)
- Failed-test report: JSON output (Q-26), run-level attachments (Q-27), stack-trace path mapping to
  repository links (Q-24), passed-test reporting, comparing two arbitrary runs
- Historical export via `asOf` (Q-05)
- Sanitized rich formatting
- Work item writes
- PRs and repositories
- Artifacts
- Interactive selectors
- `ado` executable or TUI
- Azure DevOps Server 2022+
- Azure DevOps Services and Entra
- Measured concurrency
- Languages beyond English and French

---

## 23. Design decisions

| ID | Decision | Status |
| --- | --- | --- |
| DD-001 | PowerShell is the user surface, implemented as compiled C# cmdlets over a PowerShell-free `AdoToolkit.Core` library. Replaces v0.1 DD-001/DD-010 ("C# later"): moving script functions to binary cmdlets later would change object types, error records, and binding behavior anyway. | Accepted (pass 1) |
| DD-002 | Integrate through an owned thin REST layer over `HttpClient`. No Azure CLI, browser automation, or Microsoft client libraries at runtime. | Accepted |
| DD-003 | v1 targets Azure DevOps Server 2020 only. API versions are pinned per endpoint in the registry; exact preview versions are allowed where 6.0 has no GA route. | Accepted (pass 1) |
| DD-004 | v1 authentication is Windows integrated only, behind a credential provider seam. | Accepted (pass 1) |
| DD-005 | Retrieval and rendering are separate; export is an explicit second command. | Carried from v0.1 |
| DD-006 | Use capabilities and stable reference names (fields, work item type categories), never localized display labels. | Carried, strengthened |
| DD-007 | Shared Steps expansion is two-phase: batched level-by-level resolution, then pure expansion with active-path cycle detection, depth and size limits, and diagnostics. | Accepted |
| DD-008 | Remote rich text is untrusted; v1 renders plain text only, encodes at every sink, and turns only `http`/`https`/`mailto` URLs into links. | Accepted (pass 1, links pass 2) |
| DD-009 | v1 performs no writes to ADO. Local writes use ShouldProcess and atomic replacement. | Accepted |
| DD-010 | Work data never leaves the work environment: synthetic fixtures only, read-only opt-in live checks that persist nothing. | Accepted (pass 1) |
| DD-011 | Core runtime dependencies are BCL-only in v1; any third-party runtime dependency needs a DD entry and a load-isolation plan. | Accepted |
| DD-012 | The template's `tools/dev.ps1 verify` is the single verification entry point. | Accepted |
| DD-013 | English and French are both first-class for all user-facing text. The scripting API (names, codes, IDs, JSON properties) stays English, and machine formats are culture-invariant. | Accepted (pass 2) |
| DD-014 | Every export produces one document; step numbers use ADO outline style with numbered Shared Steps group rows. | Accepted (pass 2) |
| DD-015 | Packages are Authenticode-signed on the work machine before installation and verified by the installer. Signing stays out of `verify`. | Accepted (pass 2) |
| DD-016 | The failed-test report is its own slice (Slice 5, after Slice 4), specified in §15.6–§15.17 of the pipeline triage chapter. Slice 4 stays build timeline failures and logs. Reason: it uses a different REST area with preview endpoints and a larger verification surface, and adds multi-file output and a scripted report; keeping it separate lets timeline triage ship without waiting on V-19–V-29. Placed inside §15 so later section numbers stay stable. | Accepted (pass 4) |
| DD-017 | JavaScript is allowed only in the failed-test report: static toolkit-authored inline scripts permitted by CSP SHA-256 hashes, nothing loaded from outside, no remote data inside scripts, no network or storage APIs, and a report that is complete without scripts. The Test Case report stays script-free (§12.2). DD-008 encoding and link rules apply unchanged. | Accepted (pass 4) |
| DD-018 | Toolkit HTML reports share one visual baseline (§12.7): dark design tokens, system fonts, inline assets only, accessible status glyphs, light print styles. The failed-test report adopts it first; Test Case report adoption is Q-21. | Accepted (pass 4) |
| DD-019 | Failed and flaky classification is derived by the toolkit from every attempt of a test identity in the run (§15.10), not from server flags. Every attempt of a reported test is shown. Attempt sources on Server 2020 remain V-22. | Accepted (pass 4) |
| DD-020 | Test result attachments are downloaded into a generation folder beside the report under toolkit-generated names, kept byte-for-byte, previewed only after content checks, and treated as local work artifacts like logs (DD-010). Supersedes the v1 exclusion of attachment downloads for test results only (§1.4). | Accepted (pass 4) |
| DD-021 | Syntax highlighting and charts are produced in C# at export time (owned lexers, `System.Text.Json`, inline SVG); scripts only add interaction. Keeps runtime dependencies BCL-only (DD-011), avoids bundling a third-party highlighter, and keeps output golden-testable. | Accepted (pass 4) |
| DD-022 | Live activity at work has two forms. **Live checks** (`tests/Live/*.Live.ps1`) are opt-in and read-only, persist nothing, and print only pass/fail per ID plus diagnostic codes. **Live acceptance runs** use the installed toolkit's normal commands (for example exporting a report or saving a log). Their files are local work artifacts (§2.3) that never leave the work machine. Only pass/fail per acceptance ID and V-item, diagnostic codes, status codes, exception types, and structural descriptions without values cross to the home PC, where fixtures are rewritten synthetically (§19.2). Clarifies DD-010; resolves Q-18. | Accepted (v1.0) |
| DD-023 | Product validation lives in `tools/check.ps1`, the template's extension point; template gate implementation stays unchanged. The 2026-09-15 correction isolates invocation-scope regression tests in fixture repositories so deliberately missing product prerequisites do not fail unrelated tooling assertions. Order: check restore assets (exit 2 if missing) → `dotnet build AdoToolkit.slnx --no-restore` → `dotnet test --no-build --no-restore` (Core suite under `fr-CA` and `en-US`, S0-8) → publish the module into a staging folder under `artifacts/` with generated MAML help → inspect package contents (§3.2) → Pester in a child `pwsh -NoProfile` process that imports the staged module. Product Pester files are named `*.Pester.ps1` (Pester `Run.TestExtension`), so the template's built-in stage, which runs every `*.Tests.ps1` before external stages, never runs them against a missing build. The whole script stays within the template's 300-second stage timeout. Resolves Q-19; V-09 confirms it in Slice 0, with §20 option 1 as the fallback for product ordering failures. | Accepted (v1.0); V-09 confirmed 2026-09-15 (fixture correction) |
| DD-024 | Repository names respect the template's discovery, validation, and agent-permission rules. Under `src/`, `tests/`, and `docs/`, no directory is named `bin`, `obj`, `artifacts`, `build`, `dist`, `coverage`, `target`, `TestResults`, `secret`, or `secrets` in any letter case, and no file name matches a sensitive pattern (`*.log`, `*credentials*`, `*.local.*`, `*.key`, `*.pem`, `*.pfx`, `*.p12`, `.env*`). Committed `.json` and `.xml` files must parse under the configuration stage; deliberately malformed or DTD-bearing fixtures use `.txt`. Fixtures above 1 MiB are generated by tests. The §3.5 `TestResults/` folders are therefore `TestRuns/`. Reason: excluded paths are invisible to `verify` and ignored by Git, invalid `.json`/`.xml` fails the gate, and agents are denied reads of sensitive names. | Accepted (v1.0) |

---

## 24. Open questions for the next pass

IDs are stable; resolved questions move to §24.1. At acceptance (v1.0) every question that
affects a slice received a default in §24.1; the owner may override any default, which then
needs a revision entry and an update to the affected slice plan. Q-18, Q-19, and Q-20 were
restored there. The question below affects no v1 slice and stays open.

| ID | Question | Why it matters |
| --- | --- | --- |
| Q-12 | If a second auth mode is ever needed (PAT for Server), which secret store: SecretManagement or Windows Credential Manager? | Only if §5.3 is exercised; not blocking v1 |

### 24.1 Resolved

"Answer" marks owner answers from pass 2; "Default" marks v1.0 acceptance defaults, which stand
until the owner overrides them.

| ID | Question | Answer | Where applied |
| --- | --- | --- | --- |
| Q-01 | Step numbering style | Answer: outline numbering `3.1`, `3.2`, `3.3` | §7.3, §10.5.4, DD-014 |
| Q-02 | Step attachments and inline images: ignore, list, or download? | Default: ignore step attachments in v1 (not listed, not downloaded); inline images render as `[image]` or `[image: <alt>]` per §11.2. Backlog item unchanged | §1.4, §11.2, Slice 2 plan |
| Q-03 | One document or per-case files? | Answer: one document is enough for v1 | §12.5, §13.2, DD-014 |
| Q-04 | Can scripts be signed at work? | Answer: yes | §21.1, DD-015; details in Q-16 |
| Q-05 | Historical export as of a date? | Default: not in v1. Hydration and Shared Steps use current revisions; `AsOf` is provenance only. Backlog item unchanged | §9.2, Slice 3 plan |
| Q-06 | One collection or several? | Default: keep multiple named profiles (§4.1) with one session connection per runspace and `-Connection` overrides | §4, Slice 0 plan |
| Q-07 | YAML, classic, or both? Log tails or full logs? | Default: both, through the same Build timeline; YAML fixtures first. `Save-AdoBuildLog` saves the full log unless `-Tail` is given; no default tail | §15.1–§15.4, Slice 4 plan |
| Q-08 | Server information and links in reports? | Answer: show server names and info; reports stay local; hyperlinks welcome | §2.3, §12.2, §18 |
| Q-09 | Languages | Answer: full English and French support | §3.6, §11.5, §19, DD-013 |
| Q-10 | C# test framework | Default: xUnit v3 (`xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`), versions pinned centrally; assertions with xUnit `Assert` only | §19.1, Slice 0 plan |
| Q-11 | Help: PlatyPS or hand-maintained MAML? | Default: `Microsoft.PowerShell.PlatyPS` 1.x as a build-time tool, installed with authorization like Pester. Markdown sources in `docs/commands/en-US/` and `docs/commands/fr-CA/`; MAML generated into the staged and published package, never committed. If PlatyPS proves unsuitable or unavailable at work, fall back to hand-maintained MAML and revise this row | §16.1, §21, DD-023, Slice 0 plan |
| Q-13 | Is `AdoToolkit` / `Ado` final? | Default: yes, both final | §3, §16, Slice 0 plan |
| Q-14 | `fr-CA`, `fr-FR`, or both? French language pack? | Default: one neutral `fr` resource wording for both regions, no regional satellites; `fr-CA` is the tested French culture (examples, golden files); report dates and numbers follow the culture requested. Help is authored once in French and installed where V-17 shows `Get-Help` finds it: `fr/` if parent-culture fallback works, otherwise identical `fr-CA/` and `fr-FR/`. The toolkit never depends on the server language pack (DD-006); UI wording stays V-16 | §3.6, §21, Slices 0, 2, 5 plans |
| Q-15 | One language or bilingual labels per report? | Default: one language per report | §3.6, §12, Slice 2 plan |
| Q-16 | Execution policy, AppLocker/WDAC, timestamp server | Default: assume Group Policy enforces signing and `-ExecutionPolicy Bypass` has no effect. Sign `.psd1`, `.ps1xml`, and every toolkit DLL including satellites. The signing script requires a timestamp server parameter unless `-NoTimestamp` is passed explicitly (with a warning). `verify` runs on the home PC; at work only the package, sign, install, and live scripts run, signed in the local work copy from an interactive console when policy requires it, and those signatures never return to the home source. Confirm with `Get-ExecutionPolicy -List` at work | §21.1, DD-015, Slice 0 plan |
| Q-17 | Report header metadata and custom fields | Default: fields as proposed in §10.1 and §12.2; no custom fields in reports (scripts can read `AdoWorkItem.Fields`) | §10.1, §12.2, Slices 1–2 plans |
| Q-18 | DD-010 and §19.5 say live checks persist nothing, yet S2-8, S3-6, S4-4, and S5-10 need real exports at work and S1-7, S5-10, and V-29 need real shapes described. Which rule governs? | Resolved by DD-022: read-only live checks versus manual live acceptance runs whose outputs stay at work; only pass/fail, codes, and structural descriptions leave | §2.3, §19.2, §19.5, §22, DD-022 |
| Q-19 | Should product validation be owned by a product `tools/check.ps1` (replacing inferred .NET stages) instead of template changes, and how are Pester surface tests ordered after the build? | Resolved by DD-023: yes; `check.ps1` orders build, tests, staged publish, and Pester; product Pester files are `*.Pester.ps1`. V-09 confirms | §3.5, §20, DD-023, Slice 0 plan |
| Q-20 | Does the work network need proxy settings beyond the Windows system proxy (explicit proxy, bypass list, separate credentials)? | Default: no. v1 uses the system proxy only (§5.1) and has no proxy configuration keys. If V-08 fails because of the proxy, S0-9 stays open and a DD entry adds the smallest configuration needed | §5.1, V-08, Slice 0 plan |
| Q-21 | Should the Test Case report adopt the dark baseline? | Owner revision 2026-09-15: dark charcoal/graphite screen styling, compact metadata, section navigation and responsive tables; retain §12.2 step-card layout, light print output and no scripts. The Test Case palette is scoped independently of the later failed-test report | §12.2, §12.7, DD-018, Slice 2 plan |
| Q-22 | History branch scope, pull-request builds, run count | Default: `SameBranch`, 10 runs including the current one. Pull-request builds follow their own `sourceBranch` value (for example `refs/pull/<n>/merge`); `-HistoryScope AllBranches` widens it | §4.1, §15.8, §15.12, Slice 5 plan |
| Q-23 | Failure outcomes; flaky in bars | Default: `Failed`, `Error`, `Timeout`, `Aborted` are failure-class; flaky tests count as passed in bars, with a separate Flaky count | §15.10, §15.12, Slice 5 plan |
| Q-24 | Repository links in stack traces | Default: link absolute `http(s)` URLs only; no relative-link or agent-path mapping in v1 | §15.14, Slice 5 plan |
| Q-25 | How tests are retried | Default: support all three attempt sources in §15.10 with equal priority; V-22 at work decides fixture corrections | §15.10, V-22, Slice 5 plan |
| Q-26 | HTML only, or JSON too? | Default: HTML only in v1 | §15.14, Slice 5 plan |
| Q-27 | Attachment limits; run-level attachments | Default: limits as specified (50 MiB, 500 MiB, 256 KiB); no run-level attachments | §4.1, §15.13, Slice 5 plan |
| Q-28 | Browser and `file://` policy at work | Default: current Microsoft Edge (Chromium) is the primary browser and Chrome the secondary; assume no policy exception for local files, so the report must be complete with scripts blocked. The S5-9 spike runs at home in both, then is confirmed at work | §15.15, V-27, Slice 5 plan |
| Q-29 | Link failing runs to timeline tasks and logs? | Default: not in v1 | §15.14, Slice 5 plan |
| Q-30 | Dark only, or follow the OS preference? | Default: dark only on screen; light print styles | §12.7, Slice 5 plan |

---

## 25. Verification ledger

| ID | Claim | How to verify | Blocks |
| --- | --- | --- | --- |
| V-01 | `compref` carries the Shared Steps ID in `ref` only and normally has no children | 2026-09-15, 1B–1C: offline parser and end-to-end expansion tests cover ref-only IDs, invalid refs, child skipping, numbering, repeated/diamond references, cycles, depth and size limits. Optional Microsoft helper oracle not run. Live confirmation still required; no oracle agreement claimed | S1 |
| V-02 | `isformatted="true"` marks HTML content; unformatted strings are plain text | 2026-09-15, 1B: offline routing, source retention, rich-text, hostile-content, nested-encoding and French fixtures pass. The server's isformatted convention remains unconfirmed; live check required | S1 |
| V-03 | Formats of `TCM.Parameters`, `LocalDataSource` (local XML, shared JSON), and shared parameter set XML, including how accented parameter names are encoded as XML element names | 2026-09-15, 1B–1C: offline pure parsers and service joins cover assumed declarations, DataSet-style local/shared rows, name-to-ID JSON, decoded French names, first-round shared-set retrieval and missing/malformed sets. Shared row XML is assumed to be in the set work item's Parameters field; same-name columns join by row index. All shapes marked [V-03] in the catalog. Live confirmation required | S1 |
| V-04 | Testplan plans/suites endpoint versions on the server build; GA `test`-area alternatives | Offline 3.1 (2026-09-15): TestPlansList and TestSuitesForPlan (`6.0-preview.1`) and SuiteTestCaseList (`6.0-preview.2`) registered; synthetic fixtures marked [V-04] page through short and empty token pages in order; suite tree, suite path and sibling order follow the assumed `parentSuite` shape and response order. Versions, continuation behavior, shapes, sibling order and the GA `test`-area route are unconfirmed; opt-in `tests/Live/Bulk.Live.ps1` added. Offline 3.2 (2026-09-15): SuiteTestCaseList memberships are read for every suite across continuation pages and ordered by the assumed `order` field; recursive traversal is depth-first in response order; a generated 300-case suite needs 2 + 1 + 1 WorkItemsBatch requests with suite paging counted separately | S3 |
| V-05 | `errorPolicy=omit` returns null entries for missing or inaccessible IDs | Offline 1A (2026-09-15): null placeholders and compact omissions both reconcile by ID; duplicate/unexpected IDs fail. Live shape unconfirmed; opt-in check added | S1 |
| V-06 | WIQL 20,000-result cap and its error shape | Offline 3.1 (2026-09-15): toolkit policy tested; 20,000 or more IDs without `-Top` raise an error with no output, explicit `-Top` sets `LimitApplied`. A synthetic 400 body whose message starts with `VS402337` [V-06] is surfaced with the narrowing hint; other 400s are unchanged. Server truncation or error shape (status, `typeKey`, code) unconfirmed; opt-in live check added | S3 |
| V-07 | Error body fields (`message`, `typeKey`) and `ActivityId` header | Live check | S0 |
| V-08 | Negotiate via `SocketsHttpHandler` with default credentials works on the work network and proxy | Live check | S0 |
| V-09 | Template gate ordering for Pester against a built binary module, as decided in DD-023 | Result: confirmed 2026-09-15, DD-023 (fixture-isolation correction). All seven probes passed on rerun: product ordering, separate Pester discovery, failed-test exit 1, missing-assets exit 2, consecutive builds without locks, skipped-test exit 2 with build checked, and stage duration. Passing project-check durations: 12.424 s and 9.807 s. The original failed probe is retained in the [evidence](plans/slice-0-session-0.1-evidence.md). | S0 |
| V-10 | `fields` and `$expand` cannot be combined | Offline 1A (2026-09-15): local parameter-set exclusion and both request modes tested. Server combination unconfirmed; raw live probe added | S1 |
| V-11 | Timeline record types and `previousAttempts` for YAML runs on Server 2020 | Offline 4.1 (2026-09-15): five synthetic timeline fixtures prove tree order, highest-attempt subtree pruning, deepest failures, canceled-build handling, warnings and missing logs (S4-1). Unknown type names also work. YAML type and retry wire shapes remain assumptions pending the live check | S4 |
| V-12 | Work machine .NET SDK band and NuGet access | Ask / `dotnet --list-sdks` at work | S0 |
| V-13 | Work item type category endpoint and `Microsoft.TestCaseCategory` / `Microsoft.SharedStepCategory` names | 2026-09-15, 1C: offline fixtures cover localized/customized type membership, per-project/session caching, escaped owning-project routes, and failed lookups without caching or NotAStepContainer misclassification. Both category names remain unconfirmed on Server 2020; completed opt-in live script checks membership | S1 |
| V-14 | Versions and paging of projects, build definitions, builds, logs, and log line ranges at 6.0 | Offline 4.1–4.2 (2026-09-15): definitions/builds use 6.0 and continuation headers, including short/empty nonterminal pages, opaque token encoding and repeated-token rejection. Failure derivation reads BuildLogsList once per build; saving each log reads its current count. BuildLog uses text/plain and Download budgets; S4-3 covers byte-preserving atomic streaming, fresh temp files on body retries, and tail 200 of 1,000 requesting startLine=800/endLine=999. Zero/unknown counts and range boundaries are tested. Zero-based inclusive indexing, server paging and line-count shapes remain assumptions; Triage.Live.ps1 checks them in memory at work | S0 / S4 |
| V-15 | Server 2020 web routes: work item (`{c}/{p}/_workitems/edit/{id}`), test plan/suite (`{c}/{p}/_testPlans/define?planId=…&suiteId=…`), build (`{c}/{p}/_build/results?buildId=…`) | Offline 1A, 2.1 and 2.2 (2026-09-15): HTML/Markdown header and content-link tests plus JSON/model tests confirm that report case, Shared Steps and shared parameter links are rebuilt from the connection and owning project; response URLs ignored. Plan/suite routes escape project segments and encode invariant positive IDs. Exact synthetic URLs tested; absent shared metadata remains absent. Live browser confirmation of all routes remains pending. See [2.1 evidence](plans/slice-2-session-2.1-evidence.md) and [2.2 evidence](plans/slice-2-session-2.2-evidence.md). Offline 3.2 (2026-09-15): suite memberships and the multi-case cover rebuild plan and suite links from the connection, project and IDs; response and input URLs are ignored. Offline 4.1 (2026-09-15): Core and Pester tests assert the exact build results route with escaped project and invariant build ID, reconstructed from the active connection; response links ignored | S2 / S4 |
| V-16 | Glossary terms (§3.6) match ADO Server 2020's French interface, including the Slice 5 test result terms | Offline 2.2 (2026-09-15): French report labels/help and typography tests. Offline 5.3 (2026-09-16): failed-test labels, explicit report culture and re-rendered diagnostics tested in en-US/fr-CA; external previews reviewed by the user, followed by requested visual refinements and eight golden files. Fluent terminology review and comparison with the French UI at work remain pending. See [2.2 French review](plans/slice-2-session-2.2-evidence.md#french-text-for-review), [5.3 French review](plans/slice-5-session-5.3-evidence.md#french-text-for-review) | S2 / S5 |
| V-17 | `Get-Help` culture-folder lookup for `fr-CA` / `fr-FR` / `fr` with a binary module's MAML help | Result: confirmed 2026-09-15 on PowerShell 7.6.6. Fresh children under fr-CA, fr-FR, and fr resolve the French compiled help from fr/; en-US resolves English. All eight command topics have synopsis, description, parameters, and examples. No regional help folders or user/system culture changes. [Evidence](plans/slice-0-completion-evidence.md#v-17-help-culture-lookup). | S0 |
| V-18 | Server honors `Accept-Language` for error message text | Live check | S0 |
| V-19 | TestRunsList at 6.0: route `{c}/{p}/_apis/test/runs`, GA version, `buildUri` filter with `vstfs:///Build/Build/{id}`, `includeRunDetails`, `$top`/`$skip` paging; run fields `id`, `name`, `state`, `isAutomated`, `startedDate`, `completedDate`, `totalTests`, run statistics, and whether a pipeline attempt reference exists | Server 6.0 docs, then live check. Offline 5.1 (2026-09-16): TestRunsList registered at `6.0` with TopSkip paging; synthetic fixtures marked [V-19] send `buildUri` and `includeRunDetails=true`, page by the count actually returned and stop on an empty page, and map `id`, `name`, `state`, `isAutomated`, `startedDate`, `completedDate`, `totalTests` and `runStatistics` into `OutcomeCounts`. Runs are ordered by `pipelineReference.pipelineAttempt`, then `startedDate`, then run ID; that attempt reference and every field name remain assumptions. Response URLs are read and dropped. `tests/Live/TestFailures.Live.ps1` checks the route, filter, paging and field presence at work | S5 |
| V-20 | TestResultsList at 6.0: route, version, `$top` maximum per `detailsToInclude`, `$skip` paging, `outcomes` filter semantics for rerun groups; list items include `id`, `outcome`, `automatedTestName`, `automatedTestStorage`, `testCaseTitle`, `resultGroupType`, `startedDate`, `testCase`; outcome value spellings | Server 6.0 docs, then live check. Offline 5.1 (2026-09-16): TestResultsList registered at `6.0`, requested with `detailsToInclude=None` and `$top=1000`, advancing `$skip` by the count returned. The server `outcomes` filter is never sent, and a test asserts no request carries it. Fixtures cover the listed fields, a short nonterminal page, and `Error`/`Timeout`/`Aborted`/`Inconclusive` plus an unknown outcome string kept verbatim. The `$top` maximum, field names, outcome spellings and the filter semantics for rerun groups remain assumptions; the live script reports filtered versus unfiltered counts | S5 |
| V-21 | TestResultGet with `detailsToInclude=Iterations,WorkItems,SubResults`: `errorMessage`, `stackTrace`, `subResults` (with `resultGroupType`, `sequenceId`, outcome, message, trace), `iterationDetails`, `associatedBugs`, `failingSince`, `failureType`, `resolutionState`, `customFields`, `comment`, `computerName`, `durationInMs`, `owner`, `runBy`, `priority`, `testCase.id` as a string | Server 6.0 docs, then live check. Offline 5.1 (2026-09-16): TestResultGet is requested once per reported result record with `detailsToInclude=Iterations,WorkItems,SubResults`. One fixture carries every §15.11 field, and mapping is asserted for messages, traces, durations, identities, failure type, resolution state, comment, priority, `failingSince`, associated bugs, custom fields, sub-results nested at most three levels, and iterations with parameters and action results. `testCase.id` is parsed as a string; unknown scalars reach `AdditionalFields` while unknown objects, arrays and response URLs do not. All field names remain assumptions | S5 |
| V-22 | How Server 2020 records retries: in-task reruns as `resultGroupType = Rerun` sub-results or separate results; job/stage re-attempts as new runs in the same build and how their attempt is identifiable; whether any flaky flag or result metadata exists | Live check against a run with each retry kind (Q-25). Offline 5.1 (2026-09-16): all three attempt sources in §15.10 are implemented and covered — rerun sub-results ordered by `sequenceId` then `startedDate` then sub-result ID, job re-attempts as separate runs of one build, single results, and the two combined for one identity (run first, then sub-result). A `Rerun` parent is always a detail candidate; `DataDriven`, `OrderedTest` and `Generic` groups never become attempts. Grouping is isolated in `AttemptGrouper`. The plan decision on pass-1 limit ordering is recorded: a failure-class rerun parent counts provisionally as Failed, otherwise Flaky. The observed rerun parent outcomes and any flaky metadata are reported by the live script | S5 |
| V-23 | Attachment endpoints: result and sub-result attachment list routes, exact version (`6.0-preview.1` assumed), `testSubResultId` parameter, content download with `Accept: application/octet-stream`, fields `id`, `fileName`, `size`, `comment`, `attachmentType` | Server 6.0 docs, then live check. Offline 5.1 (2026-09-16): result and sub-result attachment lists are registered at `6.0-preview.1` on the same route, the sub-result selected by `testSubResultId`, and content download is registered with `Accept: application/octet-stream` and the Download timeout class. Listing is exercised for each detailed result, each rerun attempt sub-result and each iteration sub-result; `Kind` comes from the remote extension only and `DownloadStatus` stays `NotRequested` until export. Routes, version, fields and byte length equal to `size` remain assumptions. Offline 5.4 (2026-09-16): content is streamed with `Accept: application/octet-stream`, the Download timeout class and `testSubResultId` for sub-result attachments, in report order, into toolkit-named files only; fake-handler tests cover known and streamed size limits, the total budget, retries that restart the file, empty bodies, content checks and hostile remote names, and the export passes the §13.4 commit-order tests. `tests/Live/TestFailures.Live.ps1` also compares a sub-result download with its listed size. The server shapes remain unconfirmed until the live check at work | S5 |
| V-24 | `automatedTestStorage` + `automatedTestName` identify the same C# test across builds and across runs within a build | Live check across two builds. Offline 5.1 (2026-09-16): identity is `automatedTestStorage` compared ordinally ignoring case plus `automatedTestName` compared ordinally, and history cells match identities across builds through that key alone. A result with no automated name is its own identity keyed by run and result ID and has no history cells. Cross-build stability remains unconfirmed; the live script compares the identity sets of two builds of one definition | S5 |
| V-25 | BuildGet route and fields (`uri`, `definition`, `buildNumber`, `sourceBranch`, `sourceVersion`, `repository.type`/`id`, `result`, `status`, `finishTime`, `queueTime`); BuildsList history parameters `definitions`, `branchName`, `maxTime`, `statusFilter`, `queryOrder=finishTimeDescending` | Server 6.0 docs, then live check. Offline 5.1 (2026-09-16): BuildGet is registered at `6.0` and used when no `AdoBuild` is piped, mapping the same fields as BuildsList. The build `uri` filters test runs, and `vstfs:///Build/Build/<id>` is composed when it is absent, with a test for each path. The history window sends `definitions`, `statusFilter=completed`, `queryOrder=finishTimeDescending`, `maxTime` from the current finish or queue time formatted with the invariant culture, and `branchName` for `SameBranch` only. Route, fields and parameter behavior remain assumptions | S5 |
| V-26 | Server 2020 web routes: build test results view with run and result selection (`{c}/{p}/_build/results?buildId=…&view=ms.vss-test-web.build-test-results-tab&runId=…&resultId=…`), test run (`{c}/{p}/_testManagement/runs?_a=runCharts&runId=…`), definition (`{c}/{p}/_build?definitionId=…`), commit (`{c}/{p}/_git/{repositoryId}/commit/{sha}`) | Offline 5.3 (2026-09-16): synthetic exact-route tests cover encoded project/IDs, Git/GUID/40-hex commit guards, Test Case/bug links and rejection of response URLs. [5.3 evidence](plans/slice-5-session-5.3-evidence.md). Live confirmation by opening generated links against Server 2020 remains pending | S5 |
| V-27 | Browser behavior for a report opened from `file://` in Edge and Chrome: meta CSP hash-allowed inline scripts run; `img-src 'self'` loads sibling PNGs (else record the replacement); `<dialog>`, `<details>`, inline SVG `<title>`; links to local HTML files open; Clipboard API availability | Local offline spike at home, then confirm under work browser policy (Q-28) | S5 |
| V-28 | Whether `testresults`-area, result-summary, or test-history query endpoints exist on Server 2020 and could replace per-build listings for history | Server 6.0 docs; optional live check. Not blocking | — |
| V-29 | Language and format of stack traces and assertion messages produced on the build agents (English or French .NET runtime text; runner-specific message forms) | Describe synthetically from a real run at work; rewrite fixtures (§19.2). Offline 5.2 (2026-09-16): synthetic English/French frames, inner-exception/rethrow separators, async/generic methods, framework markers, MSTest/NUnit/xUnit/French assertion text, repository URL boundaries, hostile text and generated 2 MiB truncation are covered. Pure lexer round-trip includes all TestRuns/Attachments text and decoded JSON strings; unknown content stays plain. Runtime language and exact runner wording remain unconfirmed | S5 |

---

## 26. References

- REST API versioning, including the Server version table and preview lifecycle:
  <https://learn.microsoft.com/azure/devops/integrate/concepts/rest-api-versioning?view=azure-devops>
- Work Items — Get Work Items Batch (6.0):
  <https://learn.microsoft.com/rest/api/azure/devops/wit/work-items/get-work-items-batch?view=azure-devops-rest-6.0>
- Suite Test Case — Get Test Case List (6.0-preview.2):
  <https://learn.microsoft.com/rest/api/azure/devops/testplan/suite-test-case/get-test-case-list?view=azure-devops-rest-6.0>
- Build Timeline — Get (6.0):
  <https://learn.microsoft.com/rest/api/azure/devops/build/timeline/get?view=azure-devops-rest-6.0>
- Server-specific 6.0 reference views (use for V-14): the `azure-devops-server-rest-6.0` moniker
  on each REST reference page.
- Resolving PowerShell module assembly dependency conflicts:
  <https://learn.microsoft.com/powershell/scripting/dev-cross-plat/resolving-dependency-conflicts?view=powershell-7.6>
- Test step REST client helper (Test Management helper, V-01 oracle):
  <https://devblogs.microsoft.com/devops/how-to-use-test-step-using-rest-client-helper/>
- Slice 5 pages to check for V-19–V-23 and V-25 (not opened in pass 4; use the
  `azure-devops-server-rest-6.0` moniker): Test Runs — List, Test Results — List and Get, Test
  Attachments — Get Test Result Attachments / Get Test Sub Result Attachments, Builds — Get and
  List, under <https://learn.microsoft.com/rest/api/azure/devops/test/> and
  <https://learn.microsoft.com/rest/api/azure/devops/build/>.
- Content Security Policy hash sources (V-27 background):
  <https://www.w3.org/TR/CSP3/#grammardef-hash-source>
- Secondary source on `compref/@ref` and `LocalDataSource` XML (not authoritative):
  <https://www.grizzlypeaksoftware.com/library/parameterized-tests-and-shared-steps-hwfgom4z>
