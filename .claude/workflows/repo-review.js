export const meta = {
  name: 'repo-review',
  description: 'Review AdoToolkit in nine areas, the whole repository or the changes against main, verify the findings of each area adversarially, and in fix mode fix them and run verify. Starts at most 29 agents: 1 scope, 9 reviewers, 9 verifiers, 9 fixers, 1 gate.',
  whenToUse: 'A whole-repository or branch review split across agents. args: {scope: "repo" | "changes", mode: "report" | "fix", base: "origin/main"}; the defaults are repo, report and origin/main.',
  phases: [
    { title: 'Scope', detail: 'changes scope only: the files that differ from the base' },
    { title: 'Review', detail: 'one area-reviewer per area, in parallel' },
    { title: 'Verify', detail: 'one adversarial area-reviewer per area with findings' },
    { title: 'Fix', detail: 'fix mode: one fixer per area; fixers that build run one at a time' },
    { title: 'Gate', detail: 'fix mode: the deferred edits, then one full verify' },
  ],
}

// Inputs. The base defaults to origin/main: a local main can lag behind the main branch that
// pull requests target.
const input = args && typeof args === 'object' && !Array.isArray(args) ? args : {}
const scope = input.scope ?? 'repo'
const mode = input.mode ?? 'report'
const base = input.base ?? 'origin/main'
if (!['repo', 'changes'].includes(scope)) throw new Error(`scope must be 'repo' or 'changes', not '${scope}'`)
if (!['report', 'fix'].includes(mode)) throw new Error(`mode must be 'report' or 'fix', not '${mode}'`)
if (!/^[A-Za-z0-9][A-Za-z0-9._\/-]*$/.test(base)) throw new Error(`base must name a branch or a tag, not '${base}'`)

