# Slice 5 session 5.2 handoff

Scope: shared visual baseline, owned pure lexers, encoded code blocks, localized status
presentation, inline SVG history chart, history and attempt strips. Sessions 5.3 and 5.4 remain.

## Entry and validation

- Entry `verify`: exit 0, `"Status":"pass"`, no warnings; `project-check` **31.872 s**.
- All section 4 deliverables assigned to 5.1 exist: endpoints/DTO context, models, retrieval,
  grouping/classification, Test Case resolution, history, cmdlets, format views and bilingual help.
- Completion `verify`: exit 0, `"Status":"pass"`, no warnings; `project-check` **29.725 s**,
  below the 300-second budget. All five stages passed, including both culture runs and product
  Pester. This measured run preceded only the final roadmap/evidence status update; the full
  gate is repeated after that documentation update for the delivered tree.
- Focused highlighting/chart/contrast tests passed. Existing Test Case report tests also pass;
  the new labels use `TestReport*` keys so they are not included in its `Report*` label collection.
- No restore, packages, installs, external network, browser launch or escalation was used.
  Fake HTTP handlers and the existing loopback Pester harness are the only service substitutes.
- This workspace has no Git repository; no Git lifecycle action was taken.

## Delivered behavior and integration seams

- Embedded `report-base.css` and `test-failures.css`: dark screen and light print palettes,
  responsive layout, keyboard focus, reduced motion, report components and code/chart classes.
  Initial spec colors were adjusted within §12.7 latitude to meet contrast. The contrast test
  parses the embedded CSS, including print overrides; it has no separate color value table.
- `StackTraceLexer.Lex`, `ErrorMessageLexer.Lex`, `JsonLexer.Lex`, `UrlDetector.Lex` return
  immutable token values in input order. Every fixture file and decoded JSON string under
  TestRuns/Attachments is checked for exact concatenation and repeatability with all four lexers.
  Generated tests add large/deep inputs, invalid Unicode, CR/LF/CRLF and deterministic mixed text.
- `JsonLexer.TryFormat` is a separate strict depth-64, UTF-8 byte-limited preparation step with
  two-space indentation. Call it before `JsonLexer.Lex` / the JSON code writer. Original input
  stays available to the caller. Malformed/deep JSON stays plain; oversize JSON is not prepared
  for an inline preview. Session 5.4 owns attachment bytes, download limits and mismatch handling.
- `HighlightedCodeWriter.Write` encodes each token with the existing `SinkEncoding.Attribute`,
  preserving preformatted line endings and literal U+00A0/U+202F. Token classes are a fixed switch.
  Safe absolute HTTP(S) URLs get an encoded absolute href, original link text and `noreferrer`.
  Other schemes, user info and malformed URLs stay text. No path mapping is performed.
- The writer truncates messages/traces at 1 MiB characters before lexing, at a line boundary when
  present, without splitting CRLF or surrogate pairs. It returns the localized Info diagnostic
  `TestTextTruncated` and also displays it. The 5.3 model builder can collect the returned diagnostic.
- `RunHistoryChart.Write` and `HistoryStrip.Write` accept collection/project explicitly and build
  test-results links from numeric build IDs with existing `AdoWebLinks.BuildTestResult`. They do
  not reuse a response/model WebUrl. SVG geometry is invariant; all human labels use explicit culture.
  Passed includes Flaky; the separate Flaky count appears in titles and the equivalent details table.
  The current build is outlined/labeled with `aria-current`; unavailable builds are hatched.
- `StatusPresentation`, `HistoryStrip` and `AttemptStrip` provide glyphs and labels, including
  NotRun and Unavailable, and keep unknown attempt outcomes encoded and visible.

## Acceptance and remaining work

- **S5-3 rendering portion:** chart values, current build, unavailable builds, links, table,
  history/attempt strips. Existing history fixture 13 is reused through its fake handler, and
  rendered bar counts agree with per-test cells. The report-commit portion belongs to 5.4.
- **S5-4 lexer portion:** English/French syntax, round-trip, URL rules, encoding and truncation.
  The eight full-report golden files belong to 5.3.
- Spec row updated: **§25 V-29**, offline evidence only. No normative spec changes or acceptance
  ID changes. V-29 runtime forms, V-26 server routes, V-16 terminology and all work checks remain
  unconfirmed. W5 remains recommended, not an entry gate.
- Plan decisions retained: detail-limit ordering and bars/cells agreement from 5.1; toolkit
  structural markers in tests; golden/script placeholder rule remains for 5.3. No scope deviations.
- Next: session 5.3 report model/renderer, static script, CSP, validator and full-report goldens;
  then 5.4 downloads, atomic multi-file commit, export and S5-9 browser checklist.

## French text for review

New Core resource values (neutral `fr`, exercised under `fr-CA`):

| Key | French value |
| --- | --- |
| TestReportPassed / TestReportFailed / TestReportFlaky / TestReportOther | Réussi / Échec / Instable / Autre |
| TestReportNotRun / TestReportUnavailable / TestReportAvailable | Non exécuté / Indisponible / Disponible |
| TestReportThisRun | Cette exécution |
| TestReportOpenAdo | Ouvrir dans Azure DevOps |
| TestReportHistory / TestReportHistoryData | Historique des exécutions / Données de l’historique |
| TestReportBuild / TestReportBranch / TestReportFinished | Build / Branche / Fin |
| TestReportAttempts / TestReportAttemptOf | Tentatives / Tentative {0} sur {1} |
| TestReportStackTrace | Arborescence des appels de procédure (.NET) |
| TestReportErrorMessage / TestReportJson | Message d’erreur / JSON |
| TestReportHistoryTitle | Build {0}; branche {1}; fin {2}; ✓ Réussi : {3}; ✕ Échec : {4}; ≈ Instable : {5}; – Autre : {6}; {7}; {8} |
| TestTextTruncated | Le message ou l’arborescence des appels de procédure dépasse la limite d’affichage; seul le début est affiché. |

Status/stack terminology remains provisional under V-16. The new labels and complete diagnostic
sentence need fluent-speaker review.
