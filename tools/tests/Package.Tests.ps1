BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    $packageTools = Join-Path (Split-Path -Parent $PSScriptRoot) 'package'
    . (Join-Path $packageTools 'Package.Common.ps1')
    $expected = 'A' * 40
    $wrong = 'B' * 40
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
            Set-Content -LiteralPath $file -Value 'synthetic signature fixture, not an assembly'
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
