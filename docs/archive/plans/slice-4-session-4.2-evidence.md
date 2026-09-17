# Slice 4, session 4.2 — build log downloads

Date: 2026-09-15. Scope: session 4.2 only; Slice 5 has not started.

## Entry and completion gates

- Entry `tools/dev.ps1 verify`: exit 0, `Status: pass`, no warnings;
  `project-check` 24.915 s. Slice 3 offline status and all four session 4.1
  deliverables, format views, endpoint entries and bilingual help were present.
- Completion `tools/dev.ps1 verify`: exit 0, `Status: pass`, no warnings;
  `project-check` **26.071 s**. All five stages passed (37 PowerShell files linted,
  89 tooling Pester tests passed, 98 configuration files validated, layout and
  product checks passed). Product checks include both Core cultures, staged
  PowerShell integration tests and compiled help checks.
- Acceptance source scan lists S4-1, S4-2 and S4-3. S4-4 remains live at work.
- Timeline and log fixtures are cataloged in `tests/Fixtures/README.md`; all log
  fixture names end in `.txt`. The live script exists; help covers all five
  Slice 4 cmdlets in English and French. The live script also passed a separate
  lint/parse check and synthetic in-memory line-count checks (empty, CRLF, blank
  and unterminated lines), without executing live requests.

## Implementation and checks

- `BuildLog` is GET at 6.0, `Accept: text/plain`, safe to retry, Download class.
- `BuildLogService` reads current line counts and streams with `ResponseHeadersRead`
  into the atomic writer. No text decoding or re-encoding occurs. The temporary is
  in the destination directory, flushed to disk, validated and atomically moved.
- The ten-minute operation budget includes attempts, body reads and retry delays;
  each read has a sixty-second inactivity budget. Caller cancellation takes
  precedence. Source I/O failures reach the existing retry policy; destination
  failures are file-output errors and do not trigger HTTP retries.
- A partial-body retry deletes its temporary before starting a fresh temporary.
  Injected failures at every file-writing stage preserve an existing destination.
  Core tests also cover exhausted body retries, byte preservation (including BOM,
  invalid UTF-8, CRLF and NUL), source disposal, cancellation and both timeouts.
  A source-read assertion proves the temporary exists before body consumption.
- `BuildLogService.TailRange` owns all range arithmetic: assumed zero-based,
  inclusive bounds, so the last 200 of 1,000 lines are 800–999. Tests cover one
  line, fewer/exactly/more than the tail, zero and integer boundaries. Unknown
  counts warn and use the full body; zero permits an empty log with no range.
- `Save-AdoBuildLog` binds BuildId/LogId by property name, supports WhatIf before
  any HTTP request, validates an existing FileSystem directory, defaults to the
  Downloads known folder, overwrites by default and emits FileInfo. Names are
  invariant IDs only. Missing logs produce BuildLogNotAvailable per input.
  Hidden property-bound CollectionUri preserves the collection check.
- Core S4-3 tests and `BuildLogs.Pester.ps1` cover the new behavior. Pester also
  covers the §15.5 pipeline, a zero-line log, provider/directory validation,
  overwrite, foreign collection rejection and localized unknown-count warnings.
- Package command/help inventories and their existing tests now require 19
  commands. No dependencies changed. Bilingual MAML is generated and checked by
  the existing packaging gate.

## Spec evidence and implementation adjustment

§25 V-14 records offline log range, transport, timeout and retry coverage. No
normative spec wording or acceptance IDs changed. V-11 and V-15 retain the 4.1
offline evidence and pending server confirmation.

PowerShell's sealed ValidateRange attribute rejects null even with AllowNull,
which prevented the required BuildLogNotAvailable error. LogId therefore uses
a small null-aware validator with the same positive Int32 bounds. BuildId and
Tail retain ValidateRange. This is an attribute-level adjustment to fulfill the
requested missing-log behavior; zero and negative LogId values remain invalid.

## Authorizations and remaining work

- No restores, package changes, installs, external network or ADO access. Tests
  used fake HTTP handlers and the existing loopback-only fake server.
- No sandbox escalation was needed. No Git lifecycle changes were made.
- `tests/Live/Triage.Live.ps1` is opt-in and was not run against a server. It
  checks the signed installed module, timeline/retry shapes, both continuation
  headers, JSON line counts and zero-based inclusive ranges in memory. It
  persists nothing and emits only structural PASS/FAIL/INCONCLUSIVE notes.
- At work: confirm V-11 and V-14, compare a saved tail and failure paths to the UI,
  confirm the V-15 build link, then record S4-4 per plan section 8. Real logs stay
  at work. French wording still needs fluent review.

## French text for review

New Core resource entries in `src/AdoToolkit.Core/Resources/Strings.fr.resx`:

| Key | Text |
| --- | --- |
| BuildLogTailUnknown | Build {0}, journal {1} : le nombre de lignes est inconnu; téléchargement du journal complet. |
| BuildLogNotAvailable | Build {0} : cet échec ne possède aucun journal à télécharger. |
| BuildLogDirectoryRequired | Indiquez un dossier existant pour le journal du build : {0} |
| SaveBuildLog | Enregistrer le journal du build |
| BuildLogIdRange | L’identifiant du journal doit être un entier de 1 à 2147483647, ou une valeur nulle pour un échec sans journal. |

New complete French help: `docs/commands/fr-CA/Save-AdoBuildLog.md`, including
synopsis, description, parameters and pipeline example.
