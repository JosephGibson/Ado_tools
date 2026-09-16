# Slice 2, session 2.1 — handoff

Date: 2026-09-15. Session 2.1 complete; session 2.2 and milestone M4 remain pending.

## Verification

- Entry: section 11 lists sessions 2.1 and 2.2; Slice 1 is recorded as offline done (M3).
- Baseline `tools/dev.ps1 verify`: exit 0, `"Status":"pass"`, no warnings;
  `project-check` 16.467 s.
- Implementation `tools/dev.ps1 verify`: exit 0, `"Status":"pass"`, all five stages pass,
  top-level and stage warning arrays empty; `project-check` 17.678 s.
- Core: 352 passing tests, no failures or skips. The full gate runs Core under both
  `en-US` and `fr-CA`; it also runs 89 tooling Pester tests and product Pester,
  inspects the staged module, and validates 65 configuration files.
- Acceptance source scan returns S2-3, S2-5 and S2-6. This is the 2.1 subset,
  not the full Slice 2 acceptance gate.

## What changed

- `ReportModelBuilder` projects the input into a document and one case model, with
  snapshot lists, report culture, English/French labels, optional metadata, ordered
  rows, parameter data, diagnostic counts, and links. It rejects mismatched collection
  provenance. Diagnostics are rendered again from code and arguments in report culture;
  the input message is neither translated nor reused.
- `AdoWebLinks.TestPlan` supplies plan and plan/suite routes. Work item links for the
  case, groups, ancestor paths and shared parameter sets are reconstructed from the
  connection and owning project. Unknown metadata and URLs remain absent.
- `AtomicFileWriter` uses an exclusive sibling `.<name>.<random>.tmp`, UTF-8 without
  a BOM, LF text newlines, disk flush, validation, then the existing atomic move
  primitive. It requires an existing parent and cleans temporary output on failure.
  NoClobber checks early and uses a non-overwriting move to handle races.
- `KnownFolders` calls `SHGetKnownFolderPath(FOLDERID_Downloads)` with generated
  interop; Core alone enables unsafe blocks for that generated marshalling code.
  `ReportFileNames` resolves a supplied file/existing directory or injected/actual
  Downloads folder and generates `TestCase-<id>-Steps.<ext>` invariantly.
- `JsonTestCaseRenderer` streams to the supplied writer's UTF-8 stream; a bounded
  decoder adapter supports other TextWriters. It flushes at row boundaries, uses
  English property/enum names, omits missing optional metadata, and adds original
  source fields only when requested. The writer owns the destination and lifetime.
- `ReportOutputValidator` checks existence and non-empty output; JSON reparses and
  must have exactly one supported numeric schema version before commit. Full schema
  validation remains test-only.
- Published schema: [`testcase.v1.schema.json`](../schemas/testcase.v1.schema.json),
  draft 2020-12, with closed stable objects and string-valued parameter dictionaries.

## Acceptance evidence

| ID | Evidence and limits |
| --- | --- |
| S2-3 | All five injected stages tested with existing and missing destinations; independent malformed-output and renderer failures, cancellation, locked destination, exclusive temp access, exact original bytes and cleanup. Successful output is validated before replacement and is UTF-8 without BOM. |
| S2-5 | Five variants × two report cultures × IncludeSource on/off (20 combinations), including both TextWriter and atomic-file output, validate against the published schema with format assertions enabled. Additional cases cover service-expanded nested/partial input, generated large content and future multi-case schema shape. Negative tests reject missing/extra properties, localized enum values, wrong versions, invalid dates/URLs, and invalid parameter values. External schema fetching is disabled. Golden JSON validation will be added with the 2.2 golden files. |
| S2-6, partial | Available and absent header metadata, exact work item and plan/suite URLs, Shared Steps ancestry, shared parameter URLs, provenance rejection, diagnostic localization and immutable snapshots. HTML headers and content-link handling remain 2.2. |

NoClobber race behavior is proven at the Core writer level. S2-4 as a whole remains
pending the exporter/cmdlet, WhatIf and FileInfo integration tests in 2.2.

## Design decisions and next-session contract

