# Synthetic fixture catalog

Fixture rules are in `tests/AGENTS.md`. Every fixture file has exactly one entry below;
`FixtureCatalogTests` fails when a file has no entry or two, or when an entry names no file.

- An entry is a code span with a `/` in the first cell of a row: a path relative to this
  folder, where `*` stands for any run of characters inside one file name.
- `V-item` is the unconfirmed server assumption that the fixture encodes;
  `docs/archive/README.md` resolves the IDs. When a live check contradicts an assumption,
  correct these fixtures and their tests.
- A number prefix in `Steps/` or `Parameters/` is the fixture number in §19.3.
- Paging sequences, inputs above 1 MiB and most malformed or hostile variants are generated
  inside the tests, not stored here.

## Loaders

| Loader | Reads |
| --- | --- |
| `FakeHttpMessageHandler.Fixture(name)` | `Rest/` |
| `ParserFixture.Read(path)` | Any folder |
| `ExpansionFixture.Xml(name)` | `Steps/` |
| `TestRunFixture.Route(fixture, fragments)`, `TestRunFixture.Read(name)` | `TestRuns/` |
| `AttachmentFixture.Bytes(name)` | `Attachments/` |
| `BuildFailureDeriverTests.Timeline(name)` | `Timelines/` |
| `LexerFixtures.EveryText()` | Every `.txt` and `.json` file in `TestRuns/` and `Attachments/`, as lexer round-trip input |
| The three golden test classes | `Reports/`; see the `update-goldens` skill |
| A helper at the top of each `*.Pester.ps1` file | `Rest/`, `TestRuns/`, `Attachments/` |

## Rest/

HTTP pipeline, work items, WIQL, test plans, builds. Tests add continuation and
`Retry-After` headers.

