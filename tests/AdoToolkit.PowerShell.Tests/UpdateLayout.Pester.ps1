BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    . (Join-Path $PSScriptRoot 'Support/FakeGitHub.ps1')
    # The gate's staged copy has the module-only shape, AdoToolkit\<version>, but its root is not on
    # PSModulePath: it is not an install, and the update refuses it.
    $staged = Split-Path -Parent $env:ADOTOOLKIT_MODULE_MANIFEST
    $env:PSModulePath = (@($env:PSModulePath -split [IO.Path]::PathSeparator) |
            Where-Object { $_ -and [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($_)) -ne (Split-Path -Parent (Split-Path -Parent $staged)) }) -join [IO.Path]::PathSeparator
    Import-Module -Name $env:ADOTOOLKIT_MODULE_MANIFEST -Force
    $github = New-FakeGitHub
    Set-UpdateTransport -Handler $github
}

AfterAll { Set-UpdateTransport -Handler $null }

Describe 'Update-AdoToolkit outside an install' {
    It 'refuses the staged copy before any request and points to the manual installation' {
        $record = { Update-AdoToolkit } | Should -Throw -ErrorId 'AdoConfiguration,AdoToolkit.UpdateAdoToolkitCommand' -PassThru

        $record.CategoryInfo.Category | Should -Be 'InvalidArgument'
        $record.Exception.Message | Should -BeLike "*$staged*"
        $record.Exception.Message | Should -BeLike '*https://github.com/JosephGibson/Ado_tools#installation*'
        @($github.Requests()).Count | Should -Be 0
    }
}
