BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    . (Join-Path $PSScriptRoot '../lib/test-results.ps1')
}

Describe 'Product test completeness' {
    It 'returns <Expected> for <Case> even if the runner exits successfully' -TestCases @(
        @{ Case = 'all passed'; Total = 3; Executed = 3; Passed = 3; Failed = 0; Errors = 0; Expected = 0 }
        @{ Case = 'a skipped test'; Total = 3; Executed = 2; Passed = 2; Failed = 0; Errors = 0; Expected = 2 }
        @{ Case = 'no discovered tests'; Total = 0; Executed = 0; Passed = 0; Failed = 0; Errors = 0; Expected = 2 }
        @{ Case = 'a failed test'; Total = 3; Executed = 3; Passed = 2; Failed = 1; Errors = 0; Expected = 1 }
        @{ Case = 'a test error'; Total = 3; Executed = 3; Passed = 2; Failed = 0; Errors = 1; Expected = 1 }
        @{ Case = 'an inconclusive test'; Total = 3; Executed = 3; Passed = 2; Failed = 0; Errors = 0; Expected = 2 }
    ) {
        param($Case, $Total, $Executed, $Passed, $Failed, $Errors, $Expected)
        $document = [xml] @"
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <ResultSummary><Counters total="$Total" executed="$Executed" passed="$Passed" failed="$Failed" error="$Errors" timeout="0" aborted="0" /></ResultSummary>
</TestRun>
"@
        (Get-AdoTestOutcome -Document $document).ExitCode | Should -Be $Expected
    }

    It 'reports incomplete when counters are absent or malformed' -TestCases @(
        @{ Text = '<TestRun />' }
        @{ Text = '<TestRun><ResultSummary><Counters total="3" /></ResultSummary></TestRun>' }
        @{ Text = '<TestRun><ResultSummary><Counters total="-1" executed="0" passed="0" failed="0" error="0" timeout="0" aborted="0" /></ResultSummary></TestRun>' }
    ) {
        param($Text)
        (Get-AdoTestOutcome -Document ([xml] $Text)).ExitCode | Should -Be 2
    }
}

Describe 'Product Pester child process' {
    # A cmdlet run with -WhatIf prints "What if:" straight to the host. That text once reached the
    # gate's output, where a test folder named "missing" made it look like a warning.
    It 'returns only the report, not what the child or its tests print to the host' {
        $report = Join-Path $TestDrive 'report.txt'
        $script = @'
[Console]::Out.WriteLine('What if: Performing the operation "Export" on target "C:\temp\missing\Build-401-TestFailures.html".')
Write-Host 'skipped (not installed)'
Write-Warning 'a missing tool'
[System.IO.File]::WriteAllLines($env:ADOTOOLKIT_GATE_REPORT, [string[]] @('check: product Pester: 3 passed, 0 failed, 0 skipped, 3 discovered'))
exit 0
'@
        # PowerShell forwards a child's warning and information records to the caller's streams;
        # none of them may leave the function either.
        $streams = @(Invoke-AdoReportingChild -Script $script -ReportPath $report *>&1)
        $streams.Count | Should -Be 1
        $streams[0].ExitCode | Should -Be 0
        $streams[0].Lines | Should -Be @('check: product Pester: 3 passed, 0 failed, 0 skipped, 3 discovered')
    }

    It 'keeps the child exit code and report of a failing run' {
        $report = Join-Path $TestDrive 'failing-report.txt'
        $script = "[System.IO.File]::WriteAllLines(`$env:ADOTOOLKIT_GATE_REPORT, [string[]] @('check: product Pester: 2 passed, 1 failed, 0 skipped, 3 discovered', 'Suite.Test: Expected 1, but got 2.')); exit 1"
        $result = Invoke-AdoReportingChild -Script $script -ReportPath $report
        $result.ExitCode | Should -Be 1
        $result.Lines.Count | Should -Be 2
        $result.Lines[1] | Should -Be 'Suite.Test: Expected 1, but got 2.'
    }

    It 'fails with the child output when no report was written, even if the child exits successfully' {
        $previous = $env:ADOTOOLKIT_GATE_REPORT
        $result = Invoke-AdoReportingChild -Script "Write-Output 'stopped before the tests'; exit 0" -ReportPath (Join-Path $TestDrive 'absent-report.txt')
        $result.ExitCode | Should -Be 1
        $result.Lines[0] | Should -Be 'The child process wrote no report.'
        $result.Lines | Should -Contain 'stopped before the tests'
        $env:ADOTOOLKIT_GATE_REPORT | Should -Be $previous
    }
}

Describe 'Product restore preflight' {
    It 'reports missing assets before build without restoring' {
        $fixture = Join-Path $TestDrive 'unrestored'
        [void] [System.IO.Directory]::CreateDirectory((Join-Path $fixture 'tools'))
        [void] [System.IO.Directory]::CreateDirectory((Join-Path $fixture 'src/Sample'))
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../check.ps1') -Destination (Join-Path $fixture 'tools/check.ps1')
        Set-Content -LiteralPath (Join-Path $fixture 'AdoToolkit.slnx') -Value '<Solution><Project Path="src/Sample/Sample.csproj" /></Solution>'
        Set-Content -LiteralPath (Join-Path $fixture 'src/Sample/Sample.csproj') -Value '<Project Sdk="Microsoft.NET.Sdk" />'
        $arguments = @('-NoProfile', '-File', (Join-Path $fixture 'tools/check.ps1'))
        $output = @(& (Join-Path $PSHOME 'pwsh.exe') @arguments)
        $code = $LASTEXITCODE
        $code | Should -Be 2
        $output | Should -Contain 'restore required: authorized setup step'
        Test-Path -LiteralPath (Join-Path $fixture 'src/Sample/obj') | Should -BeFalse
    }
}