| File | Represents | V-item |
| --- | --- | --- |
| `Rest/success.json`, `Rest/projects-second.json`, `Rest/projects-empty.json` | Project pages: a full page, a short page and an empty page | V-14 |
| `Rest/iis-unauthenticated.html.txt` | IIS-like 401 HTML body, which must never be echoed | V-07 |
| `Rest/forbidden.json`, `Rest/not-found.json`, `Rest/bad-version.json` | 403, 404 and API-version rejection bodies | V-07 |
| `Rest/redirect.json` | Redirect body; the test supplies the `Location` header | — |
| `Rest/throttled.json`, `Rest/unavailable.json`, `Rest/server-error.json` | 429 with `Retry-After`, 503 until retries run out, and 500, which is not retried | — |
| `Rest/malformed-body.json.txt`, `Rest/empty-body.txt` | Malformed and empty bodies of a successful response | — |
| `Rest/french-error.json` | French server error text | V-18 |
| `Rest/workitems-null.json`, `Rest/workitems-compact.json` | IDs 1 and 3 out of order, 2 and 4 absent: as `null` placeholders and as omissions | V-05 |
| `Rest/workitem-fields.json` | Typed fields, an opaque identity, unknown nested values, relation attributes, an untrusted response URL, a project name with accents, a space and a slash | V-10, V-15 |
| `Rest/test-category.json`, `Rest/shared-category.json` | Test Case and Shared Step category members with localized, customized type names | V-13 |
| `Rest/testcase-no-steps.json` | Two category members and one other type, all without a steps field | V-13 |
| `Rest/testcase-mixed.json` | A partial case, a root that is not a test and a complete case; a fourth requested ID is omitted | V-01, V-05, V-13 |
| `Rest/shared-no-steps.json` | A resolved Shared Steps item without a steps field | V-01 |
| `Rest/shared-expansion-sequence.json` | Four ordered batch responses: the root, then three Shared Steps levels | V-01, V-05 |
| `Rest/testcase-detail.json` | Test Case 3 read with its relations for `-IncludeDetail`: description markup with a script and an unsafe link, tags, creation and automation fields, a nested field that is never read, links of three types (one repeated, one to the case itself, one with an unreadable URL), a safe and an unsafe hyperlink, an artifact link, attachments with and without a name, a `null` relation and untrusted response URLs | V-31, V-10, V-15 |
| `Rest/testcase-detail-linked.json` | The linked work items 3001 and 3050, each with its project; 3099 is omitted | V-31, V-05 |
| `Rest/testpoints.json` | The four test points of Test Case 3: failed and passed with string IDs, one without plan and suite references whose tester is text and whose date is not a date, and one that was never run | V-32 |
| `Rest/testplans-first.json`, `Rest/testplans-last.json` | Plan pages with accented names and an untrusted `_links` URL | V-04, V-15 |
| `Rest/testplan-empty-page.json` | Empty page that still carries a token; shared by the three test plan endpoints | V-04 |
| `Rest/testsuites-first.json`, `Rest/testsuites-last.json` | Suite tree across pages: root, two children, grandchildren listed after later siblings; French names with `’` | V-04, V-15 |
| `Rest/suitetestcases-first.json`, `Rest/suitetestcases-last.json` | Suite membership pages with `workItem.id` and `order` 1 to 3 | V-04 |
| `Rest/wiql-flat.json` | Flat WIQL result: ordered IDs, columns, `asOf` with an offset, untrusted URLs | V-06 |
| `Rest/wiql-tree.json` | Tree result with `workItemRelations`; tests derive the one-hop and mixed variants | — |
| `Rest/wiql-limit-error.json` | 400 body whose message starts with `VS402337` | V-06 |
| `Rest/build-definitions-first.json`, `Rest/build-definitions-last.json` | Definition pages with folder, revision and queue status; one exact name in two folders and an unaccented name | V-14, V-26 |
| `Rest/builds-first.json`, `Rest/builds-last.json` | Build pages with source, repository, identity, dates and URI; untrusted web links | V-14, V-15 |
| `Rest/builds-empty.json` | Empty page with a scripted continuation header; also an empty log list | V-14 |
| `Rest/build-logs.json` | Log list: log 11 with 1,000 lines, log 12 with none, log 13 with an unknown count | V-14 |
| `Rest/build-log-body.txt` | UTF-8 French log body, saved byte for byte | V-14 |

## Steps/

Steps XML of Test Cases and Shared Steps (§10.2, §10.3, §10.5, §11).

| File | Represents | V-item |
| --- | --- | --- |
| `Steps/01-direct.xml` | One direct Action step; also the nested leaf of other fixtures | V-01, V-02 |
| `Steps/02-multiple.xml` | Action and Validate steps in document order with non-consecutive source IDs | V-01, V-02 |
| `Steps/03-empty-result.xml` | An Action whose expected result is HTML without text | V-02 |
| `Steps/04-unknown-type.xml` | An unknown step type, read as Validate | V-01 |
| `Steps/05-unformatted.xml` | No `isformatted`: literal angle brackets, an ampersand and `@user` | V-02 |
| `Steps/06-rich.xml` | Nested lists, a table, links, images and block tags | V-02 |
| `Steps/07-nested.xml` | Several layers of encoded markup | V-02 |
| `Steps/08-literal-entity.xml` | Literal markup-like text that a later pass consumes (§11.3) | V-02 |
| `Steps/09-french.xml` | NFD accents, U+00A0, U+202F, punctuation, an apostrophe, an emoji and Japanese | V-02 |
| `Steps/10-shared.xml`, `Steps/11-nested.xml` | One Shared Steps group and its nested group | V-01 |
| `Steps/12-repeated.xml`, `Steps/13-diamond.xml` | Repeated and diamond references | V-01 |
| `Steps/14-direct-cycle.xml`, `Steps/15-indirect-cycle.xml`, `Steps/16-self-cycle.xml` | Direct, indirect and self reference cycles | V-01 |
| `Steps/17-missing-ref.xml`, `Steps/18-invalid-ref.xml` | A reference without `ref` and one with an invalid `ref`; no fallback to `id` | V-01 |
| `Steps/19-compref-children.xml` | A reference with child steps: the children are skipped, the reference and the next step kept | V-01 |
| `Steps/20-missing.xml` | A reference that the batch omits, followed by a direct step | V-01, V-05 |
| `Steps/23-malformed-root.xml.txt`, `Steps/23-malformed-shared.xml.txt` | Malformed Test Case and Shared Steps documents | — |
| `Steps/24-depth.xml` | A reference beyond the configured depth | V-01 |
| `Steps/26-hostile.xml` | Script, style, head, comment, event attributes, an unsafe `href` and a remote image | V-02 |
| `Steps/27-dtd.xml.txt` | An external entity declaration, which is rejected | — |
| `Steps/28-numbering.xml`, `Steps/28-child.xml` | Numbering 3.3.1, then unresolved group 4 and direct step 5 | V-01 |
| `Steps/29-urls.xml` | HTTP(S), `mailto`, `javascript`, `data` and `file` links | V-02 |

