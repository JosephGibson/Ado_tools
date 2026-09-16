# Slice 4, session 4.1 handoff

Date: 2026-09-15. Session 4.1 is complete; Slice 4 offline completion requires 4.2.

## Gate evidence

| Gate | Observed result |
| --- | --- |
| Entry | Roadmap records Slice 3 offline done (M5); plan §11 lists sessions 4.1 and 4.2 |
| Baseline `verify` | Exit 0, `"Status":"pass"`, all warning arrays empty; `project-check` 26.444 s |
| Completion `verify` | Exit 0, `"Status":"pass"`, all warning arrays empty; `project-check` 25.542 s |
| Acceptance tag scan | S4-1 and S4-2 in Core and product Pester test sources |

The completion gate includes both Core cultures, product Pester, compiled help lookup
under en-US/fr-CA/fr-FR/fr, package inspection, PowerShell lint, 89 tooling tests,
configuration validation and tooling layout. No skipped checks or missing prerequisites.
The recorded completion run precedes the final documentation-only handoff update.

## Delivered

- `Get-AdoBuildDefinition`: paged listing, ID filter, accent-sensitive NFC wildcard matching.
- `Get-AdoBuild`: integer IDs or exact-name resolution with a count-bearing ambiguity error;
  branch normalization, Latest/status/result filters, independent Top limit, typed definition
  input, source/repository/identity metadata and UTC dates. Links are reconstructed.
- `Get-AdoBuildTimeline`: typed build input or BuildId; parent-first tree order with ordered
  children, complete attempt history, issues and log references.
- `Get-AdoBuildFailure`: deepest qualifying records, canceled-build handling, optional warnings,
  latest attempts per identifier, superseded subtree pruning, ancestor paths and error issues.
  One JSON log-list request supplies zero/known/unknown line counts. No log bodies downloaded.
- Core models, four format views, eight Markdown help topics compiled into English and French
  MAML. Export/package assertions now require all 18 implemented commands.

BuildId failure retrieval uses the existing-in-scope BuildsList route with `buildIds` to
obtain build number, definition, branch and result. Pipeline input supplies those fields
without another build request. Collection provenance is checked before HTTP and foreign
inputs report the established `AdoConnectionMismatch` error ID.

## Tests and traceability

- **S4-1:** [BuildFailureDeriverTests.cs](../../tests/AdoToolkit.Core.Tests/Builds/BuildFailureDeriverTests.cs)
  covers the five cataloged timelines, paths, attempts, canceled versus failed builds,
  warnings, null log IDs, zero/unknown counts, error issues, unknown record types, successful
  retries, nonqualifying intermediate nodes, malformed trees/log lists and cancellation.
- **S4-2:** [BuildQueryTests.cs](../../tests/AdoToolkit.Core.Tests/Builds/BuildQueryTests.cs)
  asserts the exact Latest/Branch/Result parameter set, branch normalization, status defaults
  and overrides, exact-name matching, zero/multiple matches, short and empty token pages,
  opaque tokens, repeated-token errors, Top stopping, model mapping, registry and links.
- [Builds.Pester.ps1](../../tests/AdoToolkit.PowerShell.Tests/Builds.Pester.ps1) proves
  definition → build → failure binding, build → timeline binding, owning project, BuildId
  lookup, IncludeWarnings, French ambiguity guidance, NFC wildcard matching, local validation
  and foreign-input rejection followed by continued processing.
- [Fixture catalog](../../tests/Fixtures/README.md#slice-4-session-41) records the synthetic
  YAML/retry shapes [V-11] and continuation/log-list assumptions [V-14].
- Spec §25 **V-11, V-14 and V-15** now record offline evidence. No normative spec changes.

## Authorizations and deviations

No restore, dependency changes, installs, external network, ADO access or Git mutations.
HTTP tests used fake handlers and loopback-only FakeAdoServer. No sandbox block occurred.
No scope deviations; build timeline triage only (DD-016).

## Remaining work

- Session 4.2: Save-AdoBuildLog, byte streaming/atomic writes, Tail/range handling, S4-3,
  live triage script and the complete Slice 4 offline gate.
- At work: S4-4, V-11/V-14 server confirmation and V-15 build-link browser confirmation.
  Definition links also remain a V-26 assumption for later confirmation.
- Fluent French review below. Slice 5/test-result work has not started.

## French text for review

New neutral/French message resource pairs in Core:

| Key | French text |
| --- | --- |
| `BuildDefinitionMatches` | Le nom correspond à {0} définitions. Utilisez -Definition &lt;id&gt;. |
| `BuildDefinitionNotFound` | La définition de build {0} est introuvable. |
| `BuildNotFound` | Le build {0} est introuvable. |
| `InvalidBuildDefinition` | Definition doit être un identifiant entier positif ou un nom exact non vide. |

New French help (synopsis, description, every parameter and examples):

- [Get-AdoBuildDefinition](../commands/fr-CA/Get-AdoBuildDefinition.md)
- [Get-AdoBuild](../commands/fr-CA/Get-AdoBuild.md)
- [Get-AdoBuildTimeline](../commands/fr-CA/Get-AdoBuildTimeline.md)
- [Get-AdoBuildFailure](../commands/fr-CA/Get-AdoBuildFailure.md)
