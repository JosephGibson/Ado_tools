BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    $packageTools = Join-Path (Split-Path -Parent $PSScriptRoot) 'package'
    . (Join-Path $packageTools 'Package.Common.ps1')
    $expected = 'A' * 40
    $wrong = 'B' * 40
    $assemblyFixtures = Join-Path $TestDrive 'assembly-fixtures'
    foreach ($name in @('AdoToolkit.Core.dll', 'AdoToolkit.PowerShell.dll', 'fr/AdoToolkit.Core.resources.dll', 'fr/AdoToolkit.PowerShell.resources.dll')) {
        $file = Join-Path $assemblyFixtures $name
        [void] [IO.Directory]::CreateDirectory((Split-Path -Parent $file))
        $identity = [Reflection.AssemblyName]::new([IO.Path]::GetFileNameWithoutExtension($name))
        $identity.Version = [version]'0.1.0.0'
        $assembly = [Reflection.Emit.PersistedAssemblyBuilder]::new($identity, [object].Assembly)
        $null = $assembly.DefineDynamicModule($identity.Name).DefineType('SyntheticFixture').CreateType()
        $assembly.Save($file)
    }
    function New-SyntheticPackage {
        param([string] $Path)
        [void] [System.IO.Directory]::CreateDirectory($Path)
        $manifest = @"
@{
ModuleVersion = '0.1.0'
RootModule = 'AdoToolkit.PowerShell.dll'
CmdletsToExport = @('Connect-Ado','Disconnect-Ado','Get-AdoConnection','Test-AdoConnection','Get-AdoProfile','Set-AdoProfile','Remove-AdoProfile','Get-AdoProject','Get-AdoWorkItem','Get-AdoTestCase','Export-AdoTestCase','Get-AdoTestPlan','Get-AdoTestSuite','Invoke-AdoWiql','Get-AdoBuildDefinition','Get-AdoBuild','Get-AdoBuildTimeline','Get-AdoBuildFailure','Save-AdoBuildLog','Get-AdoTestRun','Get-AdoBuildTestFailure','Export-AdoBuildTestFailure')
}
"@
        Set-Content -LiteralPath (Join-Path $Path 'AdoToolkit.psd1') -Value $manifest
        Set-Content -LiteralPath (Join-Path $Path 'AdoToolkit.Format.ps1xml') -Value '<Configuration><ViewDefinitions /></Configuration>'
        foreach ($name in @('AdoToolkit.Core.dll', 'AdoToolkit.PowerShell.dll', 'fr/AdoToolkit.Core.resources.dll', 'fr/AdoToolkit.PowerShell.resources.dll')) {
            $file = Join-Path $Path $name
            [void] [System.IO.Directory]::CreateDirectory((Split-Path -Parent $file))
            [IO.File]::Copy((Join-Path $assemblyFixtures $name), $file)
        }
        foreach ($culture in @('en-US', 'fr')) {
            $folder = Join-Path $Path $culture
            [void] [System.IO.Directory]::CreateDirectory($folder)
            Set-Content -LiteralPath (Join-Path $folder 'AdoToolkit.PowerShell.dll-Help.xml') -Value ('<helpItems>' + ('<command />' * 22) + '</helpItems>')
        }
        return $Path
    }
}
Describe 'Package validation and deployment boundaries' {
    BeforeEach { $package = New-SyntheticPackage -Path (Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))) }

    It 'rejects assemblies from a previous release under a new manifest version' {
        $manifest = Join-Path $package 'AdoToolkit.psd1'
        $text = [IO.File]::ReadAllText($manifest).Replace("ModuleVersion = '0.1.0'", "ModuleVersion = '0.2.0'")
        [IO.File]::WriteAllText($manifest, $text)
        { Assert-AdoPackage -PackagePath $package } | Should -Throw '*assembly version*'
    }

    It 'rejects an invalid assembly before writing release assets' {
        Set-Content -LiteralPath (Join-Path $package 'AdoToolkit.Core.dll') -Value 'not an assembly'
        $output = Join-Path $TestDrive 'invalid-release'
        { New-AdoReleaseArchive -PackagePath $package -OutputRoot $output } | Should -Throw '*assembly*'
        Test-Path -LiteralPath (Join-Path $output 'AdoToolkit-0.1.0.zip') | Should -BeFalse
    }

    It 'rejects a DLL with the right version but the wrong assembly identity' {
        [IO.File]::Copy((Join-Path $package 'AdoToolkit.Core.dll'), (Join-Path $package 'AdoToolkit.PowerShell.dll'), $true)
        { Assert-AdoPackage -PackagePath $package } | Should -Throw '*assembly identity*'
    }

    It 'F09 restores the existing release when its checksum cannot be replaced' {
        $output = Join-Path $TestDrive 'release with spaces'
        $release = New-AdoReleaseArchive -PackagePath $package -OutputRoot $output
        $before = (Get-FileHash -LiteralPath $release.Archive).Hash
        $checksum = [IO.File]::ReadAllText($release.Checksum)
        [IO.File]::AppendAllText((Join-Path $package 'AdoToolkit.Core.dll'), 'changed synthetic assembly overlay')
        $lock = [IO.File]::Open($release.Checksum, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try { { New-AdoReleaseArchive -PackagePath $package -OutputRoot $output } | Should -Throw }
        finally { $lock.Dispose() }
        (Get-FileHash -LiteralPath $release.Archive).Hash | Should -Be $before
        [IO.File]::ReadAllText($release.Checksum) | Should -Be $checksum
        @(Get-ChildItem -LiteralPath $output -Force).Count | Should -Be 2
    }

    It 'F10 resolves relative package paths from the PowerShell location' {
        Push-Location -LiteralPath $TestDrive
        try {
            $relative = Split-Path -Leaf $package
            Resolve-AdoPackagePath -Path $relative -Root $TestDrive | Should -Be $package
        }
        finally { Pop-Location }
    }

    It 'F10 builds all release assets using relative paths after Set-Location' {
        $repository = Join-Path $TestDrive 'synthetic repository'
        $scripts = Join-Path $repository 'tools/package'
        [void] [IO.Directory]::CreateDirectory($scripts)
        foreach ($name in @('New-AdoToolkitRelease.ps1', 'Package.Common.ps1', 'Install-AdoToolkit.ps1')) {
            Copy-Item -LiteralPath (Join-Path $packageTools $name) -Destination (Join-Path $scripts $name)
        }
        $null = New-SyntheticPackage -Path (Join-Path $repository 'staged package')
        Push-Location -LiteralPath $repository
        try { & (Join-Path $scripts 'New-AdoToolkitRelease.ps1') -PackagePath 'staged package' -OutputRoot 'release assets' | Out-Null }
        finally { Pop-Location }
        $assets = @(Get-ChildItem -LiteralPath (Join-Path $repository 'release assets') -File | Sort-Object Name)
        $assets.Name | Should -Be @('AdoToolkit-0.1.0.zip', 'AdoToolkit-0.1.0.zip.sha256', 'Install-AdoToolkit.ps1')
    }

    It 'F09 validates the installer and rolls back the whole release if it is locked' {
        $output = Join-Path $TestDrive 'complete release'
        $installerSource = Join-Path $packageTools 'Install-AdoToolkit.ps1'
        $release = New-AdoReleaseArchive -PackagePath $package -OutputRoot $output -InstallerPath $installerSource
        $before = @(Get-ChildItem -LiteralPath $output -File | Sort-Object Name | Get-FileHash | ForEach-Object Hash)
        [IO.File]::AppendAllText((Join-Path $package 'AdoToolkit.Core.dll'), 'changed synthetic assembly overlay')
        $lock = [IO.File]::Open((Join-Path $output 'Install-AdoToolkit.ps1'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try { { New-AdoReleaseArchive -PackagePath $package -OutputRoot $output -InstallerPath $installerSource } | Should -Throw }
        finally { $lock.Dispose() }
        @(Get-ChildItem -LiteralPath $output -File -Force | Sort-Object Name | Get-FileHash | ForEach-Object Hash) | Should -Be $before
        Test-Path -LiteralPath $release.Archive | Should -BeTrue
    }

    It 'F09 restores all targets if a staged file disappears during commit' {
        $output = Join-Path $TestDrive 'rollback'
        [void] [IO.Directory]::CreateDirectory($output)
        $first = Join-Path $output 'first'
        $second = Join-Path $output 'second'
        $temporary = Join-Path $output 'first.tmp'
        [IO.File]::WriteAllText($first, 'old first')
        [IO.File]::WriteAllText($second, 'old second')
        [IO.File]::WriteAllText($temporary, 'new first')
        $files = @(@{ Source = $temporary; Target = $first }, @{ Source = (Join-Path $output 'missing.tmp'); Target = $second })
        { Publish-AdoReleaseFiles -Files $files -Root $output } | Should -Throw
        [IO.File]::ReadAllText($first) | Should -Be 'old first'
        [IO.File]::ReadAllText($second) | Should -Be 'old second'
        @(Get-ChildItem -LiteralPath $output -Force).Count | Should -Be 2
    }

    It 'enumerates the exact package and all six signable files including satellites' {
        Assert-AdoPackage -PackagePath $package | Should -Be '0.1.0'
        @(Get-AdoPackageFile -PackagePath $package).Count | Should -Be 8
        $signable = @(Get-AdoSignableFile -PackagePath $package)
        $signable.Count | Should -Be 6
        @($signable | Where-Object Name -like '*.resources.dll').Count | Should -Be 2
    }

    It 'rejects extra host or runtime assets' {
        Set-Content -LiteralPath (Join-Path $package 'System.Management.Automation.dll') -Value 'synthetic'
        { Assert-AdoPackage -PackagePath $package } | Should -Throw '*required layout*'
    }

    It 'refuses installation for <Status> signatures' -TestCases @(
        @{ Status = 'NotSigned' }, @{ Status = 'HashMismatch' }
    ) {
        param($Status)
        Mock Get-AuthenticodeSignature { [pscustomobject]@{ Status = $Status; SignerCertificate = $null } }
        { & (Join-Path $packageTools 'Install-AdoToolkitPackage.ps1') -PackagePath $package -ExpectedThumbprint $expected -WhatIf } | Should -Throw '*signature*'
        Should -Invoke Get-AuthenticodeSignature -Times 1 -Exactly
    }

    It 'refuses a valid signature from the wrong signer' {
        Mock Get-AuthenticodeSignature { [pscustomobject]@{ Status = 'Valid'; SignerCertificate = [pscustomobject]@{ Thumbprint = $wrong } } }
        { & (Join-Path $packageTools 'Install-AdoToolkitPackage.ps1') -PackagePath $package -ExpectedThumbprint $expected -WhatIf } | Should -Throw '*signer*'
    }

    It 'checks every file for a valid expected signer and WhatIf writes nothing' {
        Mock Get-AuthenticodeSignature { [pscustomobject]@{ Status = 'Valid'; SignerCertificate = [pscustomobject]@{ Thumbprint = $expected.ToLowerInvariant() } } }
        Mock Copy-Item { throw 'WhatIf must not copy.' }
        Mock Move-AdoPackageDirectory { throw 'WhatIf must not commit.' }
        $before = @(Get-AdoPackageFile -PackagePath $package | Get-FileHash | Select-Object -ExpandProperty Hash)
        Assert-AdoPackageSignature -PackagePath $package -ExpectedThumbprint $expected
        # A hostless runspace captures ShouldProcess's host messages so the public
        # verify command still emits one JSON document. All signatures stay fake.
        $initialState = [System.Management.Automation.Runspaces.InitialSessionState]::CreateDefault2()
        $stub = @'
param([string] $LiteralPath)
$Probe.Checks++
[pscustomobject]@{ Status = 'Valid'; SignerCertificate = [pscustomobject]@{ Thumbprint = $Probe.Thumbprint } }
'@
        $initialState.Commands.Add([System.Management.Automation.Runspaces.SessionStateFunctionEntry]::new('Get-AuthenticodeSignature', $stub))
        $runspace = [System.Management.Automation.Runspaces.RunspaceFactory]::CreateRunspace($initialState)
        $runspace.Open()
        $shell = [powershell]::Create()
        $shell.Runspace = $runspace
        $probe = [pscustomobject]@{ Checks = 0; Thumbprint = $expected }
        try {
            [void] $shell.AddScript({
                param($Script, $Package, $Probe)
                & $Script -PackagePath $Package -ExpectedThumbprint $Probe.Thumbprint -WhatIf
                $Probe.Checks
            }).AddArgument((Join-Path $packageTools 'Install-AdoToolkitPackage.ps1')).AddArgument($package).AddArgument($probe)
            $shell.Invoke()[0] | Should -Be 6
            $shell.HadErrors | Should -BeFalse
        }
        finally { $shell.Dispose(); $runspace.Dispose() }
        @(Get-AdoPackageFile -PackagePath $package | Get-FileHash | Select-Object -ExpandProperty Hash) | Should -Be $before
        Should -Invoke Get-AuthenticodeSignature -Times 6 -Exactly
        Should -Invoke Copy-Item -Times 0 -Exactly
        Should -Invoke Move-AdoPackageDirectory -Times 0 -Exactly
    }

    It 'does not access a certificate or sign when WhatIf is supplied' {
        Mock Set-AuthenticodeSignature { throw 'WhatIf must not sign.' }
        $shell = [powershell]::Create()
        try {
            [void] $shell.AddCommand((Join-Path $packageTools 'Set-AdoToolkitPackageSignature.ps1')).AddParameter('PackagePath', $package).AddParameter('CertificateThumbprint', $expected).AddParameter('TimestampServer', 'https://timestamp.example.test').AddParameter('WhatIf')
            $shell.Invoke() | Out-Null
            $shell.HadErrors | Should -BeFalse
        }
        finally { $shell.Dispose() }
        Should -Invoke Set-AuthenticodeSignature -Times 0 -Exactly
    }

    It 'signs all toolkit files with SHA256 and a timestamp using mocked certificate boundaries' {
        Mock Get-Item -ParameterFilter { $LiteralPath -like 'Cert:*' } {
            [pscustomobject]@{ HasPrivateKey = $true; EnhancedKeyUsageList = @([pscustomobject]@{ ObjectId = '1.3.6.1.5.5.7.3.3' }) }
        }
        Mock Set-AuthenticodeSignature -RemoveParameterType Certificate { [pscustomobject]@{ Status = 'Valid' } }
        Mock Get-AuthenticodeSignature { [pscustomobject]@{ Status = 'Valid'; SignerCertificate = [pscustomobject]@{ Thumbprint = $expected } } }
        & (Join-Path $packageTools 'Set-AdoToolkitPackageSignature.ps1') -PackagePath $package -CertificateThumbprint $expected -TimestampServer 'https://timestamp.example.test' -Confirm:$false
        Should -Invoke Set-AuthenticodeSignature -Times 6 -Exactly -ParameterFilter { $HashAlgorithm -eq 'SHA256' -and $TimestampServer -eq 'https://timestamp.example.test/' }
    }

    It 'rejects a path outside the allowed root' {
        { Resolve-AdoPackagePath -Path (Join-Path $TestDrive '../outside') -Root $TestDrive } | Should -Throw '*escapes*'
        { Remove-AdoPackageDirectory -Path $TestDrive -Root $TestDrive } | Should -Throw '*escapes*'
    }

    It 'commits through sibling staging and removes the old package only after success' {
        $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        [void] [System.IO.Directory]::CreateDirectory($root)
        $stage = New-SyntheticPackage -Path (Join-Path $root '.staging-test')
        $destination = Join-Path $root '0.1.0'
        [void] [System.IO.Directory]::CreateDirectory($destination)
        Set-Content -LiteralPath (Join-Path $destination 'old.txt') -Value 'old fixture'
        Move-AdoPackageDirectory -Staging $stage -Destination $destination -Root $root
        Assert-AdoPackage -PackagePath $destination | Should -Be '0.1.0'
        @(Get-ChildItem -LiteralPath $root -Force).Count | Should -Be 1
    }
}

Describe 'Release archive and standalone installer' {
    BeforeAll {
        Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.ZipFile
        $installer = Join-Path $packageTools 'Install-AdoToolkit.ps1'
        function New-TestZip {
            param([string] $Path, [string[]] $Entries)
            $zip = [System.IO.Compression.ZipFile]::Open($Path, [System.IO.Compression.ZipArchiveMode]::Create)
            try {
                foreach ($name in $Entries) {
                    $writer = [System.IO.StreamWriter]::new($zip.CreateEntry($name).Open())
                    try { $writer.Write('synthetic fixture') }
                    finally { $writer.Dispose() }
                }
            }
            finally { $zip.Dispose() }
            return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
        }
        function Get-ListAssignment {
            param([string] $Path, [string] $Variable)
            $ast = [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref] $null, [ref] $null)
            $assignment = $ast.Find({
                    param($node)
                    $node -is [System.Management.Automation.Language.AssignmentStatementAst] -and
                    $node.Left -is [System.Management.Automation.Language.VariableExpressionAst] -and
                    $node.Left.VariablePath.UserPath -eq $Variable
                }, $true)
            return @($assignment.Right.Expression.SafeGetValue())
        }
    }
    BeforeEach {
        $work = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $package = New-SyntheticPackage -Path (Join-Path $work 'package')
        $modules = Join-Path $work 'modules'
    }

    It 'zips the package with a checksum that the installer accepts, then replaces the same version' {
        $release = New-AdoReleaseArchive -PackagePath $package -OutputRoot (Join-Path $work 'release')
        $release.Version | Should -Be '0.1.0'
        [IO.Path]::GetFileName($release.Archive) | Should -Be 'AdoToolkit-0.1.0.zip'
        [IO.File]::ReadAllText($release.Checksum) | Should -Be ($release.Sha256 + '  AdoToolkit-0.1.0.zip' + "`n")
        (Get-FileHash -LiteralPath $release.Archive -Algorithm SHA256).Hash | Should -Be $release.Sha256.ToUpperInvariant()
        $archive = [System.IO.Compression.ZipFile]::OpenRead($release.Archive)
        try {
            $archive.Entries.Count | Should -Be 8
            @($archive.Entries.FullName | Where-Object { $_ -notlike 'AdoToolkit/0.1.0/*' }).Count | Should -Be 0
        }
        finally { $archive.Dispose() }
        @(Get-ChildItem -LiteralPath (Join-Path $work 'release') -Force).Name | Sort-Object | Should -Be @('AdoToolkit-0.1.0.zip', 'AdoToolkit-0.1.0.zip.sha256')

        $result = & $installer -Path $release.Archive -Destination $modules -WarningAction SilentlyContinue
        $result.Version | Should -Be '0.1.0'
        $result.Signed | Should -BeFalse
        $installed = Join-Path (Join-Path $modules 'AdoToolkit') '0.1.0'
        $result.Path | Should -Be $installed
        Assert-AdoPackage -PackagePath $installed | Should -Be '0.1.0'
        Set-Content -LiteralPath (Join-Path $installed 'stale.txt') -Value 'old fixture'
        # Leftovers of an earlier install whose old copy was still loaded are swept; other folders stay.
        $leftover = Join-Path (Join-Path $modules 'AdoToolkit') ('0.0.9.previous-' + [guid]::NewGuid().ToString('N'))
        [void] [IO.Directory]::CreateDirectory((Join-Path $leftover 'fr'))
        $unrelated = Join-Path (Join-Path $modules 'AdoToolkit') 'notes.previous'
        [void] [IO.Directory]::CreateDirectory($unrelated)
        & $installer -Path $release.Archive -Destination $modules -WarningAction SilentlyContinue | Out-Null
        Test-Path -LiteralPath $leftover | Should -BeFalse
        Remove-Item -LiteralPath $unrelated
        Assert-AdoPackage -PackagePath $installed | Should -Be '0.1.0'
        @(Get-ChildItem -LiteralPath (Join-Path $modules 'AdoToolkit') -Force).Name | Should -Be @('0.1.0')
    }

    It 'writes nothing with WhatIf' {
        $release = New-AdoReleaseArchive -PackagePath $package -OutputRoot (Join-Path $work 'release')
        # A hostless shell captures ShouldProcess messages so verify still emits one JSON document.
        $shell = [powershell]::Create()
        try {
            [void] $shell.AddCommand($installer).AddParameter('Path', $release.Archive).AddParameter('Destination', $modules).AddParameter('WhatIf')
            @($shell.Invoke()).Count | Should -Be 0
            $shell.HadErrors | Should -BeFalse
        }
        finally { $shell.Dispose() }
        Test-Path -LiteralPath $modules | Should -BeFalse
    }

    It 'refuses a zip that does not match its checksum file or hash' {
        $release = New-AdoReleaseArchive -PackagePath $package -OutputRoot (Join-Path $work 'release')
        Set-Content -LiteralPath $release.Checksum -Value (('0' * 64) + '  AdoToolkit-0.1.0.zip')
        { & $installer -Path $release.Archive -Destination $modules } | Should -Throw '*checksum*'
        { & $installer -Path $release.Archive -Sha256 ('f' * 64) -Destination $modules } | Should -Throw '*checksum*'
        Set-Content -LiteralPath $release.Checksum -Value ($release.Sha256 + '  AdoToolkit-9.9.9.zip')
        { & $installer -Path $release.Archive -Destination $modules } | Should -Throw '*describes*'
        Remove-Item -LiteralPath $release.Checksum
        { & $installer -Path $release.Archive -Destination $modules } | Should -Throw '*Checksum file not found*'
        Test-Path -LiteralPath $modules | Should -BeFalse
    }

    It 'refuses <Case> even when the checksum matches' -TestCases @(
        @{ Case = 'a traversal entry'; Extra = @('AdoToolkit/0.1.0/../evil.txt') }
        @{ Case = 'an extra file'; Extra = @('AdoToolkit/0.1.0/extra.dll') }
        @{ Case = 'a second version'; Extra = @('AdoToolkit/0.1.1/AdoToolkit.psd1') }
        @{ Case = 'a rooted entry'; Extra = @('/AdoToolkit/0.1.0/evil.txt') }
        @{ Case = 'a missing file'; Extra = @() }
    ) {
        param($Case, $Extra)
        $entries = @(Get-ListAssignment -Path $installer -Variable 'layout' | ForEach-Object { 'AdoToolkit/0.1.0/' + $_ })
        if ($Extra.Count -eq 0) { $entries = @($entries | Select-Object -Skip 1) }
        $zip = Join-Path $work 'AdoToolkit-0.1.0.zip'
        $hash = New-TestZip -Path $zip -Entries ($entries + $Extra)
        { & $installer -Path $zip -Sha256 $hash -Destination $modules } | Should -Throw '*release zip*'
        Test-Path -LiteralPath $modules | Should -BeFalse
    }

    It 'refuses an unsigned release when a thumbprint is supplied and leaves nothing behind' {
        $release = New-AdoReleaseArchive -PackagePath $package -OutputRoot (Join-Path $work 'release')
        Mock Get-AuthenticodeSignature { [pscustomobject]@{ Status = 'NotSigned'; SignerCertificate = $null } }
        { & $installer -Path $release.Archive -Destination $modules -ExpectedThumbprint $expected } | Should -Throw '*expected certificate*'
        @(Get-ChildItem -LiteralPath (Join-Path $modules 'AdoToolkit') -Force).Count | Should -Be 0
        Should -Invoke Get-AuthenticodeSignature -Times 1 -Exactly
    }

    It 'checks all six signable files against the expected signer' {
        $release = New-AdoReleaseArchive -PackagePath $package -OutputRoot (Join-Path $work 'release')
        # A literal: mock bodies resolve variables through the calling script's scopes.
        Mock Get-AuthenticodeSignature { [pscustomobject]@{ Status = 'Valid'; SignerCertificate = [pscustomobject]@{ Thumbprint = 'a' * 40 } } }
        (& $installer -Path $release.Archive -Destination $modules -ExpectedThumbprint $expected -WarningAction SilentlyContinue).Signed | Should -BeTrue
        Should -Invoke Get-AuthenticodeSignature -Times 6 -Exactly
    }

    It 'keeps the installer layout in step with the package layout' {
        $common = Join-Path $packageTools 'Package.Common.ps1'
        Get-ListAssignment -Path $installer -Variable 'layout' | Should -Be (Get-ListAssignment -Path $common -Variable 'required')
    }

    It 'rejects junctions but not plain directories in package paths' {
        $real = Join-Path $work 'real'
        [void] [IO.Directory]::CreateDirectory((Join-Path $real 'inner'))
        $junction = Join-Path $work 'junction'
        [void] (New-Item -ItemType Junction -Path $junction -Target $real)
        Resolve-AdoPackagePath -Path (Join-Path $real 'inner') | Should -Be (Join-Path $real 'inner')
        { Resolve-AdoPackagePath -Path (Join-Path $junction 'inner') } | Should -Throw '*links*'
        Test-AdoPackageLink -Item (Get-Item -LiteralPath $junction) | Should -BeTrue
        Test-AdoPackageLink -Item (Get-Item -LiteralPath $real) | Should -BeFalse
    }
}
