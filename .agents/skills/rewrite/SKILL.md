---
name: rewrite
description: Rewrite a draft prompt for a fresh agent session, grounding repository references and preserving the user's requirements. Use for /rewrite or requests to improve a prompt; do not execute its task.
---

# Rewrite a prompt

Return the prompt the user can paste into another session. The draft is the invocation
text (`<draft>` in Claude). If absent, ask for it and stop.

## Ground the draft

- Extract its deliverable, target, constraints, done criteria and referenced conversation
  context. Include everything the new session needs; preserve hypotheses as unconfirmed.
- For repository work, use a few lookups to resolve vague names to paths or commands.
  Do not diagnose or design the fix.
- Omit rules already loaded from `AGENTS.md`, `CLAUDE.md` and scoped instructions.

Ask only about material forks still unresolved: deliverable, target, behavior, conflicting
requirements, measurable success or destructive scope. Bundle questions in one round
using the available question tool, or a numbered list. Use concrete choices and recommend
a default; ask again only if the answer creates a new fork. Infer conventional defaults
and report them as assumptions.

## Write the prompt

Use only sections with content, in this order: imperative goal, `Context:`,
`Requirements:`, `Out of scope:`, `Output:`, `Done when:`.

- Scale length to the task. Name exact paths, commands, versions and errors.
- Preserve the reason when it affects decisions, and order steps only when order matters.
- Include an approval gate only when the draft or an applicable boundary requires it.
- Put verbatim material in a tagged block, last; substantial material (about 50 lines or
  more) may go first.
- Remove preamble, role lines, emphasis, generic effort advice and duplicated rules.
  Add no unsupported requirements or severity/confidence filters.
- Replace secrets and customer URLs with placeholders and disclose the redaction.
- Check once that every draft requirement survives and every added line has a source.

## Return

Give one fenced `text` block, using four backticks if its contents have triple backticks.
After it, include only necessary `Assumed:` lines and launch advice.

Example, after resolving "the launcher" to the file below:

```text
Fix Export-AdoTestCase -Open failing after the report is written when the default application cannot start.

Context:
- Suspected, unconfirmed: src/AdoToolkit.Core/IO/DocumentOpener.cs.

Done when:
- A regression test fails before the fix and passes afterwards.
- pwsh -NoProfile -File .\tools\dev.ps1 verify exits 0.
```
