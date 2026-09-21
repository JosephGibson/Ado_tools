# Creates a portable ZIP at a caller-owned temporary path. The release publisher
# commits it together with the module-only assets after all validation succeeds.
function New-AdoPortableArchive {
    param(
        [Parameter(Mandatory = $true)][string] $PackagePath,
        [Parameter(Mandatory = $true)][string] $Version,
        [Parameter(Mandatory = $true)][string] $PowerShellArchivePath,
        [Parameter(Mandatory = $true)][string] $PowerShellChecksumPath,
        [Parameter(Mandatory = $true)][ValidatePattern('^7\.6\.\d+$')][string] $PowerShellVersion,
        [Parameter(Mandatory = $true)][string] $DestinationPath
    )
    $runtimePath = Resolve-AdoPackagePath -Path $PowerShellArchivePath
    $checksumPath = Resolve-AdoPackagePath -Path $PowerShellChecksumPath
    $runtimeName = "PowerShell-$PowerShellVersion-win-x64.zip"
    if ([IO.Path]::GetFileName($runtimePath) -cne $runtimeName) { throw 'The PowerShell archive must match the pinned version and win-x64 architecture.' }
    $hashPattern = '^([A-Fa-f0-9]{64})\s+\*?' + [regex]::Escape($runtimeName) + '$'
    $hashes = @([IO.File]::ReadAllLines($checksumPath) | Where-Object { $_.Trim() -cmatch $hashPattern })
    if ($hashes.Count -ne 1) { throw 'Expected exactly one published checksum for the pinned PowerShell archive.' }
    $expectedHash = ($hashes[0].Trim() -split '\s+')[0]
    # Keep the input open without write sharing from checksum verification through copying.
    $runtimeStream = [IO.File]::Open($runtimePath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $actualHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($runtimeStream))
        if ($actualHash -ne $expectedHash) { throw 'The PowerShell archive does not match its published hash.' }
        $runtimeStream.Position = 0
        $runtime = [IO.Compression.ZipArchive]::new($runtimeStream, [IO.Compression.ZipArchiveMode]::Read, $true)
        try {
            $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            $runtimeFiles = [Collections.Generic.List[object]]::new()
            foreach ($entry in $runtime.Entries) {
                $name = $entry.FullName
                $directory = $name.EndsWith('/', [StringComparison]::Ordinal)
                $segments = $name.TrimEnd('/').Split('/')
                if ($name -match '[\\:\x00-\x1f<>"|?*]' -or
                    @($segments | Where-Object { $_ -in @('', '.', '..') -or $_ -match '[. ]$' -or $_ -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)' }).Count -gt 0 -or
                    (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000 -or
                    -not $names.Add($name.TrimEnd('/'))) {
                    throw 'The PowerShell archive contains an unsafe or duplicate entry.'
                }
                if ($directory) {
                    if ($entry.Length -ne 0) { throw 'The PowerShell archive contains a nonempty directory entry.' }
                }
                else { $runtimeFiles.Add($entry) }
            }
            $fileNames = [Collections.Generic.HashSet[string]]::new([string[]] @($runtimeFiles.FullName), [StringComparer]::OrdinalIgnoreCase)
            foreach ($name in $names) {
                $parent = $name
                while ($parent.Contains('/')) {
                    $parent = $parent.Substring(0, $parent.LastIndexOf('/'))
                    if ($fileNames.Contains($parent)) { throw 'The PowerShell archive contains a file/directory collision.' }
                }
            }
            foreach ($required in @('pwsh.exe', 'pwsh.dll', 'pwsh.runtimeconfig.json', 'System.Management.Automation.dll',
                    'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'System.Private.CoreLib.dll', 'LICENSE.txt', 'ThirdPartyNotices.txt')) {
                if (-not $fileNames.Contains($required)) { throw "The PowerShell archive is missing $required." }
            }
            $localFiles = @(Get-AdoPackageFile -PackagePath $PackagePath | ForEach-Object {
                    [pscustomobject]@{ File = $_; Entry = 'module/' + [IO.Path]::GetRelativePath($PackagePath, $_.FullName).Replace('\', '/') }
                })
            foreach ($name in @('Start-AdoToolkit.cmd', 'Start-AdoToolkit.ps1', 'README.txt')) {
                $path = Resolve-AdoPackagePath -Path (Join-Path $PSScriptRoot "portable/$name")
                $localFiles += [pscustomobject]@{ File = Get-Item -LiteralPath $path; Entry = $name }
            }
            $tokens = $null
            $errors = $null
            [void] [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'portable/Start-AdoToolkit.ps1'), [ref] $tokens, [ref] $errors)
            if (@($errors).Count -gt 0) { throw 'The portable launcher script does not parse.' }
            $expected = [Collections.Generic.List[string]]::new()
            $outputStream = [IO.File]::Open($DestinationPath, [IO.FileMode]::CreateNew)
            try {
                $zip = [IO.Compression.ZipArchive]::new($outputStream, [IO.Compression.ZipArchiveMode]::Create, $true)
                try {
                    foreach ($entry in $runtimeFiles) {
                        $target = $zip.CreateEntry('runtime/' + $entry.FullName, [IO.Compression.CompressionLevel]::Optimal)
                        $target.LastWriteTime = $entry.LastWriteTime
                        $source = $entry.Open()
                        try {
                            $destination = $target.Open()
                            try { $source.CopyTo($destination) }
                            finally { $destination.Dispose() }
                        }
                        finally { $source.Dispose() }
                        $expected.Add($target.FullName + '|' + $entry.Length)
                    }
                    foreach ($item in $localFiles) {
                        $target = $zip.CreateEntry($item.Entry, [IO.Compression.CompressionLevel]::Optimal)
                        $target.LastWriteTime = $item.File.LastWriteTime
                        $destination = $target.Open()
                        try {
                            if ($item.Entry -eq 'Start-AdoToolkit.cmd') {
                                # Batch files must work even from an LF-only source checkout.
                                $bytes = [Text.Encoding]::UTF8.GetBytes(([IO.File]::ReadAllText($item.File.FullName) -replace '\r?\n', "`r`n"))
                                $destination.Write($bytes)
                                $length = $bytes.Length
                            }
                            else {
                                $source = [IO.File]::OpenRead($item.File.FullName)
                                try { $source.CopyTo($destination); $length = $source.Length }
                                finally { $source.Dispose() }
                            }
                        }
                        finally { $destination.Dispose() }
                        $expected.Add($item.Entry + '|' + $length)
                    }
                    $metadata = [ordered]@{ ModuleVersion = $Version; PowerShellVersion = $PowerShellVersion; Architecture = 'win-x64'; PowerShellSha256 = $actualHash.ToLowerInvariant() }
                    $bytes = [Text.Encoding]::UTF8.GetBytes(($metadata | ConvertTo-Json) + "`n")
                    $destination = $zip.CreateEntry('bundle.json').Open()
                    try { $destination.Write($bytes) }
                    finally { $destination.Dispose() }
                    $expected.Add('bundle.json|' + $bytes.Length)
                }
                finally { $zip.Dispose() }
            }
            finally { $outputStream.Dispose() }
            $check = [IO.Compression.ZipFile]::OpenRead($DestinationPath)
            try {
                $actual = @($check.Entries | ForEach-Object { $_.FullName + '|' + $_.Length })
                if (@(Compare-Object -ReferenceObject @($expected) -DifferenceObject $actual -CaseSensitive).Count -ne 0) {
                    throw 'Portable archive differs from the runtime and module package.'
                }
            }
            finally { $check.Dispose() }
        }
        finally { $runtime.Dispose() }
    }
    finally { $runtimeStream.Dispose() }
}
