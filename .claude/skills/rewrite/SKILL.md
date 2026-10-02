---
name: rewrite
description: Rewrite a draft prompt for a fresh agent session, grounding repository references and preserving the user's requirements. Use for /rewrite or requests to improve a prompt; do not execute its task.
argument-hint: "[draft prompt]"
disable-model-invocation: true
disallowed-tools: Edit Write NotebookEdit
---

# Rewrite a prompt

Read `.agents/skills/rewrite/SKILL.md` now and follow it. The draft to rewrite:

<draft>
$ARGUMENTS
</draft>
