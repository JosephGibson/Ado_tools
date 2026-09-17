# Slice 2, session 2.2 — completion handoff

Date: 2026-09-15. Sessions 2.1–2.2 complete; Slice 2 is offline done (M4).
The user accepted the neutral report design and requested the final styling/logo pass, now complete.

## Verification

- Entry gate: `verify` exited 0, `Status: pass`, no warnings; project-check 17.983 s.
  Slice 1 offline completion and all session 2.1 deliverables were present.
- Completion `verify`: exit 0, `Status: pass`, no warnings in any stage;
  project-check 18.970 s after the final styling/logo pass. All 89 tooling Pester tests pass. Core runs under both
  en-US and fr-CA; product Pester and compiled help checks pass.
- Acceptance source scan lists S2-1 through S2-7. There are 30 UTF-8, LF golden files,
  ten per format, with the existing pinned clock and toolkit version.
- The published JSON schema is referenced by `ReportFixture`; every JSON golden and
  the existing with/without-source matrix are schema validated with external fetching disabled.
- English and French Export-AdoTestCase help sources are present. Packaging now builds
  eleven topics in en-US and fr, with the existing regional French fallback checks.
- No Pester test passes the Open switch. Core launcher tests use an injected fake.

## What changed

- `SinkEncoding` segments HTML around literal U+00A0/U+202F. The encoder test was run
  successfully **before** writing the HTML renderer: framework output was
  `&#xA0;&#x202F;’« »&#x1F600;`. HTML retains the emoji numeric reference; Markdown
  keeps the literal emoji. Both preserve French spaces, quotes and decomposed accents.
- HTML uses embedded dark charcoal/graphite CSS, a 700 px stacking breakpoint, light print rules, the exact
  script-free CSP, culture/generator/count markers, the ordered metadata header,
  parameter tables, linked and indented Shared Steps groups, warnings and final diagnostics.
- Markdown has numbered row headings, linked blockquote banners, parameter tables,
  culture-specific labels and sink escaping. Content links allow only HTTP, HTTPS
  and mailto. Attribute and link text encoding are separate; Markdown URL parentheses
  are percent encoded.
- HTML and Markdown validation reads bounded line prefixes from a stream, retaining
  at most 512 characters per line. It checks the renderer-owned count markers/headings
  and actual row counts. Corrupted markers, omitted rows and truncation are tested;
  generated multi-MiB content proves validation does not buffer the report.
- Core orchestration resolves culture, builds the model, resolves the target, invokes
  ShouldProcess, renders through the atomic writer, validates, commits, and only then
  optionally invokes the launcher. Tests cover failed validation, WhatIf, and NoClobber
  including a competing writer immediately before commit.
- `Export-AdoTestCase` collects pipeline input and renders in EndProcessing. It checks
  provenance and the FileSystem provider, returns FileInfo, and rejects IncludeSource
  outside JSON and more than one case before creating a file. Tests retrieve a case
  through FakeAdoServer, then assert export adds no server requests.

## Acceptance evidence

| ID | Evidence |
| --- | --- |
| S2-1 | GoldenReportTests: five variants × two cultures × three formats; exact UTF-8 bytes with pinned clock/version. User review acceptance is recorded below. |
| S2-2 | HostileContentTests: Steps fixture 26, hostile title and parameter content, separate attribute encoding, Markdown metacharacters, no script and exact CSP. |
| S2-3 | Existing atomic writer fault tests plus exporter validation-failure preservation and cleanup. |
| S2-4 | Core exporter launcher/WhatIf/NoClobber race tests; Export.Pester.ps1 covers FileInfo, paths, cultures, input guards and provenance. |
| S2-5 | Ten JSON goldens and existing generated with/without-source outputs validate against testcase.v1.schema.json. |
| S2-6 | RenderedHeaderAndLinkTests and existing ReportHeaderAndLinkTests cover metadata presence/absence, constructed URLs for cases/groups/parameter sets/plan/suite, and Steps fixture 29. |
| S2-7 | FrenchTypographyTests pins literal no-break spaces beside punctuation and inside numbers, quotes, accents and emoji behavior. |
| S2-8 | Not run; work-machine manual checklist remains in slice-2-reports.md section 8. |

## Browser review

The candidate HTML files were atomically copied outside the repository to
`C:\Users\Joe\AppData\Local\Temp\AdoToolkit-Slice2-Review-20260915`.
The required review request names `testcase-parameterized.en-US.html` and
`testcase-parameterized.fr-CA.html`. The same folder contains nested, partial and
French-content examples in both report cultures.

User confirmation: **accepted** — “Looks good” after the refreshed English/French review links,
followed by authorization for final styling and a logo. Automated golden comparisons do not
constitute browser approval. No browser was launched by a test.

## Design decisions, authorizations and remaining work

- Retain the session 2.1 JSON object root with a cases array; one case is an array of one.
- Retain injected generated-at/version for exact golden bytes, without normalization.
- Golden updates require `ADOTOOLKIT_UPDATE_GOLDEN=1` in a manual local run;
  tools/check.ps1 clears it during Core tests and restores the caller's environment.
