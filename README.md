<!-- project:start -->
# AdoToolkit

PowerShell toolkit for Azure DevOps Server 2020: compiled C\# cmdlets for work items, Test Case reports, bulk test export, and pipeline failure triage.
<!-- project:end -->

**Version 0.1.0** · Windows · PowerShell 7.6 · Azure DevOps Server 2020 · English and French

AdoToolkit is a compiled PowerShell module for an on-premises Azure DevOps Server
2020 collection. It signs in with your Windows identity and only reads from Azure
DevOps. Its cmdlets return typed objects that you can use in pipelines, and it writes
standalone HTML, Markdown or JSON reports when you need a document. All messages,
report labels and help are available in English and French.

Version 0.1.0 is feature-complete, and every feature is covered by offline tests.

> [!NOTE]
> Live validation against Azure DevOps Server 2020 is still pending.

## Features

| Area | Capabilities | Cmdlets |
| --- | --- | --- |
| Connections | Local profiles, Windows integrated authentication, access checks, project listing | `Set-AdoProfile`, `Get-AdoProfile`, `Remove-AdoProfile`, `Connect-Ado`, `Disconnect-Ado`, `Get-AdoConnection`, `Test-AdoConnection`, `Get-AdoProject` |
| Work items | Batched retrieval by ID with extra fields or relations; flat WIQL queries | `Get-AdoWorkItem`, `Invoke-AdoWiql` |
| Test Cases | Plans and suite trees; Test Cases with recursively expanded Shared Steps, parameters and diagnostics | `Get-AdoTestPlan`, `Get-AdoTestSuite`, `Get-AdoTestCase` |
| Reports and bulk export | One HTML, Markdown or JSON document for a Test Case, a suite tree or a WIQL result | `Export-AdoTestCase` |
| Pipeline triage | Build lookup by definition and branch, deepest timeline failures, byte-exact log downloads | `Get-AdoBuildDefinition`, `Get-AdoBuild`, `Get-AdoBuildTimeline`, `Get-AdoBuildFailure`, `Save-AdoBuildLog` |
| Failed-test reports | Failed and flaky tests with every attempt, run history, and an interactive HTML report with downloaded attachments | `Get-AdoTestRun`, `Get-AdoBuildTestFailure`, `Export-AdoBuildTestFailure` |

## Requirements

| To | You need |
| --- | --- |
| Use the module | Windows, PowerShell 7.6, and access to an Azure DevOps Server 2020 collection with your Windows account |
| Build and install it | The .NET 10 SDK selected by [global.json](global.json), PlatyPS 1.x (`Microsoft.PowerShell.PlatyPS`), and a code-signing certificate in `Cert:\CurrentUser\My` |
| Develop and verify | The .NET SDK and PlatyPS above, plus PowerShell 7.6.5 or later, Pester 5.x and PSScriptAnalyzer; ripgrep is recommended |

Azure DevOps Services and later Server versions are not supported.

## Installation

Build AdoToolkit from source on the machine that will use it, then install it as a
signed package with the scripts in [tools/package/](tools/package/):

| Script | Purpose |
| --- | --- |
| `Publish-AdoToolkitPackage.ps1` | Builds a Release configuration and stages `artifacts\AdoToolkit\<version>\` with compiled English and French help |
| `Set-AdoToolkitPackageSignature.ps1` | Authenticode-signs the package's scripts, manifest and assemblies |
| `Install-AdoToolkitPackage.ps1` | Checks the package and its signatures, then installs it to `Documents\PowerShell\Modules\AdoToolkit\<version>` |

For the step-by-step commands, see
[Getting started](docs/guides/getting-started.md#install-the-module).

## Quick start

```powershell
Import-Module AdoToolkit
Connect-Ado -CollectionUrl 'https://ado.example.test/DefaultCollection' -Project 'Web'
Test-AdoConnection

# Every Test Case in a suite tree, as one HTML report in Downloads
Get-AdoTestCase -PlanId 10 -SuiteId 11 -Recurse | Export-AdoTestCase -Open

# The latest failed build on main: failed tasks, then failed and flaky tests
$failed = Get-AdoBuild -Definition 'Web CI' -Branch main -Latest -Result Failed
$failed | Get-AdoBuildFailure
$failed | Get-AdoBuildTestFailure | Export-AdoBuildTestFailure -Open
```

To avoid typing the collection URL every time, save a profile as described in
[Getting started](docs/guides/getting-started.md#save-a-profile).

## Documentation

| Topic | Document |
| --- | --- |
| Installation, profiles and connecting | [Getting started](docs/guides/getting-started.md) |
| Work items and WIQL queries | [Work items and WIQL](docs/guides/work-items.md) |
| Test Case reports and bulk export | [Test Case reports](docs/guides/test-case-reports.md) |
| Build failures, logs and failed-test reports | [Pipeline failure triage](docs/guides/pipeline-triage.md) |
| Configuration file, defaults and limits | [Configuration](docs/guides/configuration.md) |
| Full cmdlet help (also available with `Get-Help <cmdlet> -Full`) | [English](docs/commands/en-US/) · [French](docs/commands/fr-CA/) |
| Test Case JSON report format | [testcase.v1.schema.json](docs/schemas/testcase.v1.schema.json) |
| Developer CLI, validation and prerequisites | [Developer tooling](docs/tooling.md) |
| Original specification, delivery plans and design notes | [Archive](docs/archive/README.md) |
| Rules for coding agents | [AGENTS.md](AGENTS.md) |

## Build and verify

Restore NuGet packages once, as a separate step. Verification never restores or
installs anything.

```powershell
dotnet restore AdoToolkit.slnx --locked-mode
pwsh -NoProfile -File .\tools\dev.ps1 verify
```

`verify` is the complete gate. It prints one JSON document and exits with `0` when
all checks pass, `1` when a check fails, and `2` when the run is incomplete, for
example because a prerequisite is missing. It lints the PowerShell code, runs the
tooling tests, checks configuration files, builds the solution, and runs the Core
tests under `en-US` and `fr-CA`. It then stages the package and runs the product
Pester tests against it. All tests run offline against synthetic data. For details and the other `dev.ps1` commands,
see [Developer tooling](docs/tooling.md).

## Repository layout

| Path | Contents |
| --- | --- |
| `src/AdoToolkit.Core` | HTTP pipeline, services, domain model, parsers and report renderers; no dependencies beyond the .NET base class library |
| `src/AdoToolkit.PowerShell` | Cmdlets, argument completers, format views and the module manifest |
| `tests/AdoToolkit.Core.Tests` | xUnit tests, including golden report files |
| `tests/AdoToolkit.PowerShell.Tests` | Pester tests against the staged module and a loopback fake server |
| `tests/Fixtures` | Synthetic fixtures, listed in [tests/Fixtures/README.md](tests/Fixtures/README.md) |
| `tests/Live` | Optional checks against a live server; `verify` never runs them |
| `tools` | `dev.ps1` developer CLI, product gate, packaging scripts and tooling tests |
| `docs` | Guides, cmdlet help sources, JSON schema, tooling reference and archive |
| `Directory.Build.props` | Shared build settings and the module version |
