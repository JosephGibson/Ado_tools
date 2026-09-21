# Shared package boundaries; callers enable strict mode and terminating errors.

# Symbolic links and junctions are never traversed. Cloud-file placeholders, such as a
# OneDrive-redirected Documents folder, are reparse points without a link target and are allowed.
function Test-AdoPackageLink {
    param([Parameter(Mandatory = $true)][System.IO.FileSystemInfo] $Item)
    return [bool] ($Item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -and $null -ne $Item.LinkTarget
}

function Resolve-AdoPackagePath {
    param([Parameter(Mandatory = $true)][string] $Path, [string] $Root)
    $provider = $null
    $drive = $null
    $full = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path, [ref] $provider, [ref] $drive)
    if ($provider.Name -ne 'FileSystem') { throw 'Package paths must use the FileSystem provider.' }
    if ($Root) {
        $base = (Resolve-AdoPackagePath -Path $Root).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
        if (-not $full.StartsWith($base + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Package path escapes its permitted root.'
        }
    }
    $ancestor = $full
    while ($ancestor) {
        if (Test-Path -LiteralPath $ancestor) {
            if (Test-AdoPackageLink -Item (Get-Item -LiteralPath $ancestor -Force)) {
                throw 'Package paths must not traverse links.'
            }
        }
        $ancestor = Split-Path -Parent $ancestor
    }
    return $full
}

function Get-AdoPackageFile {
    param([Parameter(Mandatory = $true)][string] $PackagePath)
    $root = Resolve-AdoPackagePath -Path $PackagePath
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw 'Package directory does not exist.' }
    $pending = [System.Collections.Generic.Queue[string]]::new()
    $pending.Enqueue($root)
    while ($pending.Count -gt 0) {
        foreach ($item in Get-ChildItem -LiteralPath $pending.Dequeue() -Force) {
            if (Test-AdoPackageLink -Item $item) { throw 'Package contains a link.' }
            if ($item.PSIsContainer) { $pending.Enqueue($item.FullName) }
            else { $item }
        }
    }
}

function Get-AdoSignableFile {
    param([Parameter(Mandatory = $true)][string] $PackagePath)
    Get-AdoPackageFile -PackagePath $PackagePath | Where-Object {
        $_.Extension -in @('.psd1', '.ps1xml', '.ps1') -or
        ($_.Extension -eq '.dll' -and $_.Name -match '^AdoToolkit\.(Core|PowerShell)(\.resources)?\.dll$')
    }
}

