function Get-DependencyInventory {
    param(
        [string] $Root = $script:RepositoryRoot,
        [ValidateRange(1, 100)][int] $Limit = 20
    )

    $projectProfile = Get-ProjectProfile -Root $Root
    $Root = $projectProfile.Root
    $packages = New-Object System.Collections.ArrayList
    $manifests = New-Object System.Collections.ArrayList
    $errors = New-Object System.Collections.ArrayList

    foreach ($file in $projectProfile.Files) {
        $relative = Get-RelativeRepositoryPath -Path $file.FullName -Root $Root
        if ($file.Name -eq 'package.json') {
            [void] $manifests.Add($relative)
            $data = Get-NodePackageData -PackageFile $file
            if ($null -eq $data) {
                [void] $errors.Add("${relative}: invalid package.json")
                continue
            }
            foreach ($kind in @('dependencies', 'devDependencies', 'peerDependencies')) {
                $property = $data.PSObject.Properties[$kind]
                if ($null -eq $property -or $null -eq $property.Value) { continue }
                foreach ($entry in $property.Value.PSObject.Properties | Sort-Object Name) {
                    [void] $packages.Add([pscustomobject]@{ Manager = 'npm'; Name = $entry.Name; Version = [string] $entry.Value; Scope = $kind; Manifest = $relative })
                }
            }
        }
        elseif ($file.Extension -in @('.csproj', '.fsproj', '.vbproj', '.props', '.targets')) {
            [void] $manifests.Add($relative)
            try {
                $project = Get-SafeXmlDocument -Path $file.FullName
                foreach ($reference in @($project.SelectNodes('//*[local-name()="PackageReference" or local-name()="PackageVersion"]'))) {
                    # GetAttribute rather than property access: a reference that omits
                    # Version — the normal shape under central package management — would
                    # otherwise throw under Set-StrictMode and lose the whole manifest.
                    $name = [string] $reference.GetAttribute('Include')
                    if ([string]::IsNullOrWhiteSpace($name)) { $name = [string] $reference.GetAttribute('Update') }
                    if ([string]::IsNullOrWhiteSpace($name)) { continue }

                    $version = [string] $reference.GetAttribute('Version')
                    if ([string]::IsNullOrWhiteSpace($version)) {
                        $versionNode = $reference.SelectSingleNode('./*[local-name()="Version"]')
                        if ($null -ne $versionNode) { $version = [string] $versionNode.InnerText }
                    }
                    if ([string]::IsNullOrWhiteSpace($version)) { $version = $null }
                    $scope = if ($reference.LocalName -eq 'PackageVersion') { 'central' } else { 'project' }
                    [void] $packages.Add([pscustomobject]@{ Manager = 'NuGet'; Name = $name; Version = $version; Scope = $scope; Manifest = $relative })
                }
            }
            catch {
                [void] $errors.Add("${relative}: $($_.Exception.Message)")
                continue
            }
        }
        elseif ($file.Name -eq 'requirements.txt') {
            [void] $manifests.Add($relative)
            foreach ($line in Get-Content -LiteralPath $file.FullName) {
                $clean = ($line -replace '\s*#.*$', '').Trim()
                if ([string]::IsNullOrWhiteSpace($clean) -or $clean.StartsWith('-')) { continue }
                if ($clean -match '^([A-Za-z0-9_.-]+)\s*([<>=!~].+)?$') {
                    $version = if ([string]::IsNullOrWhiteSpace($matches[2])) { $null } else { $matches[2].Trim() }
                    [void] $packages.Add([pscustomobject]@{ Manager = 'pip'; Name = $matches[1]; Version = $version; Scope = 'project'; Manifest = $relative })
                }
            }
        }
        elseif ($file.Name -in @('Cargo.toml', 'go.mod', 'pyproject.toml', 'pom.xml', 'build.gradle', 'build.gradle.kts')) {
            [void] $manifests.Add($relative)
        }
    }

    $validationTools = New-Object System.Collections.ArrayList
    if ($projectProfile.Stack -contains 'powershell') {
        if (@($projectProfile.TestFiles | Where-Object { $_ -match '\.Tests\.ps1$' }).Count -gt 0) { [void] $validationTools.Add('Pester') }
        [void] $validationTools.Add('PSScriptAnalyzer')
    }
    if ($projectProfile.Stack -contains 'dotnet') { [void] $validationTools.Add('dotnet') }
    if ($projectProfile.Stack -contains 'node') { [void] $validationTools.Add($projectProfile.NodePackageManager) }
    if ($projectProfile.Stack -contains 'python') { [void] $validationTools.Add('pytest') }
    if ($projectProfile.Stack -contains 'rust') { [void] $validationTools.Add('cargo') }
    if ($projectProfile.Stack -contains 'go') { [void] $validationTools.Add('go') }

    $allPackages = @($packages | Sort-Object Manager, Name, Manifest)
    return [pscustomobject]@{
        Status = if ($errors.Count -gt 0) { 'incomplete' } else { 'ok' }
        Manifests = @($manifests | Sort-Object -Unique)
        Packages = @($allPackages | Select-Object -First $Limit)
        PackageCount = $allPackages.Count
        Truncated = $allPackages.Count -gt $Limit
        Errors = @($errors | Select-Object -First $Limit)
        ValidationTools = @($validationTools | Sort-Object -Unique)
    }
}

