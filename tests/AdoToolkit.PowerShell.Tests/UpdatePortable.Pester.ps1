BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    . (Join-Path $PSScriptRoot 'Support/FakeGitHub.ps1')
    $packageTools = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../tools/package'))
    . (Join-Path $packageTools 'Package.Common.ps1')
    $staged = Split-Path -Parent $env:ADOTOOLKIT_MODULE_MANIFEST
    $stagedVersion = [string] (Import-PowerShellDataFile -LiteralPath $env:ADOTOOLKIT_MODULE_MANIFEST).ModuleVersion
    # A portable copy that runs version 0.0.1: its module beside the launcher and runtime\pwsh.exe,
    # which the update only looks for. The staged version is the newer release.
    $parent = Join-Path $TestDrive 'Tools'
    $old = Join-Path $parent 'AdoToolkit-0.0.1-win-x64'
    New-RelabeledModule -Source $staged -Destination (Join-Path $old 'module') -Version '0.0.1'
    Set-Content -LiteralPath (Join-Path $old 'Start-AdoToolkit.cmd') -Value '@echo off'
    [void] [IO.Directory]::CreateDirectory((Join-Path $old 'runtime'))
    Set-Content -LiteralPath (Join-Path $old 'runtime/pwsh.exe') -Value 'synthetic'
    Import-Module -Name (Join-Path $old 'module/AdoToolkit.psd1') -Force
    $github = New-FakeGitHub
    Set-UpdateTransport -Handler $github
    $culture = [cultureinfo]::GetCultureInfo($PSUICulture)

    # The portable zip that the publisher builds from the staged package over a synthetic runtime.
    $runtime = New-SyntheticRuntime -Folder (Join-Path $TestDrive 'runtime-source')
    $release = New-AdoReleaseArchive -PackagePath $staged -OutputRoot (Join-Path $TestDrive 'release') -PowerShellArchivePath $runtime.Archive `
        -PowerShellChecksumPath $runtime.Checksum -PowerShellVersion $runtime.Version
    $portableZip = [IO.File]::ReadAllBytes($release.PortableArchive)
    $portableChecksum = [IO.File]::ReadAllBytes($release.PortableChecksum)
}

AfterAll { Set-UpdateTransport -Handler $null }

Describe 'Update-AdoToolkit in a portable copy' {
    It 'installs the publisher''s portable zip into a new sibling folder and leaves the running folder alone' {
        $before = Get-ChildItem -LiteralPath $old -Recurse -File | ForEach-Object { $_.FullName + '|' + (Get-FileHash -LiteralPath $_.FullName).Hash }
        Publish-FakeRelease -GitHub $github -Version $stagedVersion -Archive $portableZip -Checksum $portableChecksum -Mode Portable
        $target = Join-Path $parent "AdoToolkit-$stagedVersion-win-x64"

        $result = Update-AdoToolkit -InformationVariable info -InformationAction SilentlyContinue -Confirm:$false

        $result.Status | Should -Be 'Installed'
        $result.InstallMode | Should -Be 'Portable'
        $result.Path | Should -Be $target
        $result.PreviousPath | Should -Be $old
        foreach ($file in @('Start-AdoToolkit.cmd', 'Start-AdoToolkit.ps1', 'README.txt', 'bundle.json', 'runtime/pwsh.exe', 'runtime/Modules/Example/Example.psd1')) {
            Test-Path -LiteralPath (Join-Path $target $file) -PathType Leaf | Should -BeTrue
        }
        foreach ($file in Get-ChildItem -LiteralPath $staged -File -Recurse) {
            $relative = [IO.Path]::GetRelativePath($staged, $file.FullName)
            (Get-FileHash -LiteralPath (Join-Path $target "module/$relative")).Hash | Should -Be (Get-FileHash -LiteralPath $file.FullName).Hash
        }
        Get-ChildItem -LiteralPath $old -Recurse -File | ForEach-Object { $_.FullName + '|' + (Get-FileHash -LiteralPath $_.FullName).Hash } | Should -Be $before
        @(Get-ChildItem -LiteralPath $parent -Force).Name | Sort-Object | Should -Be (@('AdoToolkit-0.0.1-win-x64', "AdoToolkit-$stagedVersion-win-x64") | Sort-Object)
        $info[-1].MessageData.Message | Should -Be ([AdoToolkit.Core.Resources.Messages]::Get([AdoToolkit.Core.Resources.AdoMessage] 'UpdateInstalledPortable', $culture,
                @($stagedVersion, $target, '0.0.1', $old)))
    }

    It 'refuses an existing folder of the new version before any download, and keeps it' {
        Publish-FakeRelease -GitHub $github -Version $stagedVersion -Archive $portableZip -Checksum $portableChecksum -Mode Portable
        $target = Join-Path $parent "AdoToolkit-$stagedVersion-win-x64"
        [void] [IO.Directory]::CreateDirectory($target)
        Set-Content -LiteralPath (Join-Path $target 'mine.txt') -Value 'keep'

        $record = { Update-AdoToolkit -ErrorAction Stop -InformationAction SilentlyContinue } | Should -Throw -ErrorId 'AdoFileOutput,AdoToolkit.UpdateAdoToolkitCommand' -PassThru

        $record.Exception.Message | Should -Be ([AdoToolkit.Core.Resources.Messages]::Get([AdoToolkit.Core.Resources.AdoMessage] 'UpdateTargetExists', $culture, @($target)))
        @($github.Requests()) | Should -Be @('https://api.github.com/repos/JosephGibson/Ado_tools/releases/latest')
        Get-Content -LiteralPath (Join-Path $target 'mine.txt') | Should -Be 'keep'
    }
}
