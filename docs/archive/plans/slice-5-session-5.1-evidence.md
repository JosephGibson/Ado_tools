# Slice 5, session 5.1 — retrieval, attempts, classification, history

Date: 2026-09-16. Scope: session 5.1 only. Sessions 5.2–5.4 have not started, and
`Export-AdoBuildTestFailure` does not exist yet.

## Entry and completion gates

- Entry `tools/dev.ps1 verify`: exit 0, `Status: pass`, no warnings; `project-check`
  **26.658 s** (this slice's budget baseline). `docs/plans/README.md` recorded Slice 4
  as offline done, and every earlier slice before it.
- Completion `tools/dev.ps1 verify`: exit 0, `Status: pass`, no warnings; `project-check`
  **34.209 s**, then **31.926 s** on a second run over the delivered tree. Both are up roughly
  5 to 8 s from the entry baseline and far under the 300 s stage timeout.
  All five stages passed: 39 PowerShell files linted, 89 tooling Pester tests, 132 configuration
  files validated, layout checked, and the product gate green. The product gate covers 600 Core
  tests under `fr-CA` and `en-US` plus the product Pester suite against the staged module.
- Acceptance source scan (`rg -o -g '*.cs' -g '*.Pester.ps1' "S5-[1-8]" tests`) lists
  S5-1, S5-2, S5-3 and S5-8. S5-4 to S5-7 belong to sessions 5.2–5.4; S5-9 and S5-10
  remain later work.
- Fixtures live in `tests/Fixtures/TestRuns/` and are cataloged with their V-items. No
  `TestResults` directory exists anywhere under `src/` or `tests/` (DD-024).
- `tests/Live/TestFailures.Live.ps1` exists and passes parse and analyzer checks.
  `Get-AdoTestRun` and `Get-AdoBuildTestFailure` have complete help in `en-US` and `fr-CA`;
  the compiled-help Pester check now expects twenty-one topics per culture.

## Deliverables

- **Registry (§6.2).** `BuildGet`, `TestRunsList`, `TestResultsList`, `TestResultGet`,
  `TestResultAttachmentsList`, `TestSubResultAttachmentsList` and
  `TestResultAttachmentContent`, with the documented versions, paging, `Accept` headers and
  timeout classes. The two attachment-list endpoints share one route; the sub-result form is
  selected by `testSubResultId`. The contract test now pins nineteen unique endpoints and the
  new routes, versions, paging and Download class.
- **Core `TestRuns/`.** `TestRunService`, `TestFailureRetrievalService`, `TestIdentity`,
  `TestIdentityGroup`, `AttemptGrouper`, `OutcomeClassifier`, `TestAttemptMapper`,
  `TestCaseLinkResolver`, `RunHistoryService`, `TestFailureInvocationCache`,
  `AttachmentKinds`, internal DTOs, and the models `AdoTestRun`, `AdoBuildTestFailureSet`,
  `AdoTestFailure`, `AdoTestCaseLink`, `AdoTestAttempt`, `AdoTestSubResult`,
  `AdoTestIteration`, `AdoTestAttachment`, `AdoTestHistoryEntry`, `AdoBuildTestSummary` with
  their named enumerations.
- **Cmdlets.** `Get-AdoTestRun` (`-BuildId` or a piped `AdoBuild`) and
  `Get-AdoBuildTestFailure` (`-HistoryCount` 1–50, `-HistoryScope`), with format views,
  manifest exports and bilingual help. `Get-AdoBuildTestFailure` holds one
  `TestFailureInvocationCache` and one client lease for the whole invocation and builds a
  retrieval service per pipeline record, so several piped builds share the history caches
  (§17). The lease is released in `EndProcessing` and on `Dispose`.
- **Diagnostics.** The eight codes this session can raise: `NoTestRuns`, `TestRunInProgress`,
  `FailureLimitExceeded`, `UngroupedTestResult`, `InvalidTestCaseReference`,
  `UnresolvedTestCase`, `HistoryUnavailable`, `HistoryLimitExceeded`. `Status` is `Partial`
  exactly when an Error diagnostic exists (§15.16), and one warning then names the build and
  the diagnostic counts (§8.4). The five remaining Slice 5 codes (`TestTextTruncated` and the
  four attachment codes) belong to sessions 5.2 and 5.4.

## Behavior worth recording

- **Two passes.** Pass 1 lists every result of every run with `detailsToInclude=None` and
  `$top=1000`. The server `outcomes` filter is never sent, and a test asserts that no request
  carries it. Pass 2 reads full details only for the records of candidate identities, in
  report order, so a rerun group costs one request whatever its attempt count.
- **Request bound (§15.9).** Asserted exactly. The rerun fixture costs 2 run pages + 3 result
  pages + 3 details + 11 attachment lists = 19 requests for 8 attempts over 3 identities. A
  TopSkip listing always ends on an empty page, so every listing costs one page more than its
  content needs; that page is part of the bound.
- **Identity.** `automatedTestStorage` ordinal ignoring case plus `automatedTestName` ordinal.
  An absent *or empty* automated name is treated as no name: the result becomes its own
  identity keyed by run and result ID, keeps one attempt, has no history cells, and raises
  `UngroupedTestResult`.
- **Attempt sources.** Rerun sub-results (ordered by `sequenceId`, then `startedDate`, then
  sub-result ID), run re-attempts (pipeline attempt, else run `startedDate`, then run ID), and
  single results. An attempt from a whole record is `RunAttempt` when the identity has more
  than one record and `Single` otherwise. `Runs` on the set is emitted in that same attempt
  order rather than response order.
- **Plan decision: detail-limit ordering.** Pass-1 candidate order is provisional
  classification (a failure-class rerun parent counts as Failed, otherwise Flaky), then
  storage, automated name, then the identity's lowest result ID. Reported failures are
  re-sorted with the final classification after pass 2 and numbered from 1.
- **Plan decision: bars match cells.** One evaluation produces both. A cell is `Flaky` exactly
  when the identity is reported and its last attempt is pass-class, so `Flaky` is always a
  subset of `Passed` (Q-23). Bars are tallied from the same cells they display. The current
  build uses the final classification for detailed identities and pass-1 for the rest; history
  builds use pass-1 throughout. A test asserts each bar equals the tally of its cells.
- **History.** The window sends `definitions`, `statusFilter=completed`,
  `queryOrder=finishTimeDescending`, `maxTime` from the current finish or queue time in
  invariant format, and `branchName` only for `SameBranch`. The current build is always
  included and last, whatever its position. Earlier builds are read newest first so the
  request budget is spent on the most recent ones and the remaining *older* builds become
  `Unavailable` with `HistoryLimitExceeded`; entries are then reversed to oldest-first. A
  per-build failure gives `HistoryUnavailable` for that build only, but authentication,
  authorization and cancellation still fail the whole retrieval. History never changes
  `Status`. Fewer builds than requested raises no diagnostic.
- **Test Case links.** Distinct valid IDs travel in one `WorkItemsBatch` pass with
  `errorPolicy=omit` and exactly the five documented fields, reconciled by ID and never by
  position. A missing ID keeps its `#<id>` link with `UnresolvedTestCase`; a reference that is
  not a plain positive integer gets no link and raises `InvalidTestCaseReference`.
- **Attachments.** Metadata only in this session. One list per detailed result, one per rerun
  attempt sub-result, and one per iteration sub-result of an attempt. `Kind` comes from the
  remote extension compared ordinally ignoring case; `DownloadStatus` stays `NotRequested`
  and `LocalRelativePath` stays null. Result-level attachments are assigned to the first
  attempt of their record; sub-result attachments to the attempt that owns them.
- **Untrusted data.** Response `url` fields on runs and results are read and dropped, so no
  server URL reaches the domain model or `AdditionalFields`. Only unknown *scalar* top-level
  fields reach `AdditionalFields`; unknown objects and arrays are skipped. Every web link is
  rebuilt from the connection URL plus escaped project and invariant numeric IDs.
- **Runspace discipline.** `SessionStateRegistry.Current` reads `Runspace.DefaultRunspace`, so
  the lease is taken on the pipeline thread before `RunWorker` and only the leased client and
  the record's log cross into the worker. That is why the invocation cache became a public,
  opaque seam (`TestFailureInvocationCache`) instead of living inside one long-lived service:
  each record needs its own log, but they must share the cache.
- **Progress.** Six new `AdoProgressPhase` values (`TestRuns`, `TestResults`, `TestDetail`,
  `Attachments`, `TestCaseLinks`, `History`) reported through the existing `IAdoLog.Progress`
  seam, so the shell's throttling and culture handling are unchanged.

## Spec evidence and plan deviations

- §25 V-19 to V-25 carry offline 5.1 notes stating what is implemented and tested and what
  remains an assumption. Nothing in §25 is marked confirmed: every Test-area shape is still
  unverified. V-28 was read and needed no change: v1 stays on run and result listings.
- **`AttachmentKinds.cs` arrived a session early.** The plan's deliverable table lists it
  under 5.4, but 5.1 step 7 requires `Kind` to be set from the extension, so the extension
  mapping (and the local-extension table it will need) ships here. The downloader, content
  checks and limits remain 5.4 work.
- **`BuildGet` lives on `BuildService`.** The plan named no owner. Putting it beside
  `GetByIdAsync` keeps build retrieval in one place; Slice 4's `BuildsList`-based
  `GetByIdAsync` is untouched, so no Slice 4 behavior or test changed.
- **Plan/spec count mismatch, no impact.** The plan's reading list for all sessions says "the
  14 Slice 5 codes from `NoTestRuns` on"; §8.5 lists 13 (`NoTestRuns` through
  `AttachmentContentMismatch`). The spec is normative, so 13 it is. Nothing in the
  implementation depends on the count.