function Get-BuildModule {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Pester', 'PSScriptAnalyzer', 'Microsoft.PowerShell.PlatyPS')][string] $Name,
        [switch] $Loaded
    )

    # Release children inherit this flag. Installation and every import use the same
    # manifest, so a newer module preinstalled on the runner cannot change the build.
    $pinned = $null
    if ($env:ADOTOOLKIT_RELEASE_BUILD -eq '1') {
        $versions = Import-PowerShellDataFile -LiteralPath (Join-Path $PSScriptRoot '../BuildModules.psd1')
        $pinned = [version] $versions[$Name]
    }
    Get-Module -Name $Name -ListAvailable:(-not $Loaded) | Where-Object {
        if ($null -ne $pinned) { $_.Version -eq $pinned }
        elseif ($Name -eq 'Pester') { $_.Version.Major -eq 5 }
        elseif ($Name -eq 'Microsoft.PowerShell.PlatyPS') { $_.Version.Major -eq 1 }
        else { $true }
    } | Sort-Object Version -Descending | Select-Object -First 1
}

function Get-ToolState {
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [AllowEmptyCollection()][string[]] $Commands = @(),
        [string] $MinimumVersion,
        [string] $MaximumVersion,
        [ValidateSet('required', 'recommended', 'optional')][string] $Level = 'recommended',
        [switch] $Module
    )

    if ($Module) {
        $availableModules = @(Get-Module -ListAvailable -Name $Name | Sort-Object Version -Descending)
        $compatibleModules = @(
            $availableModules | Where-Object {
                ([string]::IsNullOrWhiteSpace($MinimumVersion) -or $_.Version -ge [version] $MinimumVersion) -and
                ([string]::IsNullOrWhiteSpace($MaximumVersion) -or $_.Version -lt [version] $MaximumVersion)
            }
        )
        if ($compatibleModules.Count -gt 0) { $found = $compatibleModules[0] }
        elseif ($availableModules.Count -gt 0) { $found = $availableModules[0] }
        else { $found = $null }
        $version = if ($null -ne $found) { $found.Version } else { $null }
        $present = $null -ne $found
    }
    else {
        $found = Get-FirstCommand -Names $Commands
        $versionInfo = if ($null -ne $found) { Get-CommandVersion -Names $Commands } else { $null }
        $versionText = if ($null -ne $versionInfo) { $versionInfo.Version } else { $null }
        $version = $null
        if ($versionText -match '(\d+(?:\.\d+)+)') {
            try { $version = [version] $matches[1] } catch { $version = $null }
        }
        $present = $null -ne $found
    }

    $state = if (-not $present) { 'missing' } elseif ($MinimumVersion -and $version -and $version -lt [version] $MinimumVersion) { 'outdated' } elseif ($MaximumVersion -and $version -and $version -ge [version] $MaximumVersion) { 'incompatible' } else { 'present' }
    return [pscustomobject]@{
        Name = $Name
        Level = $Level
        State = $state
        Version = if ($null -ne $version) { $version.ToString() } else { $null }
        MinimumVersion = if ([string]::IsNullOrWhiteSpace($MinimumVersion)) { $null } else { $MinimumVersion }
        MaximumVersion = if ([string]::IsNullOrWhiteSpace($MaximumVersion)) { $null } else { $MaximumVersion }
    }
}