// The nine review areas. The same areas, with their rules, are listed in
// .claude/agents/area-reviewer.md. `owns` are the paths a fixer of the area may edit; they do
// not overlap. `reviews` adds paths the area judges without owning them. The lane decides
// what a fixer may run: dotnet fixers build and test one at a time, because two dotnet runs
// collide on bin/, obj/ and artifacts/; tooling fixers run only the tooling tests, also one
// at a time; docs fixers run nothing.
const AREAS = [
  {
    key: 'core-infra', title: 'Core infrastructure', lane: 'dotnet',
    rules: ['src/AGENTS.md', 'tests/AGENTS.md'],
    paths: 'src/AdoToolkit.Core/ folders Configuration, Connections, Diagnostics, Http, IO, Resources and Update and its root files, and the same folders of tests/AdoToolkit.Core.Tests/',
    owns: [/^src\/AdoToolkit\.Core\/(?:Configuration|Connections|Diagnostics|Http|IO|Resources|Update)\//, /^src\/AdoToolkit\.Core\/[^/]+$/,
      /^tests\/AdoToolkit\.Core\.Tests\/(?:Configuration|Connections|Diagnostics|Http|IO|Update)\//],
  },
  {
    key: 'core-domain', title: 'Core test domain', lane: 'dotnet',
    rules: ['src/AGENTS.md', 'tests/AGENTS.md'],
    paths: 'src/AdoToolkit.Core/ folders Builds, RichText, TestManagement, TestRuns and WorkItems, and the same folders of tests/AdoToolkit.Core.Tests/',
    owns: [/^src\/AdoToolkit\.Core\/(?:Builds|RichText|TestManagement|TestRuns|WorkItems)\//,
      /^tests\/AdoToolkit\.Core\.Tests\/(?:Builds|RichText|TestManagement|TestRuns|WorkItems)\//],
  },
  {
    key: 'core-reporting', title: 'Core reporting', lane: 'dotnet',
    rules: ['src/AGENTS.md', 'tests/AGENTS.md', 'docs/AGENTS.md'],
    paths: 'src/AdoToolkit.Core/Reporting/, tests/AdoToolkit.Core.Tests/Reporting/, tests/Fixtures/Reports/ and docs/schemas/',
    owns: [/^src\/AdoToolkit\.Core\/Reporting\//, /^tests\/AdoToolkit\.Core\.Tests\/Reporting\//, /^tests\/Fixtures\/Reports\//, /^docs\/schemas\//],
  },
  {
    key: 'powershell', title: 'PowerShell module and Pester tests', lane: 'dotnet',
    rules: ['src/AGENTS.md', 'tests/AGENTS.md', 'docs/commands/AGENTS.md'],
    paths: 'src/AdoToolkit.PowerShell/ and tests/AdoToolkit.PowerShell.Tests/',
    owns: [/^src\/AdoToolkit\.PowerShell\//, /^tests\/AdoToolkit\.PowerShell\.Tests\//],
  },
  {
    key: 'help', title: 'Cmdlet help', lane: 'docs',
    rules: ['docs/commands/AGENTS.md', 'src/AGENTS.md'],
    paths: 'docs/commands/, both cultures, judged against src/AdoToolkit.PowerShell/Commands/',
    owns: [/^docs\/commands\//],
    reviews: [/^src\/AdoToolkit\.PowerShell\/Commands\//],
  },
  {
    key: 'user-docs', title: 'README, guides and release notes', lane: 'docs',
    rules: ['docs/AGENTS.md'],
    paths: 'README.md, CHANGELOG.md, docs/guides/, docs/release-<version>.md, docs/plans/ and the two indexes of docs/archive/',
    owns: [/^README\.md$/, /^CHANGELOG\.md$/, /^docs\/guides\//, /^docs\/release-[^/]+\.md$/, /^docs\/plans\//, /^docs\/archive\/(?:plans\/)?README\.md$/],
  },
  {
    key: 'tooling', title: 'Dev tooling and live checks', lane: 'tooling',
    rules: ['tools/AGENTS.md', 'tests/Live/AGENTS.md', 'docs/AGENTS.md'],
    paths: 'tools/ apart from tools/package/ and tools/BuildModules.psd1, tests/Live/, .claude/, .agents/, docs/tooling.md, every AGENTS.md and CLAUDE.md, PSScriptAnalyzerSettings.psd1, .gitignore, .gitattributes and .editorconfig',
    owns: [/^tools\/(?!package\/|BuildModules\.psd1$)/, /^tests\/Live\//, /^\.claude\//, /^\.agents\//, /^docs\/tooling\.md$/,
      /^PSScriptAnalyzerSettings\.psd1$/, /^\.(?:gitignore|gitattributes|editorconfig)$/],
  },
  {
    key: 'packaging', title: 'Packaging and workflows', lane: 'tooling',
    rules: ['tools/package/AGENTS.md', 'tools/AGENTS.md'],
    paths: 'tools/package/, tools/BuildModules.psd1, .github/, Directory.Build.props, Directory.Packages.props, global.json, nuget.config and AdoToolkit.slnx',
    owns: [/^tools\/package\//, /^tools\/BuildModules\.psd1$/, /^\.github\//,
      /^(?:Directory\.Build\.props|Directory\.Packages\.props|global\.json|nuget\.config|AdoToolkit\.slnx)$/],
  },
  {
    key: 'core-tests', title: 'Core test suite quality', lane: 'dotnet',
    rules: ['tests/AGENTS.md'],
    paths: 'all of tests/AdoToolkit.Core.Tests/ and tests/Fixtures/; it owns the folders Architecture, Localization and Support, the root files of tests/AdoToolkit.Core.Tests/ and tests/Fixtures/ apart from Reports/',
    owns: [/^tests\/AdoToolkit\.Core\.Tests\/(?:Architecture|Localization|Support)\//, /^tests\/AdoToolkit\.Core\.Tests\/[^/]+$/, /^tests\/Fixtures\/(?!Reports\/)/],
    reviews: [/^tests\/AdoToolkit\.Core\.Tests\//, /^tests\/Fixtures\//],
  },
]
// Owned by no area: a fixer that needs one of these changed defers the edit to the gate step.
const SHARED = [/^src\/AdoToolkit\.Core\/Resources\/(?:Strings(?:\.fr)?\.resx|AdoMessage\.cs)$/]
// Every instruction file belongs to the agent setup.
const INSTRUCTIONS = /(?:^|\/)(?:AGENTS|CLAUDE)\.md$/

function ownerOf(path) {
  if (SHARED.some(rule => rule.test(path))) return null
  if (INSTRUCTIONS.test(path)) return 'tooling'
  const area = AREAS.find(candidate => candidate.owns.some(rule => rule.test(path)))
  return area ? area.key : null
}

function reviewersOf(path) {
  const keys = AREAS.filter(area => [...area.owns, ...(area.reviews ?? [])].some(rule => rule.test(path))).map(area => area.key)
  const owner = ownerOf(path)
  if (owner && !keys.includes(owner)) keys.push(owner)
  return keys
}

const code = text => '`' + text + '`'
const rulesOf = area => area.rules.map(code).join(', ')

const SCOPE_SCHEMA = {
  type: 'object',
  properties: {
    base: { type: 'string', description: 'the base the commands compared against' },
    files: {
      type: 'array',
      items: {
        type: 'object',
        properties: { path: { type: 'string' }, status: { type: 'string', enum: ['added', 'modified', 'deleted'] } },
        required: ['path', 'status'],
      },
    },
  },
  required: ['base', 'files'],
}

const FINDINGS_SCHEMA = {
  type: 'object',
  properties: {
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          file: { type: 'string', description: 'repository path with forward slashes' },
          line: { type: 'integer' },
          rule: { type: 'string', description: 'the nested AGENTS.md rule, decision ID, public contract or correctness property it breaks' },
          scenario: { type: 'string', description: 'concrete input or state, and the wrong result it produces' },
          summary: { type: 'string' },
          fix: { type: 'string', description: 'the change that would fix it' },
        },
        required: ['file', 'line', 'rule', 'scenario', 'summary'],
      },
    },
  },
  required: ['findings'],
}

const VERDICT_SCHEMA = {
  type: 'object',
  properties: {
    verdicts: {
      type: 'array',
      items: {
        type: 'object',
        properties: { id: { type: 'string' }, confirmed: { type: 'boolean' }, reason: { type: 'string', description: 'the evidence, with file:line' } },
        required: ['id', 'confirmed', 'reason'],
      },
    },
  },
  required: ['verdicts'],
}

const FIX_SCHEMA = {
  type: 'object',
  properties: {
    fixed: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          files: { type: 'array', items: { type: 'string' } },
          change: { type: 'string' },
          regression: { type: 'string', description: 'the regression test and its failure before the fix, or why none applies' },
        },
        required: ['id', 'files', 'change'],
      },
    },
    notFixed: {
      type: 'array',
      items: { type: 'object', properties: { id: { type: 'string' }, reason: { type: 'string' } }, required: ['id', 'reason'] },
    },
    deferred: {
      type: 'array',
      items: {
        type: 'object',
        properties: { id: { type: 'string' }, path: { type: 'string' }, edit: { type: 'string', description: 'the exact change to make' } },
        required: ['id', 'path', 'edit'],
      },
    },
  },
  required: ['fixed', 'notFixed', 'deferred'],
}

const GATE_SCHEMA = {
  type: 'object',
  properties: {
    applied: {
      type: 'array',
      items: { type: 'object', properties: { id: { type: 'string' }, path: { type: 'string' } }, required: ['id', 'path'] },
    },
    notApplied: {
      type: 'array',
      items: { type: 'object', properties: { id: { type: 'string' }, path: { type: 'string' }, reason: { type: 'string' } }, required: ['id', 'path', 'reason'] },
    },
    gate: {
      type: 'object',
      properties: {
        exitCode: { type: 'integer' },
        status: { type: 'string' },
        failures: { type: 'array', items: { type: 'string' }, description: 'each failing or incomplete stage with its named failures' },
      },
      required: ['exitCode', 'status', 'failures'],
    },
  },
  required: ['applied', 'notApplied', 'gate'],
}

// An agent call that reports its failure instead of dropping the item.
async function attempt(prompt, options) {
  try {
    const result = await agent(prompt, options)
    return result ?? { error: 'the agent returned nothing' }
  } catch (error) {
    return { error: String(error?.message ?? error) }
  }
}

// Scope: in the changes scope, the files that differ from the base, committed or not.
let changedFiles = null
let usedBase = null
const unassignedFiles = []
let targets
if (scope === 'changes') {
  phase('Scope')
  const listed = await attempt(
    `List the files of this repository that differ from ${code(base)}, using Git read commands only. Run each from the repository root:
1. ${code(`git diff --name-status --no-renames ${base}...HEAD`)}: committed on this branch since it left the base. If ${code(base)} does not exist, run ${code('git diff --name-status --no-renames main...HEAD')} instead and report ${code('main')} as the base.
2. ${code('git diff --name-status --no-renames HEAD')}: staged and unstaged changes.
3. ${code('git status --porcelain=v1 --untracked-files=all')}: the lines that start with ${code('??')} are new files.
Return every path once, relative to the repository root with forward slashes, with its latest status: deleted when the file no longer exists, added when it did not exist at the base, otherwise modified. Run nothing else and read no file.`,
    { label: 'scope:changes', phase: 'Scope', agentType: 'Explore', effort: 'low', schema: SCOPE_SCHEMA })
  if (listed.error) throw new Error(`The scope step failed, so nothing was reviewed: ${listed.error}`)
  usedBase = listed.base
  const seen = new Set()
  changedFiles = listed.files.filter(file => !seen.has(file.path) && seen.add(file.path))
  const byArea = new Map(AREAS.map(area => [area.key, []]))
  for (const file of changedFiles) {
    const keys = reviewersOf(file.path)
    if (keys.length === 0) unassignedFiles.push(file.path)
    for (const key of keys) byArea.get(key).push(file)
  }
  targets = AREAS.filter(area => byArea.get(area.key).length > 0).map(area => ({ area, files: byArea.get(area.key) }))
  log(`${changedFiles.length} changed file(s) against ${usedBase}; ${targets.length} of ${AREAS.length} areas to review`)
  if (unassignedFiles.length > 0) log(`Reviewed by no area: ${unassignedFiles.join(', ')}`)
} else {
  targets = AREAS.map(area => ({ area, files: null }))
}

function reviewPrompt(target) {
  const head = `Review the area "${target.area.title}" of AdoToolkit and change nothing. Its paths: ${target.area.paths}. Read ${rulesOf(target.area)} before the code.`
  const tail = 'Return every finding in the schema, and an empty list when the area has none.'
  if (!target.files) return `${head}\n\nScope: every file of the area.\n\n${tail}`
  return `${head}

Scope: only the files below, which differ from ${code(usedBase)}. Compare each with ${code(`git diff ${usedBase}...HEAD -- <path>`)} for committed changes and ${code('git diff HEAD -- <path>')} for uncommitted ones, and read an added file whole. Judge the changed lines in the context of the code around them; report a defect in an unchanged line only when a changed line causes or exposes it.

${target.files.map(file => `- ${file.path} (${file.status})`).join('\n')}

${tail}`
}

function verifyPrompt(target, findings) {
  return `Verify adversarially the findings below about the area "${target.area.title}" of AdoToolkit, and change nothing. Read ${rulesOf(target.area)} first. For each finding, re-read the code at its line and around it, the rule it cites and the tests that cover it, and try to refute it: the rule does not say that, the code does not do that, the scenario cannot happen, or a recorded decision requires the behavior. Confirm only what survives; when in doubt, refute. Return one verdict for every id.

Findings (JSON):
${JSON.stringify(findings, null, 2)}`
}

// Review and verify: each area is verified as soon as its review returns.
phase('Review')
const outcomes = await pipeline(
  targets,
  target => attempt(reviewPrompt(target), { label: `review:${target.area.key}`, phase: 'Review', agentType: 'area-reviewer', schema: FINDINGS_SCHEMA }),
  async (review, target) => {
    if (review.error) return { area: target.area, failed: `review: ${review.error}`, findings: [], verdicts: [] }
    const findings = review.findings.map((finding, index) => ({ id: `${target.area.key}-${index + 1}`, area: target.area.key, ...finding }))
    if (findings.length === 0) return { area: target.area, findings, verdicts: [] }
    const verified = await attempt(verifyPrompt(target, findings),
      { label: `verify:${target.area.key}`, phase: 'Verify', agentType: 'area-reviewer', schema: VERDICT_SCHEMA })
    if (verified.error) return { area: target.area, failed: `verification: ${verified.error}`, findings, verdicts: [] }
    return { area: target.area, findings, verdicts: verified.verdicts }
  },
)

const incomplete = []
const confirmed = []
const refuted = []
const unverified = []
const seenFindings = new Set()
outcomes.forEach((outcome, index) => {
  if (!outcome) { incomplete.push({ area: targets[index].area.key, reason: 'the review stage stopped' }); return }
  if (outcome.failed) incomplete.push({ area: outcome.area.key, reason: outcome.failed })
  const verdicts = new Map(outcome.verdicts.map(verdict => [verdict.id, verdict]))
  for (const finding of outcome.findings) {
    const verdict = verdicts.get(finding.id)
    if (!verdict) { unverified.push(finding); continue }
    if (!verdict.confirmed) { refuted.push({ id: finding.id, file: finding.file, line: finding.line, summary: finding.summary, reason: verdict.reason }); continue }
    // Areas overlap on a few paths; the same defect reported twice is kept once.
    const key = `${finding.file}:${finding.line}:${finding.rule}`.toLowerCase()
    if (seenFindings.has(key)) continue
    seenFindings.add(key)
    confirmed.push({ ...finding, evidence: verdict.reason })
  }
})
log(`${confirmed.length} confirmed, ${refuted.length} refuted, ${unverified.length} unverified finding(s)`)

const report = {
  scope,
  mode,
  base: usedBase,
  areasReviewed: targets.map(target => target.area.key),
  areasSkipped: AREAS.filter(area => !targets.some(target => target.area === area)).map(area => area.key),
  unassignedFiles,
  incomplete,
  confirmed,
  refuted,
  unverified,
  fixesApplied: [],
  notFixed: [],
  gate: { ran: false, reason: 'report-only mode' },
}

if (mode === 'report') {
  report.notFixed = confirmed.map(finding => ({ id: finding.id, reason: 'report-only mode' }))
  return report
}

// Fix: every confirmed finding is fixed in this tree, never in a worktree, because bringing a
// worktree's changes back needs Git writes that belong to the developer. The barrier is
// deliberate: no file changes while any area is still being reviewed or verified.
phase('Fix')
const LANE_RULES = {
  dotnet: 'You are the only fixer that builds now: build and run the regression tests as the fix-bug skill says.',
  tooling: `Run no dotnet command and not ${code('verify -Stage project-check')}: another fixer may be building in this tree. ${code('pwsh -NoProfile -File .\\tools\\dev.ps1 verify -Stage powershell-test')} is allowed.`,
  docs: 'Run no build, no test and no verify stage: another fixer may be building in this tree, and the final verify checks your edits.',
}

function fixPrompt(work) {
  return `Fix the confirmed findings below in the area "${work.area.title}" of AdoToolkit, in this working tree. Read ${rulesOf(work.area)} first.

- Never use a worktree and never run a Git command that writes.
- You own these paths: ${work.area.paths}. Edit only files there. When a fix needs a change anywhere else (a help topic of another area, ${code('src/AdoToolkit.Core/Resources/Strings.resx')}, ${code('Strings.fr.resx')} or ${code('AdoMessage.cs')}, a test file of another area), do not make it: return it in deferred with the exact edit. A final step applies the deferred edits after every fixer has finished.
- For a defect in code, follow the fix-bug skill with one change: skip its final full verify. The workflow runs one full verify after every fix.
- ${LANE_RULES[work.area.lane]}
- A finding that you judge not to be a defect after all, or cannot fix safely, goes in notFixed with the reason.

Findings (JSON):
${JSON.stringify(work.findings, null, 2)}`
}

const work = AREAS.map(area => ({ area, findings: confirmed.filter(finding => finding.area === area.key) })).filter(item => item.findings.length > 0)
const runFixer = item => attempt(fixPrompt(item), { label: `fix:${item.area.key}`, phase: 'Fix', schema: FIX_SCHEMA }).then(result => ({ item, result }))
async function oneAtATime(items) {
  const results = []
  for (const item of items) results.push(await runFixer(item))
  return results
}
const lanes = await parallel([
  () => oneAtATime(work.filter(item => item.area.lane === 'dotnet')),
  () => oneAtATime(work.filter(item => item.area.lane === 'tooling')),
  () => parallel(work.filter(item => item.area.lane === 'docs').map(item => () => runFixer(item))),
])

const deferred = []
for (const { item, result } of lanes.filter(Boolean).flat().filter(Boolean)) {
  if (result.error) {
    for (const finding of item.findings) report.notFixed.push({ id: finding.id, reason: `the fixer of ${item.area.key} failed: ${result.error}` })
    continue
  }
  for (const fixed of result.fixed) report.fixesApplied.push({ ...fixed, by: item.area.key })
  report.notFixed.push(...result.notFixed)
  deferred.push(...result.deferred)
}

// Gate: the deferred edits, then one full verify. Nothing else builds by now.
phase('Gate')
const gate = await attempt(
  `Finish a repo-review fix run of AdoToolkit in this working tree. Never use a worktree and never run a Git command that writes.
1. Apply the deferred edits below: fixers could not make them because the files are not theirs. Follow the AGENTS.md rules of each path. Skip an edit that is already made or no longer applies, and say why.
2. Run ${code('pwsh -NoProfile -File .\\tools\\dev.ps1 verify')} once from the repository root and wait for it to finish; it prints one JSON document.
3. Do not fix what the gate reports. Return its exit code, its status, and each failing or incomplete stage with its named failures.

Deferred edits (JSON):
${JSON.stringify(deferred, null, 2)}`,
  { label: 'gate', phase: 'Gate', schema: GATE_SCHEMA })
if (gate.error) {
  report.gate = { ran: false, reason: `the gate step failed: ${gate.error}` }
  for (const edit of deferred) report.notFixed.push({ id: edit.id, reason: `deferred edit to ${edit.path} not applied: the gate step failed` })
} else {
  for (const applied of gate.applied) report.fixesApplied.push({ id: applied.id, files: [applied.path], change: 'deferred edit', by: 'gate' })
  for (const skipped of gate.notApplied) report.notFixed.push({ id: skipped.id, reason: `deferred edit to ${skipped.path}: ${skipped.reason}` })
  report.gate = { ran: true, ...gate.gate }
}
return report
