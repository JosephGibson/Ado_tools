# Slice 5 session 5.3 handoff

Scope: HTML report model and renderer, static script asset, hash CSP, structural validator,
and eight English/French golden files. Session 5.4 remains.

## Entry and validation

- Entry `verify`: exit 0, `"Status":"pass"`, no warnings; `project-check` **31.201 s**.
- All section 4 deliverables assigned to 5.1 and 5.2 were present before implementation:
  endpoints/DTO context, retrieval/models/history, cmdlets/views/help, bilingual resources,
  embedded CSS, lexers, code writer and charts.
- Before browser review: **709 Core tests passed**, zero failed/skipped, excluding only the
  eight pending golden comparisons. This targeted run is not the full handoff gate.
- `node --check src/AdoToolkit.Core/Reporting/Assets/test-failures.js` passed using the
  already installed Node runtime. Browser behavior is not claimed by that syntax check.
- After visual refinement: **710 Core tests passed**, zero failed/skipped, excluding only the
  golden comparisons. The **eight golden comparisons then passed** with the explicit update
  setting enabled after user preview review.
- Completion `verify`: exit 0, `"Status":"pass"`, no warnings; `project-check` **31.378 s**,
  below the 300-second budget. All five stages passed. This measured run preceded the final
  roadmap/evidence status update; the full gate is repeated after that documentation update.
- No restore, package change, install, external network or escalation was used.
  Existing fake handlers and loopback verification are the service substitutes.
- This workspace has no Git repository; no Git lifecycle action was taken.

## Preview review

Actual reports (real script and CSP, synthetic data only) were written with the atomic writer
and validated before replacement, outside the repository:

- English: `C:\Users\Joe\AppData\Local\Temp\AdoToolkit-S5-3-20260916T135428\testfailures-review.en-US.html`
- French: `C:\Users\Joe\AppData\Local\Temp\AdoToolkit-S5-3-20260916T135428\testfailures-review.fr-CA.html`

The user reviewed the preview pair and replied “Looks good so far,” then requested another
visual-polish pass. That review satisfied the pre-golden checkpoint; the authorized refinements
were applied before creating the eight golden files.

Refreshed reports, also atomically written and validated:

- English: `C:\Users\Joe\AppData\Local\Temp\AdoToolkit-S5-3-polished-20260916T141129\testfailures-review.en-US.html`
- French: `C:\Users\Joe\AppData\Local\Temp\AdoToolkit-S5-3-polished-20260916T141129\testfailures-review.fr-CA.html`

Local headless Edge screenshots were inspected at desktop and 500-pixel widths using fresh
temporary profiles, background networking disabled, all host resolution blocked, and a closed
loopback proxy. A 390-pixel screenshot was cropped at Edge's minimum headless viewport width;
the larger document/fragment captures were unreliable or timed out, so no claim is made for
those views. No test launches a browser. These checks and the user's visual review do not replace
the S5-9 Edge/Chrome attachment and script-blocked checklist in 5.4.

Automatic approval review rejected temporary Edge-profile cleanup (including a narrowly scoped
single-folder deletion) with only “blocked by policy”; those scratch directories remain beside
the external previews. No sandbox override was available, no tests were changed for permissions,
and the block does not affect the reports or verification.

The polish pass adds a compact sticky header with section links, grouped secondary metadata,
responsive metadata grids, consistent history/attempt cells, direct attempt anchors, restrained
status accents, integrated code toolbars, clear toggle states, visible copy feedback, localized
filter counts, an Escape-to-reset filter shortcut, and measured sticky-header offsets. Repeated
outcome text is omitted only when identical to the visible localized status; distinct/unknown
wire outcomes remain present. The palette and all script-free report content are retained.

## Delivered behavior and integration seams

- `TestFailureReportModelBuilder.Build(set, options)` resolves explicit/configured/session culture
  through the existing resolver, exposes fallback warnings, re-renders diagnostics and orders
  failures before assigning ordinal anchors. It builds header links from the collection/project
  and trusted identifiers. Rendering builds run/result, Test Case, bug and history links the same way.
- `AdoWebLinks.Commit` accepts Git/TfsGit only, a GUID repository ID and exactly 40 ASCII hex
  characters; segments are escaped. Response WebUrl properties never supply toolkit links.
  The routes remain [V-26] assumptions until opened against Server 2020 at work.
- `HtmlTestFailureRenderer.Render(model, writer)` streams markup in page order: sticky header,
  secondary metadata, history chart/table, index, cards, diagnostics. It includes every present
  attempt field, nested sub-results, iterations/parameters/actions, custom/additional fields,
  Test Case resolution display, attachment names/sizes and ADO result links.
- Failure attempts start open; passing/other attempts start closed. All report content exists
  with scripts blocked. Code blocks reuse 5.2 lexers and encoding/truncation. Truncation diagnostics
  remain visible at the affected code block. JSON attachment bytes/previews arrive in 5.4.