function Get-ProjectDiagnostics {
    param([string] $Root = $script:RepositoryRoot)

    $projectProfile = Get-ProjectProfile -Root $Root
    $tools = New-Object System.Collections.ArrayList
    [void] $tools.Add((Get-ToolState -Name 'PowerShell' -Commands @('pwsh') -MinimumVersion '7.6.5' -Level 'required'))
    [void] $tools.Add((Get-ToolState -Name 'ripgrep' -Commands @('rg') -Level 'recommended'))
    [void] $tools.Add((Get-ToolState -Name 'ast-grep' -Commands @('ast-grep', 'sg') -Level 'optional'))

    if ($projectProfile.Stack -contains 'powershell') {
        if (@($projectProfile.TestFiles | Where-Object { $_ -match '\.Tests\.ps1$' }).Count -gt 0) {
            [void] $tools.Add((Get-ToolState -Name 'Pester' -Commands @() -MinimumVersion '5.0' -MaximumVersion '6.0' -Level 'required' -Module))
        }
        [void] $tools.Add((Get-ToolState -Name 'PSScriptAnalyzer' -Commands @() -Level 'required' -Module))
    }
    if ($projectProfile.Stack -contains 'dotnet') { [void] $tools.Add((Get-ToolState -Name 'dotnet' -Commands @('dotnet') -Level 'required')) }
    if (@($projectProfile.Files | Where-Object { $_.Extension -eq '.md' -and
                (Get-RelativeRepositoryPath -Path $_.FullName -Root $Root) -like 'docs/commands/*' }).Count -gt 0) {
        [void] $tools.Add((Get-ToolState -Name 'Microsoft.PowerShell.PlatyPS' -Commands @() -MinimumVersion '1.0' -MaximumVersion '2.0' -Level 'required' -Module))
    }
    if ($projectProfile.Stack -contains 'node') {
        $managers = @($projectProfile.Files | Where-Object Name -eq 'package.json' | ForEach-Object { Get-NodeManager -PackageFile $_ -Root $projectProfile.Root } | Where-Object { $_ } | Sort-Object -Unique)
        foreach ($manager in $managers) { [void] $tools.Add((Get-ToolState -Name $manager -Commands @($manager) -Level 'required')) }
    }
    if ($projectProfile.Stack -contains 'python') { [void] $tools.Add((Get-ToolState -Name 'python' -Commands @('python', 'py') -Level 'required')) }
    if ($projectProfile.Stack -contains 'rust') { [void] $tools.Add((Get-ToolState -Name 'cargo' -Commands @('cargo') -Level 'required')) }
    if ($projectProfile.Stack -contains 'go') { [void] $tools.Add((Get-ToolState -Name 'go' -Commands @('go') -Level 'required')) }

    $requiredProblems = @($tools | Where-Object { $_.Level -eq 'required' -and $_.State -ne 'present' })
    return [pscustomobject]@{
        Status = if ($requiredProblems.Count -eq 0) { 'ok' } else { 'incomplete' }
        PowerShell = $PSVersionTable.PSVersion.ToString()
        Stack = $projectProfile.Stack
        Tools = @($tools)
        RequiredProblems = @($requiredProblems | ForEach-Object { $_.Name })
    }
}
