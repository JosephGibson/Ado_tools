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
