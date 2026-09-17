# Slice 0, session 0.1 evidence

Date: 2026-09-15. **Session 0.1 recovered: all V-09 points now pass**; see the
recovery record below. The original attempt stopped at V-09 point 4, as required by
[session 0.1 step 10](slice-0-foundation.md#session-01--initialize-skeleton-product-gate-v-09).
At the end of session 0.1, the remaining sessions still blocked Slice 1A. They are
now complete; see the [Slice 0 completion record](slice-0-completion-evidence.md)
and the current [roadmap status](README.md#status).

## Recovery after the owner's instruction to fix the blocker

- DD-023 now allows a narrow correction to the tooling regression test: its
  invocation-scope assertions run in isolated fixture repositories, with both
  successful and unavailable product-check cases. Gate implementation and its
  exit-code contract are unchanged. §20 option 1 would not fix that assertion;
  retain the existing product ordering and repeat V-09 instead.
- V-09 points 1–3 passed again: the same five stages, 79 passing tooling tests,
  and a deliberate failing product test causing full verify exit 1.
- Point 4 passed: missing Core assets produced full `Status: incomplete`, exit 2,
  with 79 tooling tests passing. `project-check` reported unavailable, exit 2,
  in 718 ms. The original assets were restored.
- Point 5 passed: two consecutive full verifies after a source edit returned
  exit 0, `Status: pass`, no warnings. Project-check durations were 12,424 ms
  and 9,807 ms.
- Point 6 passed: `verify -SkipTests` returned exit 2, `Status: incomplete`,
  with its build checked successfully; the expected skipped-tests warning was
  present. Project-check took 3,635 ms. Point 7's durations are recorded above.
- The copied-fixture preflight regression test passes and creates no restore
  output. A deliberate `double.ToString()` call failed the build with CA1305,
  exit 1; that temporary source was removed.
- The owner separately authorized PSGallery lookup and CurrentUser installation
  of Microsoft.PowerShell.PlatyPS 1.x. Version 1.0.3 was installed for session 0.4.

The remaining sections preserve the first attempt's evidence as history.

## Setup and implemented work

- Baseline and post-initialization `verify`: exit 0, `Status: pass`, no warnings;
  stages `powershell-lint`, `powershell-test` (77 passed), `configuration`,
  `tooling-layout`.
- `$template-init` initialized `AdoToolkit` with the plan's exact description.
  The three projects, build settings, scoped instructions, architecture test,
  resources, staged manifest, product gate, Pester smoke test, and culture probe
  are implemented through step 9.
- Explicit user authorization covered nuget.org lookup, pinning the four planned
  packages, and solution restore including the user NuGet cache. No other install,
  ADO access, signing, deployment, or Git lifecycle action occurred.
- Restore command: `dotnet restore AdoToolkit.slnx --source https://api.nuget.org/v3/index.json`.
  Exit 0; all three projects restored. Subsequent build, test, and publish commands
  use `--no-restore`.

| Package | Pinned version |
| --- | --- |
| [System.Management.Automation](https://www.nuget.org/packages/System.Management.Automation/7.6.6) | 7.6.6 (matches installed PowerShell) |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.10.1) | 18.10.1 |
| [xunit.v3](https://www.nuget.org/packages/xunit.v3/4.0.1) | 4.0.1 |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio/4.0.0) | 4.0.0 |

Versions were selected from nuget.org's current stable version indexes and confirmed
in the restored packages. xUnit's current package defaults to Microsoft Testing
Platform. The test project explicitly sets `IsTestingPlatformApplication=false`
and `UseMicrosoftTestingPlatformRunner=false` to use the planned VSTest adapter
and preserve the plan's `dotnet test` arguments. Both Core tests actually ran and
passed in each requested culture; none were skipped.

The PowerShell compile reference also has `IncludeAssets=compile`, alongside the
required `PrivateAssets=all` and `ExcludeAssets=runtime`. Complete staged-output
inspection passed: only toolkit DLLs, French satellites, PDBs, the dependency
manifest, module manifest, and format file were present. No PowerShell host
dependency assemblies or private runtime shipped. The manifest version is generated
atomically from `Directory.Build.props`.

## V-09 observations

| Point | Observation | Result |
| --- | --- | --- |
| 1 | `plan` listed the four built-in stages plus `project-check`, with no inferred `dotnet-build` or `dotnet-test` stages. | Passed |
| 2 | Built-in Pester ran 77 tooling tests; product Pester ran the smoke test separately. With the failing `.Pester.ps1` probe present, the built-in stage still passed all 77 tests. | Passed |
| 3 | Temporary `SpikeFailure.Pester.ps1` made product Pester report one pass and one failure; `project-check` and full `verify` exited 1. The temporary file was removed. | Passed |
| 4 | Temporarily renamed Core's `obj/project.assets.json`. Direct product preflight exited 2 with `restore required: authorized setup step`. Full `verify` exited 1 because a template regression test failed; its `project-check` stage correctly reported unavailable, exit 2. The assets file was restored in `finally`. | Failed; stopped |
| 5 | Two consecutive verifies after a source edit. | Not run after the required stop |
| 6 | Deliberate full `verify -SkipTests` probe. | Not run after the required stop |
| 7 | Passing full run before probes: `project-check` 10,674 ms; no warnings. Failed-Pester probe: 10,855 ms. Missing-assets preflight stage: 854 ms. | Durations recorded; spike remains unconfirmed |

### Failure mechanism

`tools/tests/Dev.Tests.ps1`, test
`runs its in-process stages when dev.ps1 is called rather than started with -File`,
invokes the real repository's `verify -SkipTests`. It asserts that every stage has
status `pass`. When assets are missing, the required product preflight correctly
reports `unavailable`, exit 2. That causes the tooling assertion to fail (76 passed,
1 failed), and failure takes precedence over incomplete in the outer gate.

Template tests and gate code were not changed. The product script returns success
for the checks it performs under `-SkipTests`; the template owns the overall
incomplete status for skipped tests. Missing assets still always return 2 from
the product preflight, including under `-SkipTests`.

### Proposed fallback and remaining work

Propose spec §20 option 1: product Pester builds the module with
`dotnet build --no-restore` in `BeforeAll` into a test output directory. Before
implementation, revise DD-023 and add a spec revision entry defining ownership of
build/staging, test discovery, culture runs, and missing-prerequisite outcomes.
The fallback needs its own validation; this spike does not establish that it fixes
the template assertion described above.

DD-023 remains `Accepted (v1.0); pending V-09 spike`; V-09 records the observed
failure instead of a confirmation. Session 0.1 steps 11–13 have not been completed:
the copied-fixture preflight regression test and CA1305 negative-build probe remain,
along with the remaining V-09 points. Sessions 0.2–0.4 also remain before the
Slice 0 offline exit gate can be met.

Acceptance evidence is limited to S0-1 (passing full gate before probes), the
architecture assertion, and partial S0-8 (two cultures and the culture probe).
S0-2 through S0-7 and the machine-output portion of S0-8 remain incomplete;
S0-9 remains a work-machine live check.

French review is pending for the new `OperationFailed` resource in both projects:
English `The operation failed.`, French `L’opération a échoué.`

## Final cleanup verification

Full `verify` after restoring the probe inputs and recording the failed spike:
exit 0, `Status: pass`, every warnings array empty. All five stages passed;
77 tooling tests passed, and `project-check` took 10,380 ms. The two Core tests
passed in both cultures and the product Pester smoke test passed.

The failing probe and backup filename are absent; the original assets file is
present. No further implementation or spike points ran. A green normal-input
verification does not confirm the failed missing-assets behavior or complete
Slice 0.
