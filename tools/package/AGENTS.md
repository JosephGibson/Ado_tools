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
| `.github/workflows/release.yml` | Verifies, packages and publishes when a `v<version>` tag is pushed |

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
- The workflow pins each action by commit, with its version in a comment, pins the
  PowerShell runtime by version and SHA-256, and gives the write token to the publish step
  only.
- Tests: `tools/tests/Package.Tests.ps1`, `tools/tests/Portable.Tests.ps1` and
  `tools/tests/ReleaseWorkflow.Tests.ps1`. They run the workflow steps with mocked `gh`,
  `dotnet` and `Invoke-WebRequest`.

## Boundaries

- Staging under `artifacts/` is routine work.
- Installing into Documents, signing, tagging and publishing need explicit direction.
