# Synthetic fixture catalog

Fixtures are hand-written from the spec or public documentation. Hosts use `.test`
and names are fictional. No work data enters this directory (DD-010, DD-022).

| Fixture | Represents | Source or assumption | V-item |
| --- | --- | --- | --- |
| `Rest/success.json`, `Rest/projects-second.json`, `Rest/projects-empty.json` | Success and short/empty TopSkip pages; also reused for continuation-header scenarios | Spec §6.4, §19.3 HTTP 1, 12, 13; hand-written project GUIDs and names | V-14 projects |
| `Rest/iis-unauthenticated.html.txt` | IIS-like 401 HTML that must never be echoed | Spec §5.2, HTTP 2; assumed synthetic body | V-07 |
| `Rest/forbidden.json`, `Rest/not-found.json`, `Rest/bad-version.json` | 403, 404, and API-version rejection | Spec §6.7, HTTP 3–5 | V-07 |
| `Rest/redirect.json` | Redirect response; tests supply a synthetic Location header | Spec §5.1, HTTP 6 | — |
| `Rest/throttled.json`, `Rest/unavailable.json`, `Rest/server-error.json` | Retry-After, exhausted transient retries, and no retry on 500 | Spec §6.5, HTTP 7–9 | — |
| `Rest/malformed-body.json.txt`, `Rest/empty-body.txt` | Malformed and empty successful bodies | Spec §6.7, HTTP 16–17 | — |
| `Rest/french-error.json` | French server text with a localized toolkit hint | Spec §19.3 Localization 3 | V-18 |
| `../AdoToolkit.Core.Tests/Http/CancellationTests.cs` | Controlled blocked requests, caller cancellation, retry budgets, and download inactivity/total budgets | Spec §6.6, HTTP 10–11; generated fake streams | — |
| `../AdoToolkit.Core.Tests/Http/HttpPipelineContractTests.cs` | Project route with spaces, a slash, and non-ASCII text | Spec §6.3, HTTP 18; hand-written route values | — |
| `../AdoToolkit.Core.Tests/Http/PagingTests.cs` | Repeated tokens/pages, opaque continuation tokens, caller limits, and generated 10,000-page ceiling | Spec §6.4; scripted fake responses | V-14 |
| `../AdoToolkit.PowerShell.Tests/Support/FakeAdoServer.ps1` and adjacent Pester tests | Loopback-only scripted projects, HTML 401, empty pages, and a blocked request; synthetic GUIDs and names | Spec §19.1; adapter cancellation, culture, paging, and runspace ownership | V-07, V-14 assumptions |

Session 0.1 had no payload fixtures; the HTTP fixtures above were added in session 0.3.

## Slice 1A

| Fixture | Represents | Source or assumption | V-item |
| --- | --- | --- | --- |
| `Rest/workitems-null.json`, `Rest/workitems-compact.json` | IDs 1 and 3 returned out of order, IDs 2 and 4 absent; null-placeholder and compact-omission variants | Spec §9.1, §19.3 HTTP 15; both synthetic assumptions remain supported pending live confirmation | V-05 |
| `Rest/workitem-fields.json` | Typed convenience fields, opaque identity, unknown recursive values, relation attributes, untrusted response URL, owning project with accents/space/slash | Spec §6.3, §7.3–§7.4, §9.1; hand-written wire shape and relationship attributes | V-10; V-15 work item route |
| `../AdoToolkit.Core.Tests/Http/IdChunkingTests.cs` | Generated 450-ID batch, reversed responses in three sequential chunks, cancellation | Spec §6.4, §19.3 HTTP 14; generated from the synthetic field fixture | — |
| `../AdoToolkit.Core.Tests/WorkItems/WorkItemServiceTests.cs` | Generated duplicate/unexpected IDs, malformed response shapes and invalid convenience fields | Spec §9.1; hand-written strings and mutations of the synthetic field fixture | V-05 |
| `../AdoToolkit.PowerShell.Tests/WorkItems.Pester.ps1` | Pipeline values/properties, missing errors, relation switch, local parameter rejection, typed connection mismatch | Spec §4.4, §9.1; loopback fake server uses the fixtures above | V-05, V-10 (local restriction only) |

V-05, V-10 and V-15 remain unconfirmed on Server 2020. Parser/parameter assumptions
V-01 and V-03 belong to sessions 1B–1C; no parser fixtures were added in 1A.
Malformed or DTD-bearing inputs and log bodies use `.txt`; JSON/XML must parse.
Tests generate inputs larger than 1 MiB (DD-024).

## Slice 1B

All files below are hand-written synthetic inputs from spec §10.2–§10.3, §10.6,
§11.2–§11.5 and the indicated §19.3 fixture number. No server or oracle has
confirmed the wire shapes. Files are UTF-8; malformed and DTD inputs end in `.txt`.

