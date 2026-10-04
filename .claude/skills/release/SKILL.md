---
name: release
description: Prepare an AdoToolkit version bump, release notes, changelog and validated local packages, then give the developer two commands that publish the release and open its pull request. Use only for a requested release.
argument-hint: "[new version]"
disable-model-invocation: true
---

# Prepare a release

Read `.agents/skills/release/SKILL.md` now and follow it. The version to release:

<version>
$ARGUMENTS
</version>
