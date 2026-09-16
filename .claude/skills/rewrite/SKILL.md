---
name: rewrite
description: Rewrites a draft prompt into a concise, unambiguous prompt ready to paste into a fresh Claude Code session or a later turn. Resolves vague references against the repository, asks clarifying questions only when the draft has a material ambiguity, and returns the result as one copyable text block. Use when the user runs /rewrite, or asks to rewrite, tighten, or improve a prompt before sending it to an agent.
argument-hint: "[draft prompt]"
disable-model-invocation: true
disallowed-tools: Edit Write NotebookEdit
---

# Rewrite a prompt

The skill body lives at `.agents/skills/rewrite/SKILL.md` — the location Codex
reads — so both agents run the same instructions from one file.

Read that file now and follow it. The draft to rewrite:

<draft>
$ARGUMENTS
</draft>
