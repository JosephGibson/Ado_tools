# Slice 1, session 1A — work items

Date: 2026-09-15. Scope: session 1A of [the Slice 1 plan](slice-1-work-items.md).

## Entry and verification

- Section 11 contains 1A, 1B, and 1C kickoff prompts.
- The roadmap records Slice 0 offline done (M2).
- Entry `pwsh -NoProfile -File .\tools\dev.ps1 verify`: exit **0**, `"Status":"pass"`, no warnings; `project-check` **13.592 s**.
- Implementation verification with the same full command: exit **0**, `"Status":"pass"`, no warnings; `project-check` **15.410 s**. All five stages passed, including 89 tooling Pester tests, Core tests under en-US and fr-CA, package/help validation, and staged-module product Pester tests.
- The final handoff repeats the full gate after this documentation update. The duration above records the implementation verification run.

## Delivered and acceptance coverage

**S1-1** is covered by tagged tests in:

| Test source | Evidence |
| --- | --- |
| `tests/AdoToolkit.Core.Tests/Http/IdChunkingTests.cs` | 450 IDs produce sequential batches of 200, 200, and 50; reversed responses return in input order; duplicates are requested once; cancellation stops later chunks; registry uses POST, 6.0, Query timeout, safe retry |
| `tests/AdoToolkit.Core.Tests/WorkItems/WorkItemServiceTests.cs` | Null and compact omissions reconcile by ID; malformed/unexpected/duplicate IDs fail without retry; request fields and relation expansion follow §9.1; recursive read-only values, typed identities/dates, preserved unknown strings, owning-project links, and safe POST retry |
| `tests/AdoToolkit.PowerShell.Tests/WorkItems.Pester.ps1` | Pipeline by value/property and explicit IDs; invocation-wide deduplication and order; one ObjectNotFound error per absent ID; local parameter rejection; typed mismatches produce no destination request; same-collection objects work and provenance does not leak into later raw IDs |

The module exports `Get-AdoWorkItem` with a format view and compiled help in English and French.
The synthetic fixture catalog lists the new REST fixtures and generated inputs.
`tests/Live/TestCase.Live.ps1` contains opt-in V-05 and V-10 probes; it passes lint and was not run live.

## Spec ledger updates

Only §25 evidence/status cells changed; normative text and acceptance IDs are unchanged.

- **V-05:** both omission shapes tested offline; live server shape still unconfirmed.
- **V-10:** local exclusion and request modes tested; raw combination probe added; no claim about a server restriction.
- **V-15:** work item URL construction tested using the item's own project with escaped segments; response URL ignored. Browser confirmation and other routes remain pending.

## Authorizations and implementation notes

- Used the requested authorization for local inspection, implementation, offline builds, deterministic verification, and loopback-only fake-server tests.
- No restore, dependency package change, installation, external network access, live check, signing, deployment, or Git mutation was performed.
- No sandbox escalation was needed. Git inspection reported that this workspace is not a Git repository.
- No scope or normative-spec deviations. Supporting packaging/help assertions and the mocked package fixture now expect nine commands instead of eight; completeness checks remain enforced. No gate stages or checks were removed.
- A hidden `CollectionUri` parameter binds provenance by property name alongside `Id`, allowing the cmdlet to validate the input collection before buffering IDs. Help documents this adapter detail.
- `AdoWorkItemRelation` uses `Rel`, `Url`, and read-only `Attributes` for the wire relation shape; the spec names the relation type without specifying its member names.

## Open items

- Sessions **1B and 1C**, S1-2 through S1-6, and completion of the live script remain pending. Slice 1/M3 is not complete.
- Work-machine confirmation of V-05, V-10, and V-15 remains pending, along with the later-session live items and S1-7.
- Fluent French review remains pending.

## French text for review

New runtime resource values in `src/AdoToolkit.Core/Resources/Strings.fr.resx`:

| Key | French value |
| --- | --- |
| `WorkItemNotFound` | L’élément de travail {0} est introuvable ou inaccessible. |
| `FieldRelationsConflict` | La sélection de champs et le développement des relations ne peuvent pas être combinés dans cette boîte à outils. |

All prose in [Get-AdoWorkItem French help](../commands/fr-CA/Get-AdoWorkItem.md) is new and needs review: synopsis, description, two example descriptions, five parameter descriptions, common parameters, output description, and notes. Synopsis: « Récupère les éléments de travail par identifiant. »

The format view uses property identifiers. The live script emits only the prescribed status/diagnostic codes. No other French runtime strings were added.
