<!-- project:start -->
# AdoToolkit

PowerShell toolkit for Azure DevOps Server 2020: compiled C\# cmdlets for work items, Test Case reports, bulk test export, and pipeline failure triage.
<!-- project:end -->

**Version 0.4.0** · Windows · PowerShell 7.6 · Azure DevOps Server 2020 · English and French

AdoToolkit is a compiled PowerShell module for an on-premises Azure DevOps Server
2020 collection. It signs in with your Windows identity and only reads from Azure
DevOps. Its cmdlets return typed objects that you can use in pipelines, and it writes
standalone HTML, Markdown or JSON reports when you need a document. All messages,
report labels and help are available in English and French.

Version 0.4.0 shows the bugs of each failed test: the bugs associated with its test
results and the bugs linked to its Test Case, with their title and state and whether they
are still open. `Get-AdoBuildTestFailure` returns them in `Bugs` and `HasOpenBug`, and the
failed-test report marks tests with an open bug and can show only the tests that no open
bug tracks yet. This release also shows every report time in the export's time zone and
closes several security and robustness gaps. See the [release notes](docs/release-0.4.0.md)
for every change, the validation evidence and the remaining work-PC checks.

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
| Failed-test reports | Failed and flaky tests with every attempt, grouped by stage or job, their bugs and whether one is open, run history, and a compact searchable HTML report with JSON and text attachments | `Get-AdoTestRun`, `Get-AdoBuildTestFailure`, `Export-AdoBuildTestFailure` |

## Requirements

| To | You need |
| --- | --- |
| Use the portable release | Windows x64 and access to an Azure DevOps Server 2020 collection with your Windows account. PowerShell and its runtime are included; no administrator rights, .NET SDK or other modules |
| Use the module-only release | Windows and PowerShell 7.6 (`pwsh`), plus the same server access |
| Build it from source | The .NET 10 SDK selected by [global.json](global.json) (a per-user install works) and PlatyPS 1.x (`Microsoft.PowerShell.PlatyPS`) for PowerShell 7 |
| Develop and verify | The build prerequisites plus PowerShell 7.6.5 or later, Pester 5.x and PSScriptAnalyzer; ripgrep is recommended |

Azure DevOps Services and later Server versions are not supported.

## Installation

Each [GitHub release](https://github.com/JosephGibson/Ado_tools/releases) contains a
portable bundle and a module-only download, so nothing is compiled on your machine:

| Asset | Purpose |
| --- | --- |
| **`AdoToolkit-<version>-win-x64.zip`** | **Recommended:** AdoToolkit, PowerShell and a launcher in one folder |
| `AdoToolkit-<version>-win-x64.zip.sha256` | The portable bundle's SHA-256 checksum |
| `AdoToolkit-<version>.zip` | Module files for an existing PowerShell 7.6 installation |
| `AdoToolkit-<version>.zip.sha256` | The module-only ZIP's SHA-256 checksum |
| `Install-AdoToolkit.ps1` | Checks and installs the module-only ZIP for the current user |

To use the portable bundle:

1. Download `AdoToolkit-<version>-win-x64.zip`.
2. Right-click the ZIP, choose **Properties**, select **Unblock** if shown, and click **Apply**.
3. Extract the entire ZIP to a folder you can write to.
4. Double-click **`Start-AdoToolkit.cmd`**. A console opens with AdoToolkit already loaded.

Use that launcher each time. The bundle needs no runtime download on first launch.
To update, extract a new portable release into a new folder and use its launcher;
saved connection profiles are retained. PowerShell is pinned to the version tested
with the release and is refreshed through new AdoToolkit releases.

AdoToolkit scripts and modules are not code-signed; workplace policies requiring
signed code still apply. For checksum verification, module-only installation and
source builds, see [Getting started](docs/guides/getting-started.md#install-the-module).

## Quick start

```powershell
Import-Module AdoToolkit  # already loaded when using Start-AdoToolkit.cmd
# Once: save the collection URL (not a project URL) and your usual project.
Set-AdoProfile -Name work -CollectionUrl 'https://ado.example.test/DefaultCollection' `
    -DefaultProject 'Web' -DefaultProfile
Test-AdoConnection        # commands connect with the default profile

# Every Test Case in a suite tree, as one HTML report in Downloads
Get-AdoTestCase -PlanId 10 -SuiteId 11 -Recurse | Export-AdoTestCase -Open

# The failed tests of the latest failed build on main, with their bugs, as an HTML report
# (add -IncludeFlaky to Export-AdoBuildTestFailure to list flaky tests too)
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