| Fixture | Represents | Source or assumption | V-item |
| --- | --- | --- | --- |
| `Steps/01-direct.xml` | Steps 1: one direct Action step | Spec illustrative steps shape | [V-01], [V-02] |
| `Steps/02-multiple.xml` | Steps 2: Action and Validate in document order, non-consecutive source IDs | Spec parser rules | [V-01], [V-02] |
| `Steps/03-empty-result.xml` | Steps 3: Action with an HTML-only empty result | Spec rich-text routing | [V-02] |
| `Steps/04-unknown-type.xml` | Steps 4: unknown type inferred as Validate | Spec parser rule 4 | [V-01] |
| `Steps/05-unformatted.xml` | Steps 5: absent isformatted, literal angle brackets, ampersand, @user | Spec plain-text routing | [V-02] |
| `Steps/06-rich.xml` | Steps 6: nested lists, table, links, images, block tags | Spec conversion table; expected text in converter tests | [V-02] |
| `Steps/07-nested.xml` | Steps 7: multiple layers of encoded markup | Spec eight-pass conversion rule | [V-02] |
| `Steps/08-literal-entity.xml` | Steps 8: literal markup-like text consumed on a later pass | Spec documented trade-off; original source retained | [V-02] |
| `Steps/09-french.xml` | Steps 9: NFD accent, U+00A0, U+202F, punctuation, apostrophe, emoji, Japanese | Spec §11.5; UTF-8 and original normalization preserved | [V-02] |
| `Steps/17-missing-ref.xml`, `Steps/18-invalid-ref.xml` | Steps 17–18: invalid references retain group nodes; no fallback to id | Spec ref-only rule | [V-01] |
| `Steps/19-compref-children.xml` | Steps 19: child steps/metadata skipped, reference and following step retained | Spec provisional child policy; oracle not run | [V-01] |
| `Steps/23-malformed-root.xml.txt`, `Steps/23-malformed-shared.xml.txt` | Steps 23: malformed root and Shared Steps documents | Spec all-or-nothing document parsing and diagnostic location | — |
| `Steps/26-hostile.xml` | Steps 26: script, style, head, comment, event attributes, unsafe href, remote image source | Spec untrusted rich text; no I/O or active markup | [V-02] |
| `Steps/27-dtd.xml.txt` | Steps 27: external entity declaration rejected | Spec safe reader settings; synthetic .test target is never resolved | — |
| `Steps/29-urls.xml` | Steps 29: HTTP(S), mailto, javascript, data and file schemes | Converter only appends absolute HTTP(S) hrefs; text remains text. Link rendering at output sinks belongs to Slice 2 | [V-02] |
| `Parameters/01-names.xml`, `Parameters/01-local.xml` | Parameters 1: declared order, local rows, empty value, ignored xs:schema | **[V-03] Assumed** parameters/param name attributes and DataSet root/row/column XML | [V-03] |
| `Parameters/02-shared.json` | Parameters 2 at parse level: names mapped to set IDs, deduplicated set metadata | **[V-03] Assumed** JSON object with positive integer values; join deferred to 1C | [V-03] |
| `Parameters/02-shared-set.xml` | Shared set rows for later joining | **[V-03] Assumed** DataSet-style XML in the shared work item's Parameters field; parser only | [V-03] |
| `Parameters/03-declared-only.xml` | Parameters 3: names without data | **[V-03] Assumed** parameters/param name attributes; Source None, names retained | [V-03] |
| `Parameters/04-malformed.xml.txt`, `Parameters/04-malformed.json.txt`, `Parameters/04-malformed-names.xml.txt`, `Parameters/04-dtd.xml.txt` | Parameters 4: invalid data/declarations and rejected DTD | **[V-03] Assumed** shapes with synthetic syntax errors; failures give Warning and retain independently valid data | [V-03] |
| `Parameters/06-french-names.xml`, `Parameters/06-french-local.xml` | Parameters 6: accented names and values, encoded XML element names and a space | **[V-03] Assumed** XmlConvert name encoding; values and declared names stay unchanged | [V-03] |
| `../AdoToolkit.Core.Tests/TestManagement/StepsXmlParserTests.cs` | Namespaces, missing/extra values, invalid numeric refs, wrong/multiple roots, exact and over-limit XML | Spec parser rules; inputs at/above 10,485,760 characters generated in memory | [V-01], [V-02] |
| `../AdoToolkit.Core.Tests/TestManagement/ParameterDataParserTests.cs` | Invalid JSON types/IDs, duplicate columns/names, namespaces, schema import ignored, XML size ceiling | **[V-03] Assumed** shapes; large inputs generated in memory | [V-03] |
| `../AdoToolkit.Core.Tests/RichText/PlainTextConverterTests.cs` | Tag table, attributes containing >, nested block/list/cell content, eight-pass ceiling, stable text and French spacing | Spec converter rules; small synthetic inline text | [V-02] |

## Slice 1C

All new fixtures are hand-written synthetic data, derived from the spec sections
below. No live server or external oracle was contacted. REST graph responses
assembled in tests contain only these fictional values; the fake handler returns
requested IDs in reverse order and omits missing IDs.

