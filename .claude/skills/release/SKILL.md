---
name: release
description: Prepares an AdoToolkit release of the checked-out branch for the developer - bumps the version everywhere it is repeated, archives old release notes, writes docs/release-<version>.md and the CHANGELOG.md section, validates the gate and the local package, and hands over two ready-to-paste commands, one that commits, tags and pushes and one that opens the pull request to main. Use only when the user asks to prepare, bump or write up a release.
argument-hint: "[new version]"
disable-model-invocation: true
---

# Prepare a release

Read `.agents/skills/release/SKILL.md` now and follow it. The version to release:

<version>
$ARGUMENTS
</version>