- An unknown configured `testResults.historyScope` falls back to `SameBranch` silently rather
  than warning, so no new configuration string was added; `-HistoryScope` is a closed
  enumeration and rejects unknown values locally.
- `AdoTestAttemptSource.Single` carries a local `SuppressMessage` for CA1720: the member name
  is fixed by §15.11. It is the repository's first suppression.
- Command-count assertions were updated from nineteen to twenty-one in `Module.Pester.ps1`,
  `Help.Pester.ps1`, `tools/package/Publish-AdoToolkitPackage.ps1`, `tools/package/Package.Common.ps1`
  and the synthetic package fixture in `tools/tests/Package.Tests.ps1`. The endpoint contract
  test moved from twelve to nineteen endpoints. No other existing behavior or test changed.

## Authorizations and remaining work

- No restores, package changes, installs, external network or ADO access. Core tests use the
  fake `HttpMessageHandler`; the Pester tests use the existing loopback-only fake server and
  write their configuration override to `$TestDrive`.
- No sandbox escalation was needed. No Git lifecycle changes were made.
- **Checkpoint W5 is recommended before session 5.2.** Every fixture shape in this session is
  unconfirmed (V-19 to V-25), and sessions 5.2–5.4 render what this session retrieves, so a
  wrong shape is cheapest to correct now. `tests/Live/TestFailures.Live.ps1` reads
  `ADOTOOLKIT_LIVE_PROFILE` and `ADOTOOLKIT_LIVE_TEST_BUILD_ID`, optionally
  `ADOTOOLKIT_LIVE_RERUN_BUILD_ID` and `ADOTOOLKIT_LIVE_REATTEMPT_BUILD_ID`. It holds every
  response and downloaded byte in memory, writes nothing, and prints only
  `PASS|FAIL|INCONCLUSIVE <V-ID> <structural note>`.