| Fixture | Represents | Source or assumption | V-item |
| --- | --- | --- | --- |
| `Steps/10-shared.xml`, `Steps/11-nested.xml` | Steps 10–11: one group and its nested group | §10.5; positive ref attributes, no child content | [V-01] |
| `Steps/12-repeated.xml`, `Steps/13-diamond.xml` | Steps 12–13: repeated and diamond references; leaf/branch responses assembled by ExpansionFixture | §10.5 active-path cycle rule | [V-01] |
| `Steps/14-direct-cycle.xml`, `Steps/15-indirect-cycle.xml`, `Steps/16-self-cycle.xml` | Steps 14–16: direct, indirect and self cycles; graph wiring in StepExpanderCycleTests | §10.5; roots seeded in invocation cache | [V-01] |
| `Steps/20-missing.xml` | Steps 20: omitted reference followed by direct step | §10.5.4 stable group numbering | [V-01], V-05 |
| `Rest/shared-no-steps.json` | Steps 21: resolved item with metadata and no steps field | §8.5 SharedStepHasNoSteps warning; no children | [V-01] |
| `Rest/testcase-no-steps.json` | Steps 22: two category members and one other type, all without steps | §10.4; localized synthetic type names | [V-13] |
| `Rest/test-category.json`, `Rest/shared-category.json` | Category membership for localized/customized types | §6.2 WorkItemTypeCategory and §10.4; assumed stable category reference names | [V-13] |
| `Steps/24-depth.xml` | Steps 24: a reference beyond the configured depth | §10.5 depth before cache lookup | [V-01] |
| `../AdoToolkit.Core.Tests/TestManagement/StepExpanderLimitTests.cs` | Steps 25: generated 10-level binary fan-out, exact boundary, depth and resolution budgets | §10.5.2 literal post-recursion check: limit 30 yields 34 ordinary rows and one Truncated marker; missing IDs consume resolution budget | [V-01] |
| `Steps/28-numbering.xml`, `Steps/28-child.xml` | Steps 28: 3.3.1 and unresolved group 4 followed by direct step 5 | §10.5.4; reuses Steps/01-direct.xml as nested leaf | [V-01] |
| `Rest/shared-expansion-sequence.json` | Four ordered batch responses: root plus three Shared Steps levels | §10.5.1 and S1-6; exact request IDs and zero category calls asserted | [V-01], V-05 |
| `Rest/testcase-mixed.json` | Partial case, non-test root and complete case; a fourth requested ID is omitted | §8.3–§8.4; fake loopback requests in TestCases.Pester.ps1 | [V-01], V-05, V-13 |
| `Parameters/05-unresolved.json` | Parameters 5: mapping to omitted set | **[V-03] Assumed** name-to-ID JSON; warning preserves steps and set ID | [V-03] |
| `../AdoToolkit.Core.Tests/TestManagement/ExpansionFixture.cs`, `TestCaseServiceTests.cs` | REST wrappers for parser fixtures and Parameters 2 join; malformed/missing sets, warning-only and partial results, optional metadata | **[V-03] Assumed** shared XML in Microsoft.VSTS.TCM.Parameters; same-name columns joined by row index, declared order and literal step tokens retained | [V-01], [V-02], [V-03] |
| `../AdoToolkit.Core.Tests/TestManagement/SharedStepResolverRequestCountTests.cs` | Root seeding, missing/rejected root reuse, first-round shared parameters, generated 450-reference chunking, separate category counts | §10.5.1; 1+3 batch requests for three levels; each of the two category routes once per project/session | [V-01], [V-03], [V-13] |
| `../AdoToolkit.Core.Tests/TestManagement/TestCapabilityDetectorTests.cs` | Failed second category lookup (403, 404, malformed response) is uncached; present null steps field bypasses categories | §10.4 ordered rules and translated errors | [V-13] |
| `../AdoToolkit.PowerShell.Tests/TestCases.Pester.ps1` | Binding, order, missing/non-test errors, partial warning, Strict diagnostic payload, French UI culture and local validation | §4.4, §8.3–§8.4, §16.1; loopback fake only | [V-01], [V-13] |

The 1B malformed Shared Steps fixture is now exercised through the service; its
diagnostic carries the referenced document ID while following root rows survive.
The 1B Parameters 2 shared-set fixture is now joined end to end.

## Slice 2, session 2.1

| Fixture | Represents | Source or assumption | V-item |
| --- | --- | --- | --- |
| `../AdoToolkit.Core.Tests/Reporting/ReportFixture.cs` | Direct, nested, partial/truncated, shared-parameter and French-content report models; optional metadata, identities and suite provenance; untrusted response URLs | Hand-written from §7.3–§7.4 and §12.1/§12.4. Generated-at is pinned to `2026-09-15T10:30:00-03:00`, version to `2.1.0-test`; English and French reports, with/without source, are schema-validated | V-15 constructed routes only |
| `Reports/testcase-*.{html,md,json}` | 30 exact UTF-8, LF golden files: direct, nested, partial, parameterized, French content × en-US/fr-CA × HTML/Markdown/JSON | Same pinned models and clock as above. HTML emoji is `&#x1F600;`; Markdown emoji is literal. U+00A0/U+202F remain literal in HTML/Markdown. Updates require `ADOTOOLKIT_UPDATE_GOLDEN=1` during a manual run; the full gate removes that variable | S2-1, S2-5, S2-7; browser approval recorded in session 2.2 evidence |
| `../AdoToolkit.Core.Tests/Reporting/HostileContentTests.cs`, `RenderedHeaderAndLinkTests.cs` | Existing Steps 26/29 through conversion and report sinks; hostile title/parameter text, separate attribute encoding and safe content links | Synthetic only; no server or browser requests | S2-2, S2-6 |
| `../AdoToolkit.PowerShell.Tests/Export.Pester.ps1` | Export with loopback-retrieved cases; WhatIf, NoClobber, FileInfo, paths, culture, provenance, single-case guard, source validation | FakeAdoServer only; never passes the Open switch | S2-4 |
| `../AdoToolkit.Core.Tests/Reporting/JsonSchemaValidationTests.cs` | Invalid schema mutations, invariant bytes, multi-case schema compatibility, generated text above 1 MiB; service-to-report test reuses `Steps/28-numbering.xml`, `28-child.xml`, `01-direct.xml` via fake HTTP | §12.4, §12.6 and §19.1. No external schema fetching; malformed JSON stays in test strings. Large content is generated, never committed | — |
| `../AdoToolkit.Core.Tests/IO/AtomicFileWriterTests.cs`, `ReportFileNamesTests.cs` | Write/flush/validation/commit failures, cancellation, locked destination, NoClobber race, literal paths and redirected Downloads seam | §13.1–§13.3; temporary local files only, no shell launch | — |