## Parameters/

Parameter declarations and data (§10.6). Every shape here is an assumption.

| File | Represents | V-item |
| --- | --- | --- |
| `Parameters/01-names.xml`, `Parameters/01-local.xml` | Declared order, local rows, an empty value and an ignored `xs:schema` | V-03 |
| `Parameters/02-shared.json`, `Parameters/02-shared-set.xml` | Names mapped to shared set IDs, and the rows of the shared set | V-03 |
| `Parameters/03-declared-only.xml` | Names without data | V-03 |
| `Parameters/04-malformed.xml.txt`, `Parameters/04-malformed.json.txt`, `Parameters/04-malformed-names.xml.txt`, `Parameters/04-dtd.xml.txt` | Invalid data, invalid declarations and a rejected DTD | V-03 |
| `Parameters/05-unresolved.json` | A mapping to a shared set that the batch omits | V-03 |
| `Parameters/06-french-names.xml`, `Parameters/06-french-local.xml` | Accented names and values, encoded XML element names and a space | V-03 |

## Timelines/

Build timelines (§15.3). Stage, Phase, Job and Task are YAML timeline assumptions.

| File | Represents | V-item |
| --- | --- | --- |
| `Timelines/multi-stage.json` | Shuffled records; failures `Deploy › Release › Prepare` (log 11) and `Deploy › Release › Publish` (log 12); parent failures suppressed | V-11 |
| `Timelines/retried-job.json` | Only attempt 2 of `Build › Retry job › Retry task` fails; the superseded job is dropped and `previousAttempts` kept | V-11 |
| `Timelines/canceled.json` | A canceled build: `Deploy › Waiting` without a log and `Deploy › Failed first` | V-11 |
| `Timelines/warnings.json` | No failure; with warnings included, `Build › Compile › Analyze` | V-11 |
| `Timelines/no-log.json` | `Build › Agent allocation` with no log ID or line count and an error issue | V-11 |

## TestRuns/

Test runs, results, attachments lists, history and bugs of one build (§15.9 to §15.13).
The folder is not named `TestResults` (DD-024).

