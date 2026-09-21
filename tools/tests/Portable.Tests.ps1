BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    $packageTools = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../package'))
    . (Join-Path $packageTools 'Package.Common.ps1')

    function New-SyntheticRuntime {
        param([string] $Path, [string] $ExtraEntry, [string] $OmitEntry, [switch] $Link)
        $zip = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
        try {
            $names = @('pwsh.exe', 'pwsh.dll', 'pwsh.runtimeconfig.json', 'System.Management.Automation.dll',
                'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'System.Private.CoreLib.dll',
                'LICENSE.txt', 'ThirdPartyNotices.txt', 'Modules/Example/Example.psd1')
            if ($ExtraEntry) { $names += $ExtraEntry }
            foreach ($name in $names | Where-Object { $_ -ne $OmitEntry }) {
                $entry = $zip.CreateEntry($name)
                if ($Link -and $name -eq $ExtraEntry) { $entry.ExternalAttributes = 0xA000 -shl 16 }
                $stream = $entry.Open()
                try { $stream.Write([Text.Encoding]::UTF8.GetBytes("synthetic $name")) }
                finally { $stream.Dispose() }
            }
        }
        finally { $zip.Dispose() }
        $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
        # Microsoft's published checksum file uses a BOM and the binary-mode '*'.
        [IO.File]::WriteAllText(($Path + '.sha256'), "$hash *$([IO.Path]::GetFileName($Path))`r`n", [Text.Encoding]::Unicode)
    }

    function Read-ZipText {
        param([IO.Compression.ZipArchive] $Zip, [string] $Name)
        $reader = [IO.StreamReader]::new($Zip.GetEntry($Name).Open())
        try { return $reader.ReadToEnd() }
        finally { $reader.Dispose() }
    }
}