No golden files are introduced in 2.1. The 30 approved report files and their
manual update guard belong to session 2.2. Server route confirmation remains live.

## Slice 3, session 3.1

All new files are hand-written synthetic data from spec §6.2, §6.4, §9.2, §11.5 and
§19.3 HTTP 12. No server was contacted. Testplan wire shapes (`rootSuite`,
`parentSuite`, `workItem`, `order`) and the pinned preview versions are **[V-04]
assumptions**; the WIQL cap failure shape is a **[V-06] assumption**. Tests add the
continuation headers; tokens contain spaces and reserved characters.

| Fixture | Represents | Source or assumption | V-item |
| --- | --- | --- | --- |
| `Rest/testplans-first.json`, `Rest/testplans-last.json` | HTTP 12: short first plan page and final page; accented plan names, an untrusted `_links` URL | **[V-04] Assumed** TestPlansList `value[].id/name/rootSuite.id` at `6.0-preview.1`; links rebuilt from the connection | [V-04], V-15 plan route |
| `Rest/testplan-empty-page.json` | HTTP 12: empty nonterminal page that still carries a token, shared by all three testplan endpoints | Spec §6.4 short/empty page rule | [V-04] |
| `Rest/testsuites-first.json`, `Rest/testsuites-last.json` | Suite tree split across pages: root, two children, grandchildren listed after later siblings; French names with `’` | **[V-04] Assumed** TestSuitesForPlan `parentSuite.id`; root has no parent; sibling order is response order (provisional) | [V-04], V-15 suite route |
| `Rest/suitetestcases-first.json`, `Rest/suitetestcases-last.json` | Suite test case membership across pages with `order` 1–3 | **[V-04] Assumed** SuiteTestCaseList `value[].workItem.id` and `order` at `6.0-preview.2`; service use belongs to 3.2 | [V-04] |
| `Rest/wiql-flat.json` | Flat WIQL result: query-ordered IDs, columns, offset `asOf`, untrusted URLs | Spec §9.2 and public WIQL response shape | [V-06] |
| `Rest/wiql-tree.json` | Tree/link result with `workItemRelations`; tests also derive oneHop and mixed variants | Spec §9.2 flat-only rule | — |
| `Rest/wiql-limit-error.json` | 400 body whose message starts with `VS402337`, plus `typeKey` | **[V-06] Assumed** server limit error shape; synthetic type name | [V-06] |
| `../AdoToolkit.Core.Tests/TestManagement/TestPlanPagingTests.cs` | S3-1 paging for plans, suites and suite test cases; repeated tokens; invalid plan shapes | Fixtures above with scripted tokens | [V-04] |
| `../AdoToolkit.Core.Tests/TestManagement/TestSuiteServiceTests.cs` | Start suite versus depth-first subtree, missing suite, unknown parent, parent cycle, duplicates, generated 5,000-level chain | Fixtures above; malformed trees and the deep chain are generated in tests | [V-04] |
| `../AdoToolkit.Core.Tests/WorkItems/WiqlServiceTests.cs` | S3-3: body and `$top`, generated 19,999/20,000/20,001-ID results, limit error hint, link/tree rejection, invalid shapes, 450-ID hydration across chunks, query text absent from logs | Generated from `wiql-flat.json`, `wiql-tree.json` and `workitem-fields.json` | [V-06] |
| `../AdoToolkit.PowerShell.Tests/TestPlans.Pester.ps1` | Cmdlet paging, accent-sensitive NFC/NFD wildcard matching, Id filter, project requirement, typed-plan-only pipeline binding, connection mismatch, WIQL hydration, NotSupported and French cap errors, local Top validation | Loopback fake server only; NFD names and the 20,000-ID body are generated in the test | [V-04], [V-06] |

## Slice 4, session 4.1

Synthetic fixtures based on the accepted §15.3/§19.3 shapes. Stage, Phase, Job and Task
are YAML timeline assumptions **[V-11]**, pending Server 2020 confirmation. Tests also
substitute unknown type names to prove derivation does not depend on type. No server
or documentation network requests were made in this session. Continuation headers are
added by the tests; the header mechanism and log line counts are **[V-14]** assumptions.

| Fixture | Expected behavior | V-item |
| --- | --- | --- |
| `Timelines/multi-stage.json` | Tree order despite shuffled input; failures `Deploy › Release › Prepare` (log 11) and `Deploy › Release › Publish` (log 12), attempt 1; parent failures suppressed | [V-11] |
| `Timelines/retried-job.json` | Only `Build › Retry job › Retry task`, attempt 2, log 12; superseded job and unique-identifier child dropped; `previousAttempts` retained on timeline output | [V-11] |
| `Timelines/canceled.json` | Canceled build: `Deploy › Waiting` (no log) and `Deploy › Failed first` (log 11); non-canceled build: only the failed task | [V-11] |
| `Timelines/warnings.json` | Empty by default; IncludeWarnings yields only `Build › Compile › Analyze`, log 11 | [V-11] |
| `Timelines/no-log.json` | `Build › Agent allocation`, null log ID/count, error issue retained | [V-11] |
| `Rest/build-definitions-first.json`, `Rest/build-definitions-last.json` | Definition pages with folder, revision, queue status; same exact name in two folders and an unaccented name | [V-14], definition web route [V-26] |
| `Rest/builds-first.json`, `Rest/builds-last.json` | Build pages with source/repository/identity/UTC-normalized dates and URI; untrusted web links ignored | [V-14], V-15 build route |
| `Rest/builds-empty.json` | Empty nonterminal page with a scripted continuation header; also an empty log list | [V-14] |
| `Rest/build-logs.json` | Log 11: 1,000 lines; log 12: zero lines; log 13: unknown count. No log body download | [V-14] |
| `../AdoToolkit.Core.Tests/Builds/BuildQueryTests.cs`, `BuildFailureDeriverTests.cs` | S4-1/S4-2: query parameter set, name resolution, pagination, tree validation, retry pruning, result selection, reconstructed links | [V-11], [V-14], V-15 |
| `../AdoToolkit.PowerShell.Tests/Builds.Pester.ps1` | Typed pipeline binding, owning project, provenance rejection, French ambiguity error, warnings and local parameter validation | S4-1, S4-2 |

