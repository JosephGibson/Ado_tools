BeforeAll {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    . (Join-Path $PSScriptRoot 'Support/FakeGitHub.ps1')
    $packageTools = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../tools/package'))
    . (Join-Path $packageTools 'Package.Common.ps1')
    $installerScript = Join-Path $packageTools 'Install-AdoToolkit.ps1'
    $staged = Split-Path -Parent $env:ADOTOOLKIT_MODULE_MANIFEST
    $stagedVersion = [string] (Import-PowerShellDataFile -LiteralPath $env:ADOTOOLKIT_MODULE_MANIFEST).ModuleVersion
    # A module-only install that runs version 0.0.1, so that the staged version is a newer release.
    # It is imported by path, here and in every runspace: once a newer copy is installed beside it,
    # a by-name import or autoload would try to load a second copy of the assemblies.
    $modules = Join-Path $TestDrive 'Modules'
    $root = Join-Path $modules 'AdoToolkit'
    $running = Join-Path $root '0.0.1'
    New-RelabeledModule -Source $staged -Destination $running -Version '0.0.1'
    $env:PSModulePath = $modules + [IO.Path]::PathSeparator + $env:PSModulePath
    $runningManifest = Join-Path $running 'AdoToolkit.psd1'
    Import-Module -Name $runningManifest -Force
    $github = New-FakeGitHub
    Set-UpdateTransport -Handler $github
    $culture = [cultureinfo]::GetCultureInfo($PSUICulture)
    $commandId = ',AdoToolkit.UpdateAdoToolkitCommand'

    function Get-UpdateMessage {
        param([string] $Key, [object[]] $Arguments = @(), [cultureinfo] $Culture = $culture)
        [AdoToolkit.Core.Resources.Messages]::Get([AdoToolkit.Core.Resources.AdoMessage] $Key, $Culture, $Arguments)
    }

    # Publishes a newer release whose bytes do not matter, for a test that stops before the download.
    function Publish-AnyNewerRelease { Publish-FakeRelease -GitHub $github -Version '1.2.3' -Archive ([byte[]] @(1, 2, 3)) }

    # The zips of the planning spike, at version 1.2.3; Install-AdoToolkit.ps1 and the cmdlet must
    # agree on each, except where the cmdlet is stricter by design.
    function Get-ParityZip {
        param([string] $Name)
        $folder = 'AdoToolkit/1.2.3/'
        $files = New-ModuleFiles -Version '1.2.3'
        $valid = @($files.Keys | ForEach-Object { @{ Name = $folder + $_; Data = $files[$_] } })
        $without = { param($File) @($valid | Where-Object { $_.Name -cne $folder + $File }) }
        $renamed = { param($From, $To) @($valid | ForEach-Object { @{ Name = $_.Name.Replace($From, $To); Data = $_.Data } }) }
        if ($Name -like 'missing-*') { return , (New-ZipBytes -Entries (& $without $Name.Substring(8))) }
        $entries = switch ($Name) {
            'valid' { $valid }
            'valid-with-directories' { @(@{ Name = 'AdoToolkit/' }, @{ Name = $folder }, @{ Name = $folder + 'fr/' }) + $valid }
            'symlink-attribute' {
                @(& $without 'AdoToolkit.Format.ps1xml') + @{ Name = $folder + 'AdoToolkit.Format.ps1xml'; Data = 'link'; Attributes = [int] (0xA1FF -shl 16) }
            }
            'backslash' { $valid + @{ Name = 'AdoToolkit\1.2.3\x.dll'; Data = 'x' } }
            'colon-stream' { $valid + @{ Name = $folder + 'AdoToolkit.psd1:evil'; Data = 'x' } }
            'absolute' { $valid + @{ Name = '/' + $folder + 'x.dll'; Data = 'x' } }
            'dot-dot' { $valid + @{ Name = $folder + '../1.2.3/x.dll'; Data = 'x' } }
            'dot' { $valid + @{ Name = $folder + './x.dll'; Data = 'x' } }
            'empty-segment' { $valid + @{ Name = $folder + '/x.dll'; Data = 'x' } }
            'wrong-root' { & $renamed 'AdoToolkit/1.2.3/' 'Other/1.2.3/' }
            'root-case' { & $renamed 'AdoToolkit/1.2.3/' 'adotoolkit/1.2.3/' }
            'version-not-numeric' { & $renamed '/1.2.3/' '/latest/' }
            'version-four-parts' { & $renamed '/1.2.3/' '/1.2.3.4/' }
            'two-versions' { $valid + @{ Name = 'AdoToolkit/1.2.4/AdoToolkit.Core.dll'; Data = 'x' } }
            'file-at-root' { $valid + @{ Name = 'AdoToolkit/readme.txt'; Data = 'x' } }
            'extra-file' { $valid + @{ Name = $folder + 'extra.dll'; Data = 'x' } }
            'case-different-file' { & $renamed 'AdoToolkit.Format' 'adotoolkit.format' }
            'duplicate-entry' { $valid + @{ Name = $folder + 'AdoToolkit.Core.dll'; Data = 'second copy' } }
            'nonempty-directory-entry' { $valid + @{ Name = $folder + 'fr/'; Data = 'x' } }
            'oversize-entry' { @(& $without 'AdoToolkit.Format.ps1xml') + @{ Name = $folder + 'AdoToolkit.Format.ps1xml'; Size = 64MB + 1 } }
            'manifest-version' { @(& $without 'AdoToolkit.psd1') + @{ Name = $folder + 'AdoToolkit.psd1'; Data = (New-ModuleManifestText -Version '1.2.4') } }
            'manifest-root-module' {
                @(& $without 'AdoToolkit.psd1') + @{ Name = $folder + 'AdoToolkit.psd1'; Data = (New-ModuleManifestText -Version '1.2.3' -RootModule 'Other.dll') }
            }
            'manifest-unparsable' { @(& $without 'AdoToolkit.psd1') + @{ Name = $folder + 'AdoToolkit.psd1'; Data = '@{ ModuleVersion = ' } }
            'header-length-lie' { return , (Set-ZipCentralSize -Zip (New-ZipBytes -Entries $valid) -Name ($folder + 'AdoToolkit.Format.ps1xml') -Size 999) }
            'leading-zero-folder' {
                $other = New-ModuleFiles -Version '01.2.3' -AssemblyVersion '1.2.3.0'
                @($other.Keys | ForEach-Object { @{ Name = 'AdoToolkit/01.2.3/' + $_; Data = $other[$_] } })
            }
            'unicode-digit-folder' {
                $other = New-ModuleFiles -Version "1.2.$([char] 0x0663)" -AssemblyVersion '1.2.3.0'
                @($other.Keys | ForEach-Object { @{ Name = "AdoToolkit/1.2.$([char] 0x0663)/" + $_; Data = $other[$_] } })
            }
            'checksum-name-case' { $valid }
            'dll-not-an-assembly' { @(& $without 'AdoToolkit.Core.dll') + @{ Name = $folder + 'AdoToolkit.Core.dll'; Data = 'MZ not an assembly' } }
            'dll-other-version' {
                @(& $without 'AdoToolkit.PowerShell.dll') + @{ Name = $folder + 'AdoToolkit.PowerShell.dll'; Data = (New-SyntheticAssembly -Name 'AdoToolkit.PowerShell' -Version '1.2.2.0') }
            }
            'too-many-entries' { @(0..69 | ForEach-Object { @{ Name = "AdoToolkit/d$_/" } }) + $valid }
            default { throw "Unknown case $Name" }
        }
        return , (New-ZipBytes -Entries $entries)
    }
}

