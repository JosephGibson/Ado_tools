# Slice 1, session 1B — Pure parsers, rich text, diagnostics

## Scope and entry gate

Implemented only session 1B of [the Slice 1 plan](slice-1-work-items.md).
The required 1A endpoint/chunking, work-item models/service/mapper, web links,
cmdlet, format view and both help files existed before editing.
Baseline verify: exit 0, Status pass, no warnings; project-check 14.215 s.

## Verification

Recorded full verify: **exit 0, `"Status":"pass"`, no warnings** on 2026-09-15.
The `project-check` duration was **15.826 s** (below the plan's 240 s target).
All five stages passed: powershell-lint, powershell-test (89 passed, no failures
or skips), configuration (46 files), tooling-layout, and project-check.
The local Release build passes with zero warnings and the Core suite passes
240 tests, with no failures or skips. The same full gate is rerun after recording
this evidence and the roadmap update; the handoff reports that final result.

## Delivered and acceptance coverage

- **S1-5:** rich-text fixture outputs, nested encoding and the eight-pass ceiling,
  literal-entity trade-off, hostile content, nested lists and cells, safe href
  text, French no-break spaces, NFD accents and emoji.
- **S1-2 (document-level portion):** Steps fixtures 1–9, 17–19, 23, 26, 27 and 29;
  Parameters fixtures 1–4 and 6, with fixture 2 limited to parsing. Complete
  numbering, expansion and service integration remain in 1C.
- All 16 Slice 1 diagnostic codes have stable severities and English/French
  resources. Diagnostics capture immutable argument/chain snapshots and render
  from Code + Arguments using an explicit culture.
- One SafeXml settings factory enforces DTD prohibition, no resolver, ignored
  comments/processing instructions, no validation, and 10,485,760 characters.
  Tests generate large inputs in memory; malformed/DTD fixtures end in .txt.
- Parsers and converter accept strings and perform no file, socket or service I/O.
  No regex markup parser, DataSet.ReadXml, package or runtime dependency was added.

## Interfaces for session 1C

- `StepsXmlParser.Parse(xml, workItemId, rev, culture)` returns a `StepDocument`
  with ordered `StepNode` values, source IDs, XML-decoded source strings and diagnostics.
  Valid references retain SharedStepId; invalid references retain a SharedStep node
  with InvalidSharedStepReference and no ID. SourceStepId never substitutes for ref.
- `ParameterDataParser.Parse(declarations, localDataSource, culture, workItemId)`
  returns `ParameterDocument`: AdoTestParameters, a read-only SharedMapping of names
  to positive IDs, and diagnostics. No data gives Source None while retaining names.
  Invalid declarations and data are parsed independently, preserving valid parts.
- `ParameterDataParser.ParseSharedSet(xml, culture, workItemId)` parses assumed
  DataSet-style rows. Unknown set metadata stays null. 1C owns fetching and joining.
- `PlainTextConverter.Convert(source, culture, workItemId)` returns text and diagnostics.
  NestedEncodingDecoded means more than one pass changed the text, with eight passes
  maximum. Unformatted step values bypass conversion entirely.
- `DiagnosticMessageRenderer.Create(...)` captures invariant string arguments and
  provenance; `Render(code, arguments, culture)` supports later report localization.
  AdoTestStepKind is declared now because StepNode uses the specified shared enum.

## Spec ledger and assumptions

Only verification ledger rows **V-01, V-02, V-03** were updated; no normative
requirements or acceptance IDs changed. The fixture catalog identifies each
synthetic input and every V-03 assumed shape.

The parameter assumptions are isolated: parameters/param name attributes;
DataSet-style root/row/column XML (skip xs:schema); JSON object mapping test names
to positive integer set IDs; shared-set XML uses the same row shape. These still
need Server 2020 confirmation. Fixture 29 proves plain-text conversion; output-sink
link rendering remains Slice 2 work.

## Authorizations, deviations and open items

No restore, package changes, installs, external network, Git lifecycle changes,
or writes outside the project. The optional V-01 oracle was offered; no authorization
was received, so it was not run. No sandbox escalation was needed.

No scope deviations. Session **1C remains pending**: capability detection, batched
resolution, expansion and bounds, shared-parameter joins, Get-AdoTestCase, partial
warnings and Strict behavior, format/help and completion of the live script.
S1-2 is only partially covered; S1-3, S1-4, S1-6 and live S1-7 remain pending.
M3 is not complete. V-01/V-02/V-03 need live confirmation; French review remains pending.

## French text for review

All new runtime strings are in [Strings.fr.resx](../../src/AdoToolkit.Core/Resources/Strings.fr.resx).
Review the following 16 diagnostics and two image labels:

| Key | French value |
| --- | --- |
| NotAStepContainer | L’élément de travail ne contient pas d’étapes de test. |
| EmptySteps | L’élément de travail ne comporte aucune étape de test. |
| MalformedStepsXml | Le XML des étapes est invalide à la ligne {0}, position {1}. |
| UnknownStepElement | Un élément d’étape non pris en charge ou une valeur supplémentaire a été ignoré. |
| UnknownStepType | Le type d’étape est inconnu; il a été déduit du résultat attendu. |
| UnexpectedComprefChildren | Les éléments enfants d’une référence à des étapes partagées ont été ignorés. |
| InvalidSharedStepReference | L’attribut ref de la référence à des étapes partagées doit être un entier positif. |
| UnresolvedSharedStep | Les étapes partagées référencées sont introuvables ou inaccessibles. |
| SharedStepHasNoSteps | L’élément de travail référencé ne comporte aucune étape de test. |
| CircularSharedStepReference | Une référence circulaire à des étapes partagées a empêché le développement. |
| MaximumDepthExceeded | La profondeur d’imbrication des étapes partagées dépasse le maximum configuré. |
| ExpansionLimitExceeded | La limite du nombre d’étapes développées a été dépassée; le développement a été arrêté. |
| ResolutionLimitExceeded | La limite de résolution des éléments de travail a été atteinte; certaines références restent non résolues. |
| NestedEncodingDecoded | L’encodage imbriqué a nécessité plusieurs passes de conversion du texte. |
| MalformedParameterData | Le format XML ou JSON des données de paramètres est invalide. |
| UnresolvedSharedParameter | Un jeu de paramètres partagés référencé est introuvable ou inaccessible. |
| RichTextImage | [image] |
| RichTextImageAlt | [image : {0}] |
