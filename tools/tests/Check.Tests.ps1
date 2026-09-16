BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
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
