<!-- project:start -->
# AdoToolkit

PowerShell toolkit for Azure DevOps Server 2020: compiled C\# cmdlets for work items, Test Case reports, bulk test export, and pipeline failure triage.
<!-- project:end -->

**Version 0.1.1** · Windows · PowerShell 7.6 · Azure DevOps Server 2020 · English and French

AdoToolkit is a compiled PowerShell module for an on-premises Azure DevOps Server
2020 collection. It signs in with your Windows identity and only reads from Azure
DevOps. Its cmdlets return typed objects that you can use in pipelines, and it writes
standalone HTML, Markdown or JSON reports when you need a document. All messages,
report labels and help are available in English and French.

Every feature is covered by offline tests. Version 0.1.1 fixes the failed-test report on
Azure DevOps Server 2020, which sends some IDs as text, and ships as a prebuilt release.

> [!NOTE]
> Live validation against Azure DevOps Server 2020 is in progress. Connections, projects,
> builds and test runs have been confirmed; the opt-in checks in `tests/Live` cover the rest.

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
| Use the module | Windows, PowerShell 7.6 (`pwsh`), and access to an Azure DevOps Server 2020 collection with your Windows account. No administrator rights, .NET SDK or other modules |
| Build it from source | The .NET 10 SDK selected by [global.json](global.json) (a per-user install works) and PlatyPS 1.x (`Microsoft.PowerShell.PlatyPS`) for PowerShell 7 |
| Develop and verify | The build prerequisites plus PowerShell 7.6.5 or later, Pester 5.x and PSScriptAnalyzer; ripgrep is recommended |

Azure DevOps Services and later Server versions are not supported.

## Installation

Each [GitHub release](https://github.com/JosephGibson/Ado_tools/releases) contains a
prebuilt module, so nothing is compiled on your machine:

| Asset | Purpose |
| --- | --- |
| `AdoToolkit-<version>.zip` | The module files |
| `AdoToolkit-<version>.zip.sha256` | The SHA-256 checksum of the zip |
| `Install-AdoToolkit.ps1` | Checks the zip against the checksum and installs it for the current user |

Download the three files into one folder, close any PowerShell window that has
AdoToolkit loaded, and run this in PowerShell 7 from that folder:

```powershell
Unblock-File .\Install-AdoToolkit.ps1
.\Install-AdoToolkit.ps1 -Path .\AdoToolkit-0.1.1.zip
```

Then open a new PowerShell window and run `Import-Module AdoToolkit`. Releases are not
code-signed. For other options, such as installing without the script or from a
source build, see [Getting started](docs/guides/getting-started.md#install-the-module).

## Quick start

```powershell
Import-Module AdoToolkit
# Once: save the collection URL (not a project URL) and your usual project.
Set-AdoProfile -Name work -CollectionUrl 'https://ado.example.test/DefaultCollection' `
    -DefaultProject 'Web' -DefaultProfile
Test-AdoConnection        # commands connect with the default profile

# Every Test Case in a suite tree, as one HTML report in Downloads
Get-AdoTestCase -PlanId 10 -SuiteId 11 -Recurse | Export-AdoTestCase -Open

# The failed and flaky tests of the latest failed build on main, as an HTML report
Get-AdoBuildTestFailure -Definition 'Web CI' -Branch main -Result Failed |
    Export-AdoBuildTestFailure -Path .\reports -Open

# The failed tasks of the same build
Get-AdoBuild -Definition 'Web CI' -Branch main -Latest -Result Failed | Get-AdoBuildFailure
```

Profiles, connections and the other options are described in
[Getting started](docs/guides/getting-started.md).

## Documentation

| Topic | Document |
| --- | --- |
| Installation, profiles and connecting | [Getting started](docs/guides/getting-started.md) |
| Work items and WIQL queries | [Work items and WIQL](docs/guides/work-items.md) |
| Test Case reports and bulk export | [Test Case reports](docs/guides/test-case-reports.md) |
| A failed-test report from one build ID | [Export a report from a build ID](docs/guides/build-report.md) |
| Build failures, logs and failed-test reports | [Pipeline failure triage](docs/guides/pipeline-triage.md) |
| Configuration file, defaults and limits | [Configuration](docs/guides/configuration.md) |
| Full cmdlet help (also available with `Get-Help <cmdlet> -Full`) | [English](docs/commands/en-US/) · [French](docs/commands/fr-CA/) |
| Test Case JSON report format | [testcase.v1.schema.json](docs/schemas/testcase.v1.schema.json) |
| Developer CLI, validation and prerequisites | [Developer tooling](docs/tooling.md) |
| Original specification, delivery plans and design notes | [Archive](docs/archive/README.md) |
| Rules for coding agents | [AGENTS.md](AGENTS.md) |

## Build and verify

Restore NuGet packages once, as a separate step. Verification never restores or
installs anything. The repository `nuget.config` uses nuget.org only, so package feeds
and credentials configured on your machine are not involved.

```powershell
dotnet restore AdoToolkit.slnx --locked-mode
pwsh -NoProfile -File .\tools\dev.ps1 verify
```

`verify` is the complete gate. It prints one JSON document and exits with `0` when
all checks pass, `1` when a check fails, and `2` when the run is incomplete, for
example because a prerequisite is missing. It lints the PowerShell code, runs the
tooling tests, checks configuration files, builds the solution, and runs the Core
tests under `en-US` and `fr-CA`. It then stages the package and runs the product
Pester tests against it. All tests run offline against synthetic data.

To build release assets locally, run `tools\package\Publish-AdoToolkitPackage.ps1` and
then `tools\package\New-AdoToolkitRelease.ps1`. Pushing a `v<version>` tag runs the same
steps in GitHub Actions and publishes the release. For details and the other `dev.ps1`
commands, see [Developer tooling](docs/tooling.md#packaging-and-releases).

## Repository layout

| Path | Contents |
| --- | --- |
| `src/AdoToolkit.Core` | HTTP pipeline, services, domain model, parsers and report renderers; no dependencies beyond the .NET base class library |
| `src/AdoToolkit.PowerShell` | Cmdlets, argument completers, format views and the module manifest |
| `tests/AdoToolkit.Core.Tests` | xUnit tests, including golden report files |
| `tests/AdoToolkit.PowerShell.Tests` | Pester tests against the staged module and a loopback fake server |
| `tests/Fixtures` | Synthetic fixtures, listed in [tests/Fixtures/README.md](tests/Fixtures/README.md) |
| `tests/Live` | Optional checks against a live server; `verify` never runs them |
| `tools` | `dev.ps1` developer CLI, product gate, packaging and release scripts, tooling tests |
| `.github/workflows` | The release workflow |
| `docs` | Guides, cmdlet help sources, JSON schema, tooling reference and archive |
| `Directory.Build.props` | Shared build settings and the module version |