AfterAll { Set-UpdateTransport -Handler $null }

Describe 'Update-AdoToolkit in a module-only install' {
    BeforeEach {
        Get-ChildItem -LiteralPath $root -Force | Where-Object Name -ne '0.0.1' | Remove-Item -Recurse -Force
        Publish-FakeRelease -GitHub $github -Version '0.0.1' -Archive ([byte[]] @(1))
    }

    It 'declares its output type, its table view and ShouldProcess' {
        $command = Get-Command Update-AdoToolkit
        $command.OutputType.Type.FullName | Should -Contain 'AdoToolkit.Core.Update.AdoToolkitUpdate'
        $command.Parameters.Keys | Should -Contain 'WhatIf'
        $command.Parameters.Keys | Should -Contain 'Confirm'
        $view = (Get-FormatData -TypeName 'AdoToolkit.Core.Update.AdoToolkitUpdate').FormatViewDefinition[0]
        @($view.Control.Headers.Label) | Should -Be @('Status', 'CurrentVersion', 'LatestVersion', 'Path')
    }

    It 'says the running version is the latest and downloads nothing' {
        $result = Update-AdoToolkit -InformationVariable info -InformationAction SilentlyContinue
        $result.Status | Should -Be 'UpToDate'
        $result.Path | Should -Be $running
        $result.PreviousPath | Should -BeNullOrEmpty
        @($info).Count | Should -Be 1
        $info[0].Tags | Should -Contain 'PSHOST'
        $info[0].MessageData.Message | Should -Be (Get-UpdateMessage 'UpdateUpToDate' @('0.0.1'))
        @($github.Requests()) | Should -Be @('https://api.github.com/repos/JosephGibson/Ado_tools/releases/latest')
        @(Get-ChildItem -LiteralPath $root -Force).Name | Should -Be @('0.0.1')
    }

    It 'says a running version newer than the release is up to date' {
        Publish-FakeRelease -GitHub $github -Version '0.0.0' -Archive ([byte[]] @(1))
        $result = Update-AdoToolkit -InformationVariable info -InformationAction SilentlyContinue
        $result.Status | Should -Be 'UpToDate'
        $info[0].MessageData.Message | Should -Be (Get-UpdateMessage 'UpdateNewerThanRelease' @('0.0.1', '0.0.0'))
    }

    It 'installs the zip that New-AdoReleaseArchive builds, then finds it installed, and replaces an incomplete copy' {
        $release = New-AdoReleaseArchive -PackagePath $staged -OutputRoot (Join-Path $TestDrive 'release')
        Publish-FakeRelease -GitHub $github -Version $stagedVersion -Archive ([IO.File]::ReadAllBytes($release.Archive)) `
            -Checksum ([IO.File]::ReadAllBytes($release.Checksum))
        $target = Join-Path $root $stagedVersion

        $result = Update-AdoToolkit -InformationVariable info -InformationAction SilentlyContinue -Confirm:$false

        $result.Status | Should -Be 'Installed'
        $result.InstallMode | Should -Be 'Module'
        $result.CurrentVersion | Should -Be ([version] '0.0.1')
        $result.LatestVersion | Should -Be ([version] $stagedVersion)
        $result.Path | Should -Be $target
        $result.PreviousPath | Should -Be $running
        $result.ReleaseUri.AbsoluteUri | Should -Be "https://github.com/JosephGibson/Ado_tools/releases/tag/v$stagedVersion"
        foreach ($file in Get-ChildItem -LiteralPath $staged -File -Recurse) {
            $relative = [IO.Path]::GetRelativePath($staged, $file.FullName)
            (Get-FileHash -LiteralPath (Join-Path $target $relative)).Hash | Should -Be (Get-FileHash -LiteralPath $file.FullName).Hash
        }
        $info[-1].MessageData.Message | Should -Be (Get-UpdateMessage 'UpdateInstalledModule' @($stagedVersion, $target, '0.0.1'))
        @(Get-ChildItem -LiteralPath $root -Force).Name | Sort-Object | Should -Be (@('0.0.1', $stagedVersion) | Sort-Object)

        $github.ClearRequests()
        $again = Update-AdoToolkit -InformationVariable installed -InformationAction SilentlyContinue
        $again.Status | Should -Be 'AlreadyInstalled'
        $again.Path | Should -Be $target
        $again.PreviousPath | Should -Be $running
        $installed[0].MessageData.Message | Should -Be (Get-UpdateMessage 'UpdateAlreadyInstalledModule' @($stagedVersion, $target))
        @($github.Requests()).Count | Should -Be 1

        Remove-Item -LiteralPath (Join-Path $target 'fr/AdoToolkit.PowerShell.dll-Help.xml')
        (Update-AdoToolkit -InformationAction SilentlyContinue -Confirm:$false).Status | Should -Be 'Installed'
        Test-Path -LiteralPath (Join-Path $target 'fr/AdoToolkit.PowerShell.dll-Help.xml') | Should -BeTrue
        @(Get-ChildItem -LiteralPath $root -Force).Name | Sort-Object | Should -Be (@('0.0.1', $stagedVersion) | Sort-Object)
    }

    It 'with WhatIf reads the release, returns nothing and writes nothing' {
        Publish-AnyNewerRelease
        $output = Update-AdoToolkit -WhatIf -InformationAction SilentlyContinue
        $output | Should -BeNullOrEmpty
        @(Get-ChildItem -LiteralPath $root -Force).Name | Should -Be @('0.0.1')
        @($github.Requests()).Count | Should -Be 1
    }

    It 'speaks French in a fr-CA runspace' {
        $shell = [powershell]::Create()
        try {
            [void] $shell.AddScript({
                    param($Manifest)
                    [System.Threading.Thread]::CurrentThread.CurrentUICulture = [cultureinfo]::GetCultureInfo('fr-CA')
                    Import-Module -Name $Manifest
                    $null = Update-AdoToolkit -InformationVariable info -InformationAction SilentlyContinue
                    $info[0].MessageData.Message
                }).AddArgument($runningManifest)
            $message = $shell.Invoke()
            $shell.Streams.Error.Count | Should -Be 0
            $message | Should -Be (Get-UpdateMessage 'UpdateUpToDate' @('0.0.1') ([cultureinfo]::GetCultureInfo('fr-CA')))
        }
        finally { $shell.Dispose() }
    }

    It 'names a rate limit with the time it resets' {
        Publish-AnyNewerRelease
        $reset = [DateTimeOffset]::UtcNow.AddMinutes(30).ToUnixTimeSeconds()
        $github.ApiStatus = 403
        $github.ApiHeaders['x-ratelimit-remaining'] = '0'
        $github.ApiHeaders['x-ratelimit-reset'] = [string] $reset
        $record = { Update-AdoToolkit -ErrorAction Stop -InformationAction SilentlyContinue } | Should -Throw -ErrorId ('AdoThrottled' + $commandId) -PassThru
        $record.Exception.Message | Should -Be (Get-UpdateMessage 'UpdateRateLimited' @([DateTimeOffset]::FromUnixTimeSeconds($reset).ToLocalTime().DateTime))
    }

    It 'names a proxy that refuses the Windows sign-in' {
        Publish-AnyNewerRelease
        $github.ProxyTunnelStatus = 407
        $record = { Update-AdoToolkit -InformationAction SilentlyContinue } | Should -Throw -ErrorId ('AdoAuthentication' + $commandId) -PassThru
        $record.Exception.Message | Should -Be (Get-UpdateMessage 'UpdateProxyAuthentication' @('api.github.com'))
    }

    It 'refuses a redirect outside the three GitHub hosts' {
        Publish-AnyNewerRelease
        $github.AssetHost = 'evil.example.test'
        { Update-AdoToolkit -ErrorAction Stop -InformationAction SilentlyContinue -Confirm:$false } | Should -Throw -ErrorId ('AdoRedirect' + $commandId)
        @(Get-ChildItem -LiteralPath $root -Force).Name | Should -Be @('0.0.1')
    }

    It 'refuses a zip that does not match the digest that GitHub reports' {
        $zip = New-ZipBytes -Entries @((New-ModuleFiles -Version '1.2.3').GetEnumerator() | ForEach-Object { @{ Name = 'AdoToolkit/1.2.3/' + $_.Key; Data = $_.Value } })
        Publish-FakeRelease -GitHub $github -Version '1.2.3' -Archive $zip -ArchiveDigest ('sha256:' + ('0' * 64))
        $record = { Update-AdoToolkit -ErrorAction Stop -InformationAction SilentlyContinue -Confirm:$false } | Should -Throw -ErrorId ('AdoResponseFormat' + $commandId) -PassThru
        $record.Exception.Message | Should -Be (Get-UpdateMessage 'UpdateDigestMismatch' @('AdoToolkit-1.2.3.zip'))
        @(Get-ChildItem -LiteralPath $root -Force).Name | Should -Be @('0.0.1')
    }

    It 'refuses a release that requires a newer PowerShell' {
        $files = New-ModuleFiles -Version '1.2.3' -Manifest (New-ModuleManifestText -Version '1.2.3' -PowerShellVersion '99.0')
        $zip = New-ZipBytes -Entries @($files.GetEnumerator() | ForEach-Object { @{ Name = 'AdoToolkit/1.2.3/' + $_.Key; Data = $_.Value } })
        Publish-FakeRelease -GitHub $github -Version '1.2.3' -Archive $zip
        { Update-AdoToolkit -InformationAction SilentlyContinue -Confirm:$false } | Should -Throw -ErrorId ('AdoConfiguration' + $commandId)
        @(Get-ChildItem -LiteralPath $root -Force).Name | Should -Be @('0.0.1')
    }

    It 'gives the verdict of Install-AdoToolkit.ps1 on <Name>' -TestCases @(
        @('valid', 'valid-with-directories', 'symlink-attribute') | ForEach-Object { @{ Name = $_; Installer = 'accept'; Cmdlet = 'accept' } }
        @('backslash', 'colon-stream', 'absolute', 'dot-dot', 'dot', 'empty-segment', 'wrong-root', 'root-case', 'version-not-numeric',
            'version-four-parts', 'two-versions', 'file-at-root', 'extra-file', 'case-different-file', 'duplicate-entry', 'nonempty-directory-entry',
            'oversize-entry', 'manifest-version', 'manifest-root-module', 'manifest-unparsable', 'header-length-lie',
            'missing-AdoToolkit.psd1', 'missing-AdoToolkit.Format.ps1xml', 'missing-AdoToolkit.Core.dll', 'missing-AdoToolkit.PowerShell.dll',
            'missing-fr/AdoToolkit.Core.resources.dll', 'missing-en-US/AdoToolkit.PowerShell.dll-Help.xml', 'missing-fr/AdoToolkit.PowerShell.dll-Help.xml') |
            ForEach-Object { @{ Name = $_; Installer = 'reject'; Cmdlet = 'reject' } }
        # Stricter than the installer by design.
        @('leading-zero-folder', 'unicode-digit-folder', 'checksum-name-case', 'dll-not-an-assembly', 'dll-other-version', 'too-many-entries') |
            ForEach-Object { @{ Name = $_; Installer = 'accept'; Cmdlet = 'reject' } }
    ) {
        param($Name, $Installer, $Cmdlet)
        $zip = Get-ParityZip -Name $Name
        $case = Join-Path $TestDrive ('parity/' + ($Name -replace '[^A-Za-z0-9.-]', '_'))
        [void] [IO.Directory]::CreateDirectory($case)
        $zipPath = Join-Path $case 'AdoToolkit-1.2.3.zip'
        [IO.File]::WriteAllBytes($zipPath, $zip)
        $listed = if ($Name -eq 'checksum-name-case') { 'adotoolkit-1.2.3.zip' } else { 'AdoToolkit-1.2.3.zip' }
        $checksum = [Text.Encoding]::UTF8.GetBytes((Get-Sha256Hex $zip) + "  $listed`n")
        [IO.File]::WriteAllBytes($zipPath + '.sha256', $checksum)

        $installerVerdict = try {
            $null = & $installerScript -Path $zipPath -Destination (Join-Path $case 'installed') -Confirm:$false -WarningAction SilentlyContinue
            'accept'
        }
        catch { 'reject: ' + $_.Exception.Message }
        Publish-FakeRelease -GitHub $github -Version '1.2.3' -Archive $zip -Checksum $checksum
        $cmdletVerdict = try {
            $null = Update-AdoToolkit -ErrorAction Stop -InformationAction SilentlyContinue -WarningAction SilentlyContinue -Confirm:$false
            'accept'
        }
        catch { 'reject: ' + $_.FullyQualifiedErrorId + ': ' + $_.Exception.Message }

        ($installerVerdict -split ':', 2)[0] | Should -Be $Installer -Because "the installer said: $installerVerdict"
        ($cmdletVerdict -split ':', 2)[0] | Should -Be $Cmdlet -Because "the cmdlet said: $cmdletVerdict"
        # A refusal is the toolkit's own error, never an exception that escaped it.
        if ($Cmdlet -eq 'reject') { $cmdletVerdict | Should -BeLike ('reject: AdoResponseFormat' + $commandId + ': *') }
    }

    It 'keeps the updater''s copies of the release layouts equal to the packaging scripts' {
        $flags = [Reflection.BindingFlags] 'NonPublic, Static'
        $core = [AdoToolkit.Core.Resources.Messages].Assembly
        $layout = $core.GetType('AdoToolkit.Core.Update.ModuleArchive', $true).GetField('Layout', $flags).GetValue($null)
        $runtime = $core.GetType('AdoToolkit.Core.Update.PortableArchive', $true).GetField('RuntimeFiles', $flags).GetValue($null)
        $installerAst = [Management.Automation.Language.Parser]::ParseFile($installerScript, [ref] $null, [ref] $null)
        $portableAst = [Management.Automation.Language.Parser]::ParseFile((Join-Path $packageTools 'Portable.Common.ps1'), [ref] $null, [ref] $null)
        $installerLayout = $installerAst.Find({
                param($node)
                $node -is [Management.Automation.Language.AssignmentStatementAst] -and
                $node.Left -is [Management.Automation.Language.VariableExpressionAst] -and $node.Left.VariablePath.UserPath -eq 'layout'
            }, $true)
        $required = $portableAst.Find({
                param($node)
                $node -is [Management.Automation.Language.ForEachStatementAst] -and $node.Variable.VariablePath.UserPath -eq 'required'
            }, $true)
        $isString = { param($node) $node -is [Management.Automation.Language.StringConstantExpressionAst] }

        @($layout | Sort-Object -CaseSensitive) | Should -Be @($installerLayout.Right.FindAll($isString, $true).Value | Sort-Object -CaseSensitive)
        @($runtime | Sort-Object -CaseSensitive) | Should -Be @($required.Condition.FindAll($isString, $true).Value | Sort-Object -CaseSensitive)
    }
}