1. JSON has an object root with `schemaVersion`, `generator` (`name`, `version`),
   `generatedAt`, `culture`, `collectionUrl`, `project`, `source`, and `cases`.
   One case is an array of one; the schema also accepts multiple cases without a
   version change. Current source is `TestCase`.
2. `ReportModelOptions` requires generated-at (including its offset), toolkit version
   and resolved report culture. Tests pin these before rendering; output is never
   normalized after rendering. JSON indentation uses LF explicitly.
3. Orchestration remains in Core. Session 2.2 adds `TestCaseExporter`, injected
   `IDocumentLauncher`, culture resolution and the thin cmdlet adapter; it must keep
   WhatIf, NoClobber, validation, commit and Open ordering testable in Core.
4. Golden files are a 2.2 deliverable. Add the manual `ADOTOOLKIT_UPDATE_GOLDEN=1`
   guard there, and have `tools/check.ps1` remove that variable before tests.
   Do the specified HTML encoder spike and browser review before approval.
5. The 2.2 cmdlet must reject more than one case before file creation and localize
   the multi-case-unavailable message. The schema's ability to represent multiple
   cases does not enable multi-case export in this session.

Session 2.2 also owns HTML/Markdown count validation, content link encoding, provider
resolution, IncludeSource format validation, help and the remaining S2-1–S2-7 coverage.

## Spec updates, authorization and open items

- §25 V-15 now records offline report work item and plan/suite route evidence.
  Live browser confirmation of these routes and offline/live build routes remain
  pending. No normative spec text or acceptance IDs changed.
- At step 6 the user authorized NuGet lookup, a pinned test-only JsonSchema.Net
  dependency and `dotnet restore`. Selected
  [JsonSchema.Net 9.4.0](https://www.nuget.org/packages/JsonSchema.Net/9.4.0);
  its test dependency closure includes JsonPointer.Net 7.0.2, Json.More.Net 3.0.1
  and Humanizer.Core 3.0.10. Central version, private test reference and test lock
  file are updated. Product lock files contain none of these dependencies;
  Core runtime remains BCL-only, and staged package inspection passes.
- No other package was directly added, no tool/module installed, no live ADO access,
  no external test traffic and no document launcher used. NuGet lookup/restore was
  the authorized external network activity. No sandbox escalation was needed.
- Deviations: none. Git inspection reported that the folder is not a Git repository;
  no Git lifecycle action was performed.
- Open: session 2.2, fluent French review, S2-8 at work, V-15 live and V-16.
  The Downloads resolver seam tests redirection without creating reports in the
  user's actual Downloads folder; provider integration remains 2.2.

## French text for review

Neutral `fr` resources; tested using `fr-CA`. These are pending fluent review.
Existing diagnostic translations are reused without changes.

| Resource key | French text |
| --- | --- |
| ReportTestCase | Cas de test |
| ReportSharedSteps | Étapes partagées |
| ReportSharedParameters | Paramètres partagés |
| ReportStep | Étape |
| ReportAction | Action |
| ReportExpectedResult | Résultat attendu |
| ReportParameters | Paramètres |
| ReportTestPlan | Plan de test |
| ReportTestSuite | Suite de tests |
| ReportState | État |
| ReportAssignedTo | Assigné à |
| ReportPriority | Priorité |
| ReportAreaPath | Chemin de zone |
| ReportIterationPath | Chemin d’itération |
| ReportGeneratedAt | Généré le |
| ReportServer | Serveur |
| ReportCollection | Collection |
| ReportProject | Projet |
| ReportTitle | Titre |
| ReportId | ID |
| ReportRevision | Révision |
| ReportWorkItemType | Type d’élément de travail |
| ReportAutomationStatus | État d’automatisation |
| ReportChangedDate | Dernière modification |
| ReportChangedBy | Modifié par |
| ReportStatus | État |
| ReportStepCount | Nombre d’étapes |
| ReportDiagnostics | Diagnostics |
| ReportComplete | Complet |
| ReportPartial | Partiel |
| ReportToolkitVersion | Version de la boîte à outils |
| DownloadsUnavailable | Impossible de déterminer le dossier Téléchargements. |
| InvalidReportOutput | Le rapport produit est vide ou invalide. |