- French wording still needs fluent review.

## French text for review

New Core resource entries in `src/AdoToolkit.Core/Resources/Strings.fr.resx`. Diagnostic keys
are the stable English codes; only their text is localized.

| Key | Text |
| --- | --- |
| NoTestRuns | La build {0} n’a aucune série de tests. |
| TestRunInProgress | La build {0} ou l’une de ses séries de tests n’est pas terminée; les résultats peuvent changer. |
| FailureLimitExceeded | {0} identités de tests en échec dépassent le maximum de {1}; les identités restantes sont comptées, sans détail. |
| UngroupedTestResult | Le résultat {0} de la série {1} n’a pas de nom de test automatisé; ses tentatives ne peuvent pas être regroupées. |
| InvalidTestCaseReference | Le résultat {0} de la série {1} a une référence de cas de test qui n’est pas un entier positif. |
| UnresolvedTestCase | Le cas de test {0} est introuvable ou inaccessible; le lien vers son ID est conservé. |
| HistoryUnavailable | La build {0} n’a pas pu être lue; sa barre et ses cellules d’historique sont indisponibles. |
| HistoryLimitExceeded | Le maximum de {0} requête(s) d’historique est atteint; les entrées d’historique plus anciennes sont indisponibles. |
| ProgressTestFailureActivity | Récupération des tests en échec |
| ProgressTestRuns | Lecture des séries de tests : {0} série(s) |
| ProgressTestResults | Liste des résultats de tests : {0} de {1} série(s) |
| ProgressTestDetail | Lecture des détails des échecs : {0} de {1} résultat(s) |
| ProgressAttachments | Lecture des listes de pièces jointes : {0} de {1} résultat(s) |
| ProgressTestCaseLinks | Résolution des liens de cas de test : {0} lot(s) |
| ProgressHistory | Lecture de l’historique des exécutions : {0} de {1} build(s) |
| ProgressTestFailureCompleted | {0} test(s) en échec ou instable(s) récupéré(s). |
| PartialTestFailureSet | Build {0} : le résultat des tests en échec est partiel ({1} erreur(s), {2} avertissement(s), {3} information(s)). |

Progress and message texts use a narrow no-break space before the colon, as the Slice 2 and 4
resources do. Terms to compare with the French ADO interface (V-16): *série de tests* for test
run, *tentative* for attempt, *instable* for flaky, *identité de test* for test identity,
*pièce jointe* for attachment.

New complete French help: `docs/commands/fr-CA/Get-AdoTestRun.md` and
`docs/commands/fr-CA/Get-AdoBuildTestFailure.md`, each with synopsis, description, every
parameter, and pipeline examples.