## Slice 4, session 4.2 — log downloads

All bodies are synthetic. The zero-based, inclusive `startLine`/`endLine` convention is a
**[V-14] assumption**, isolated in `BuildLogService.TailRange` and awaiting the opt-in live check.
No real log contents or `.log` files are used.

| Fixture or generated input | Expected behavior | V-item |
| --- | --- | --- |
| `Rest/build-log-body.txt` | UTF-8 French synthetic log body; saved byte-for-byte by Core and Pester | [V-14] |
| `Rest/build-logs.json` | Reused counts: tail 200 of 1,000 requests 800–999; zero permits an empty body without a range; unknown warns and requests the full body | [V-14] |
| `../AdoToolkit.Core.Tests/Builds/ChunkedLogStream.cs`, `BuildLogServiceTests.cs` | Generated BOM/invalid UTF-8/CRLF/NUL bytes, short chunks, partial-body failures, source disposal, fresh temp names per retry, file faults at every stage, cancellation, inactivity and total download budgets | S4-3 |
| `../AdoToolkit.PowerShell.Tests/BuildLogs.Pester.ps1` | Example pipeline, property binding, missing-log continuation, provenance, FileInfo/overwrite, WhatIf without requests, provider/directory checks and French fallback warning | S4-3 |

## Slice 3, session 3.2

No new REST fixture files. Suite memberships, work item batches and the 300-case suite are
generated in tests from the 3.1 testplan fixtures and `Steps/01-direct.xml`; nothing above
1 MiB is committed. Testplan shapes and suite order remain **[V-04] assumptions**.

| Fixture | Represents | Source or assumption | V-item |
| --- | --- | --- | --- |
| `../AdoToolkit.Core.Tests/TestManagement/BulkFixture.cs` | Path-routed fake: plans, merged suite tree, per-suite membership pages with numeric continuation tokens, work item batches | Reuses `Rest/testplans-first.json`, `Rest/testsuites-first.json`, `Rest/testsuites-last.json`; membership entries `workItem.id` and `order` generated | [V-04] |
| `../AdoToolkit.Core.Tests/TestManagement/SuiteTraversalTests.cs`, `BulkTestCaseServiceTests.cs` | S3-2: depth-first recursion, `order` sorting across pages, one plan listing and tree per plan, case in two suites fetched once and emitted twice sharing steps, invalid membership shapes | Generated entries and synthetic step XML | [V-04] |
| `../AdoToolkit.Core.Tests/TestManagement/BulkRequestCountTests.cs` | S3-4: generated 300 cases in three suites (50-entry pages) sharing 20 Shared Steps over two levels | Generated once per test class; batch sizes 200, 100, 10, 10 asserted | [V-01], [V-04] |
| `Reports/testcases-multi.{en-US,fr-CA}.{html,md,json}` | Six exact UTF-8, LF golden files: three cases in two suites, case 10 in both (four occurrences), one partial | `MultiCaseFixture.cs` over the pinned 2.1 report models and clock; updates only with `ADOTOOLKIT_UPDATE_GOLDEN=1` in a manual run | S3-5, V-15 constructed routes only |
| `../AdoToolkit.Core.Tests/Reporting/MultiCaseDocumentTests.cs` | S3-5: grouped contents, unique and working anchors, numbering restart, sources Query/Mixed, totals schema, truncated output rejected, generated 300-case document rendered by streaming | 300-case models generated in memory; structural assertions only | — |
| `../AdoToolkit.PowerShell.Tests/BulkTestCase.Pester.ps1` | S3-2/S3-5 through the cmdlets: recursive suite retrieval, binding per input type (typed suite, WIQL result, work item, untyped Id object) with ParameterBinding traces, untyped suite/plan rejection, mixed pipeline, missing suite, fr-CA progress records, one FileInfo per export | Loopback fake server only; membership and batch bodies generated; request bodies captured by `Support/FakeAdoServer.ps1` | [V-04] |

## Slice 5, session 5.1 — test-area retrieval

All files are hand-written synthetic data from spec §15.9–§15.13 and the §19.3 "Test results"
fixture numbers. No server or documentation request was made. **Every Test-area route, version,
parameter and field below is an assumption pending V-19 to V-25**, including `pipelineReference.
pipelineAttempt`, `runStatistics`, `resultGroupType = Rerun`, `testCase.id` as a string, and the
`6.0-preview.1` attachment routes. Folder names use `TestRuns/`, never `TestResults/` (DD-024).
Hosts are `.test` and every name, identity and ID is fictional. Paging, over-limit and hostile
inputs are generated inside the tests.

