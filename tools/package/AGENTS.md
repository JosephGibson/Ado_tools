# Packaging and release

Covers `tools/package/`, `tools/BuildModules.psd1` and `.github/`. `docs/tooling.md`
describes each script, both workflows and the setup action under Packaging and releases.
Read it before changing one, and correct it in the same change.

## Rules

- The version is declared once: `VersionPrefix` in `Directory.Build.props`. The manifest
  holds `@VERSION@` until staging. The `release` skill lists the documents that repeat it.
- A package is exactly seven files, listed in `Assert-AdoPackage` in
  `tools/package/Package.Common.ps1`; the gate and every packaging script call it. `$layout`
  in `tools/package/Install-AdoToolkit.ps1` repeats the list, and a tooling test keeps the
  two equal.
- The cmdlet count, 22, is asserted in `Assert-AdoPackage`,
  `tools/package/Publish-AdoToolkitPackage.ps1` and
  `tools/package/portable/Start-AdoToolkit.ps1`.
- `tools/package/Install-AdoToolkit.ps1` is self-contained: it dot-sources nothing.
- Resolve every path with `Resolve-AdoPackagePath`: FileSystem provider, inside the permitted
  root, no link traversed.
- Stage beside the target, validate, then move. Never extract or copy over a live folder,
  and never delete the only previous copy: when a rollback fails, its backup stays.
- These scripts never download. Only `tools/package/Publish-AdoToolkitPackage.ps1`
  restores, with `--locked-mode`, and not with `-NoBuild` or `-NoRestore`.
- Every action is pinned by commit, with its version in a comment. A new action in a new
  directory also needs an entry in `.github/dependabot.yml`.
- The toolchain of a workflow comes from the setup action, which pins PowerShell and
  actionlint by version and SHA-256, each once. A workflow declares no pin of its own.
- In `.github/workflows/release.yml` the publish step is the only `run` step that receives
  the write token; checkout fetches with it and does not keep it.
  `.github/workflows/verify.yml` has a read-only token.
- A release step reads the tag from `RELEASE_TAG`, never from the ref of the run: in a
  manual run the ref is a branch.
- Tests: `tools/tests/Package.Tests.ps1`, `tools/tests/Portable.Tests.ps1`,
  `tools/tests/ReleaseWorkflow.Tests.ps1` and `tools/tests/VerifyWorkflow.Tests.ps1`. They
  run the steps of the workflows and of the setup action with mocked `gh`, `dotnet`,
  `pwsh` and `Invoke-WebRequest`.

## Boundaries

- Staging under `artifacts/` is routine work.
- Installing into Documents, signing, tagging and publishing need explicit direction.
  Pushing a `v<version>` tag publishes at once.
