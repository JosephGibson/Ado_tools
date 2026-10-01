# Packaging and release

| File | Does |
| --- | --- |
| `tools/package/Publish-AdoToolkitPackage.ps1` | Builds, compiles the help of both cultures, stages `artifacts/AdoToolkit/<version>/` |
| `tools/package/New-AdoToolkitRelease.ps1` | Staged package to `artifacts/release/`: module zip, portable zip, `.sha256` files, installer |
| `tools/package/Install-AdoToolkit.ps1` | End-user installer, shipped beside the zip |
| `tools/package/Install-AdoToolkitPackage.ps1`, `tools/package/Set-AdoToolkitPackageSignature.ps1` | Install or sign a staged package with a code-signing certificate |
| `tools/package/Test-AdoToolkitPortable.ps1` | Offline start of the portable zip |
| `tools/package/Package.Common.ps1`, `tools/package/Portable.Common.ps1` | Shared path, layout, archive and signature checks |
| `tools/package/portable/` | Launcher and `README.txt` shipped inside the portable zip |
| `tools/BuildModules.psd1` | Module versions that a release build requires exactly |
| `.github/workflows/release.yml` | Verifies, packages and publishes when a `v<version>` tag is pushed, or for an existing tag named in a manual run. The release text is the `CHANGELOG.md` section of the version, then the installation instructions |
| `.github/workflows/verify.yml` | Runs `verify` on a pull request to `main` |
| `.github/actions/setup/action.yml` | Installs the toolchain for both workflows; holds the PowerShell and actionlint pins |
| `.github/dependabot.yml` | Proposes newer commits for the pinned actions, each week |

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
- Stage beside the target, validate, then move. Never extract or copy over a live folder.
- These scripts never download. Only `tools/package/Publish-AdoToolkitPackage.ps1`
  restores, with `--locked-mode`, and not with `-NoBuild` or `-NoRestore`.
- Every action is pinned by commit, with its version in a comment. A new action in a new
  directory also needs an entry in `.github/dependabot.yml`.
- The toolchain of a workflow comes from the setup action, which pins PowerShell and
  actionlint by version and SHA-256, each once. A workflow declares no pin of its own.
- `.github/workflows/release.yml` gives the write token to the publish step only;
  `.github/workflows/verify.yml` has a read-only token.
- A release step reads the tag from `RELEASE_TAG`, never from the ref of the run: in a
  manual run the ref is a branch.
- The release workflow publishes only a version that has a `## <version>` section in
  `CHANGELOG.md`. The `release` skill writes the section and defines its form, and
  `verify` requires it for `VersionPrefix`.
- Pushing a `v<version>` tag publishes at once. The `release` skill ends with the commands
  that commit, tag, push and open the pull request; the developer runs them. Recovery
  from a run that did not publish is in `docs/tooling.md`, under Release workflow.
- Tests: `tools/tests/Package.Tests.ps1`, `tools/tests/Portable.Tests.ps1`,
  `tools/tests/ReleaseWorkflow.Tests.ps1` and `tools/tests/VerifyWorkflow.Tests.ps1`. They
  run the steps of the workflows and of the setup action with mocked `gh`, `dotnet`,
  `pwsh` and `Invoke-WebRequest`.

## Boundaries

- Staging under `artifacts/` is routine work.
- Installing into Documents, signing, tagging and publishing need explicit direction.