| Fixture | Represents | Source or assumption | V-item |
| --- | --- | --- | --- |
| `TestRuns/runs-empty.json` | Test results 1: a build with no test runs; also the terminating empty page of every TopSkip listing | Spec §15.9 step 2 and §6.4 | [V-19] |
| `TestRuns/runs-two.json`, `results-run-201.json`, `results-run-202.json` | Test results 2: two runs with a failed, a passed and a not-executed identity, plus one passing identity present in both runs | **[V-19], [V-20] assumed** run and list item fields; ordering by `startedDate` when no pipeline attempt exists | [V-19], [V-20] |
| `TestRuns/result-detail-201-1.json` | Every §15.11 attempt field in one detailed result: message, trace, duration, computer, owner, runBy, failure type, resolution state, comment, priority, `failingSince`, associated bugs (one repeated), custom fields, plus unknown scalar, object and array fields and an untrusted response `url` | **[V-21] assumed** detail field names; response URLs are read and dropped; only unknown scalars reach `AdditionalFields` | [V-21] |
| `TestRuns/result-detail-202-11.json` | A second detailed failure with French decimal text in its message | **[V-21] assumed**; French message forms remain V-29 | [V-21] |
| `TestRuns/results-rerun.json`, `result-detail-rerun-flaky.json`, `result-detail-rerun-flaky3.json`, `result-detail-rerun-failed.json` | Test results 3: in-task rerun groups fail→pass, fail→fail→pass (listed out of sequence order) and fail→fail→fail | **[V-22] assumed** `resultGroupType = Rerun` with `sequenceId` sub-results as attempts | [V-22] |
| `TestRuns/runs-reattempt.json`, `results-run-301.json`, `results-run-302.json`, `result-detail-301-51.json`, `result-detail-302-61.json` | Test results 4: a job re-attempt as a second run of the same build; attempt 2 is listed first to prove ordering by `pipelineAttempt` | **[V-19], [V-22] assumed** pipeline attempt reference on the run | [V-19], [V-22] |
| `TestRuns/result-detail-301-52.json`, `result-detail-302-62.json` | Test results 5: both attempt sources combined for one identity — a rerun group in run 301 and a single result in run 302 | **[V-22] assumed**; ordering is run first, then sub-result | [V-22] |
| `TestRuns/result-detail-datadriven.json` | Test results 6: `DataDriven` sub-results (one nested a level deeper) and two `iterationDetails` with parameters and action results, none of which are attempts | **[V-21], [V-22] assumed** sub-result and iteration shapes | [V-21], [V-22] |
| `TestRuns/results-ungrouped.json`, `result-detail-ungrouped-81.json`, `result-detail-ungrouped-82.json` | Test results 7: a result with no automated name and one with an empty automated name | Spec §15.10: own identity keyed by run and result ID, one attempt, no history cells | [V-20] |
| `TestRuns/results-outcomes.json` | Test results 8: `Error`, `Timeout`, `Aborted`, `Inconclusive` and an unknown outcome string with an accent | **[V-20] assumed** outcome spellings; unknown values are kept verbatim and classified Other (Q-23) | [V-20] |
| `TestRuns/results-testcases.json`, `workitems-testcases.json` | Test results 9: a valid Test Case reference, one missing from the batch, and a non-integer reference; the batch returns only the valid ID with French title | **[V-21] assumed** string `testCase.id`; §9.1 reconciliation by ID, never by position | [V-21], V-05, V-15 work item route |
| `TestRuns/attachments-result.json`, `attachments-subresult.json`, `attachments-empty.json` | Attachment metadata for one of each kind (`.PNG` in mixed case, `.json`, `.htm`, unknown extension, no extension) plus a sub-result attachment and an empty list | **[V-23] assumed** list route, `6.0-preview.1` and fields; kinds come from the extension only, and nothing is downloaded in 5.1 | [V-23] |
| `TestRuns/build-401.json`, `builds-history.json` | BuildGet for one build, and the history window returning the current build and three earlier ones, newest first | **[V-25] assumed** BuildGet route/fields and the `definitions`/`branchName`/`maxTime`/`statusFilter`/`queryOrder` parameters | [V-25] |
| `TestRuns/runs-history-400.json`, `results-history-400.json`, `runs-history-399.json`, `results-history-399.json` | Test results 13: two readable history builds, one of which ran only one of the two reported identities (`NotRun`); build 398 answers 500 in the test | Spec §15.12; each history build uses its own run ID so routing stays unambiguous | [V-19], [V-20] |
| `../AdoToolkit.Core.Tests/TestRuns/TestRunFixture.cs` | Path-and-query routing over the fixtures above. An unrouted later `$skip` offset answers an empty page, so TopSkip terminates as §6.4 requires while every explicit page stays scripted | Spec §6.4; unmatched requests fail the test loudly | [V-19], [V-20] |
| `../AdoToolkit.Core.Tests/TestRuns/TestFailureRetrievalTests.cs` | S5-1: fixtures 1–2 and 7–12, identities, report order, every mapped field, attachment kinds, the composed `vstfs:` URI, the absence of the `outcomes` filter, and test results 10 paging with a short second page | Generated short pages and detail bodies | [V-19]–[V-21], [V-23] |
| `../AdoToolkit.Core.Tests/TestRuns/RetrievalRequestBoundTests.cs` | S5-1: exact request counts against the §15.9 bound, and that history builds cost only run and result pages | Rerun fixture: 2 run pages + 3 result pages + 3 details + 11 attachment lists = 19 | [V-19]–[V-23] |
| `../AdoToolkit.Core.Tests/TestRuns/AttemptClassificationTests.cs` | S5-2: fixtures 3–6, both flaky sources, a pass-only identity that is counted but not reported, and data-driven sub-results that never become attempts | Fixtures above | [V-22] |
| `../AdoToolkit.Core.Tests/TestRuns/RunHistoryTests.cs` | S5-3: window parameters and both scopes, `maxTime` from finish or queue time, oldest-first order with the current build last, `NotRun`, `HistoryUnavailable`, the request budget, and bars equal to the tally of their cells | Fixtures above plus a synthetic 500 body | [V-25], [V-19], [V-20] |
| `../AdoToolkit.Core.Tests/TestRuns/TestCaseLinkResolutionTests.cs` | S5-8: valid, missing and invalid references; exactly the five documented fields in one batch body; reference parsing rejects zero, negatives, padding, decimals and overflow | Fixtures above | [V-21], V-05 |
| `../AdoToolkit.Core.Tests/TestRuns/RetrievalCancellationTests.cs` | Cancellation during pass 2 stops before the next request; an authorization failure in a history build still fails the whole retrieval | Scripted fake responses | — |
| `../AdoToolkit.PowerShell.Tests/TestRuns.Pester.ps1` | S5-1/S5-3 through the cmdlets: BuildGet then runs, typed pipeline binding, foreign-collection rejection, local range validation, one set per piped build, history cells, and the configured failure maximum giving one warning and `Partial` | Loopback fake server only; the configuration override writes to `$TestDrive` | [V-19]–[V-25] |

