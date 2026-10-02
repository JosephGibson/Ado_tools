---
name: rewrite
description: Rewrites a draft prompt into a concise, unambiguous prompt ready to paste into a fresh Claude Code session or a later turn. Resolves vague references against the repository, asks clarifying questions only when the draft has a material ambiguity, and returns the result as one copyable text block. Use when the user runs /rewrite, or asks to rewrite, tighten, or improve a prompt before sending it to an agent.
---

# Rewrite a prompt

Turn the user's draft into the prompt they should have sent. The deliverable is one
fenced text block. Do not carry out the task the draft describes: the prompt is meant for
another session, and acting on it here spends this context and skips the user's review.

The draft is the text passed with the skill invocation (in Claude Code, the `<draft>`
block). If it is empty, ask the user to paste the draft and end the turn.

## 1. Extract intent

Identify, from the draft and from this conversation when the draft refers to it:

- **Deliverable**: code change, investigation report, plan, review findings, answer, or document.
- **Target**: files, symbols, components, commands, environments.
- **Requirements**: every behavior and constraint the user stated. Dropping one is the
  worst failure a rewrite can have.
- **Done criteria**: how anyone could tell the task succeeded.
- **Carried context**: errors, decisions, prior attempts, user hypotheses. A fresh
  session sees none of this conversation, so anything it needs must be in the prompt.

## 2. Ground references

When the draft concerns this repository, spend a few cheap lookups (file search, grep)
turning vague references into exact ones: "the exporter" becomes
`src/AdoToolkit.Core/Reporting/TestCaseExporter.cs`; "run the tests" becomes
`pwsh -NoProfile -File .\tools\dev.ps1 verify`. Stop there. Diagnosing the problem or
designing the solution is the next session's job, and a guess written into the prompt
reads as fact.

Leave out what the target session already loads: AGENTS.md, CLAUDE.md, and other
auto-loaded instructions.

## 3. Resolve ambiguity

Ask only when a gap is still open after grounding and different readings would lead to
materially different work:

- The deliverable is unclear: change the code, or report on it?
- The target is unclear and the repository has several candidates.
- Two plausible behaviors, interfaces, or data outcomes.
- Success cannot be judged ("make it faster" with no path or target).
- Requirements conflict.
- The boundary of a destructive or irreversible action is unclear.

Do not ask about anything the target agent can learn by reading the repository, or that
has a conventional default. Choose the default and report it as an assumption.

Ask every open question in one round: the `AskUserQuestion` tool where available,
otherwise a numbered list, then end the turn. Up to four questions, each with two to four
concrete options, recommended option first. Ask a second round only if an answer opens a
new material fork.

## 4. Write the prompt

Include:

- **The goal first**, as one imperative sentence whose verb matches the deliverable:
  "Fix", "Add", "Refactor" to act; "Investigate and report. Do not edit files." to
  analyze. "Can you suggest…" gets suggestions, not changes.