| File | Represents | V-item |
| --- | --- | --- |
| `TestRuns/runs-empty.json` | A build without test runs; also the last, empty page of a listing | V-19 |
| `TestRuns/runs-two.json`, `TestRuns/results-run-201.json`, `TestRuns/results-run-202.json` | Two runs: a failed, a passed and a not-executed test, and one test that passes in both. `build.id` is a numeric string, as observed at work | V-19, V-20 |
| `TestRuns/runs-server2020.json` | The run shape observed at work on 2026-09-17: string `build.id`, the totals `totalTests`, `passedTests`, `notApplicableTests`, `unanalyzedTests` and `incompleteTests`, no `runStatistics` | V-19 |
| `TestRuns/runs-in-progress.json` | A run whose state is `InProgress` | V-19 |
| `TestRuns/result-detail-201-1.json` | Every attempt field of §15.11, a repeated associated bug, custom fields, unknown fields and an untrusted `url`. `testRun.id` and bug IDs are numeric strings | V-21 |
| `TestRuns/result-detail-202-11.json` | A second failure, with French decimal text in its message | V-21 |
| `TestRuns/results-rerun.json`, `TestRuns/result-detail-rerun-flaky.json`, `TestRuns/result-detail-rerun-flaky3.json`, `TestRuns/result-detail-rerun-failed.json` | In-task reruns: fail then pass, fail twice then pass (listed out of order), and three failures | V-22 |
| `TestRuns/runs-reattempt.json`, `TestRuns/results-run-301.json`, `TestRuns/results-run-302.json`, `TestRuns/result-detail-301-51.json`, `TestRuns/result-detail-302-61.json` | A job re-attempt as a second run with the same stage, phase and job names; attempt 2 is listed first | V-19, V-22 |
| `TestRuns/result-detail-301-52.json`, `TestRuns/result-detail-302-62.json` | Both retry kinds for one test: a rerun group in run 301 and a single result in run 302 | V-22 |
| `TestRuns/result-detail-datadriven.json` | `DataDriven` sub-results and two iterations with parameters and action results; none is an attempt | V-21, V-22 |
| `TestRuns/results-ungrouped.json`, `TestRuns/result-detail-ungrouped-81.json`, `TestRuns/result-detail-ungrouped-82.json` | A result without an automated name and one with an empty name | V-20 |
| `TestRuns/results-outcomes.json` | `Error`, `Timeout`, `Aborted`, `Inconclusive` and an unknown outcome with an accent | V-20 |
| `TestRuns/results-testcases.json`, `TestRuns/workitems-testcases.json` | A valid Test Case reference, one missing from the batch and one that is not an integer; the batch returns the valid ID | V-21, V-05, V-15 |
| `TestRuns/attachments-result.json`, `TestRuns/attachments-empty.json` | Attachment lists: one of each kind (`.PNG`, `.json`, `.htm`, unknown extension, no extension) and an empty list | V-23 |
| `TestRuns/attachments-hostile.json` | Hostile attachment names: traversal, a device name, a closing script tag, quotes, absolute and UNC paths, a stream and a trailing dot | V-23 |
| `TestRuns/build-401.json`, `TestRuns/builds-history.json` | The build, and its history window: the current build and three earlier ones, newest first | V-25 |
| `TestRuns/runs-history-400.json`, `TestRuns/results-history-400.json`, `TestRuns/runs-history-399.json`, `TestRuns/results-history-399.json` | Two readable history builds; one ran only one of the two reported tests | V-19, V-20 |
| `TestRuns/stack-english.txt`, `TestRuns/stack-french.txt` | Stack traces: frames with and without paths, headers, inner-exception and rethrow separators, async and generic types, URLs, unsafe schemes | V-29 |
| `TestRuns/messages.txt` | Assertion messages of MSTest, NUnit and xUnit, French markers, quoted text, numbers and URLs | V-29 |
| `TestRuns/hostile-text.txt` | Tags, closing script tags, event attributes, quotes, traversal and reserved names, combining characters, French spacing | V-29 |
| `TestRuns/workitems-bugs.json` | The bugs associated with `result-detail-201-1.json`: 2001 Active and 2002 Closed | V-30 |
| `TestRuns/workitems-testcase-links.json` | Test Case 1010 with relations: bugs through three link types, a User Story, Shared Steps, and a hyperlink, artifact link, attachment and unreadable URL that are never requested | V-30 |
| `TestRuns/workitems-linked.json` | The linked work items: 3001 Bug Active, 3002 Bug Closed, 3050 User Story, 3060 Shared Steps, 3080 of the custom type `Défaut de production` | V-30 |
| `TestRuns/workitemtypecategory-bug.json` | `Microsoft.BugCategory` holding Bug and the custom type | V-30 |
| `TestRuns/workitemtype-states-bug.json` | The Bug states New, Active, Resolved and Closed with their categories | V-30 |