`tests/Live/TestFailures.Live.ps1` checks V-19 to V-25 at work in memory only and writes nothing
(DD-022). Attachment download, content checks and report rendering belong to sessions 5.2–5.4.

## Slice 5 session 5.2 — Highlighting, charts and shared palette

All new text is hand-written synthetic content. The English/French runtime and assertion forms
remain assumptions pending V-29; no network, work data, browser or file launch is involved.

| Fixture / generated input | Represents | Source or assumption | V-item |
| --- | --- | --- | --- |
| `TestRuns/stack-english.txt`, `stack-french.txt` | Test results 14: frames with/without paths, line numbers, headers, inner/rethrow separators, async/generic types, framework prefixes, query URLs and punctuation, unsafe schemes | §15.14; French runtime wording remains assumed | V-29 |
| `TestRuns/messages.txt` | Test results 15: MSTest delimiters, NUnit/xUnit assertions, French markers, quoted text, signed/decimal/exponent numbers and URLs | §15.14; runner wording remains assumed | V-29 |
| `TestRuns/hostile-text.txt` | Text parts of Test results 16: tags, closing script tags, event attributes, quotes, traversal/reserved names, combining characters, French spacing and unsafe schemes | §11.4 and §19.3; strings never become paths, ids or classes | V-29 |
| `Attachments/valid.json`, `malformed.json.txt` | Attachments 2: valid JSON with nesting, literals, escapes and hostile text; malformed JSON remains plain | §15.14; formatting precedes the preserving lexer | — |
| `Reporting/Highlighting/LexerRoundTripTests.cs`, `HighlightedCodeWriterTests.cs` under Core tests | Generate the 2 MiB message, its truncated head, lone surrogates, mixed line endings and deterministic mixed content; run every lexer over each fixture file and every decoded JSON string in TestRuns/Attachments | Exact token concatenation and pure repeatability; 1 MiB character display limit | V-29 |
| `Reporting/Highlighting/JsonLexerTests.cs` under Core tests | Generate depth 64/65 and above-inline-byte-limit JSON; exact UTF-8 limit, malformed Unicode, no comments/trailing commas; separate two-space formatting | Attachments 2; no large committed fixtures | — |
| `Reporting/Charts/RunHistoryChartTests.cs`, `StripTests.cs` under Core tests | Reuse Test results 13 through a fake handler; bar counts equal cells, Flaky remains a Passed subset, current outline/label, unavailable hatch, equivalent table, six history states and every attempt | S5-3 rendering only; dates/labels localized and geometry invariant | V-26 links remain unconfirmed |
| `Reporting/ThemeContrastTests.cs` under Core tests | Read embedded `report-base.css` custom properties for screen and print; text/code/status labels ≥4.5:1 and meaningful marks/borders/focus ≥3:1 | §12.7; no duplicate color table | — |

## Slice 5 session 5.3 — Report fixtures and golden files

All report inputs are synthetic C# objects in
`AdoToolkit.Core.Tests/Reporting/TestFailures/TestFailureReportFixture.cs`. No network or work
data is used. The renderer ignores deliberately untrusted response URLs. Clock and toolkit
version are pinned. English/French previews are written outside the repository for user review.

| Fixture / generated input | Represents | Source or assumption | V-item |
| --- | --- | --- | --- |
| `Reports/testfailures-failed.en-US.html`, `.fr-CA.html` | A failed identity with all attempt metadata, nested data-driven sub-results, iterations/parameters/actions, custom/additional fields, resolved Test Case and attachment metadata | §15.11, §15.14; generated from the fixture above | V-16, V-21, V-23, V-26, V-29 |
| `Reports/testfailures-flaky.en-US.html`, `.fr-CA.html` | Failed and flaky identities; failed then passed attempts, open/closed defaults, strips and history chart/table | §15.10, §15.14 | V-16, V-22, V-26 |
| `Reports/testfailures-partial.en-US.html`, `.fr-CA.html` | Failure limit, counted-only failures, partial banner, unresolved Test Case ID link, localized diagnostics | §15.14–§15.16 | V-16, V-26 |
| `Reports/testfailures-hostile.en-US.html`, `.fr-CA.html` | Closing script tags, inline-handler text, quoted attributes, traversal/reserved-name text, unsafe response URLs, French spacing/Unicode, invalid reference with no Test Case link | §15.14–§15.15; hostile names remain display-only | V-16, V-26, V-29 |
| Fixture `review` variant | Failed and flaky cards, nested details and a partial diagnostic banner in one external preview | 5.3 visual review only; S5-9 attachment/browser checklist remains 5.4 | V-16; does not confirm V-27 |
| `Reporting/TestFailures/ContentSecurityPolicyTests.cs`, `ScriptAssetTokenScanTests.cs`, `StructuralCompletenessTests.cs`, `TestFailureReportValidatorTests.cs` | Exact written script bytes, one-to-one CSP, forbidden APIs, no visible script strings, hidden controls, scripts-ignored completeness, corrupted tags/counts/hashes | S5-5, no browser runner | — |