- `test-failures.js` is one static embedded asset, independent of report/culture. Labels are
  read from localized DOM attributes; no user-visible words or report data are interpolated
  into scripts. Controls start hidden and handlers attach from the asset. It supports filters,
  toggles, keyboard navigation, fragment opening, expansion, frame/wrap controls, copy with
  selection fallback, existing local-image dialog behavior, and printing.
- Asset line endings normalize to LF **before both hashing and writing** (HTML raw-text parsing
  normalizes CRLF). `ContentSecurityPolicy` computes SHA-256 from UTF-8 bytes, never a pinned hash.
  S5-5 tests independently hash the actual bytes written by the atomic writer.
- `TestFailureReportValidator.Validate(path, model)` uses strict UTF-8 and scans markup with
  memory bounded by a tag/static raw-text asset. It checks generator, model counts, actual card
  and attempt markers, unique IDs, document completion, CSP ordering, exact one-to-one hashes,
  and bare inline script tags. It rejects script sources, extra policies/scripts, inline handlers
  and mutations. Local attachment file validation is the 5.4 extension.
- Goldens pin clock/version and normalize **only** script body to `__SCRIPT_ASSET__` and its
  CSP hash to `sha256-__SCRIPT_SHA256__`. Their placeholder script is not a runnable preview.
  Actual output is validated before any normalization or comparison.

## Acceptance, decisions and remaining work

- **S5-4:** eight golden files created after user preview review; comparisons passed. Existing
  5.2 lexer coverage retained.
- **S5-5:** byte/hash, static asset/token scan, script-free completeness and corruption tests passed.
- **S5-8:** valid/unresolved/invalid Test Case rendering tests passed in addition to 5.1 resolution.
- **S5-3 rendering** remains covered by chart/strip tests; report-commit coverage is 5.4.
- Spec ledger rows **V-16** and **V-26** record offline evidence only. **V-27/S5-9** remains
  unconfirmed; 5.3 visual review is not the complete browser spike.
- Plan decisions retained: 5.1 detail-limit candidate ordering and bars/cells agreement;
  fixed script/hash golden placeholders; toolkit markers/encoded expectations without an HTML
  parser package. No normative spec changes, acceptance ID changes or scope deviations.
- Next: 5.4 attachment downloads/content checks, generation folder commit, local links/previews,
  attachment validation, export cmdlet/help, browser spike and final Slice 5 offline done check.
  V-16 fluent/UI terminology review, live V-19–V-26/V-29 and work browser policy remain pending.

## French text for review

New neutral `fr` resource values, exercised under `fr-CA`. Existing 5.1/5.2 translations are
retained. Visual preview approval alone does not confirm Server 2020 UI terminology.

| Key | French value |
| --- | --- |
| TestReportHeading | Rapport des tests en échec |
| TestReportIndex | Index des échecs |
| TestReportAttachments | Pièces jointes |
| TestReportFilter | Filtrer les tests |
| TestReportHasAttachments | Avec pièces jointes |
| TestReportExpandAll | Tout développer |
| TestReportCollapseAll | Tout réduire |
| TestReportCopy | Copier |
| TestReportCopied | Copié |
| TestReportSelectCopy | Texte sélectionné ; utilisez votre raccourci de copie. |
| TestReportWrap | Retour automatique à la ligne |
| TestReportFramework | Masquer les appels du framework |
| TestReportClose | Fermer |
| TestReportImage | Image jointe |
| TestReportDuration | Durée |
| TestReportSeconds | {0:N3} s |
| TestReportStarted | Début |
| TestReportMachine | Machine |
| TestReportRun | Exécution de tests |
| TestReportResult | Résultat du test |
| TestReportRunBy | Exécuté par |
| TestReportFailureType | Type d’échec |
| TestReportResolution | État de résolution |
| TestReportFailingSince | En échec depuis |
| TestReportComment | Commentaire |
| TestReportBugs | Bogues associés |
| TestReportOwner | Responsable |
| TestReportSubResults | Sous-résultats |
| TestReportIterations | Itérations |
| TestReportCustomFields | Champs personnalisés |
| TestReportAdditionalFields | Champs supplémentaires |
| TestReportSource | Source de la tentative |
| TestReportSingle | Résultat unique |
| TestReportRerun | Réexécution dans la tâche |
| TestReportRunAttempt | Nouvelle tentative d’exécution |
| TestReportOutcome | Résultat |
| TestReportGroupType | Type de groupe de résultats |
| TestReportSequence | Séquence |
| TestReportActionPath | Chemin de l’action |
| TestReportStepIdentifier | Identifiant d’étape |
| TestReportBytes | {0:N0} octets |
| TestReportAttachmentType | Type de pièce jointe |
| TestReportCommit | Commit |
| TestReportStorage | Assembly de tests |
| TestReportFullName | Nom complet du test |
| TestReportNoFailures | Aucun test en échec ou instable à signaler. |
| TestReportNoMatches | Aucun test ne correspond à ces filtres. |
| TestReportNavigationHint | / Filtre · j/k Test suivant/précédent · o Ouvrir/fermer la tentative |
| TestReportInfo | Information |
| TestReportWarning | Avertissement |
| TestReportError | Erreur |
| TestReportFilterCount | {0} tests sur {1} |