## Attachments/

Attachment content and the download list (§15.13). Only JSON and text are downloaded.

| File | Represents | V-item |
| --- | --- | --- |
| `Attachments/attachments-download.json` | The download list, 6001 to 6014. Each `comment` names its case: never requested (PNG, HTML, unknown or no extension), bytes that are not text, valid, malformed, large and deep JSON, declared and streamed oversize, a failing and a partial download, a sub-result attachment | V-23 |
| `Attachments/valid.json` | Valid JSON with nesting, literals, escapes and hostile text; shown inline | V-23 |
| `Attachments/malformed.json.txt` | Malformed JSON served for a `.json` name: a content mismatch | V-23 |
| `Attachments/other-bytes.txt` | Text content; also the payload of the partial-body retry | V-23 |
| `Attachments/pattern.png` | A valid 16 × 12 PNG, 94 bytes: served for `binary.log`, whose text check then fails | V-23 |
| `Attachments/test-output.html` | HTML with a harmless script: linked, never embedded | V-23 |

## Reports/

Golden reports: exact UTF-8 bytes, LF line ends, no BOM. Each `*` covers both cultures
(`en-US`, `fr-CA`) and, for Test Case reports, the three formats (`html`, `md`, `json`);
`testcase-rich.*` and `testcase-detailed.*` show what only the HTML report renders and
have no other format. They change only through the `update-goldens` skill.

| File | Represents | V-item |
| --- | --- | --- |
| `Reports/testcase-direct.*` | Test Case report: direct steps | V-15 |
| `Reports/testcase-nested.*` | Test Case report: nested Shared Steps | V-15 |
| `Reports/testcase-partial.*` | Test Case report: partial and truncated expansion with diagnostics | V-15 |
| `Reports/testcase-parameterized.*` | Test Case report: shared parameters | V-15 |
| `Reports/testcase-french.*` | Test Case report: French content, U+00A0, U+202F and an emoji | V-15 |
| `Reports/testcase-rich.*` | Test Case report, HTML only: formatted steps with lists, tables, emphasis, a link, preformatted text, a quote, an image placeholder and markup escaped twice; a step that is not formatted; a parameter with two iterations | V-02, V-15 |
| `Reports/testcase-detailed.*` | Test Case report, HTML only, with the details of `-IncludeDetail`: description, tags, automation fields, linked work items, hyperlinks, attachments, test points of every outcome and one lookup warning | V-15, V-31, V-32 |
| `Reports/testcases-multi.*` | One document for three cases in two suites; case 10 is in both, one case is partial | V-15 |
| `Reports/testfailures-failed.*` | Failed-test report: one failed test with every attempt field, data-driven sub-results, custom fields, a resolved Test Case and attachment metadata | V-16, V-21, V-23, V-26, V-29 |
| `Reports/testfailures-flaky.*` | Failed and flaky tests, history strips and the history chart | V-16, V-22, V-26 |
| `Reports/testfailures-partial.*` | The failure limit, counted-only failures, the partial banner, an unresolved Test Case, diagnostics | V-16, V-26 |
| `Reports/testfailures-hostile.*` | Closing script tags, handler text, quoted attributes, traversal names, unsafe response URLs, French spacing, an invalid Test Case reference | V-16, V-26, V-29 |
| `Reports/testfailures-grouped.*` | English and French stages, a run outside the attachment window, a passing retry, a repeated message and trace, text and JSON attachments | V-19, V-22 |

In an HTML golden, of either report, the script body is `__SCRIPT_ASSET__` and its CSP hash
is `sha256-__SCRIPT_SHA256__`; an exported report carries the real script and hash.