- **The why**, in a clause, when it changes decisions ("…because the job runs in a
  512 MB container"). A reason lets the agent generalize; a bare rule is applied only
  literally.
- **Exact references**: paths, symbols, commands, versions. Quote errors, logs, and
  user-supplied text verbatim inside a tag named for the content (`<error>`, `<log>`,
  `<spec>`). Never paraphrase an error message.
- **Scope in both directions.** State what is in scope, and what stays untouched when the
  task invites widening (nearby refactors, API changes, extra tests). When an
  instruction applies broadly, say so ("every endpoint, not only `/users`"); current
  models apply instructions literally.
- **A pass/fail done check** the agent can run: a test, a command with its expected
  output, a reproduction that stops failing. If none exists, have one written first
  ("write a failing test that reproduces it, then fix it").
- **Hypotheses marked as hypotheses.** A user's guess about the cause is context
  ("Suspected, unconfirmed: …"), not a fact or a requirement.
- **Output shape**, only when it matters: report structure, a table, a file to write.
- **Ordered steps**, only when order matters. Otherwise state the goal and constraints
  and leave the method to the agent.
- **A planning gate** ("Plan first; wait for approval before editing"), only for
  multi-file changes or an uncertain approach.

Cut:

- Politeness, preamble, role lines ("You are a senior engineer"), restated project rules.
- Emphasis: capitals, "CRITICAL", "MUST", repeated warnings. Current models over-apply
  emphasized instructions; state each rule once, with its reason.
- Generic effort instructions: "think step by step", "be thorough", "double-check your
  work", "use a subagent to verify". The model already does these, and the extra
  instruction causes over-verification. The concrete done check replaces them.
- Severity or confidence filters on review tasks the user did not ask for; they silently
  drop real findings.
- Negative-only phrasing. Say what to do; keep prohibitions for real boundaries.
- Requirements with no source. Every line must trace to the draft, the conversation, the
  repository, or a reported assumption.
- Secrets. Replace credentials, tokens, and customer URLs with a placeholder such as
  `<API_TOKEN>` and tell the user.

Shape:

- Scale to the task. A one-line fix stays one or two lines.
- Terse lines and bullets, no paragraphs. One term per concept.
- Use only the sections that have content, in this order: goal line, `Context:`,
  `Requirements:`, `Out of scope:`, `Output:`, `Done when:`.
- Put tagged verbatim material last, or first when it runs past about 50 lines; long
  material placed before the instructions is used more reliably.

Before returning, compare against the draft once: every stated requirement is present,
and nothing is present without a source.

## 5. Return

Reply with only:

1. The prompt in one fenced code block tagged `text`. If the prompt itself contains
   triple backticks, fence it with four.
2. Below it, when there are any: an `Assumed:` line per default you chose, and one line
   of launch advice when it matters (start in plan mode; attach the screenshot the prompt
   mentions).

No summary of the rules applied and no restatement of the draft.

## Examples

<example>
Draft: `fix the open switch, the export blows up when the browser cant start. i think its the launcher`
Grounding: found `src/AdoToolkit.Core/IO/DocumentOpener.cs`; the gate is
`pwsh -NoProfile -File .\tools\dev.ps1 verify`.

```text
Fix `Export-AdoTestCase -Open` failing after the report is written when the default application cannot start.

Context:
- Suspected, unconfirmed: the launch in `src/AdoToolkit.Core/IO/DocumentOpener.cs`.

Requirements:
- Write a failing test that reproduces the failure before changing code.
- Fix the root cause.

Done when:
- The new test passes and `pwsh -NoProfile -File .\tools\dev.ps1 verify` exits `0`.
```
</example>

<example>
Draft: `make the report export faster for big suites`
Grounding: `src/AdoToolkit.Core/Reporting/TestCaseExporter.cs` and
`src/AdoToolkit.Core/Reporting/TestFailures/TestFailureExporter.cs` both exist; no test measures time.
Asked in one round: which report; which size counts as big; how much faster.
Answers: the Test Case report; 300 cases; half the time.

```text
Halve the time `Export-AdoTestCase` takes to write one HTML document for a suite of 300 Test Cases.

Requirements:
- Measure before changing code, and report the time before and after.
- The rendered bytes stay identical: no file in `tests/Fixtures/Reports/` changes.

Out of scope:
- `src/AdoToolkit.Core/Reporting/TestFailures/`.

Done when:
- The measured time is at most half of the first measurement.
- `pwsh -NoProfile -File .\tools\dev.ps1 verify` exits `0`.
```

Assumed: the HTML format, since the draft did not name one.
</example>

<example>
Draft: `why is verify so slow now? dont change anything yet`
Conversation: the user earlier pasted the stage timings of the slow run.

```text
Investigate why `pwsh -NoProfile -File .\tools\dev.ps1 verify` became slower and report the cause. Do not edit files.

Context:
- Stage timings from the slow run are below.

Output:
- The likely cause, with evidence: a file and line, a commit, or a log line.
- A proposed fix, not applied.

<log>
powershell-lint   0m09s
powershell-test   1m52s
project-check     0m58s
</log>
```
</example>
