# Shared package boundaries; callers enable strict mode and terminating errors.
function Resolve-AdoPackagePath {
    param([Parameter(Mandatory = $true)][string] $Path, [string] $Root)
    $full = [System.IO.Path]::GetFullPath($Path)
    if ($Root) {
        $base = [System.IO.Path]::GetFullPath($Root).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
        if (-not $full.StartsWith($base + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'Package path escapes its permitted root.'
        }
    }
    $ancestor = $full
    while ($ancestor) {
        if (Test-Path -LiteralPath $ancestor) {
            if ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
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
            if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) { throw 'Package contains a link.' }
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
        if ($hasBackup) { Remove-AdoPackageDirectory -Path $backup -Root $Root }
    }
}

function Get-AdoModuleRoot {
    $documents = [Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)
    if ([string]::IsNullOrWhiteSpace($documents)) { throw 'Documents known folder is unavailable.' }
    return Join-Path $documents 'PowerShell/Modules/AdoToolkit'
}