- Keep orchestration in Core, with an injected IDocumentLauncher and a shell implementation
  used only by the cmdlet when requested.
- Multiple inputs terminate with InvalidOperation until Slice 3 supplies the multi-case layout.
- No restore, package changes, installs, external network, deployment or Git lifecycle actions.
  The user authorized the outside-repository synthetic previews. Loopback tests use the
  plan's fake server. No sandbox denial or escalation occurred.
- Initial integration failures were fixed in the test fixtures: package command count,
  single-ID mock response, per-test output directory and unconditional fake-server cleanup.
  The implementation and gate were not weakened for the environment.
- Live S2-8, V-15 browser routes and V-16 server French terminology remain at work.

## French text for review

Existing report labels are listed in the [2.1 evidence](slice-2-session-2.1-evidence.md#french-text-for-review).
New resources in this session:

| Key | French |
| --- | --- |
| ReportHeading | Cas de test Azure DevOps |
| IncludeSourceJsonOnly | IncludeSource nécessite le format de rapport JSON. |
| SingleCaseExportOnly | Fournissez exactement un cas de test. L’exportation de plusieurs cas de test n’est pas encore disponible. |
| ExportReport | Exporter le rapport du cas de test |
| FileSystemPathRequired | Le chemin du rapport doit utiliser le fournisseur FileSystem. |

Also review the new [French command help](../commands/fr-CA/Export-AdoTestCase.md).
Fluent wording review and comparison with the Server 2020 French UI are still pending.

## Owner-requested UI pass

The owner explicitly requested a modern dark-mode reporting UI after the first browser-review
request. This authorizes the Q-21 revision; the spec revision history and Slice 2 plan record it.
The refreshed HTML candidates use raised graphite surfaces, subtle borders, high-contrast text and
one cool accent. Compact metadata retains field order, values and links; IDs, numeric metrics,
paths, dates, versions and diagnostic codes use local monospace fonts. Action/expected-result
cards stack below 700 px, with compact nested Shared Steps and distinct warning banners.

Keyboard-visible focus, a skip-to-steps link and sticky section navigation provide direct access
to available sections. Parameter tables have a named keyboard-focusable horizontal scroll region.
Print CSS restores a light palette and hides navigation. No scripts or remote font assets were added.
The existing report contains no charts or filtering controls to restyle.

Validation: all 30 goldens regenerated through the explicit update variable; full verify exits 0,
Status pass, no warnings, project-check 19.120 s. Navigation tests check unique targets, localized
labels and absence of links to missing sections. Existing data/link/encoding tests remain green.
Local Edge screenshots were inspected at desktop and narrow widths; no external network was used.
Current neutral-palette text contrast: body 14.82:1, muted text 6.68:1, links 10.47:1, warning text 9.13:1.
The outside-repository HTML review files contain the final design. The user subsequently accepted
the neutral design; see the final completion record below.

Additional French navigation strings for fluent review: **Vue d’ensemble**, **Étapes**,
and **Sections du rapport**.

### Second visual refinement

The owner requested less blue and a more professional appearance. Screen surfaces now use
neutral charcoal and graphite, with desaturated silver links, quieter borders, smaller corner
radii, flat navigation and restrained heading weights. Expected-result panels retain separation
through neutral surface contrast; amber remains reserved for warnings. Layout, data, navigation,
responsive behavior and light print styling are preserved. The refreshed HTML goldens remain
approved goldens after the user's acceptance and authorized final refinement.

## Final completion record

The user's “Looks good” accepted the neutral report design after the outside-repository
English/French previews. Their requested final refinement adds a static embedded vector A mark,
an AdoToolkit wordmark, subtle neutral header shading, section rules, table dividers and boxed
step numbers. The SVG is decorative beside the text wordmark, has no scripts or external
references, scales for narrow layouts, and uses neutral print colors. The brand string is the
same proper name in both resource cultures; no additional translation is needed.

Final desktop and narrow Edge previews were inspected locally and the English/French report
files were refreshed in the existing outside-repository review folder. All 30 goldens were
regenerated through the explicit update variable. The full completion gate exits 0 with
Status pass, no warnings, project-check 18.970 s. The acceptance-source scan returns S2-1
through S2-7; 30 files exist, the JSON schema is referenced and tested, and both help cultures
are present. The roadmap records M4 offline done on 2026-09-15.

Spec updates: §12.2 and Q-21 reflect the user-authorized dark/neutral design; V-15 records
offline constructed-link evidence and V-16 records localized output evidence without claiming
live Server 2020 confirmation. Design decisions remain the session 2.1 JSON object root,
pinned generation metadata, explicitly updated goldens, Core export orchestration and a
terminating multi-case guard until Slice 3. The styling departure from the original light-screen
default was explicitly requested by the user. No restore, package changes, installs, external
network, deployment or Git lifecycle operations were used in 2.2. Outside-repository synthetic
previews were authorized. No sandbox rejection occurred and no test uses the Open switch.

Remaining work: S2-8, live V-15/V-16 and fluent French wording review at work. Slice 3 may start.
