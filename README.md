<!-- project:start -->
# AdoToolkit

PowerShell toolkit for Azure DevOps Server 2020: compiled C\# cmdlets for work items, Test Case reports, bulk test export, and pipeline failure triage.
<!-- project:end -->

AdoToolkit is a binary PowerShell 7.6 module for one on-premises Azure DevOps Server 2020
collection, using Windows integrated authentication. It is read-only against ADO. Cmdlets
return typed objects that compose in pipelines, and reports are written as standalone HTML,
Markdown or JSON files. All user-facing text and help are available in English and French.

The normative specification is [docs/ado-toolkit-spec.md](docs/ado-toolkit-spec.md).
Delivery status is tracked in [docs/plans/README.md](docs/plans/README.md).

## Status

| Slice | Scope | State |
| --- | --- | --- |
| 0 | Profiles, connections, HTTP pipeline, packaging scripts | Offline done |
| 1 | Work items, Test Case steps, Shared Steps, parameters | Offline done |
| 2 | Test Case reports (HTML, Markdown, JSON) | Offline done |
| 3 | Bulk export by plan/suite or WIQL | Offline done |
| 4 | Pipeline failure triage and build logs | Offline done |
| 5 | Failed-test report for one build | Sessions 5.1–5.3 done; 5.4 (report commit acceptance, browser spike) remains |

Live acceptance checks run only on the work machine (milestone M8) and have not run yet.

## Commands

| Area | Cmdlets |
| --- | --- |
| Connection | `Connect-Ado`, `Disconnect-Ado`, `Get-AdoConnection`, `Test-AdoConnection`, `Get-AdoProject` |
| Profiles (local) | `Get-AdoProfile`, `Set-AdoProfile`, `Remove-AdoProfile` |
| Work items | `Get-AdoWorkItem`, `Invoke-AdoWiql` |
| Test cases | `Get-AdoTestPlan`, `Get-AdoTestSuite`, `Get-AdoTestCase`, `Export-AdoTestCase` |
| Pipelines | `Get-AdoBuildDefinition`, `Get-AdoBuild`, `Get-AdoBuildTimeline`, `Get-AdoBuildFailure`, `Save-AdoBuildLog` |
| Test results | `Get-AdoTestRun`, `Get-AdoBuildTestFailure`, `Export-AdoBuildTestFailure` |

Every cmdlet has English and French help (`Get-Help <cmdlet> -Full`). The help sources are
in [docs/commands/](docs/commands/).

## Quick start

```powershell
Set-AdoProfile -Name work -CollectionUrl 'https://ado.example.test/DefaultCollection' -DefaultProject 'Web' -DefaultProfile
Connect-Ado
Test-AdoConnection

# One Test Case as an HTML report in Downloads
Get-AdoTestCase -Id 1234 | Export-AdoTestCase -Open

# A whole suite tree as one Markdown document
Get-AdoTestCase -PlanId 10 -SuiteId 11 -Recurse | Export-AdoTestCase -Format Markdown

# Why did the latest main build fail?
Get-AdoBuild -Definition 'Web CI' -Branch main -Latest | Get-AdoBuildFailure | Save-AdoBuildLog -Tail 200

# Failed and flaky tests of a build, with history and attachments
Get-AdoBuildTestFailure -BuildId 401 | Export-AdoBuildTestFailure -Open
```

## Configuration

Profiles and defaults are stored in `%APPDATA%\AdoToolkit\config.json`. Set
`ADOTOOLKIT_CONFIG_PATH` to use a different file. Change profiles with `Set-AdoProfile` and
`Remove-AdoProfile`; the file is replaced atomically, and unknown keys are kept with a
warning. The file layout and limits are in spec §4.

| Section | Keys |
| --- | --- |
| `profiles.<name>` | `collectionUrl`, `defaultProject`, `authentication` (`WindowsIntegrated`), `requestTimeoutSeconds` (1–86400, default 100) |
| `testCases` | `maximumSharedStepDepth`, `maximumExpandedSteps`, `maximumResolvedWorkItems` |
| `testResults` | `historyCount`, `historyScope` (`SameBranch` or `AllBranches`), `maximumReportedFailures`, `maximumHistoryRequests`, `maximumAttachmentBytes`, `maximumTotalAttachmentBytes`, `maximumInlineJsonBytes` |
| `reporting` | `culture` (report language when `-Culture` is not given) |

## Build and verify

Requirements: Windows, PowerShell 7.6.5+, the .NET 10 SDK pinned by `global.json`,
Pester 5.x, PSScriptAnalyzer, and PlatyPS 1.x. Restore packages once, in a separate
authorized step (`dotnet restore AdoToolkit.slnx --locked-mode`). Verification never
installs anything.

```powershell
pwsh -NoProfile -File .\tools\dev.ps1 context   # compact orientation
pwsh -NoProfile -File .\tools\dev.ps1 plan      # checks that would run
pwsh -NoProfile -File .\tools\dev.ps1 verify    # full gate
```

The CLI prints one JSON document. `verify` exits 0 for pass, 1 for failure and 2 when
incomplete. It runs PowerShell lint and tooling tests, configuration checks, and the
product gate in [tools/check.ps1](tools/check.ps1): a Release build, the Core tests under
`en-US` and `fr-CA`, package staging and inspection, and the product Pester tests against
the staged module. All tests are offline and use synthetic fixtures.

Packaging, signing and installation on the work machine use the scripts in
[tools/package/](tools/package/). The steps are in spec §21 and the work-machine track
(W-1 to W-9) in [docs/plans/README.md](docs/plans/README.md#work-machine-track-m8).

## Repository layout

| Path | Contents |
| --- | --- |
| `src/AdoToolkit.Core` | HTTP pipeline, services, domain model, parsers, report renderers; BCL-only |
| `src/AdoToolkit.PowerShell` | Cmdlets, completers, formats, module manifest |
| `tests/AdoToolkit.Core.Tests` | xUnit tests, including golden report files |
| `tests/AdoToolkit.PowerShell.Tests` | Pester tests against the staged module and a loopback fake server |
| `tests/Fixtures` | Synthetic fixtures, cataloged in [Fixtures/README.md](tests/Fixtures/README.md) |
| `tests/Live` | Opt-in live checks for the work machine; never run by `verify` |
| `tools` | `dev.ps1` developer CLI, product gate, packaging scripts, tooling tests |
| `docs` | Specification, slice plans and evidence, command help, JSON schema |

Agent instructions are in [AGENTS.md](AGENTS.md). The developer tooling is documented in
[docs/tooling.md](docs/tooling.md), and its design rationale in [docs/design.md](docs/design.md).