Describe 'Portable release packaging without downloads' {
    BeforeEach {
        $fixture = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $package = Join-Path $fixture 'module'
        [void] [IO.Directory]::CreateDirectory($package)
        Set-Content -LiteralPath (Join-Path $package 'AdoToolkit.psd1') -Value "@{ModuleVersion='0.1.0'}"
        # Assembly/layout validation is covered by Package.Tests; runtime fixtures are never executed.
        Mock Assert-AdoPackage { '0.1.0' }
        $runtime = Join-Path $fixture 'PowerShell-7.6.6-win-x64.zip'
        $releaseArguments = @{
            PackagePath = $package
            OutputRoot = Join-Path $fixture 'release with spaces'
            InstallerPath = Join-Path $packageTools 'Install-AdoToolkit.ps1'
            PowerShellArchivePath = $runtime
            PowerShellChecksumPath = $runtime + '.sha256'
            PowerShellVersion = '7.6.6'
        }
    }

    It 'ships both downloads, preserves the runtime and notices, and records matching checksums' {
        New-SyntheticRuntime -Path $runtime
        $release = New-AdoReleaseArchive @releaseArguments
        @(Get-ChildItem -LiteralPath $releaseArguments.OutputRoot -Force).Count | Should -Be 5
        $zip = [IO.Compression.ZipFile]::OpenRead($release.PortableArchive)
        try {
            $zip.Entries.FullName | Should -Contain 'Start-AdoToolkit.cmd'
            $zip.Entries.FullName | Should -Contain 'Start-AdoToolkit.ps1'
            $zip.Entries.FullName | Should -Contain 'README.txt'
            $zip.Entries.FullName | Should -Contain 'module/AdoToolkit.psd1'
            Read-ZipText -Zip $zip -Name 'runtime/LICENSE.txt' | Should -Be 'synthetic LICENSE.txt'
            Read-ZipText -Zip $zip -Name 'runtime/ThirdPartyNotices.txt' | Should -Be 'synthetic ThirdPartyNotices.txt'
            Read-ZipText -Zip $zip -Name 'runtime/Modules/Example/Example.psd1' | Should -Be 'synthetic Modules/Example/Example.psd1'
            $metadata = Read-ZipText -Zip $zip -Name 'bundle.json' | ConvertFrom-Json
            $metadata.ModuleVersion | Should -Be '0.1.0'
            $metadata.PowerShellVersion | Should -Be '7.6.6'
            $metadata.Architecture | Should -Be 'win-x64'
            $metadata.PowerShellSha256 | Should -Be (Get-FileHash -LiteralPath $runtime).Hash.ToLowerInvariant()
            $launcher = Read-ZipText -Zip $zip -Name 'Start-AdoToolkit.cmd'
            $launcher | Should -Not -Match '(?<!\r)\n'
        }
        finally { $zip.Dispose() }
        $hash = (Get-FileHash -LiteralPath $release.PortableArchive).Hash.ToLowerInvariant()
        [IO.File]::ReadAllText($release.PortableChecksum) | Should -Be "$hash  AdoToolkit-0.1.0-win-x64.zip`n"
        $zip = [IO.Compression.ZipFile]::OpenRead($release.Archive)
        try { $zip.Entries.FullName | Should -Be @('AdoToolkit/0.1.0/AdoToolkit.psd1') }
        finally { $zip.Dispose() }
    }

    It 'rejects a modified runtime before replacing any release assets' {
        New-SyntheticRuntime -Path $runtime
        $null = New-AdoReleaseArchive @releaseArguments
        $before = @(Get-ChildItem -LiteralPath $releaseArguments.OutputRoot -File | Sort-Object Name | Get-FileHash | ForEach-Object Hash)
        [IO.File]::AppendAllText($runtime, 'tampered')
        { New-AdoReleaseArchive @releaseArguments } | Should -Throw '*does not match its published hash*'
        @(Get-ChildItem -LiteralPath $releaseArguments.OutputRoot -Force | Sort-Object Name | Get-FileHash | ForEach-Object Hash) | Should -Be $before
    }

    It 'rejects an ambiguous or missing published checksum' -TestCases @(@{ Duplicate = $true }, @{ Duplicate = $false }) {
        param($Duplicate)
        New-SyntheticRuntime -Path $runtime
        $text = if ($Duplicate) { [IO.File]::ReadAllText(($runtime + '.sha256')) * 2 } else { ('0' * 64) + ' *unrelated.zip' }
        [IO.File]::WriteAllText(($runtime + '.sha256'), $text)
        { New-AdoReleaseArchive @releaseArguments } | Should -Throw '*exactly one published checksum*'
        @(Get-ChildItem -LiteralPath $releaseArguments.OutputRoot -Force).Count | Should -Be 0
    }

    It 'rejects an archive for a different runtime version or architecture' {
        New-SyntheticRuntime -Path $runtime
        $releaseArguments.PowerShellVersion = '7.6.5'
        { New-AdoReleaseArchive @releaseArguments } | Should -Throw '*pinned version and win-x64*'
    }

    It 'requires the complete runtime including its notices' {
        New-SyntheticRuntime -Path $runtime -OmitEntry 'ThirdPartyNotices.txt'
        { New-AdoReleaseArchive @releaseArguments } | Should -Throw '*missing ThirdPartyNotices.txt*'
        @(Get-ChildItem -LiteralPath $releaseArguments.OutputRoot -Force).Count | Should -Be 0
    }

    It 'rejects unsafe or colliding runtime entry <Entry>' -TestCases @(
        @{ Entry = '../escape.txt'; IsLink = $false },
        @{ Entry = '/rooted.txt'; IsLink = $false },
        @{ Entry = 'folder\file.txt'; IsLink = $false },
        @{ Entry = 'file:stream'; IsLink = $false },
        @{ Entry = 'PWSH.EXE'; IsLink = $false },
        @{ Entry = 'pwsh.exe/child'; IsLink = $false },
        @{ Entry = 'NUL.txt'; IsLink = $false },
        @{ Entry = 'linked.txt'; IsLink = $true }
    ) {
        param($Entry, $IsLink)
        New-SyntheticRuntime -Path $runtime -ExtraEntry $Entry -Link:$IsLink
        { New-AdoReleaseArchive @releaseArguments } | Should -Throw '*PowerShell archive contains*'
        @(Get-ChildItem -LiteralPath $releaseArguments.OutputRoot -Force).Count | Should -Be 0
        Test-Path -LiteralPath (Join-Path $fixture 'escape.txt') | Should -BeFalse
    }

    It 'rolls back all five assets if the portable checksum is locked' {
        New-SyntheticRuntime -Path $runtime
        $release = New-AdoReleaseArchive @releaseArguments
        $before = @(Get-ChildItem -LiteralPath $releaseArguments.OutputRoot -File | Sort-Object Name | Get-FileHash | ForEach-Object Hash)
        Add-Content -LiteralPath (Join-Path $package 'AdoToolkit.psd1') -Value '# new fixture'
        $lock = [IO.File]::Open($release.PortableChecksum, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try { { New-AdoReleaseArchive @releaseArguments } | Should -Throw }
        finally { $lock.Dispose() }
        @(Get-ChildItem -LiteralPath $releaseArguments.OutputRoot -Force | Sort-Object Name | Get-FileHash | ForEach-Object Hash) | Should -Be $before
    }

    It 'rejects partial portable parameters before writing output' {
        $releaseArguments.Remove('PowerShellChecksumPath')
        { New-AdoReleaseArchive @releaseArguments } | Should -Throw '*together*'
        Test-Path -LiteralPath $releaseArguments.OutputRoot | Should -BeFalse
    }
}

Describe 'Portable console startup' {
    It 'loads the adjacent module globally and reports the host without contacting a server' {
        $launcherRoot = Join-Path $packageTools 'portable'
        Mock Import-Module {
            [pscustomobject]@{ Version = [version] '0.1.0'; ExportedCmdlets = @(1..22); ModuleBase = Join-Path $launcherRoot 'module' }
        }
        Mock Set-Location { }
        $probe = & (Join-Path $launcherRoot 'Start-AdoToolkit.ps1') -SmokeTest | ConvertFrom-Json
        $probe.ModuleVersion | Should -Be '0.1.0'
        $probe.PowerShellVersion | Should -Be $PSVersionTable.PSVersion.ToString()
        Should -Invoke Import-Module -Exactly -Times 1 -ParameterFilter { $Global -and $Name -eq (Join-Path $launcherRoot 'module/AdoToolkit.psd1') }
        Should -Invoke Set-Location -Exactly -Times 1 -ParameterFilter { $LiteralPath -eq $launcherRoot }
    }
}