Golden comparison replaces only the script body with `__SCRIPT_ASSET__` and its CSP hash with
`sha256-__SCRIPT_SHA256__`; runnable reports retain the actual asset and matching hash. Golden
approval followed the user's review of the external English/French previews and the requested
visual refinements. The opt-in
`ADOTOOLKIT_UPDATE_GOLDEN=1` update path validates actual HTML first and uses atomic replacement.

## Slice 5 session 5.4 — Attachments, commit order and export

Hand-written or generated synthetic content only. Attachment routes, version, fields and the
`testSubResultId` content parameter remain assumptions pending V-23. Real attachments are work
data and never enter the repository (DD-010, DD-020). Over-limit, depth-65 and long-token inputs
are generated inside the tests; limits are configured small so no generated input exceeds 200 KiB.

| Fixture / generated input | Represents | Source or assumption | V-item |
| --- | --- | --- | --- |
| `Attachments/pattern.png` | Attachments 1: a 16 × 12 valid PNG (94 bytes, CRCs checked when generated); `*.png binary` in `.gitattributes` keeps it byte for byte | §15.13 PNG signature check | [V-23] |
| `Attachments/png-without-signature.txt` | Attachments 1: text served for a `.png` name; saved as `.bin` with `AttachmentContentMismatch` | §15.13 | [V-23] |
| `Attachments/valid.json`, `malformed.json.txt` (5.2) | Attachments 2: served as JSON content; inline preview and mismatch | §15.13; inline limit from `maximumInlineJsonBytes` | [V-23] |
| Generated `large.json`, depth-65 JSON, 200 KB string token | Attachments 2: above the inline limit (linked only), depth 65 (mismatch), and a token longer than the streaming check's buffer | Generated in `AttachmentDownloaderTests.cs` | — |
| `Attachments/test-output.html` | Attachments 3: HTML with a harmless script; linked as test output, never embedded | §15.13, §18 item 12 | [V-23] |
| `Attachments/other-bytes.txt` | Attachments 4: bytes served for an unknown extension and for a name without one (`.bin`); also the partial-body retry payload | §15.13 | [V-23] |
| `Attachments/attachments-download.json` | Attachments 1–8 metadata in the assumed list shape: declared oversize (5), understated size exceeded while streaming (5, generated 3,000-byte body without `Content-Length`), budget subsets (6), 503 after three attempts and a body that fails after a prefix (7), and a sub-result attachment requested with `testSubResultId` (8) | **[V-23] assumed** fields `id`, `fileName`, `size`, `attachmentType`, `comment`; `comment` names the fixture number | [V-23] |
| `TestRuns/attachments-hostile.json` | Test results 16 file names: traversal, reserved device name, closing script tag, quotes and markup, absolute and UNC paths, stream and trailing dot | Hostile names live only in metadata JSON and never reach a path, id or class | [V-23] |
| `../AdoToolkit.Core.Tests/TestRuns/AttachmentDownloaderTests.cs`, `AttachmentFixture.cs` | S5-6: report order, toolkit names, byte-identical files, content checks, per-file and total limits (known size and while streaming), retries, empty bodies, terminating authentication/authorization/cancellation/local-write failures, de-duplication, hostile names, JSON parser rules, export copies | Fake `HttpMessageHandler` per attachment ID; `FakeClock` removes retry delays | [V-23] |
| `../AdoToolkit.Core.Tests/IO/GenerationFolderCommitTests.cs` | S5-7: an injected failure at each §13.4 step with and without attachments, a stop after step 6, re-export cleanup with look-alike names, natural rename/move/NoClobber/cleanup failures, and junctions created with `cmd /c mklink /J` (the test fails if creation fails) | Temporary folders under the test directory; links are removed before cleanup | — |
| `../AdoToolkit.Core.Tests/Reporting/TestFailures/AttachmentRenderingTests.cs`, `TestFailureExporterTests.cs` | S5-6 markup (thumbnail, inline JSON, HTML label, `.bin`, status notes, escaped folder names) and validator corruptions; S5-3 export with a 500 history build; `-SkipAttachments`; end-to-end download and re-export | 5.3 report fixture and Test results 2/13 routes with binary content routes first | [V-23], V-26 |
| `../AdoToolkit.PowerShell.Tests/TestFailureExport.Pester.ps1` | S5-6/S5-7 through the cmdlet: `-WhatIf` and `-NoClobber` request no content, `AttachmentDirectory`, `-SkipAttachments` without a connection, one file path per set, invalid paths and foreign collections | Loopback fake server; `Support/FakeAdoServer.ps1` now serves `Bytes` unchanged | [V-23] |

Golden files: the eight `Reports/testfailures-*.html` files changed only by the seven new CSS
rules for local attachments (their attachments are never downloaded, so their markup is unchanged).