function Read-AdoPackageXml {
    param([Parameter(Mandatory = $true)][string] $Path)
    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($Path, $settings)
    try {
        $document = [System.Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
        return ,$document
    }
    finally { $reader.Dispose() }
}

function Assert-AdoPackage {
    param([Parameter(Mandatory = $true)][string] $PackagePath)
    $required = @(
        'AdoToolkit.psd1', 'AdoToolkit.Format.ps1xml',
        'AdoToolkit.Core.dll', 'AdoToolkit.PowerShell.dll',
        'fr/AdoToolkit.Core.resources.dll', 'fr/AdoToolkit.PowerShell.resources.dll',
        'en-US/AdoToolkit.PowerShell.dll-Help.xml', 'fr/AdoToolkit.PowerShell.dll-Help.xml'
    )
    $files = @(Get-AdoPackageFile -PackagePath $PackagePath)
    $names = @($files | ForEach-Object { [System.IO.Path]::GetRelativePath($PackagePath, $_.FullName).Replace('\', '/') })
    if (@(Compare-Object -ReferenceObject $required -DifferenceObject $names).Count -ne 0) { throw 'Package files differ from the required layout.' }
    foreach ($file in $files | Where-Object { $_.Extension -in @('.xml', '.ps1xml') }) {
        $xml = Read-AdoPackageXml -Path $file.FullName
        if ($file.Name -like '*-Help.xml' -and $xml.SelectNodes("//*[local-name()='command']").Count -ne 22) {
            throw 'Package help must describe all twenty-two implemented commands.'
        }
    }
    $manifest = Import-PowerShellDataFile -LiteralPath (Join-Path $PackagePath 'AdoToolkit.psd1')
    if ($manifest.ModuleVersion -notmatch '^\d+\.\d+\.\d+$' -or
        $manifest.RootModule -ne 'AdoToolkit.PowerShell.dll' -or
        @($manifest.CmdletsToExport).Count -ne 22) { throw 'Invalid package manifest.' }
    $expectedVersion = [version] ([string] $manifest.ModuleVersion + '.0')
    foreach ($file in $files | Where-Object Extension -eq '.dll') {
        # Read PE metadata without loading package code into this process. In particular,
        # -NoBuild must not relabel binaries left over from the previous release.
        try { $identity = [Reflection.AssemblyName]::GetAssemblyName($file.FullName) }
        catch { throw "Invalid package assembly: $($file.Name)." }
        if ($identity.Version -ne $expectedVersion) {
            throw "Package assembly version mismatch: $($file.Name) is $($identity.Version), expected $expectedVersion. Rebuild before packaging."
        }
        if ($identity.Name -cne [IO.Path]::GetFileNameWithoutExtension($file.Name)) {
            throw "Package assembly identity mismatch: $($file.Name)."
        }
    }
    return [string] $manifest.ModuleVersion
}

function Assert-AdoPackageSignature {
    param([Parameter(Mandatory = $true)][string] $PackagePath,
        [Parameter(Mandatory = $true)][string] $ExpectedThumbprint)
    $files = @(Get-AdoSignableFile -PackagePath $PackagePath)
    if ($files.Count -eq 0) { throw 'Package has no signable files.' }
    foreach ($file in $files) {
        $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
        if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate -or
            -not [string]::Equals($signature.SignerCertificate.Thumbprint, $ExpectedThumbprint, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Package signature is invalid or the signer does not match.'
        }
    }
}

function Remove-AdoPackageDirectory {
    param([Parameter(Mandatory = $true)][string] $Path, [Parameter(Mandatory = $true)][string] $Root)
    $full = Resolve-AdoPackagePath -Path $Path -Root $Root
    if (Test-Path -LiteralPath $full) {
        # Enumerate before recursive deletion so nested links are rejected too.
        Get-AdoPackageFile -PackagePath $full | Out-Null
        Remove-Item -LiteralPath $full -Recurse -Force
    }
}

function Move-AdoPackageDirectory {
    param([Parameter(Mandatory = $true)][string] $Staging,
        [Parameter(Mandatory = $true)][string] $Destination,
        [Parameter(Mandatory = $true)][string] $Root)
    $source = Resolve-AdoPackagePath -Path $Staging -Root $Root
    $target = Resolve-AdoPackagePath -Path $Destination -Root $Root
    if ((Split-Path -Parent $source) -ne (Split-Path -Parent $target)) { throw 'Atomic package staging must be a sibling of its destination.' }
    Get-AdoPackageFile -PackagePath $source | Out-Null
    $backup = Resolve-AdoPackagePath -Path ($target + '.previous-' + [guid]::NewGuid().ToString('N')) -Root $Root
    $hasBackup = $false
    try {
        if (Test-Path -LiteralPath $target) {
            Get-AdoPackageFile -PackagePath $target | Out-Null
            [System.IO.Directory]::Move($target, $backup)
            $hasBackup = $true
        }
        try { [System.IO.Directory]::Move($source, $target) }
        catch {
            if ($hasBackup) { [System.IO.Directory]::Move($backup, $target); $hasBackup = $false }
            throw
        }
    }
    finally {
        # Another PowerShell process may still hold the replaced assemblies; the new copy stays installed.
        if ($hasBackup) {
            try { Remove-AdoPackageDirectory -Path $backup -Root $Root }
            catch { Write-Warning "The previous copy is still in use and was left at $backup; delete it after closing PowerShell." }
        }
    }
}

function Get-AdoModuleRoot {
    $documents = [Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)
    if ([string]::IsNullOrWhiteSpace($documents)) { throw 'Documents known folder is unavailable.' }
    return Join-Path $documents 'PowerShell/Modules/AdoToolkit'
}

# Zips a validated package as AdoToolkit/<version>/... and writes a sha256sum-style checksum file.
# All assets are staged beside their targets and validated before any target is replaced.
# File systems do not offer multi-file atomic replacement; backups restore the previous set
# on a failed commit. A process crash may leave backups for manual recovery.
function Publish-AdoReleaseFiles {
    param([Parameter(Mandatory = $true)][object[]] $Files, [Parameter(Mandatory = $true)][string] $Root)
    $backups = [System.Collections.Generic.List[object]]::new()
    $installed = [System.Collections.Generic.List[string]]::new()
    foreach ($file in $Files) {
        $null = Resolve-AdoPackagePath -Path $file.Source -Root $Root
        $null = Resolve-AdoPackagePath -Path $file.Target -Root $Root
    }
    try {
        foreach ($file in $Files) {
            if (Test-Path -LiteralPath $file.Target) {
                $backup = $file.Target + '.previous-' + [guid]::NewGuid().ToString('N')
                [IO.File]::Move($file.Target, $backup)
                $backups.Add(@{ Source = $backup; Target = $file.Target })
            }
        }
        foreach ($file in $Files) {
            [IO.File]::Move($file.Source, $file.Target)
            $installed.Add($file.Target)
        }
    }
    catch {
        foreach ($target in $installed) { [IO.File]::Delete($target) }
        # Leave a backup in place if recovery itself fails; never discard the only old copy.
        foreach ($backup in $backups) { [IO.File]::Move($backup.Source, $backup.Target) }
        throw
    }
    foreach ($backup in $backups) { [IO.File]::Delete($backup.Source) }
}

function New-AdoReleaseArchive {
    param([Parameter(Mandatory = $true)][string] $PackagePath, [Parameter(Mandatory = $true)][string] $OutputRoot,
        [string] $InstallerPath, [string] $PowerShellArchivePath, [string] $PowerShellChecksumPath, [string] $PowerShellVersion)
    $portable = [bool] ($PowerShellArchivePath -or $PowerShellChecksumPath -or $PowerShellVersion)
    if ($portable -and (-not $PowerShellArchivePath -or -not $PowerShellChecksumPath -or -not $PowerShellVersion)) {
        throw 'Portable releases require PowerShellArchivePath, PowerShellChecksumPath and PowerShellVersion together.'
    }
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.ZipFile
    $package = Resolve-AdoPackagePath -Path $PackagePath
    $version = Assert-AdoPackage -PackagePath $package
    $output = Resolve-AdoPackagePath -Path $OutputRoot
    [void] [System.IO.Directory]::CreateDirectory($output)
    $name = "AdoToolkit-$version.zip"
    $archive = Join-Path $output $name
    $checksum = $archive + '.sha256'
    $random = [guid]::NewGuid().ToString('N')
    $temporaryArchive = Join-Path $output ".$name.$random.tmp"
    $temporaryChecksum = Join-Path $output ".$name.sha256.$random.tmp"
    $temporaryInstaller = Join-Path $output ".Install-AdoToolkit.ps1.$random.tmp"
    $portableName = "AdoToolkit-$version-win-x64.zip"
    $temporaryPortable = Join-Path $output ".$portableName.$random.tmp"
    $temporaryPortableChecksum = Join-Path $output ".$portableName.sha256.$random.tmp"
    try {
        $files = @(Get-AdoPackageFile -PackagePath $package | ForEach-Object {
                [pscustomobject]@{
                    File = $_
                    Entry = "AdoToolkit/$version/" + [System.IO.Path]::GetRelativePath($package, $_.FullName).Replace('\', '/')
                }
            } | Sort-Object -Property Entry)
        $stream = [System.IO.File]::Open($temporaryArchive, [System.IO.FileMode]::CreateNew)
        try {
            $zip = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create)
            try {
                foreach ($item in $files) {
                    $entry = $zip.CreateEntry($item.Entry, [System.IO.Compression.CompressionLevel]::Optimal)
                    $entry.LastWriteTime = $item.File.LastWriteTime
                    $target = $entry.Open()
                    try {
                        $source = [System.IO.File]::OpenRead($item.File.FullName)
                        try { $source.CopyTo($target) }
                        finally { $source.Dispose() }
                    }
                    finally { $target.Dispose() }
                }
            }
            finally { $zip.Dispose() }
        }
        finally { $stream.Dispose() }
        $check = [System.IO.Compression.ZipFile]::OpenRead($temporaryArchive)
        try {
            $actual = @($check.Entries | ForEach-Object { $_.FullName + '|' + $_.Length })
            $expected = @($files | ForEach-Object { $_.Entry + '|' + $_.File.Length })
            if (@(Compare-Object -ReferenceObject $expected -DifferenceObject $actual -CaseSensitive).Count -ne 0) {
                throw 'Release archive differs from the package.'
            }
        }
        finally { $check.Dispose() }
        $hash = (Get-FileHash -LiteralPath $temporaryArchive -Algorithm SHA256).Hash.ToLowerInvariant()
        [System.IO.File]::WriteAllText($temporaryChecksum, "$hash  $name`n", [System.Text.UTF8Encoding]::new($false))
        $assets = @(
            @{ Source = $temporaryArchive; Target = $archive },
            @{ Source = $temporaryChecksum; Target = $checksum }
        )
        if ($InstallerPath) {
            [IO.File]::Copy((Resolve-AdoPackagePath -Path $InstallerPath), $temporaryInstaller)
            $tokens = $null
            $errors = $null
            [void] [System.Management.Automation.Language.Parser]::ParseFile($temporaryInstaller, [ref] $tokens, [ref] $errors)
            if (@($errors).Count -gt 0) { throw 'The installer script does not parse.' }
            $assets += @{ Source = $temporaryInstaller; Target = (Join-Path $output 'Install-AdoToolkit.ps1') }
        }
        $portableArchive = $null
        $portableChecksum = $null
        if ($portable) {
            . (Join-Path $PSScriptRoot 'Portable.Common.ps1')
            New-AdoPortableArchive -PackagePath $package -Version $version -PowerShellArchivePath $PowerShellArchivePath `
                -PowerShellChecksumPath $PowerShellChecksumPath -PowerShellVersion $PowerShellVersion -DestinationPath $temporaryPortable
            $portableHash = (Get-FileHash -LiteralPath $temporaryPortable -Algorithm SHA256).Hash.ToLowerInvariant()
            [IO.File]::WriteAllText($temporaryPortableChecksum, "$portableHash  $portableName`n", [Text.UTF8Encoding]::new($false))
            $portableArchive = Join-Path $output $portableName
            $portableChecksum = $portableArchive + '.sha256'
            $assets += @(
                @{ Source = $temporaryPortable; Target = $portableArchive },
                @{ Source = $temporaryPortableChecksum; Target = $portableChecksum }
            )
        }
        Publish-AdoReleaseFiles -Files $assets -Root $output
        return [pscustomobject]@{ Version = $version; Archive = $archive; Checksum = $checksum; Sha256 = $hash; PortableArchive = $portableArchive; PortableChecksum = $portableChecksum }
    }
    finally {
        foreach ($temporary in @($temporaryArchive, $temporaryChecksum, $temporaryInstaller, $temporaryPortable, $temporaryPortableChecksum)) {
            if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
        }
    }
}

# Live checks load the newest release installed for the current user. A signed release must be
# signed throughout by one certificate; an unsigned release is accepted and reported as unsigned.
function Import-AdoInstalledModule {
    $root = Resolve-AdoPackagePath -Path (Get-AdoModuleRoot)
    $prefix = $root.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $candidate = Get-Module -ListAvailable -Name AdoToolkit |
        Where-Object { $_.ModuleBase.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) } |
        Sort-Object -Property Version -Descending | Select-Object -First 1
    if ($null -eq $candidate) { throw 'INSTALLED_MODULE_REQUIRED' }
    $package = Resolve-AdoPackagePath -Path $candidate.ModuleBase -Root $root
    $null = Assert-AdoPackage -PackagePath $package
    $signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $package 'AdoToolkit.psd1')
    $signed = $signature.Status -ne 'NotSigned'
    if ($signed) {
        if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate) { throw 'SIGNATURE_INVALID' }
        Assert-AdoPackageSignature -PackagePath $package -ExpectedThumbprint $signature.SignerCertificate.Thumbprint
    }
    Import-Module -Name (Join-Path $package 'AdoToolkit.psd1') -Force -Global
    return [pscustomobject]@{ Path = $package; Version = [string] $candidate.Version; Signed = $signed }
}
